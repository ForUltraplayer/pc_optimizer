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

/// <summary>창이 닫혀도 보존하는 공통 조치 화면 모델입니다. Dispose는 실제 작업을 취소하지 않습니다.</summary>
public sealed partial class ActionCenterViewModel : ObservableObject, IDisposable
{
    private readonly IActionWorkflow _workflow;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<Task> _rescan;
    private readonly SemaphoreSlim _reconcile = new(1, 1);
    private readonly HashSet<Guid> _finished = [];
    private ActionPreviewViewModel? _executing;
    private ActionResult? _returned;
    private CancellationTokenSource? _cancellation;
    private bool _requestInFlight;
    private bool _disposed;
    private bool _rescanPending;

    /// <summary>앱에서 한 번 만들고 같은 실행 관문/조율기를 연결합니다.</summary>
    public ActionCenterViewModel(IActionWorkflow workflow, IUiDispatcher dispatcher, Func<Task> rescan, IEnumerable<ActionChoice>? choices = null)
    {
        _workflow = workflow;
        _dispatcher = dispatcher;
        _rescan = rescan;
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
    public IReadOnlyList<ActionChoice> Choices { get; }
    /// <summary>실행 확인 영역 표시 여부입니다.</summary>
    public bool HasPreview => Preview is not null;
    /// <summary>결과 표시 여부입니다.</summary>
    public bool HasResult => Result is not null;
    /// <summary>현재 작업 또는 다른 검사 때문에 변경을 시작할 수 없는지 여부입니다.</summary>
    public bool IsBlocked => IsWorking || IsLoading || _workflow.Operations.State.IsBusy;
    /// <summary>메인 화면에서도 진행/결과 안내를 보존합니다.</summary>
    public string Overview => IsWorking ? "조치가 진행 중입니다. 창을 닫아도 종료 확인을 계속합니다." : Result?.Title ?? HistoryStatus;

    partial void OnIsWorkingChanged(bool value) => NotifyGates();
    partial void OnIsLoadingChanged(bool value) => NotifyGates();
    partial void OnResultChanged(ActionResultViewModel? value) => OnPropertyChanged(nameof(Overview));
    partial void OnHistoryStatusChanged(string value) => OnPropertyChanged(nameof(Overview));
    private void NotifyGates()
    {
        OnPropertyChanged(nameof(IsBlocked)); OnPropertyChanged(nameof(Overview));
        ExecuteCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); DismissPreviewCommand.NotifyCanExecuteChanged();
        PrepareChoiceCommand.NotifyCanExecuteChanged();
    }
    private bool CanPrepare() => !_disposed && !IsBlocked;
    private bool CanExecute() => CanPrepare() && Preview is not null;
    private bool CanDismiss() => !_disposed && !IsWorking && Preview is not null;
    private bool CanCancel() => !_disposed && _cancellation is not null && IsWorking && !_cancellation.IsCancellationRequested;
    private bool CanRestore(RollbackItemViewModel? item) => CanPrepare() && item?.CanRestore == true;
    private bool CanPrepareChoice(ActionChoice? choice) => CanPrepare() && choice is not null && Choices.Contains(choice);
    /// <summary>선택한 카탈로그 대상을 조회합니다. 실행은 별도 확인이 필요합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepareChoice))]
    private Task PrepareChoiceAsync(ActionChoice? choice) => choice is null ? Task.CompletedTask : PrepareAsync(choice.Id, choice.Target);

    /// <summary>후속 조치 카드에서 호출하는 미리보기 진입점입니다. 준비만으로 실행하지 않습니다.</summary>
    public async Task PrepareAsync(ActionId id, ActionTarget target, bool restore = false)
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
    private Task RestoreAsync(RollbackItemViewModel? item) => item is null ? Task.CompletedTask : PrepareAsync(item.ActionId, new ActionTarget.Restore(item.Id), true);
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
        Status = plan.IsRestore ? "설정을 되돌리고 있습니다." : "선택한 조치를 실행하고 있습니다.";
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
        IsLoading = true;
        try
        {
            var catalog = await _workflow.InspectAsync(CancellationToken.None);
            if (_disposed) { return; }
            Records.Clear();
            foreach (var r in catalog.Records.OrderByDescending(r => r.NeedsRecovery).ThenByDescending(r => r.UpdatedAt))
            {
                var completed = r.State is RollbackState.Restored or RollbackState.Unchanged;
                var state = r.NeedsRecovery ? "중단된 작업 — 현재 상태 확인 필요" : completed ? "복구 완료" : "적용됨";
                var responsibility = r.Purpose == RollbackPurpose.UserUndo ? "사용자 되돌리기" : "임시 변경 복구";
                Records.Add(new(r.Id, r.ActionId, ActionText.Name(r.ActionId), $"{state} · {responsibility} · {r.UpdatedAt.ToLocalTime():g}", r.NeedsRecovery,
                    !completed && _workflow.Supports(r.ActionId, true)));
            }
            var pending = catalog.Records.Count(r => r.NeedsRecovery);
            HistoryStatus = catalog.Issues.Count > 0 ? $"확인할 수 없는 기록 {catalog.Issues.Count}건 · 복구 필요 {pending}건. 손상된 기록은 실행하지 않습니다."
                : Records.Count == 0 ? "이 앱에서 변경한 설정 기록이 없습니다." : $"변경 기록 {Records.Count}건 · 복구 필요 {pending}건";
            Status = "복구 기록을 확인했습니다. 되돌리기는 선택한 항목만 실행합니다.";
        }
        catch (Exception) { if (!_disposed) { HistoryStatus = "복구 기록을 확인하지 못했습니다. 권한 또는 파일 상태를 확인해 주세요."; } }
        finally { IsLoading = false; }
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
                    _rescanPending |= final?.Started == true;
                    refresh = true;
                }
                if (_rescanPending) { _rescanPending = false; rescan = true; }
            });
            // 자신의 목록 갱신이 발생시키는 관문 이벤트는 세마포어를 기다리므로 중복 후속 작업을 시작하지 않는다.
            if (refresh && !_disposed) { await OnUiAsync(RefreshAsync); }
            if (rescan && !_disposed)
            {
                try
                {
                    await _dispatcher.InvokeAsync(() => RescanStatus = "변경 후 상태를 다시 검사합니다.");
                    await OnUiAsync(_rescan);
                    await _dispatcher.InvokeAsync(() => RescanStatus = "재검사가 끝났습니다. 메인 화면에서 결과를 확인해 주세요.");
                }
                catch (Exception) { await _dispatcher.InvokeAsync(() => RescanStatus = "자동 재검사를 시작하지 못했습니다. 메인 화면에서 다시 검사해 주세요."); }
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
