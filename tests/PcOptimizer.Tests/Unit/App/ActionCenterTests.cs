/**
 * @file    : ActionCenterTests.cs
 * @author  : rudals252
 * @brief   : 실제 조율기 대역 조치로 확인·거절·중단·늦은 결과·한 번 재검사·복구 목록 경계 검증
 */
using System.IO;
using System.Text.Json;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>사용자 설정을 변경하지 않는 코드 어댑터로 공통 화면을 검증합니다.</summary>
public sealed class ActionCenterTests
{
    internal static readonly ActionSession Session = new("fixture-private-sid", 1, ActionUserScope.Full);
    internal static readonly ActionTarget Target = new ActionTarget.Power(Guid.NewGuid());

    /// <summary>미리보기만으로 실행하지 않으며 확인 취소 뒤 실행할 수 없습니다.</summary>
    [Fact]
    public async Task PreviewAndDismissNeverExecute()
    {
        var adapter = new Adapter();
        using var vm = Create(adapter);
        await vm.PrepareAsync(ActionId.Power, Target);
        Assert.Equal(0, adapter.Executions);
        Assert.True(vm.ExecuteCommand.CanExecute(null));
        Assert.Contains("fixture target", vm.Preview!.Target);
        vm.DismissPreviewCommand.Execute(null);
        Assert.False(vm.HasPreview);
        Assert.False(vm.ExecuteCommand.CanExecute(null));
        Assert.Equal(0, adapter.Executions);
    }

    /// <summary>실제 공통 관문이 풀린 뒤에만 한 번 재검사하고 결과를 계속 보존합니다.</summary>
    [Fact]
    public async Task SuccessfulActionRescansOnceAfterReleaseAndPreservesResult()
    {
        var adapter = new Adapter();
        var gate = new OperationCoordinator();
        var rescans = 0;
        using var vm = Create(adapter, gate, () => { Assert.False(gate.State.IsBusy); rescans++; return Task.CompletedTask; });
        await vm.PrepareAsync(ActionId.Power, Target);
        await vm.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(1, adapter.Executions);
        Assert.Equal(1, rescans);
        Assert.Single(vm.Results);
        Assert.Contains("완료", vm.Result!.Title);
        await vm.RefreshCommand.ExecuteAsync(null);
        using (gate.TryAcquire(OperationKind.Scan)!) { }
        Assert.Equal(1, rescans);
        Assert.Single(vm.Results);
        Assert.NotNull(vm.Result);
    }

    /// <summary>실행 전에 세션이 바뀌면 거절로 표시하고 일부 정리나 재검사를 주장하지 않습니다.</summary>
    [Fact]
    public async Task SessionRejectionIsNotPartialCleanup()
    {
        var adapter = new Adapter();
        var session = Session;
        var rescans = 0;
        var workflow = new ActionWorkflow(new OperationCoordinator(), new Store(), () => session, [adapter], []);
        using var vm = new ActionCenterViewModel(workflow, new Dispatch(), () => { rescans++; return Task.CompletedTask; });
        await vm.PrepareAsync(ActionId.Power, Target);
        session = session with { SessionId = 99 };
        await vm.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(0, adapter.Executions);
        Assert.Equal(0, rescans);
        Assert.Contains("변경하기 전에", vm.Result!.Title);
        Assert.DoesNotContain("일부", vm.Result.Detail);
    }

    /// <summary>취소를 무시하는 작업은 종료 대기/진입 차단을 유지하고 늦은 실제 실패로 갱신합니다.</summary>
    [Fact]
    public async Task CancellationWaitsForActualExitThenShowsLateFailure()
    {
        var adapter = new Adapter { Release = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var gate = new OperationCoordinator();
        var rescan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rescans = 0;
        using var vm = Create(adapter, gate, () => { rescans++; rescan.TrySetResult(); return Task.CompletedTask; });
        await vm.PrepareAsync(ActionId.Power, Target);
        var execution = vm.ExecuteCommand.ExecuteAsync(null);
        await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(vm.CancelCommand.CanExecute(null));
        vm.CancelCommand.Execute(null);
        await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(vm.IsWorking);
        Assert.True(vm.Result!.IsDraining);
        Assert.False(vm.ExecuteCommand.CanExecute(null));
        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.Equal(0, rescans);
        Assert.Null(gate.TryAcquire(OperationKind.Scan));
        adapter.Release.SetException(new IOException("fixture late failure"));
        await rescan.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(vm.IsWorking);
        Assert.False(vm.Result!.IsDraining);
        Assert.Contains("일부", vm.Result.Detail);
        Assert.Equal(1, rescans);
        Assert.Single(vm.Results);
    }

    /// <summary>후속 재검사 실패를 조치 성공/실패와 별도로 남깁니다.</summary>
    [Fact]
    public async Task RescanFailureDoesNotEraseActionOutcome()
    {
        using var vm = Create(new Adapter(), rescan: () => throw new IOException("fixture"));
        await vm.PrepareAsync(ActionId.Power, Target);
        await vm.ExecuteCommand.ExecuteAsync(null);
        Assert.Contains("완료", vm.Result!.Title);
        Assert.Contains("시작하지 못했습니다", vm.RescanStatus);
        Assert.Single(vm.Results);
    }

    /// <summary>부족한 공간/불명확한 관측을 0 또는 확보 성공으로 표현하지 않습니다.</summary>
    [Theory]
    [InlineData(null, "확인되지 않음")]
    [InlineData(-1024L, "감소")]
    [InlineData(2048L, "증가")]
    [InlineData(0L, "변화 없음")]
    [InlineData(long.MinValue, "감소")]
    public void ActualAndEstimatedSpaceRemainSeparate(long? delta, string expected)
    {
        var result = new ActionResultViewModel(new(Guid.NewGuid(), true, true, "Completed", new(delta)), ActionId.UserFiles, false, "대상 100 MiB (예상치)");
        Assert.Contains(expected, result.Actual);
        Assert.Contains("예상치", result.Estimate);
        Assert.DoesNotContain("100", result.Actual);
    }

    /// <summary>부분 실패/복구 실패/실행 전 거절은 다른 결과 문구를 갖습니다.</summary>
    [Theory]
    [InlineData(true, false, "Partial", "일부")]
    [InlineData(true, true, "Failed", "복구")]
    [InlineData(false, false, "PlanExpired", "만료")]
    [InlineData(false, false, "Busy", "다른 검사")]
    public void ResultStatesOfferAppropriateNextStep(bool started, bool restore, string code, string expected)
    {
        var result = new ActionResultViewModel(new(Guid.NewGuid(), started, false, code), ActionId.Power, restore, "");
        Assert.Contains(expected, result.Detail);
        Assert.DoesNotContain("완료했습니다", result.Title);
    }

    /// <summary>손상 기록은 빈 정상 목록으로 숨기지 않으며 지원되지 않는 복구 버튼은 이유와 함께 비활성입니다.</summary>
    [Fact]
    public async Task RecoveryListSeparatesIssuesAndNeverExposesRawValuesOrSid()
    {
        var record = RollbackTests.Record(Session.Sid) with { TargetKey = "secret-path", Before = new(true, 1, [81, 82, 83]) };
        var store = new Store { Catalog = new([record], [new("private.json", "UntrustedRecord")]) };
        var workflow = new ActionWorkflow(new OperationCoordinator(), store, () => Session, [], []);
        using var vm = new ActionCenterViewModel(workflow, new Dispatch(), () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("확인할 수 없는 기록 1건", vm.HistoryStatus);
        Assert.Contains("복구 필요 1건", vm.HistoryStatus);
        var item = Assert.Single(vm.Records);
        Assert.False(item.CanRestore);
        Assert.False(vm.RestoreCommand.CanExecute(item));
        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("fixture-private-sid", json);
        Assert.DoesNotContain("secret-path", json);
        Assert.DoesNotContain("Before", json);
    }

    /// <summary>목록 읽기 실패는 이전 기록과 결과를 지우지 않고 확인 불가를 표시합니다.</summary>
    [Fact]
    public async Task HistoryReadFailureDoesNotMeanNoRecords()
    {
        var store = new Store { Catalog = new([RollbackTests.Record(Session.Sid)], []) };
        var workflow = new ActionWorkflow(new OperationCoordinator(), store, () => Session, [], []);
        using var vm = new ActionCenterViewModel(workflow, new Dispatch(), () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        store.Fail = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Single(vm.Records);
        Assert.Contains("확인하지 못했습니다", vm.HistoryStatus);
    }

    /// <summary>저장 기록 선택부터 확인·실제 복구 조율기·목록 갱신까지 연결됩니다.</summary>
    [Fact]
    public async Task RegisteredRecoveryCanBePreviewedExecutedAndRefreshed()
    {
        var record = RollbackTests.Record(Session.Sid, RollbackState.Applied, 1);
        var store = new Store { Catalog = new([record], []) };
        var adapter = new Reversible(record.Applied);
        var workflow = new ActionWorkflow(new OperationCoordinator(), store, () => Session, [], [adapter]);
        var rescans = 0;
        using var vm = new ActionCenterViewModel(workflow, new Dispatch(), () => { rescans++; return Task.CompletedTask; });
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.RestoreCommand.ExecuteAsync(Assert.Single(vm.Records));
        Assert.True(vm.Preview!.IsRestore);
        Assert.Equal(0, adapter.Writes);
        await vm.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(1, adapter.Writes);
        Assert.Equal(1, rescans);
        Assert.False(adapter.Current.Exists);
        Assert.Contains("되돌렸습니다", vm.Result!.Title);
        Assert.False(Assert.Single(vm.Records).CanRestore);
    }

    /// <summary>같은 조치를 중복 등록하거나 SystemOnly에서 사용자 조치를 제공하지 않습니다.</summary>
    [Fact]
    public void RegistrationAndCurrentScopeDetermineAvailability()
    {
        var adapter = new Adapter();
        Assert.Throws<ArgumentException>(() => new ActionWorkflow(new OperationCoordinator(), new Store(), () => Session, [adapter, adapter], []));
        var workflow = new ActionWorkflow(new OperationCoordinator(), new Store(), () => Session with { Scope = ActionUserScope.SystemOnly }, [adapter], []);
        Assert.False(workflow.Supports(ActionId.Power, false));
        Assert.False(workflow.Supports(ActionId.Power, true));
    }

    internal static ActionCenterViewModel Create(Adapter adapter, OperationCoordinator? gate = null, Func<Task>? rescan = null)
        => new(new ActionWorkflow(gate ?? new(), new Store(), () => Session, [adapter], []), new Dispatch(), rescan ?? (() => Task.CompletedTask));
    internal sealed class Dispatch : IUiDispatcher { public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; } }
    internal sealed class Adapter : IActionAdapter
    {
        public ActionDefinition Definition => new(ActionId.Power, ActionScope.CurrentUser);
        internal int Executions;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? Release;
        internal string TargetLabel = "fixture target";
        public Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
            => Task.FromResult<ActionPreview?>(new(target, "선택한 설정을 적용합니다.", "전력 소비가 달라질 수 있습니다.", new(TargetLabel, "현재 상태를 다시 확인합니다.", 4096, false)));
        public async Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
        {
            execution.MarkStarted(); Executions++; Entered.TrySetResult();
            if (Release is not null) { await Release.Task; }
            return new(plan.Id, true, true, "Completed", new(-1024));
        }
    }
    internal sealed class Store : IRollbackStore
    {
        internal bool Fail;
        internal RollbackCatalog Catalog = new([], []);
        public IRollbackTransaction Open(ActionSession session) => Fail ? throw new IOException("fixture") : new Transaction(this);
        private sealed class Transaction(Store owner) : IRollbackTransaction
        {
            public RollbackRecord? Read(Guid id) => owner.Catalog.Records.SingleOrDefault(r => r.Id == id);
            public RollbackCatalog ReadAll() => owner.Catalog;
            public void Save(RollbackRecord record)
            {
                PcOptimizer.Probes.Actions.RollbackCodec.CheckTransition(Read(record.Id), record);
                owner.Catalog = new(owner.Catalog.Records.Where(r => r.Id != record.Id).Append(record).ToArray(), owner.Catalog.Issues);
            }
            public int PruneCompleted(DateTimeOffset olderThan) => 0;
            public void Dispose() { }
        }
    }
    private sealed class Reversible(RollbackValue current) : IReversibleActionAdapter
    {
        internal RollbackValue Current = current;
        internal int Writes;
        public ActionDefinition Definition => new(ActionId.Power, ActionScope.CurrentUser, true);
        public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct) => throw new NotSupportedException();
        public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct) => Task.FromResult(record.TargetKey == "fixture-power");
        public Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct) => Task.FromResult(Current);
        public Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
        {
            if (!Current.SameAs(expected)) { return Task.FromResult(false); }
            Current = desired; Writes++; return Task.FromResult(true);
        }
    }
}
