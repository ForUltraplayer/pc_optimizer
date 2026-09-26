/**
 * @file    : MainViewModel.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델(검사 시작/취소·상태·Finding 기준 요약·분류 목록/필터·카드·마지막 측정 시각·온라인 확인·종료 중 표시·익명화 내보내기)
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

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 메인 화면 모델입니다. 엔진 결과는 항상 <see cref="IUiDispatcher"/>를 거쳐 UI 스레드에서 반영합니다.
/// 건수는 PC 상태가 아니라 Finding 기준이며, 후보 0건을 전체 정상으로 표시하지 않습니다.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const string LOG_CATEGORY = nameof(MainViewModel);
    private const string PROBE_ID_SEPARATOR = ", ";

    private readonly ScanService _scanService;
    private readonly ReportExporter _exporter;
    private readonly IExportPathPicker _exportPathPicker;
    private readonly SettingsUriPolicy _settingsPolicy;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLogger _logger;

    private List<FindingCardViewModel> _allCards = [];
    private CancellationTokenSource? _scanCancellation;
    private int _scanGeneration;

    /// <summary>검사 상태.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(IsScanning), nameof(CanChangeOptions), nameof(StartButtonText))]
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

    /// <summary>사용자가 온라인 업데이트 확인을 켰는지 여부.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnlineCheckText))]
    private bool _isOnlineCheckRequested;

    /// <summary>아직 종료 중인 프로브 안내(없으면 null).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDrainingNote))]
    private string? _drainingNote;

    /// <summary>내보내기 등 최근 동작 결과 안내(없으면 null).</summary>
    [ObservableProperty]
    private string? _statusMessage;

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
    /// <param name="dispatcher">UI 스레드 마샬러.</param>
    /// <param name="logger">공용 로거.</param>
    public MainViewModel(
        ScanService scanService,
        ReportExporter exporter,
        IExportPathPicker exportPathPicker,
        SettingsUriPolicy settingsPolicy,
        IUiDispatcher dispatcher,
        IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(scanService);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(exportPathPicker);
        ArgumentNullException.ThrowIfNull(settingsPolicy);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _scanService = scanService;
        _exporter = exporter;
        _exportPathPicker = exportPathPicker;
        _settingsPolicy = settingsPolicy;
        _dispatcher = dispatcher;
        _logger = logger;

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
        State = ScanState.Scanning;
        StatusMessage = null;

        ScanResult result;
        try
        {
            result = await _scanService.RunScanAsync(IsOnlineCheckRequested, cancellation.Token).ConfigureAwait(false);
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
        finally
        {
            _scanCancellation = null;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (generation == _scanGeneration)
            {
                ApplyResult(result);
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
    /// 검사 결과를 화면 상태에 반영한다(UI 스레드에서 호출).
    /// </summary>
    private void ApplyResult(ScanResult result)
    {
        var report = result.Report;
        _allCards = [.. report.Findings.Select(finding => new FindingCardViewModel(finding, _settingsPolicy))];

        OkCount = report.Findings.Count(f => f.Verdict == Verdict.Ok);
        CandidateCount = report.Findings.Count(f => f.Verdict == Verdict.Candidate);
        CannotVerifyCount = report.Findings.Count(f => f.Verdict == Verdict.CannotVerify);
        InfoCount = report.Findings.Count(f => f.Verdict == Verdict.Info);
        LastMeasuredAtUtc = report.CompletedAtUtc;
        DrainingNote = CreateDrainingNote(report);
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
    private string? CreateDrainingNote(ScanReport report)
    {
        string[] draining = [.. report.ProbeSummaries
            .Where(summary => summary.IsStillRunning)
            .Select(summary => summary.ProbeId)
            .Union(_scanService.DrainingProbeIds, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        return draining.Length == 0
            ? null
            : DisplayText.Format(Strings.Draining_Format, string.Join(PROBE_ID_SEPARATOR, draining));
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
        foreach (var card in _allCards.Where(card => selected is null || selected.Includes(card.Finding.Category)))
        {
            Cards.Add(card);
        }
    }
}
