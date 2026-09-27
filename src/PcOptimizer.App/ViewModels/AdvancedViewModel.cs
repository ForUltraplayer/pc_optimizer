/**
 * @file    : AdvancedViewModel.cs
 * @author  : rudals252
 * @brief   : 고급 카드의 읽기 전용 상태와 기존 공통 조치 확인 화면 연결
 */
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>선택값을 확인 화면으로 보내는 버튼입니다. 클릭만으로 설정을 쓰지 않습니다.</summary>
public sealed record AdvancedChoice(string Label, ActionId Id, ActionTarget Target);

/// <summary>현재 설정값과 영향·선택을 한 카드에 표시합니다.</summary>
public sealed partial class AdvancedCard : ObservableObject
{
    /// <summary>카드의 고정 옵션입니다.</summary>
    public AdvancedOption Option { get; }
    /// <summary>옵션의 한국어 이름입니다.</summary>
    public string Title => AdvancedOptions.Name(Option);
    /// <summary>기대 효과와 실제 적용 조건입니다.</summary>
    public string Explanation => Option switch
    {
        AdvancedOption.Mpo => "화면 깜박임·브라우저 영상 문제를 비교할 때 사용하세요. MPO를 끄면 전력·영상 처리 효율이 달라질 수 있습니다. Windows 기본 동작은 MPO를 항상 강제로 켠다는 뜻이 아닙니다.",
        AdvancedOption.Hags => "GPU 작업 예약 방식을 바꿉니다. 게임·GPU·드라이버에 따라 결과가 달라지므로 무조건 켜기를 권장하지 않습니다. 설정값이 없는 PC는 Windows에서 지원 여부를 먼저 확인하세요.",
        _ => "Windows가 게임 실행 중 자원을 배분하는 동작을 조정합니다. 성능 향상을 보장하지 않으며 다음 게임 실행에서 차이를 확인하세요.",
    };
    /// <summary>선택 버튼의 닫힌 목록입니다.</summary>
    public IReadOnlyList<AdvancedChoice> Choices { get; }
    /// <summary>설정값을 조회하기 전에는 켜짐/꺼짐을 추정하지 않습니다.</summary>
    [ObservableProperty] private string _stateText = "아직 확인하지 않았습니다";
    /// <summary>관측 범위와 확인 실패 설명입니다.</summary>
    [ObservableProperty] private string _detail = "현재 상태 확인을 누르면 저장된 설정값을 읽습니다.";
    /// <summary>관측 및 범위가 허용될 때만 버튼을 활성화합니다.</summary>
    [ObservableProperty] private bool _canChange;
    /// <summary>지원하지 않는 선택값을 만들지 않습니다.</summary>
    public AdvancedCard(AdvancedOption option)
    {
        Option = option;
        Choices = (option == AdvancedOption.Mpo ? new[] { AdvancedSetting.Off, AdvancedSetting.Default }
            : [AdvancedSetting.On, AdvancedSetting.Off, AdvancedSetting.Default]).Select(setting => new AdvancedChoice(
                setting switch { AdvancedSetting.On => "켜기 확인", AdvancedSetting.Off => "끄기 확인", _ => "Windows 기본값 확인" },
                AdvancedOptions.Action(option), new ActionTarget.Advanced(option, setting))).ToArray();
    }
    /// <summary>설정값을 실제 GPU 동작 상태와 구분합니다.</summary>
    public void Update(AdvancedObservation observation)
    {
        StateText = observation.Setting switch
        { AdvancedSetting.Default => "설정값: Windows 기본 동작", AdvancedSetting.On => "설정값: 켜짐", AdvancedSetting.Off => "설정값: 꺼짐", _ => "설정값: 확인 불가" };
        Detail = observation.Detail; CanChange = observation.CanChange;
    }
}

/// <summary>앱 수명 동안 같은 실행 관문과 조치 화면을 공유합니다.</summary>
public sealed partial class AdvancedViewModel : ObservableObject, IDisposable
{
    private readonly IOperationCoordinator _operations;
    private readonly Func<IReadOnlyList<AdvancedObservation>> _observe;
    private readonly Func<AdvancedChoice, Task> _prepare;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<string, bool> _openSettings;
    private bool _disposed;
    /// <summary>읽기·변경 경계를 분리해 연결합니다.</summary>
    public AdvancedViewModel(IOperationCoordinator operations, Func<IReadOnlyList<AdvancedObservation>> observe,
        Func<AdvancedChoice, Task> prepare, IUiDispatcher dispatcher, Func<string, bool> openSettings)
    {
        _operations = operations; _observe = observe; _prepare = prepare; _dispatcher = dispatcher; _openSettings = openSettings;
        operations.Changed += OnOperationChanged;
    }
    /// <summary>지원 계약이 등록된 카드만 표시합니다.</summary>
    public IReadOnlyList<AdvancedCard> Cards { get; } = Enum.GetValues<AdvancedOption>().Select(o => new AdvancedCard(o)).ToArray();
    /// <summary>재부팅 선택은 이후 별도 확인 화면에서 실행합니다.</summary>
    public IReadOnlyList<AdvancedChoice> Restarts { get; } = [
        new("BIOS/UEFI 진입 확인", ActionId.Restart, new ActionTarget.Restart(RestartDestination.Firmware)),
        new("안전 모드 선택 화면 진입 확인", ActionId.Restart, new ActionTarget.Restart(RestartDestination.AdvancedStartup))];
    /// <summary>읽기 시각·실행 관문 상태 안내입니다.</summary>
    [ObservableProperty] private string _status = "기능별 영향을 확인하고 필요한 항목만 선택하세요. 설정은 확인 화면에서 실행한 뒤에 바뀝니다.";
    /// <summary>자체 읽기/확인 작업 진행 여부입니다.</summary>
    [ObservableProperty] private bool _isLoading;
    private bool CanAct() => !_disposed && !IsLoading && !_operations.State.IsBusy;
    partial void OnIsLoadingChanged(bool value) => NotifyCommands();
    private void NotifyCommands()
    { RefreshCommand.NotifyCanExecuteChanged(); PrepareCommand.NotifyCanExecuteChanged(); OpenSettingCommand.NotifyCanExecuteChanged(); }
    private async void OnOperationChanged(object? sender, EventArgs e)
    {
        try { await _dispatcher.InvokeAsync(() => { if (!_disposed) { NotifyCommands(); } }); }
        catch (TaskCanceledException) { }
    }
    /// <summary>공통 관문 안에서만 비동기 조회하고 실제 종료까지 점유합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task RefreshAsync()
    {
        using var lease = _operations.TryAcquire(OperationKind.Prepare);
        if (lease is null) { return; }
        IsLoading = true;
        try
        {
            var work = Task.Run(_observe); lease.Track(work);
            var observations = await work;
            if (_disposed) { return; }
            foreach (var card in Cards) { card.Update(observations.Single(o => o.Option == card.Option)); }
            Status = $"설정값 확인: {DateTime.Now:HH:mm:ss}. 재부팅 여부와 실제 영상 처리 상태는 이 값만으로 확인할 수 없습니다.";
        }
        catch (Exception)
        {
            foreach (var card in Cards) { card.Update(new(card.Option, null, false, "조회에 실패했습니다. 다시 확인하세요.")); }
            Status = "현재 상태를 확인하지 못했습니다. 설정을 변경하지 않았습니다.";
        }
        finally { IsLoading = false; }
    }
    private bool CanPrepare(AdvancedChoice? choice) => CanAct() && choice is not null
        && (Restarts.Contains(choice) || Cards.Any(c => c.CanChange && c.Choices.Contains(choice)));
    /// <summary>기존 준비/실행/복원 UI로 연결합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private async Task PrepareAsync(AdvancedChoice? choice)
    {
        if (choice is null || !CanPrepare(choice)) { return; }
        await _prepare(choice);
    }
    /// <summary>등록된 Windows 설정 URI만 연결합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private void OpenSetting(string? uri)
    {
        if (uri is SettingsUriPolicy.GAME_MODE_SETTINGS_URI or SettingsUriPolicy.ADVANCED_GRAPHICS_SETTINGS_URI)
        { if (!_openSettings(uri)) { Status = "Windows 설정을 열지 못했습니다. 시작 메뉴에서 설정을 직접 열어 주세요."; } }
    }
    /// <inheritdoc />
    public void Dispose() { _disposed = true; _operations.Changed -= OnOperationChanged; }
}
