/**
 * @file    : MainViewModel.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델(검사 시작/취소·상태·Finding 기준 요약·분류 목록/필터·카드·마지막 측정 시각·온라인 확인과 마지막 온라인 확인 시각·종료 중 표시·익명화 내보내기·관리자 권한 재검사 요청과 별도 검사 배너)
 */

// 기본 패키지
using System.Collections.ObjectModel;
using System.IO;

// 서드파티 패키지
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 메인 화면 모델입니다. 엔진 결과는 항상 <see cref="IUiDispatcher"/>를 거쳐 UI 스레드에서 반영합니다.
/// 건수는 PC 상태가 아니라 Finding 기준이며, 후보 0건을 전체 정상으로 표시하지 않습니다.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const string LOG_CATEGORY = nameof(MainViewModel);
    private const string PROBE_ID_SEPARATOR = ", ";

    private readonly ScanService _scanService;
    private readonly ReportExporter _exporter;
    private readonly IExportPathPicker _exportPathPicker;
    private readonly SettingsUriPolicy _settingsPolicy;
    private readonly LinkPolicy _linkPolicy;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLogger _logger;

    private List<FindingCardViewModel> _allCards = [];
    private CancellationTokenSource? _scanCancellation;
    private int _scanGeneration;
    private bool _disposed;

    [ObservableProperty]
    private bool _showCommunityDetails;

    /// <summary>접어 둔 커뮤니티 규칙 카드 수를 보여 줍니다.</summary>
    public string CommunitySummary => DisplayText.Format(Strings.Community_Summary, _allCards.Count(IsCommunityCard));

    partial void OnShowCommunityDetailsChanged(bool value) => RefreshVisibleCards();

    private static bool IsCommunityCard(FindingCardViewModel card) => card.Finding.Category == FindingCategory.AppCache
        && card.Finding.Measured.Any(m => m.Name.EndsWith(".origin", StringComparison.Ordinal) && m.Value is TextValue { Value: AppCacheProbeContract.ORIGIN_COMMUNITY })
        && !card.Finding.Measured.Any(m => m.Name.EndsWith(".origin", StringComparison.Ordinal) && m.Value is TextValue { Value: AppCacheProbeContract.ORIGIN_SUPPLEMENT });

    /// <summary>검사 상태.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(IsScanning), nameof(CanChangeOptions), nameof(StartButtonText), nameof(CanOpenCacheTools))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand), nameof(CancelScanCommand), nameof(ExportCommand))]
    private ScanState _state = ScanState.Idle;

    /// <summary>정상(Ok) Finding 수.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _okCount;

    /// <summary>개선 후보(Candidate) Finding 수.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _candidateCount;

    /// <summary>확인 불가(CannotVerify) Finding 수.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _cannotVerifyCount;

    /// <summary>정보(Info) Finding 수.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _infoCount;

    /// <summary>마지막 측정(검사 완료) 시각(UTC).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMeasuredText))]
    private DateTimeOffset? _lastMeasuredAtUtc;

    /// <summary>마지막으로 온라인 확인을 켠 검사가 끝난 시각(UTC, 없으면 null).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastOnlineCheckText), nameof(HasLastOnlineCheck))]
    private DateTimeOffset? _lastOnlineCheckAtUtc;

    /// <summary>사용자가 온라인 업데이트 확인을 켰는지 여부.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnlineCheckText))]
    private bool _isOnlineCheckRequested;

    /// <summary>아직 종료 중인 프로브 안내(없으면 null).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDrainingNote), nameof(CanOpenCacheTools))]
    private string? _drainingNote;

    /// <summary>내보내기 등 최근 동작 결과 안내(없으면 null).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>선택한 분류(전체 포함).</summary>
    [ObservableProperty]
    private CategoryItemViewModel? _selectedCategory;

    /// <summary>마지막 검사 결과.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyPropertyChangedFor(nameof(HasCards), nameof(ShowEmptyHint))]
    private ScanResult? _lastResult;

    /// <summary>
    /// 메인 화면 모델을 만듭니다.
    /// </summary>
    /// <param name="scanService">검사 서비스.</param>
    /// <param name="exporter">리포트 내보내기.</param>
    /// <param name="exportPathPicker">저장 경로 선택기.</param>
    /// <param name="settingsPolicy">설정 URI 허용 정책.</param>
    /// <param name="linkPolicy">외부 링크 허용 정책.</param>
    /// <param name="dispatcher">UI 스레드 마샬러.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="elevationState">현재 프로세스 권한 상태.</param>
    /// <param name="relauncher">관리자 권한 재검사 시작기.</param>
    /// <param name="launchMode">이 인스턴스의 시작 방식(관리자 재검사 인스턴스면 배너 표시).</param>
    public MainViewModel(
        ScanService scanService,
        ReportExporter exporter,
        IExportPathPicker exportPathPicker,
        SettingsUriPolicy settingsPolicy,
        LinkPolicy linkPolicy,
        IUiDispatcher dispatcher,
        IAppLogger logger,
        IElevationState elevationState,
        ElevationRelauncher relauncher,
        ScanLaunchMode launchMode)
    {
        ArgumentNullException.ThrowIfNull(scanService);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(exportPathPicker);
        ArgumentNullException.ThrowIfNull(settingsPolicy);
        ArgumentNullException.ThrowIfNull(linkPolicy);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(elevationState);
        ArgumentNullException.ThrowIfNull(relauncher);

        _scanService = scanService;
        _scanService.DrainingChanged += OnDrainingChanged;
        _exporter = exporter;
        _exportPathPicker = exportPathPicker;
        _settingsPolicy = settingsPolicy;
        _linkPolicy = linkPolicy;
        _dispatcher = dispatcher;
        _logger = logger;
        _elevationState = elevationState;
        _relauncher = relauncher;
        LaunchMode = launchMode;

        RebuildCategories([]);
    }

    /// <summary>왼쪽 분류 목록("전체" + 분류별 건수).</summary>
    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];

    /// <summary>선택한 분류에 해당하는 카드.</summary>
    public ObservableCollection<FindingCardViewModel> Cards { get; } = [];

    /// <summary>검사 중인지 여부.</summary>
    public bool IsScanning => State == ScanState.Scanning;

    /// <summary>검사 옵션(온라인 확인 등)을 바꿀 수 있는지 여부(검사 중에는 바꾸지 않음).</summary>
    public bool CanChangeOptions => !IsScanning;

    /// <summary>진단 작업이 실제 종료된 뒤에만 정리 도구를 엽니다.</summary>
    public bool CanOpenCacheTools => !IsScanning && !HasDrainingNote;

    /// <summary>상태 문자열("상태: …").</summary>
    public string StateText => DisplayText.Format(Strings.State_Format, DisplayText.State(State));

    /// <summary>시작 버튼 문구(처음에는 검사 시작, 이후에는 다시 검사).</summary>
    public string StartButtonText => State == ScanState.Idle ? Strings.Button_StartScan : Strings.Button_Rescan;

    /// <summary>Finding 기준 요약 문자열.</summary>
    public string SummaryText => DisplayText.Format(Strings.Summary_Format, OkCount, CandidateCount, CannotVerifyCount, InfoCount);

    /// <summary>마지막 측정 시각 문자열.</summary>
    public string LastMeasuredText => LastMeasuredAtUtc is { } measured
        ? DisplayText.Format(Strings.LastMeasured_Format, DisplayText.LocalTime(measured))
        : Strings.LastMeasured_None;

    /// <summary>온라인 확인 상태 문자열.</summary>
    public string OnlineCheckText => IsOnlineCheckRequested ? Strings.OnlineCheck_On : Strings.OnlineCheck_Off;

    /// <summary>마지막 온라인 확인 시각 문자열(온라인 확인을 한 검사가 없으면 null).</summary>
    public string? LastOnlineCheckText => LastOnlineCheckAtUtc is { } checkedAt
        ? DisplayText.Format(Strings.LastOnlineCheck_Format, DisplayText.LocalTime(checkedAt))
        : null;

    /// <summary>마지막 온라인 확인 시각을 보여 주는지 여부.</summary>
    public bool HasLastOnlineCheck => LastOnlineCheckAtUtc is not null;

    /// <summary>종료 중 안내가 있는지 여부.</summary>
    public bool HasDrainingNote => DrainingNote is not null;

    /// <summary>표시할 결과가 있는지 여부.</summary>
    public bool HasCards => LastResult is not null;

    /// <summary>아직 결과가 없어 빈 안내를 보여야 하는지 여부.</summary>
    public bool ShowEmptyHint => LastResult is null;

    /// <summary>
    /// 검사를 시작합니다. 결과는 UI 스레드에서 반영합니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScanAsync()
    {
        var generation = ++_scanGeneration;
        using var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        var previousState = State;
        var onlineRequested = IsOnlineCheckRequested;
        State = ScanState.Scanning;
        StatusMessage = null;

        ScanResult result;
        try
        {
            result = await _scanService.RunScanAsync(onlineRequested, cancellation.Token).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // 이전 검사가 아직 조율기를 점유한 드문 경우: 상태를 되돌리고 안내만 한다.
            _logger.Warn(LOG_CATEGORY, $"ScanRejected error={ex.GetType().Name}");
            await _dispatcher.InvokeAsync(() =>
            {
                State = previousState;
                StatusMessage = Strings.Scan_Busy;
            }).ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            _logger.Warn(LOG_CATEGORY, $"ScanFailed error={ex.GetType().Name}");
            await _dispatcher.InvokeAsync(() => { State = ScanState.Partial; StatusMessage = Strings.Scan_Failed; }).ConfigureAwait(false);
            return;
        }
        finally
        {
            _scanCancellation = null;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (generation == _scanGeneration)
            {
                ApplyResult(result, onlineRequested);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 진행 중인 검사를 취소합니다. 이미 얻은 결과는 남습니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelScan))]
    private void CancelScan()
    {
        _scanCancellation?.Cancel();
    }

    /// <summary>
    /// 마지막 리포트를 익명화 JSON으로 사용자가 고른 경로에 저장합니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var report = LastResult?.Report;
        if (report is null)
        {
            return;
        }

        var path = _exportPathPicker.PickSavePath(ReportExporter.SuggestFileName(report));
        if (path is null)
        {
            return;
        }

        string message;
        try
        {
            await _exporter.ExportAnonymizedAsync(report, path, CancellationToken.None).ConfigureAwait(false);
            message = DisplayText.Format(Strings.Export_Done, Path.GetFileName(path));
            _logger.Info(LOG_CATEGORY, $"ReportExported scan={report.ScanId}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            message = DisplayText.Format(Strings.Export_Failed, ex.GetType().Name);
            _logger.Warn(LOG_CATEGORY, $"ReportExportFailed scan={report.ScanId} error={ex.GetType().Name}");
        }

        await _dispatcher.InvokeAsync(() => StatusMessage = message).ConfigureAwait(false);
    }

    /// <summary>검사를 시작할 수 있는지 여부.</summary>
    private bool CanStartScan() => State != ScanState.Scanning;

    /// <summary>검사를 취소할 수 있는지 여부.</summary>
    private bool CanCancelScan() => State == ScanState.Scanning;

    /// <summary>내보낼 수 있는지 여부.</summary>
    private bool CanExport() => LastResult is not null && State != ScanState.Scanning;

    /// <summary>
    /// 선택한 분류가 바뀌면 카드 목록을 다시 거른다.
    /// </summary>
    partial void OnSelectedCategoryChanged(CategoryItemViewModel? value)
    {
        RefreshVisibleCards();
    }

    /// <summary>
    /// 검사 결과를 화면 상태에 반영한다(UI 스레드에서 호출). 온라인 확인을 켠 검사였으면 마지막 온라인 확인 시각을 갱신한다.
    /// </summary>
    private void ApplyResult(ScanResult result, bool onlineRequested)
    {
        var report = result.Report;
        _lastScanIncludedOnline = onlineRequested;
        _allCards = [.. report.Findings.Select(finding => new FindingCardViewModel(finding, _settingsPolicy, _linkPolicy))];
        OnPropertyChanged(nameof(CommunitySummary));
        if (onlineRequested)
        {
            LastOnlineCheckAtUtc = report.CompletedAtUtc;
        }

        OkCount = report.Findings.Count(f => f.Verdict == Verdict.Ok);
        CandidateCount = report.Findings.Count(f => f.Verdict == Verdict.Candidate);
        CannotVerifyCount = report.Findings.Count(f => f.Verdict == Verdict.CannotVerify);
        InfoCount = report.Findings.Count(f => f.Verdict == Verdict.Info);
        LastMeasuredAtUtc = report.CompletedAtUtc;
        DrainingNote = CreateDrainingNote();
        LastResult = result;
        RebuildCategories(report.Findings);
        State = report.Outcome switch
        {
            ScanOutcome.Completed => ScanState.Completed,
            ScanOutcome.Cancelled => ScanState.Cancelled,
            _ => ScanState.Partial,
        };
    }

    /// <summary>
    /// 아직 끝나지 않은 프로브가 있으면 "아직 종료 중" 안내를 만든다.
    /// </summary>
    private string? CreateDrainingNote()
    {
        string[] draining = [.. _scanService.DrainingProbeIds.Order(StringComparer.Ordinal)];

        return draining.Length == 0
            ? null
            : DisplayText.Format(Strings.Draining_Format, string.Join(PROBE_ID_SEPARATOR, draining));
    }

    /// <summary>변경 시점의 스냅샷 대신 UI 실행 시점의 현재 목록을 읽습니다.</summary>
    private async void OnDrainingChanged(object? sender, EventArgs e)
    {
        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed) { DrainingNote = CreateDrainingNote(); }
            });
        }
        catch (Exception ex) { _logger.Warn(LOG_CATEGORY, $"DrainingUiFailed error={ex.GetType().Name}"); }
    }

    /// <summary>창이 닫히면 변경 구독을 해제하고 진행 중 검사를 취소합니다.</summary>
    public void Dispose()
    {
        _disposed = true;
        _scanService.DrainingChanged -= OnDrainingChanged;
        _scanCancellation?.Cancel();
    }

    /// <summary>
    /// 분류 목록을 등록 분류와 Finding 분류로 다시 만들고, 가능하면 이전 선택을 유지한다.
    /// </summary>
    private void RebuildCategories(IReadOnlyList<Finding> findings)
    {
        var previous = SelectedCategory?.Category;
        var categories = _scanService.Categories
            .Concat(findings.Select(f => f.Category))
            .Distinct()
            .ToList();

        Categories.Clear();
        Categories.Add(new CategoryItemViewModel(null, findings.Count));
        foreach (var category in categories)
        {
            Categories.Add(new CategoryItemViewModel(category, findings.Count(f => f.Category == category)));
        }

        SelectedCategory = Categories.FirstOrDefault(item => item.Category == previous) ?? Categories[0];
        RefreshVisibleCards();
    }

    /// <summary>
    /// 선택한 분류에 맞는 카드만 보여 준다.
    /// </summary>
    private void RefreshVisibleCards()
    {
        var selected = SelectedCategory;
        Cards.Clear();
        foreach (var card in _allCards.Where(card => (selected is null || selected.Includes(card.Finding.Category))
            && (ShowCommunityDetails || !IsCommunityCard(card))))
        {
            Cards.Add(card);
        }
        NotifyOverview();
    }
}
