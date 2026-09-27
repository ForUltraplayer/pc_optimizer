/**
 * @file    : GpuCardViewModel.cs
 * @author  : rudals252
 * @brief   : GPU 대상·현재 값·변경 확인을 분리하고 네이티브 읽기의 실제 수명을 추적
 */
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>조회로 확인한 대상만 기존 조치 확인 화면으로 보냅니다.</summary>
public sealed partial class GpuCardViewModel : ObservableObject, IDisposable
{
    private readonly IOperationCoordinator _operations;
    private readonly Func<CancellationToken, GpuOptionsResult> _discover;
    private readonly Func<string, GpuSettingState> _observe;
    private readonly Func<AdvancedChoice, Task> _prepare;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeSpan _timeout;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private bool _observed;
    /// <summary>기능과 관측 공급자를 연결하며 생성 시에는 드라이버를 호출하지 않습니다.</summary>
    public GpuCardViewModel(GpuFeature feature, IOperationCoordinator operations,
        Func<CancellationToken, GpuOptionsResult> discover, Func<string, GpuSettingState> observe,
        Func<AdvancedChoice, Task> prepare, IUiDispatcher dispatcher, TimeSpan? timeout = null)
    {
        Feature = feature; _operations = operations; _discover = discover; _observe = observe;
        _prepare = prepare; _dispatcher = dispatcher; _timeout = timeout ?? TimeSpan.FromSeconds(45);
        operations.Changed += OnOperationChanged;
    }
    /// <summary>닫힌 GPU 기능입니다.</summary>
    public GpuFeature Feature { get; }
    /// <summary>기능 이름입니다.</summary>
    public string Title => GpuOptions.Name(Feature);
    /// <summary>설정 변경 범위와 실제 효과의 차이를 설명합니다.</summary>
    public string Explanation => Feature switch
    {
        GpuFeature.NvidiaRebar => "게임별 ReBAR 드라이버 프로필을 켜거나 끕니다. BIOS의 ReBAR를 켜는 기능은 아닙니다. 지원 설정이 확인된 게임만 표시하며 게임을 종료한 뒤 변경하세요. 켜도 게임에 따라 성능이 떨어질 수 있습니다.",
        GpuFeature.NvidiaVideo => "RTX 영상 초고해상도를 켜거나 끄고 품질을 선택합니다. 드라이버 비공개 인터페이스를 사용하는 베타 기능입니다. GPU 부하·전력이 늘 수 있으며 MPO·지원 재생기 조건에 따라 실제 효과가 달라집니다.",
        _ => "AMD 공식 ADLX의 동영상 업스케일링을 켜거나 끕니다. Radeon Image Sharpening이 켜져 있으면 무시될 수 있습니다. 게임용 RSR·가상 초고해상도·선명도 값은 바꾸지 않습니다.",
    };
    /// <summary>현재 드라이버에서 확인한 대상만 제공합니다.</summary>
    [ObservableProperty] private IReadOnlyList<GpuOptionTarget> _targets = [];
    /// <summary>사용자가 선택한 프로필 또는 GPU 출력입니다.</summary>
    [ObservableProperty] private GpuOptionTarget? _selectedTarget;
    /// <summary>선택 가능한 값입니다.</summary>
    public IReadOnlyList<GpuSettingChoice> Choices => SelectedTarget?.Choices ?? [];
    /// <summary>확인 화면에 보낼 값이며 이 선택만으로 변경하지 않습니다.</summary>
    [ObservableProperty] private GpuSettingChoice? _selectedChoice;
    /// <summary>지원 여부·조회 시각 또는 실패 이유입니다.</summary>
    [ObservableProperty] private string _status = "아직 조회하지 않았습니다. 지원 대상 찾기를 누르세요.";
    /// <summary>읽기 전 값을 켜짐/꺼짐으로 추정하지 않습니다.</summary>
    [ObservableProperty] private string _stateText = "현재 설정: 확인 전";
    /// <summary>호출이 진행 중일 때 선택도 잠급니다.</summary>
    [ObservableProperty] private bool _isLoading;
    /// <summary>다른 조치와 상호 배제합니다.</summary>
    public bool CanSelect => CanAct();
    private bool CanAct() => !_disposed && !IsLoading && !_operations.State.IsBusy;
    private bool CanRead() => CanAct() && SelectedTarget is { } target && Targets.Contains(target);
    private bool CanPrepare() => CanRead() && _observed && SelectedChoice is { } choice && Choices.Contains(choice);
    partial void OnSelectedTargetChanged(GpuOptionTarget? value)
    {
        _observed = false; StateText = "현재 설정: 선택 대상 상태 읽기가 필요합니다";
        OnPropertyChanged(nameof(Choices)); SelectedChoice = null; NotifyCommands();
    }
    partial void OnSelectedChoiceChanged(GpuSettingChoice? value) => NotifyCommands();
    partial void OnIsLoadingChanged(bool value) => NotifyCommands();
    private void NotifyCommands()
    {
        DiscoverCommand.NotifyCanExecuteChanged(); ReadCommand.NotifyCanExecuteChanged(); PrepareCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSelect));
    }
    private async void OnOperationChanged(object? sender, EventArgs args)
    {
        try { await _dispatcher.InvokeAsync(() => { if (!_disposed) { NotifyCommands(); } }); }
        catch (TaskCanceledException) { }
    }
    /// <summary>지원 대상 검색과 첫 대상 관측을 UI 스레드 밖에서 수행합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task DiscoverAsync()
    {
        var previousKey = SelectedTarget?.Key;
        await RunAsync(ct =>
        {
            var result = _discover(ct);
            var target = result.Targets.FirstOrDefault(t => t.Key == previousKey) ?? result.Targets.FirstOrDefault();
            var state = target is null ? null : _observe(target.Key);
            return (result, target, state);
        }, data =>
        {
            Targets = data.result.Targets; SelectedTarget = data.target; Status = data.result.Status;
            if (data.state is { } state) { SetState(state); }
        });
    }
    /// <summary>대상 변경 후 반드시 실제 값을 읽어야 변경 버튼이 활성화됩니다.</summary>
    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadAsync()
    {
        var key = SelectedTarget!.Key;
        return RunAsync(_ => _observe(key), state => { SetState(state); Status = $"설정값 확인: {DateTime.Now:HH:mm:ss}. 영상 처리 중 또는 BIOS 활성 상태를 의미하지 않습니다."; });
    }
    private void SetState(GpuSettingState state)
    {
        var value = Feature == GpuFeature.NvidiaVideo && !state.Enabled ? 0 : state.Value;
        SelectedChoice = Choices.FirstOrDefault(c => c.Value == value);
        var label = SelectedChoice?.Label ?? "알 수 없는 값";
        var origin = Feature == GpuFeature.NvidiaRebar ? state.Location != 0 ? " · 상위 프로필 상속" : state.Predefined != 0 ? " · 드라이버 기본값" : " · 사용자 지정" : "";
        StateText = "현재 설정: " + label + origin; _observed = SelectedChoice is not null; NotifyCommands();
    }
    private async Task RunAsync<T>(Func<CancellationToken, T> read, Action<T> update)
    {
        using var lease = _operations.TryAcquire(OperationKind.Prepare);
        if (lease is null) { return; }
        IsLoading = true; _observed = false; StateText = "현재 설정: 읽는 중"; Status = "드라이버 응답을 기다리는 중입니다.";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        try
        {
            var token = cancellation.Token;
            var work = Task.Run(() => read(token), CancellationToken.None); lease.Track(work);
            var result = await work.WaitAsync(_timeout, token);
            if (!_disposed) { update(result); }
        }
        catch (TimeoutException)
        {
            cancellation.Cancel(); StateText = "현재 설정: 확인 불가";
            Status = "드라이버 응답 시간이 초과됐습니다. 실제 조회가 종료될 때까지 다른 검사·변경은 기다려야 합니다.";
        }
        catch (Exception)
        {
            StateText = "현재 설정: 확인 불가";
            Status = "설정을 읽지 못했습니다. 지원 대상 찾기로 GPU·드라이버·사용자 범위를 다시 확인하세요.";
        }
        finally { IsLoading = false; }
    }
    /// <summary>목록에 없는 대상·값은 전달하지 않으며 실제 쓰기는 확인 화면에서만 일어납니다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private async Task PrepareAsync()
    {
        if (!CanPrepare()) { return; }
        var choice = new AdvancedChoice(Title, GpuOptions.Action(Feature), new ActionTarget.Gpu(Feature, SelectedTarget!.Key, SelectedChoice!.Value));
        _observed = false; StateText = "확인 화면 이동 후 현재 설정을 다시 읽어 주세요."; NotifyCommands();
        await _prepare(choice);
    }
    /// <inheritdoc />
    public void Dispose()
    { if (_disposed) { return; } _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); _operations.Changed -= OnOperationChanged; }
}
