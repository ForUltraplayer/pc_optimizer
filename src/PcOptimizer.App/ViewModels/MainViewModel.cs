/**
 * @file    : MainViewModel.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델(검사 시작/취소·상태·Finding 기준 요약·분류 목록/필터·카드·마지막 측정 시각·온라인 확인과 마지막 온라인 확인 시각·종료 중 표시·보호 위치 도구가 있을 때만 여는 정리 창·익명화 내보내기·다른 관리자 계정으로 실행되거나 사용자를 확인하지 못했을 때 시스템 범위 안내 배너(문구 구분)·결과와 내 PC 사양 본문 전환, 검사 중 사양 새로 고침 막기)
 */

// 기본 패키지
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly IActionAvailability _actionAvailability;
    private readonly IElevationState _elevationState;
    private readonly bool _displayTrialsAvailable;

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

    /// <summary>표시할 작업 결과 안내가 있는지 여부.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>선택한 분류(전체 포함).</summary>
    [ObservableProperty]
    private CategoryItemViewModel? _selectedCategory;

    /// <summary>본문에 내 PC 사양 화면을 보여 주는지 여부(false면 검사 결과 영역).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResultsVisible), nameof(SpecToggleText))]
    private bool _isSpecVisible;

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
    /// <param name="userScope">사용자 범위(대화형 사용자와 다른 관리자 계정으로 실행 중이면 시스템만, 배너 표시).</param>
    /// <param name="actionAvailability">후보의 앱 내 실행 가능 여부와 정리 창 노출 조건(보호 위치 도구 존재, 생성 시 한 번 확인).</param>
    /// <param name="spec">내 PC 사양 화면 모델(검사와 프로브를 공유하므로 검사 중에는 새로 고침을 막음).</param>
    /// <param name="userScopeUnresolved">
    /// 대화형 세션 사용자(또는 토큰 사용자)를 확인하지 못해 시스템만으로 정했으면 true. 범위 제한은 같고 배너 문구만 "확인하지 못함"으로 바뀝니다.
    /// </param>
    public MainViewModel(
        ScanService scanService,
        ReportExporter exporter,
        IExportPathPicker exportPathPicker,
        SettingsUriPolicy settingsPolicy,
        LinkPolicy linkPolicy,
        IUiDispatcher dispatcher,
        IAppLogger logger,
        IElevationState elevationState,
        UserScopeMode userScope,
        IActionAvailability actionAvailability,
        PcSpecViewModel spec,
        bool userScopeUnresolved = false,
        ActionCenterViewModel? actions = null,
        bool displayTrialsAvailable = false)
    {
        ArgumentNullException.ThrowIfNull(scanService);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(exportPathPicker);
        ArgumentNullException.ThrowIfNull(settingsPolicy);
        ArgumentNullException.ThrowIfNull(linkPolicy);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(elevationState);
        ArgumentNullException.ThrowIfNull(actionAvailability);
        ArgumentNullException.ThrowIfNull(spec);

        _scanService = scanService;
        _scanService.DrainingChanged += OnDrainingChanged;
        _scanService.Operations.Changed += OnOperationsChanged;
        _exporter = exporter;
        _exportPathPicker = exportPathPicker;
        _settingsPolicy = settingsPolicy;
        _linkPolicy = linkPolicy;
        _dispatcher = dispatcher;
        _logger = logger;
        _elevationState = elevationState;
        UserScope = userScope;
        IsUserScopeUnresolved = userScopeUnresolved;
        _actionAvailability = actionAvailability;
        Spec = spec;
        Actions = actions;
        _displayTrialsAvailable = displayTrialsAvailable && userScope == UserScopeMode.Full && !userScopeUnresolved;
        Spec.PropertyChanged += OnSpecPropertyChanged;

        RebuildCategories([]);
    }

    /// <summary>왼쪽 분류 목록("전체" + 분류별 건수).</summary>
    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];

    /// <summary>선택한 분류에 해당하는 카드.</summary>
    public ObservableCollection<FindingCardViewModel> Cards { get; } = [];

    /// <summary>내 PC 사양 화면 모델.</summary>
    public PcSpecViewModel Spec { get; }
    /// <summary>창을 닫아도 유지하는 조치 확인·기록 화면입니다.</summary>
    public ActionCenterViewModel? Actions { get; }
    /// <summary>조치 화면이 조립된 앱에서만 진입 버튼을 보여 줍니다.</summary>
    public bool HasActionCenter => Actions is not null;

    /// <summary>현재 프로세스가 관리자 권한인지 여부(매니페스트가 requireAdministrator라 정상 실행이면 true).</summary>
    public bool IsElevated => _elevationState.IsElevated;

    /// <summary>이 인스턴스의 사용자 범위.</summary>
    public UserScopeMode UserScope { get; }

    /// <summary>대화형 세션 사용자를 확인하지 못해 범위를 정했는지 여부(배너 문구 구분에만 씀).</summary>
    public bool IsUserScopeUnresolved { get; }

    /// <summary>시스템 범위만 검사하는지(다른 관리자 계정으로 실행됐거나 사용자를 확인하지 못함).</summary>
    public bool IsSystemOnly => UserScope == UserScopeMode.SystemOnly;

    /// <summary>범위 안내 배너(전체 범위면 null). 사용자를 확인하지 못했으면 "다른 계정" 대신 "확인하지 못함" 문구를 씁니다.</summary>
    public string? ScopeBannerText => !IsSystemOnly ? null
        : IsUserScopeUnresolved ? Strings.Banner_ScopeUnknown
        : Strings.Banner_SystemOnly;

    /// <summary>배너 표시 여부.</summary>
    public bool HasScopeBanner => ScopeBannerText is not null;

    /// <summary>검사 결과 영역을 보여 주는지 여부(사양 화면과 번갈아 표시).</summary>
    public bool IsResultsVisible => !IsSpecVisible;

    /// <summary>머리글 전환 버튼 문구("내 PC 사양" 또는 "결과로 돌아가기").</summary>
    public string SpecToggleText => IsSpecVisible ? Strings.Spec_NavBack : Strings.Spec_NavOpen;

    /// <summary>검사 중인지 여부.</summary>
    public bool IsScanning => State == ScanState.Scanning;

    /// <summary>검사 옵션(온라인 확인 등)을 바꿀 수 있는지 여부(검사 중에는 바꾸지 않음).</summary>
    public bool CanChangeOptions => !IsScanning;

    /// <summary>마지막 도구 위치 갱신에서 정리 창을 열 수 있었는지. 검사 시작 때 갱신하며 속성 읽기는 디스크를 조회하지 않습니다.</summary>
    public bool CacheToolsAvailable => _actionAvailability.CacheToolsAvailable;

    /// <summary>
    /// 보호 위치에 도구가 있고, 진단·사양 읽기와 양쪽의 프로브 종료 대기가 끝났으며, 이 계정의 사용자 범위를 다룰 수 있을 때(SystemOnly 아님)만 정리 도구를 엽니다.
    /// </summary>
    public bool CanOpenCacheTools => CacheToolsAvailable && !Operations.State.IsBusy && !IsScanning && !HasDrainingNote && !IsSystemOnly && !Spec.IsLoading && !Spec.IsDraining;

    /// <summary>정리 창에도 전달하는 공통 실행 관문입니다.</summary>
    public PcOptimizer.Core.Actions.IOperationCoordinator Operations => _scanService.Operations;

    /// <summary>화면의 정리 창이 닫혀도 실제 조치가 끝나지 않은 사유를 남깁니다.</summary>
    public bool HasOperationNote => Operations.State.IsBusy && Operations.State.Kind is PcOptimizer.Core.Actions.OperationKind.Prepare or PcOptimizer.Core.Actions.OperationKind.Apply or PcOptimizer.Core.Actions.OperationKind.Restore;

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

    /// <summary>
    /// 이전 사양 읽기의 프로브가 아직 끝나지 않아(<see cref="PcSpecViewModel.IsDraining"/>) 검사 시작·정리 창이 막혀 있음을
    /// 결과 화면에도 알릴지 여부.
    /// </summary>
    public bool HasSpecDrainingNote => Spec.IsDraining;

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
        _actionAvailability.Refresh();
        OnPropertyChanged(nameof(CacheToolsAvailable));
        OnPropertyChanged(nameof(CanOpenCacheTools));
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

    /// <summary>
    /// 본문을 내 PC 사양 화면으로 바꿉니다. 아직 읽은 사양이 없으면 읽기를 시작만 하고 기다리지 않으므로,
    /// 전환 버튼은 첫 읽기(프로브 여러 개) 동안에도 바로 다시 누를 수 있습니다. 검사 중이면 검사가 끝난 뒤 읽습니다.
    /// </summary>
    [RelayCommand]
    private void ShowSpec()
    {
        IsSpecVisible = true;
        EnsureSpecLoaded();
    }

    /// <summary>
    /// 본문을 검사 결과 영역으로 되돌립니다(진행 중인 사양 읽기는 계속됨).
    /// </summary>
    [RelayCommand]
    private void ShowResults()
    {
        IsSpecVisible = false;
    }

    /// <summary>
    /// 머리글 버튼: 결과와 내 PC 사양 화면을 번갈아 보여 줍니다(즉시 반환).
    /// </summary>
    [RelayCommand]
    private void ToggleSpec()
    {
        if (IsSpecVisible)
        {
            ShowResults();
            return;
        }

        ShowSpec();
    }

    /// <summary>
    /// 사양 화면이 보이고 아직 읽은 사양이 없으며 읽을 수 있으면(읽는 중·검사 중이 아니면) 읽기를 시작한다. 완료는 기다리지 않는다
    /// (진행은 <see cref="PcSpecViewModel.RefreshCommand"/>의 ExecutionTask로 추적).
    /// </summary>
    private void EnsureSpecLoaded()
    {
        if (IsSpecVisible && Spec.Snapshot is null && Spec.RefreshCommand.CanExecute(null))
        {
            Spec.RefreshCommand.Execute(null);
        }
    }

    /// <summary>검사를 시작할 수 있는지 여부(사양을 읽는 중이거나 사양 프로브가 아직 종료 중이면 프로브 공유를 피하려고 막음, REV-017).</summary>
    private bool CanStartScan() => !Operations.State.IsBusy && State != ScanState.Scanning && !Spec.IsLoading && !Spec.IsDraining;

    /// <summary>
    /// 검사 상태가 바뀌면 사양 새로 고침 가능 여부를 맞춘다(프로브 공유). 검사 중에 사양 화면을 열어 읽지 못했다면 검사가 끝날 때 읽는다.
    /// </summary>
    partial void OnStateChanged(ScanState oldValue, ScanState newValue)
    {
        SyncSpecBusy();
    }

    /// <summary>
    /// 검사 프로브의 종료 중 안내가 바뀌면 사양 새로 고침 가능 여부를 맞춘다(검사 종료 중 → 사양 재진입 차단, REV-017).
    /// </summary>
    partial void OnDrainingNoteChanged(string? value)
    {
        SyncSpecBusy();
    }

    /// <summary>
    /// 검사가 진행 중이거나 검사 프로브가 아직 종료 중이면 사양 새로 고침을 막고, 둘 다 끝나면 풀어 준다.
    /// 풀린 뒤 사양 화면이 보이고 아직 읽은 사양이 없으면 읽기를 시작한다.
    /// </summary>
    private void SyncSpecBusy()
    {
        var operation = Operations.State;
        Spec.SetBusy(IsScanning || HasDrainingNote || (operation.IsBusy && operation.Kind != PcOptimizer.Core.Actions.OperationKind.Specification));
        if (!Spec.IsScanBusy)
        {
            EnsureSpecLoaded();
        }
    }

    /// <summary>
    /// 사양을 읽는 중인지·사양 프로브 종료 대기 중인지가 바뀌면 검사 시작·정리 창 진입 가능 여부를 다시 계산한다.
    /// </summary>
    private void OnSpecPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PcSpecViewModel.IsLoading) or nameof(PcSpecViewModel.IsDraining))
        {
            StartScanCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanOpenCacheTools));
            OnPropertyChanged(nameof(HasSpecDrainingNote));
        }
    }

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
    /// 검사 결과를 화면 상태에 반영한다(UI 스레드에서 호출). 온라인 비교가 실제 완료됐을 때만 마지막 확인 시각을 갱신한다.
    /// </summary>
    private void ApplyResult(ScanResult result)
    {
        var report = result.Report;
        Actions?.UpdateStartupChoices(result.Snapshot, !IsSystemOnly && !IsUserScopeUnresolved);
        var adobeAvailable = !IsSystemOnly && !IsUserScopeUnresolved && Actions?.Choices.Any(c => c.Id == PcOptimizer.Core.Actions.ActionId.AppFiles) == true;
        var startupTargets = (Actions?.Choices ?? []).Select(c => c.Target).OfType<PcOptimizer.Core.Actions.ActionTarget.Startup>()
            .ToDictionary(t => PcOptimizer.Core.Rules.StartupItemsRule.FINDING_ID_PREFIX + t.SourceKey + ":" + t.ValueName, StringComparer.Ordinal);
        _allCards = [.. report.Findings.Select(finding => new FindingCardViewModel(finding, _settingsPolicy, _linkPolicy, _displayTrialsAvailable, adobeAvailable,
            startupTargets.GetValueOrDefault(finding.Id)))];
        OnPropertyChanged(nameof(CommunitySummary));
        if (OnlineComparisonComplete(report))
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

    private async void OnOperationsChanged(object? sender, EventArgs e)
    {
        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (_disposed) { return; }
                StartScanCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanOpenCacheTools));
                OnPropertyChanged(nameof(HasOperationNote));
                SyncSpecBusy();
            });
        }
        catch (Exception ex) { _logger.Warn(LOG_CATEGORY, $"OperationUiFailed type={ex.GetType().Name}"); }
    }

    /// <summary>창이 닫히면 변경 구독을 해제하고 진행 중 검사를 취소합니다.</summary>
    public void Dispose()
    {
        _disposed = true;
        _scanService.DrainingChanged -= OnDrainingChanged;
        _scanService.Operations.Changed -= OnOperationsChanged;
        Spec.PropertyChanged -= OnSpecPropertyChanged;
        Spec.Dispose();
        Actions?.Dispose();
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
