/**
 * @file    : ActionCenterViewModel.cs
 * @author  : rudals252
 * @brief   : 앱 수명의 미리보기·실행·늦은 결과·복구 목록, 관문 종료 후 한 번 재검사
 */
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>앱 코드에서 등록한 대상과 사용자에게 설명할 효과입니다.</summary>
public sealed record ActionChoice(ActionId Id, ActionTarget Target, string Title, string Benefit);

/// <summary>사용자가 얻고 싶은 효과별 실행 항목 묶음입니다.</summary>
public sealed record ActionChoiceGroup(string Title, string Description, IReadOnlyList<ActionChoice> Items)
{
    /// <summary>실행 가능 확정 수가 아닌 확인할 항목 수입니다.</summary>
    public string Header => $"{Title} · {Items.Count}개 항목 확인";
}

/// <summary>자동 변경을 제공하지 않는 기능의 이유와 공식 설정 연결입니다.</summary>
public sealed record ManualActionChoice(string Title, string Reason, string? SettingsUri, string? SupportUrl = null)
{
    /// <summary>공식 설정 연결이 있는 항목만 버튼을 표시합니다.</summary>
    public bool HasSettings => SettingsUri is not null;
    /// <summary>코드에서 허용한 공식 안내가 있으면 별도 버튼을 표시합니다.</summary>
    public bool HasSupport => CacheSupportLinks.IsAllowed(SupportUrl);
}

/// <summary>창이 닫혀도 보존하는 공통 조치 화면 모델입니다. Dispose는 실제 작업을 취소하지 않습니다.</summary>
public sealed partial class ActionCenterViewModel : ObservableObject, IDisposable
{
    private readonly IActionWorkflow _workflow;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<Task> _rescan;
    private readonly Func<string, bool>? _openSettings;
    private readonly Func<string?>? _pickAdobeFolder;
    private readonly Func<string, (ActionChoice? Choice, string? Code)>? _registerAdobeFolder;
    private readonly Func<string, bool?>? _selectVideoFolder;
    private readonly Action? _resetVideoFolders;
    private readonly Func<string?>? _pickSteamFolder;
    private readonly Func<string, (ActionChoice? Choice, string? Code)>? _registerSteamFolder;
    private readonly Func<string, bool>? _openSupport;
    private readonly SemaphoreSlim _reconcile = new(1, 1);
    private readonly HashSet<Guid> _finished = [];
    private ActionPreviewViewModel? _executing;
    private ActionResult? _returned;
    private CancellationTokenSource? _cancellation;
    private bool _requestInFlight;
    private bool _disposed;
    private bool _rescanPending;

    /// <summary>앱에서 한 번 만들고 같은 실행 관문/조율기를 연결합니다.</summary>
    public ActionCenterViewModel(IActionWorkflow workflow, IUiDispatcher dispatcher, Func<Task> rescan, IEnumerable<ActionChoice>? choices = null, Func<string, bool>? openSettings = null,
        Func<string?>? pickAdobeFolder = null, Func<string, (ActionChoice? Choice, string? Code)>? registerAdobeFolder = null,
        Func<string, bool?>? selectVideoFolder = null, Action? resetVideoFolders = null,
        Func<string?>? pickSteamFolder = null, Func<string, (ActionChoice? Choice, string? Code)>? registerSteamFolder = null,
        Func<string, bool>? openSupport = null)
    {
        _workflow = workflow;
        _dispatcher = dispatcher;
        _rescan = rescan;
        _openSettings = openSettings;
        _pickAdobeFolder = pickAdobeFolder;
        _registerAdobeFolder = registerAdobeFolder;
        _selectVideoFolder = selectVideoFolder;
        _resetVideoFolders = resetVideoFolders;
        _pickSteamFolder = pickSteamFolder;
        _registerSteamFolder = registerSteamFolder;
        _openSupport = openSupport;
        Choices = (choices ?? []).Where(c => workflow.Supports(c.Id, false)).ToArray();
        workflow.Operations.Changed += OnOperationsChanged;
    }
    /// <summary>실행기에서 발급한 읽기 전용 확인 화면입니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCommand), nameof(DismissPreviewCommand))]
    private ActionPreviewViewModel? _preview;
    /// <summary>가장 최근 결과이며 창 재열기/목록 갱신으로 지우지 않습니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private ActionResultViewModel? _result;
    /// <summary>자신의 준비/실행/실제 종료 대기 상태입니다.</summary>
    [ObservableProperty]
    private bool _isWorking;
    /// <summary>목록 확인 상태입니다.</summary>
    [ObservableProperty]
    private bool _isLoading;
    /// <summary>목록 및 실행의 현재 안내입니다.</summary>
    [ObservableProperty]
    private string _status = "조치 기록을 아직 확인하지 않았습니다.";
    /// <summary>자동 재검사 수행/실패를 조치 결과와 별도로 표시합니다.</summary>
    [ObservableProperty]
    private string _rescanStatus = "";
    /// <summary>확인 불가 기록도 빈 정상 목록으로 숨기지 않습니다.</summary>
    [ObservableProperty]
    private string _historyStatus = "복구 기록 확인 전";
    /// <summary>실제 사용자 원문 없이 구성한 기록 목록입니다.</summary>
    public ObservableCollection<RollbackItemViewModel> Records { get; } = [];
    /// <summary>최근 완료 결과는 화면을 전환해도 보존합니다.</summary>
    public ObservableCollection<ActionResultViewModel> Results { get; } = [];
    /// <summary>현재 범위와 코드 등록이 허용하는 조치만 표시합니다. 실행 가능 여부는 미리보기에서 다시 검사합니다.</summary>
    public IReadOnlyList<ActionChoice> Choices { get; private set; }
    /// <summary>효과 필터이며 준비된 계획·실행 결과는 필터와 관계없이 보존합니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChoiceGroups), nameof(ChoiceNotice))]
    private string _effectFilter = "all";
    /// <summary>코드에 정의된 효과 범주로 이동합니다.</summary>
    [RelayCommand]
    private void SelectEffect(string? effect) => EffectFilter = effect is "space" or "startup" or "power" ? effect : "all";
    /// <summary>진단 경고 개수와 별도로 사용자가 고를 수 있는 관리 기능을 효과별로 묶습니다.</summary>
    public IReadOnlyList<ActionChoiceGroup> ChoiceGroups => new[]
    {
        new ActionChoiceGroup("디스크 공간 확보", "정리 가능한 실제 파일과 예상 크기를 먼저 확인합니다. 성능 향상을 보장하는 정리는 아닙니다.", DisplayChoices.Where(c => ActionText.IsSpaceAction(c.Id)).ToArray()),
        new ActionChoiceGroup("로그인할 때 자동 실행 줄이기", "필요 없는 항목만 직접 고르세요. 프로그램은 삭제하지 않으며 원래 등록을 복원할 수 있습니다.", DisplayChoices.Where(c => c.Target is ActionTarget.Startup).ToArray()),
        new ActionChoiceGroup("전력·발열·응답성 조정", "현재 사용 목적에 맞는 전원 계획을 선택합니다. 더 빠른 성능이나 절전 효과가 항상 보장되지는 않습니다.", DisplayChoices.Where(c => c.Id == ActionId.Power).ToArray()),
    }.Where(g => g.Items.Count > 0 && (EffectFilter == "all" || g.Items.Any(c => c == _activeChoice && FeedbackFor(c).HasPreview) || g.Items.Any(c => EffectFilter switch
        { "space" => ActionText.IsSpaceAction(c.Id), "startup" => c.Target is ActionTarget.Startup, "power" => c.Id == ActionId.Power, _ => true }))).ToArray();
    /// <summary>빈 목록을 정상 판정으로 오해하지 않도록 현재 선택의 한계를 안내합니다.</summary>
    public string ChoiceNotice => ChoiceGroups.Count > 0 ? "각 항목의 확인 버튼에서 실제 적용 조건과 영향을 확인하세요." : EffectFilter == "startup"
        ? "아직 선택할 시작 항목이 없습니다. 메인 화면에서 검사한 뒤 다시 확인하세요. 지원하지 않는 항목은 아래 Windows 설정에서 관리할 수 있습니다."
        : "현재 범위에서 선택할 항목이 없습니다. 아래 공식 설정·앱 자체 기능도 확인하세요.";
    /// <summary>검사 결과의 현재 사용자 등록만 갱신하고 오래된 선택 대상을 남기지 않습니다.</summary>
    public void UpdateStartupChoices(PcOptimizer.Core.Models.ScanSnapshot snapshot, bool fullScope)
    {
        var fixedChoices = Choices.Where(c => c.Id is not (ActionId.Startup or ActionId.MachineStartup or ActionId.StartupFolder or ActionId.CommonStartupFolder or ActionId.StartupApproval or ActionId.MachineStartupApproval));
        var targets = StartupSelection.Targets(snapshot).Where(t => StartupRegistration.IsMachine(t.SourceKey)
            ? _workflow.Supports(StartupRegistration.ActionFor(t.SourceKey), false) : fullScope && _workflow.Supports(StartupRegistration.ActionFor(t.SourceKey), false));
        Choices = fixedChoices.Concat(targets.Select(t => StartupRegistration.IsApproval(t.SourceKey)
            ? new ActionChoice(StartupRegistration.ActionFor(t.SourceKey), t, $"작업 관리자 시작 상태 전환 · {t.ValueName} · {StartupRegistration.Label(t.SourceKey)}",
                (StartupRegistration.IsMachine(t.SourceKey) ? "이 PC의 모든 사용자에게 영향을 줍니다. " : "") +
                "작업 관리자의 시작 앱 '사용/사용 안 함'과 같은 값을 씁니다. 등록과 파일은 그대로 두며 현재 상태의 반대로 바꿉니다. 확인 화면에서 어느 쪽으로 바뀌는지 보여 줍니다.")
            : new ActionChoice(StartupRegistration.ActionFor(t.SourceKey), t, $"자동 실행 등록 해제 · {t.ValueName} · {StartupRegistration.Label(t.SourceKey)}",
                (StartupRegistration.IsMachine(t.SourceKey) ? "이 PC의 모든 사용자에게 영향을 줍니다. " : "") +
                "다음 로그인부터 이 등록으로 시작하지 않게 합니다. 필요한 앱인지 직접 선택하세요. 프로그램 삭제·앱 종료는 하지 않으며 원래 등록을 되돌릴 수 있습니다."))).ToArray();
        OnPropertyChanged(nameof(Choices)); OnPropertyChanged(nameof(ChoiceGroups)); OnPropertyChanged(nameof(ChoiceNotice)); PrepareChoiceCommand.NotifyCanExecuteChanged();
    }
    /// <summary>자동 지원 미확인 기능은 성공 버튼 대신 이유와 공식 경로를 제공합니다.</summary>
    public IReadOnlyList<ManualActionChoice> ManualChoices { get; } = [
        new("그 밖의 시작 앱 관리", "현재 사용자·모든 사용자 Run 등록은 검사 후 위 목록에서 해제·복원하거나 작업 관리자 상태를 전환할 수 있습니다. 기본 시작 폴더의 바로가기도 보관·복원할 수 있습니다. RunOnce·서비스·예약 작업 등 미지원 항목은 Windows 설정에서 관리하세요.", SettingsUriPolicy.STARTUP_APPS_SETTINGS_URI),
        new("Windows에서 주사율 설정", "앱의 주사율 시험이 지원되지 않는 화면은 Windows 디스플레이 설정에서 직접 확인하세요.", SettingsUriPolicy.DISPLAY_SETTINGS_URI),
        new("그 밖의 Windows 업데이트 파일 정리", "배달 최적화·업데이트 다운로드 캐시는 위 목록에서 조건을 확인할 수 있습니다. 이전 Windows 설치와 구성 요소 저장소는 Windows 저장소에서 확인하세요.", SettingsUriPolicy.STORAGE_SENSE_SETTINGS_URI),
        new("Steam 다운로드 캐시", "위의 라이브러리 셰이더 캐시와 다른 기능입니다. 다운로드 문제가 있을 때 Steam 자체 정리 절차를 확인하세요. 다시 로그인해야 할 수 있습니다.", null, CacheSupportLinks.SteamDownload),
        new("NVIDIA 셰이더 캐시 전체 초기화", "위 목록의 그래픽 캐시 정리는 30일 이상 쓰이지 않은 파일만 지우는 부분 정리입니다. 캐시를 완전히 비우려면 공식 절차(캐시 끄기·재부팅·정리·설정 복원)를 따르세요.", null, CacheSupportLinks.NvidiaShader),
        new("Direct3D 셰이더 캐시", "Windows 저장소의 임시 파일에서 DirectX 셰이더 캐시 항목을 확인하세요. 정리 후 캐시를 다시 만들 때 로딩·끊김이 늘 수 있습니다.", SettingsUriPolicy.STORAGE_SENSE_SETTINGS_URI)];
    /// <summary>현재 사용자 범위에서 전용 Steam 실행기가 연결된 경우에만 선택을 제공합니다.</summary>
    public bool CanSelectSteamLocation => _pickSteamFolder is not null && _registerSteamFolder is not null && _workflow.Supports(ActionId.SteamShaderCache, false);
    /// <summary>사용자 범위 실행기를 쓸 수 있을 때만 폴더 선택을 제공합니다.</summary>
    public bool CanSelectAdobeLocation => _pickAdobeFolder is not null && _registerAdobeFolder is not null && _workflow.Supports(ActionId.AppFiles, false);
    /// <summary>개인 폴더를 검사할 수 있는 실행 범위에서만 수동 위치 선택을 제공합니다.</summary>
    public bool CanSelectVideoLocations => _selectVideoFolder is not null && _resetVideoFolders is not null && _workflow.Supports(ActionId.AppFiles, false);
    /// <summary>실행 확인 영역 표시 여부입니다.</summary>
    public bool HasPreview => Preview is not null;
    /// <summary>결과 표시 여부입니다.</summary>
    public bool HasResult => Result is not null;
    /// <summary>현재 작업 또는 다른 검사 때문에 변경을 시작할 수 없는지 여부입니다.</summary>
    public bool IsBlocked => IsWorking || IsLoading || _workflow.Operations.State.IsBusy;
    /// <summary>메인 화면에서도 진행/결과 안내를 보존합니다.</summary>
    public string Overview => IsWorking ? "조치가 진행 중입니다. 창을 닫아도 종료 확인을 계속합니다." : Result?.Title ?? HistoryStatus;

    partial void OnIsWorkingChanged(bool value) { CurrentFeedback.IsWorking = value; NotifyGates(); }
    partial void OnIsLoadingChanged(bool value) => NotifyGates();
    partial void OnResultChanged(ActionResultViewModel? value) { UpdateFeedbackResult(value); OnPropertyChanged(nameof(Overview)); }
    partial void OnHistoryStatusChanged(string value) => OnPropertyChanged(nameof(Overview));
    private void NotifyGates()
    {
        OnPropertyChanged(nameof(IsBlocked)); OnPropertyChanged(nameof(Overview));
        ExecuteCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); UndoFeedbackCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); DismissPreviewCommand.NotifyCanExecuteChanged();
        PrepareChoiceCommand.NotifyCanExecuteChanged();
        OpenManualCommand.NotifyCanExecuteChanged();
        SelectAdobeFolderCommand.NotifyCanExecuteChanged();
        SelectSteamFolderCommand.NotifyCanExecuteChanged(); OpenSupportCommand.NotifyCanExecuteChanged();
        SelectVideoFolderCommand.NotifyCanExecuteChanged(); ResetVideoFoldersCommand.NotifyCanExecuteChanged();
    }
    private bool CanPrepare() => !_disposed && !IsBlocked;
    private bool CanExecute() => CanPrepare() && Preview is not null;
    private bool CanDismiss() => !_disposed && !IsWorking && Preview is not null;
    private bool CanCancel() => !_disposed && _cancellation is not null && IsWorking && !_cancellation.IsCancellationRequested;
    private bool CanRestore(RollbackItemViewModel? item) => CanPrepare() && item?.CanRestore == true;
    private bool CanPrepareChoice(ActionChoice? choice) => CanPrepare() && choice is not null && Choices.Contains(choice);
    private bool CanSelectAdobeFolder() => CanPrepare() && CanSelectAdobeLocation;
    private bool CanSelectSteamFolder() => CanPrepare() && CanSelectSteamLocation;
    /// <summary>라이브러리 캐시 선택을 등록하고 공통 워커에서 실제 설정·보호·실행 상태를 확인합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanSelectSteamFolder))]
    private async Task SelectSteamFolderAsync()
    {
        if (!CanSelectSteamFolder()) { return; }
        ActivateFeedback("steam");
        Status = "폴더를 선택한 뒤 대상을 확인합니다.";
        Preview = null;
        ActionChoice? choice = null;
        IsWorking = true;
        try
        {
            var path = _pickSteamFolder!();
            if (path is null || _disposed) { Status = "폴더 선택을 취소했습니다. 변경하지 않았습니다."; return; }
            var registration = _registerSteamFolder!(path);
            choice = registration.Choice;
            if (choice is null)
            {
                Status = new ActionResultViewModel(new(Guid.Empty, false, false, registration.Code ?? "TargetRejected"), ActionId.SteamShaderCache, false, "").Detail;
                return;
            }
            if (!Choices.Any(c => c.Target == choice.Target)) { Choices = [.. Choices, choice]; OnPropertyChanged(nameof(Choices)); OnPropertyChanged(nameof(ChoiceGroups)); OnPropertyChanged(nameof(ChoiceNotice)); }
        }
        catch (Exception) { Status = "Steam 캐시 폴더를 선택하지 못했습니다. 파일을 변경하지 않았습니다."; }
        finally { IsWorking = false; NotifyGates(); }
        if (choice is not null) { await PrepareCoreAsync(choice.Id, choice.Target, false); }
    }
    private bool CanOpenSupport(ManualActionChoice? choice) => CanPrepare() && _openSupport is not null && choice?.HasSupport == true && ManualChoices.Contains(choice);
    /// <summary>공식 안내만 일반 권한 브라우저로 열며 캐시 정리 완료로 기록하지 않습니다.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenSupport))]
    private void OpenSupport(ManualActionChoice? choice)
    {
        if (!CanOpenSupport(choice)) { return; }
        ActivateFeedback(choice!);
        Status = _openSupport!(choice!.SupportUrl!) ? "공식 안내를 열었습니다. 직접 조치한 뒤 다시 검사하세요." : "공식 안내를 열지 못했습니다. 기본 브라우저와 데스크톱 세션을 확인하세요.";
    }
    private bool CanSelectVideoFolder(string? app) => CanPrepare() && CanSelectVideoLocations && app is "davinci" or "capcut";
    private bool CanResetVideoFolders() => CanPrepare() && CanSelectVideoLocations;
    /// <summary>앱에서 확인한 캐시 폴더를 지정하고 기존 검사 흐름에서 용량을 다시 관측합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanSelectVideoFolder))]
    private async Task SelectVideoFolderAsync(string? app)
    {
        if (!CanSelectVideoFolder(app)) { return; }
        ActivateFeedback("video");
        bool? accepted = null;
        IsWorking = true;
        try
        {
            accepted = _selectVideoFolder!(app!);
            Status = accepted switch
            {
                true => "캐시 위치를 이번 실행에 등록했습니다. 재검사에서 용량을 확인합니다. 파일을 삭제하지 않습니다.",
                false => app == "davinci" ? "로컬 드라이브의 CacheClip 폴더 자체를 선택하세요. 이전 선택은 유지했습니다." : "로컬 드라이브의 Cache 폴더 자체를 선택하세요. 초안·프로젝트 상위 폴더는 선택하지 마세요.",
                _ => "폴더 선택을 취소했습니다. 이전 선택은 유지했습니다.",
            };
        }
        catch (Exception) { Status = "캐시 폴더를 선택하지 못했습니다. 파일을 변경하지 않았습니다."; }
        finally { IsWorking = false; NotifyGates(); }
        if (accepted == true && !_disposed) { await RescanVideoLocationsAsync(); }
    }
    /// <summary>수동 선택 두 개를 해제하고 기본 위치·설정 기반 검사로 돌아갑니다.</summary>
    [RelayCommand(CanExecute = nameof(CanResetVideoFolders))]
    private async Task ResetVideoFoldersAsync()
    {
        if (!CanResetVideoFolders()) { return; }
        ActivateFeedback("video");
        _resetVideoFolders!();
        Status = "수동 위치 선택을 해제했습니다. 기본 위치와 Resolve 전역 설정으로 다시 검사합니다.";
        await RescanVideoLocationsAsync();
    }
    private async Task RescanVideoLocationsAsync()
    {
        try { await _rescan(); }
        catch (Exception) { if (!_disposed) { Status = "위치 선택은 반영됐지만 재검사를 시작하거나 완료하지 못했습니다. 다른 작업이 끝나면 메인 화면에서 다시 검사해 주세요."; } }
    }
    /// <summary>선택 위치를 메모리에만 등록한 뒤 공통 관문에서 미리보기를 만듭니다. 선택만으로 삭제하지 않습니다.</summary>
    [RelayCommand(CanExecute = nameof(CanSelectAdobeFolder))]
    private async Task SelectAdobeFolderAsync()
    {
        if (!CanSelectAdobeFolder()) { return; }
        ActivateFeedback("adobe");
        Status = "폴더를 선택한 뒤 대상을 확인합니다.";
        Preview = null;
        ActionChoice? choice = null;
        IsWorking = true;
        try
        {
            var path = _pickAdobeFolder!();
            if (path is null || _disposed) { Status = "폴더 선택을 취소했습니다. 변경하지 않았습니다."; return; }
            var registration = _registerAdobeFolder!(path);
            choice = registration.Choice;
            if (choice is null)
            {
                Status = new ActionResultViewModel(new(Guid.Empty, false, false, registration.Code ?? "TargetRejected"), ActionId.AppFiles, false, "").Detail;
                return;
            }
            if (!Choices.Any(c => c.Target == choice.Target))
            {
                Choices = [.. Choices, choice];
                OnPropertyChanged(nameof(Choices));
                OnPropertyChanged(nameof(ChoiceGroups)); OnPropertyChanged(nameof(ChoiceNotice));
            }
        }
        catch (Exception) { Status = "폴더를 선택하지 못했습니다. 변경하지 않았습니다."; }
        finally { IsWorking = false; NotifyGates(); }
        if (choice is not null) { await PrepareCoreAsync(choice.Id, choice.Target, false); }
    }
    /// <summary>선택한 카탈로그 대상을 조회합니다. 실행은 별도 확인이 필요합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepareChoice))]
    private Task PrepareChoiceAsync(ActionChoice? choice) => !CanPrepareChoice(choice) ? Task.CompletedTask : PrepareInlineAsync(choice!.Id, choice.Target, choice);
    private bool CanOpenManual(ManualActionChoice? choice) => CanPrepare() && _openSettings is not null && choice?.HasSettings == true && ManualChoices.Contains(choice);
    /// <summary>코드 허용 목록의 공식 설정으로 연결하며 자체 변경 완료로 기록하지 않습니다.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenManual))]
    private void OpenManual(ManualActionChoice? choice)
    {
        if (!CanOpenManual(choice)) { return; }
        ActivateFeedback(choice!);
        Status = _openSettings!(choice!.SettingsUri!) ? "Windows 설정에서 조치한 뒤 메인 화면에서 다시 검사해 주세요." : "설정을 열지 못했습니다. Windows 설정에서 해당 항목을 직접 열어 주세요.";
    }

    /// <summary>후속 조치 카드에서 호출하는 미리보기 진입점입니다. 준비만으로 실행하지 않습니다.</summary>
    public async Task PrepareAsync(ActionId id, ActionTarget target, bool restore = false)
    {
        if (!CanPrepare()) { return; }
        ActivateFeedback("external");
        await PrepareCoreAsync(id, target, restore);
    }
    private async Task PrepareCoreAsync(ActionId id, ActionTarget target, bool restore)
    {
        if (!CanPrepare()) { Status = "다른 작업이 끝난 뒤 다시 확인해 주세요."; return; }
        Preview = null;
        IsWorking = true;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        NotifyGates();
        Status = "대상과 영향을 확인하고 있습니다.";
        try
        {
            var prepared = await _workflow.PrepareAsync(id, target, restore, cancellation.Token);
            if (_disposed) { return; }
            Preview = prepared.Plan is { } plan ? new(plan) : null;
            Status = Preview is not null ? "대상과 영향을 읽은 뒤 실행을 선택해 주세요. 아직 변경하지 않았습니다."
                : new ActionResultViewModel(new(Guid.Empty, false, false, prepared.Code ?? "Blocked"), id, restore, "").Detail;
        }
        catch (Exception) { if (!_disposed) { Status = "미리보기를 만들지 못했습니다. 변경을 시작하지 않았습니다."; } }
        finally
        {
            _cancellation = null;
            IsWorking = false;
            NotifyGates();
        }
    }
    /// <summary>복구 목록에서 선택한 기록의 현재 상태부터 확인합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanRestore))]
    private Task RestoreAsync(RollbackItemViewModel? item) => !CanRestore(item) ? Task.CompletedTask : PrepareInlineAsync(item!.ActionId, new ActionTarget.Restore(item.Id), item, true);
    /// <summary>확인 화면을 취소합니다. 원본 계획은 실행되지 않고 자체 만료됩니다.</summary>
    [RelayCommand(CanExecute = nameof(CanDismiss))]
    private void DismissPreview() { Preview = null; Status = "실행하지 않고 확인 화면을 닫았습니다."; }
    /// <summary>실행 확인을 누른 뒤에만 계획 ID를 소비합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        if (!CanExecute()) { return; }
        var plan = Preview!;
        Preview = null;
        _executing = plan;
        _returned = null;
        IsWorking = true;
        _requestInFlight = true;
        RescanStatus = "";
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        NotifyGates();
        Status = plan.ActionId == ActionId.Restart ? "5초 후 Windows에 재부팅을 요청합니다. 지금 취소할 수 있습니다. 전달 후에는 취소할 수 없습니다." : plan.IsRestore ? "설정을 되돌리고 있습니다." : "선택한 조치를 실행하고 있습니다.";
        try
        {
            var result = await _workflow.ExecuteAsync(plan.Id, cancellation.Token);
            _returned = result;
            if (!_disposed) { Result = new(result, plan.ActionId, plan.IsRestore, plan.Estimate); }
        }
        catch (Exception)
        {
            // 서비스의 시작 기록이 있으면 그것을 우선한다. 불명확한 예외를 미시작 성공으로 바꾸지 않는다.
            var observed = _workflow.GetResult(plan.Id);
            _returned = observed ?? new(plan.Id, true, false, "ObservationFailed");
            if (!_disposed)
            {
                Result = new(observed ?? new(plan.Id, true, false, "ObservationFailed"), plan.ActionId, plan.IsRestore, plan.Estimate);
                Status = "실행 결과를 읽지 못했습니다. 현재 상태와 복구 기록을 확인해 주세요.";
            }
        }
        finally { _cancellation = null; _requestInFlight = false; NotifyGates(); }
        await ReconcileAsync();
    }
    /// <summary>실제 실행기에 중단을 요청합니다. 종료 확인 전에는 완료로 표시하지 않습니다.</summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (!CanCancel()) { return; }
        try { _cancellation!.Cancel(); }
        catch (AggregateException) { /* 서비스의 실제 종료/결과 관측은 계속한다. */ }
        Status = "중단을 요청했습니다. 실제 종료가 확인될 때까지 기다려 주세요.";
        NotifyGates();
    }
    /// <summary>앱 시작/창 열기/사용자 갱신 때 목록을 읽습니다. 복구는 자동 실행하지 않습니다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private async Task RefreshAsync()
    {
        if (!CanPrepare()) { return; }
        IsLoading = true; _readingHistory = true;
        try
        {
            var catalog = await _workflow.InspectAsync(CancellationToken.None);
            if (_disposed) { return; }
            foreach (var feedback in _feedback.Values)
            {
                if (feedback.Undo is { } undo && !catalog.Records.Any(r => r.Id == undo.Id && r.State is not (RollbackState.Restored or RollbackState.Unchanged)))
                { feedback.Undo = null; }
            }
            Records.Clear();
            foreach (var r in catalog.Records.OrderByDescending(r => r.NeedsRecovery).ThenByDescending(r => r.UpdatedAt))
            {
                var completed = r.State is RollbackState.Restored or RollbackState.Unchanged;
                var state = r.NeedsRecovery ? "중단된 작업 — 현재 상태 확인 필요" : completed ? "복구 완료" : "적용됨";
                var responsibility = r.Purpose == RollbackPurpose.UserUndo ? "사용자 되돌리기" : "임시 변경 복구";
                var label = r.ActionId is ActionId.Startup or ActionId.MachineStartup or ActionId.StartupFolder or ActionId.CommonStartupFolder or ActionId.StartupApproval or ActionId.MachineStartupApproval && StartupRegistration.Name(r.TargetKey) is { } name
                    ? $"{(StartupRegistration.IsApproval(StartupRegistration.SourceOfKey(r.TargetKey)) ? "작업 관리자 시작 상태" : "자동 실행 등록")} · {name} · {StartupRegistration.Label(StartupRegistration.SourceOfKey(r.TargetKey)!)}" : ActionText.Name(r.ActionId);
                if (r.ActionId is ActionId.NvidiaRebar or ActionId.NvidiaVideo or ActionId.AmdVideo)
                { label += " · " + PcOptimizer.Probes.Actions.Advanced.GpuActionAdapter.TargetLabel(r); }
                Records.Add(new(r.Id, r.ActionId, label, $"{state} · {responsibility} · {r.UpdatedAt.ToLocalTime():g}", r.NeedsRecovery,
                    !completed && _workflow.Supports(r.ActionId, true)));
            }
            var pending = catalog.Records.Count(r => r.NeedsRecovery);
            HistoryStatus = catalog.Issues.Count > 0 ? $"확인할 수 없는 기록 {catalog.Issues.Count}건 · 복구 필요 {pending}건. 손상된 기록은 실행하지 않습니다."
                : Records.Count == 0 ? "이 앱에서 변경한 설정 기록이 없습니다." : $"변경 기록 {Records.Count}건 · 복구 필요 {pending}건";
            Status = "복구 기록을 확인했습니다. 되돌리기는 선택한 항목만 실행합니다.";
        }
        catch (Exception) { if (!_disposed) { HistoryStatus = "복구 기록을 확인하지 못했습니다. 권한 또는 파일 상태를 확인해 주세요."; } }
        finally { _readingHistory = false; IsLoading = false; }
    }

    private void OnOperationsChanged(object? sender, EventArgs e) => _ = HandleOperationsChangedAsync();
    private async Task HandleOperationsChangedAsync()
    {
        try { await _dispatcher.InvokeAsync(NotifyGates); await ReconcileAsync(); }
        catch (Exception) { /* UI 종료 중이어도 실제 조율기의 작업/저장소 소유권은 유지된다. */ }
    }
    private async Task ReconcileAsync()
    {
        await _reconcile.WaitAsync();
        var refresh = false;
        var rescan = false;
        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (_disposed || _requestInFlight || _workflow.Operations.State.IsBusy) { return; }
                if (_executing is { } plan && _finished.Add(plan.Id))
                {
                    var final = _workflow.GetResult(plan.Id) ?? _returned;
                    Result = new(final ?? new(plan.Id, true, false, "ObservationFailed"), plan.ActionId, plan.IsRestore, plan.Estimate);
                    Results.Insert(0, Result);
                    _executing = null;
                    IsWorking = false;
                    Status = Result.Title;
                    _rescanPending |= final?.Started == true && plan.ActionId != ActionId.Restart;
                    refresh = true;
                }
                if (_rescanPending) { _rescanPending = false; rescan = true; }
            });
            // 자신의 목록 갱신이 발생시키는 관문 이벤트는 세마포어를 기다리므로 중복 후속 작업을 시작하지 않는다.
            if (refresh && !_disposed) { await OnUiAsync(RefreshAsync); }
            if (rescan && !_disposed)
            {
                var feedback = CurrentFeedback;
                try
                {
                    await _dispatcher.InvokeAsync(() => SetRescanFeedback(feedback, "변경 후 상태를 다시 검사합니다."));
                    await OnUiAsync(_rescan);
                    await _dispatcher.InvokeAsync(() => SetRescanFeedback(feedback, "재검사가 끝났습니다. 메인 화면에서 결과를 확인해 주세요."));
                }
                catch (Exception) { await _dispatcher.InvokeAsync(() => SetRescanFeedback(feedback, "자동 재검사를 시작하지 못했습니다. 메인 화면에서 다시 검사해 주세요.")); }
            }
        }
        finally { _reconcile.Release(); }
    }
    private async Task OnUiAsync(Func<Task> action)
    {
        Task task = Task.CompletedTask;
        await _dispatcher.InvokeAsync(() => task = action());
        await task;
    }
    /// <summary>앱 수명의 구독만 해제합니다. 별도 창 닫기에는 호출하지 않습니다.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _workflow.Operations.Changed -= OnOperationsChanged;
    }
}
