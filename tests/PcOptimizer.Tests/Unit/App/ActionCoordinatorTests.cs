/**
 * @file    : ActionCoordinatorTests.cs
 * @author  : rudals252
 * @brief   : 신규 조치의 일회성·만료·세션·범위·취소·복구·늦은 실패 이력을 소유 대역으로 검증
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 변경 어댑터를 등록하지 않고 계약만 검증합니다.</summary>
public sealed class ActionCoordinatorTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
    private static readonly ActionTarget Target = new ActionTarget.Power(Guid.NewGuid());
    private static readonly ActionSession Session = new("sid", 1, ActionUserScope.Full);

    /// <summary>확인된 SystemOnly는 시스템 조치만 허용하며 판정 불가 세션은 시스템 조치도 거절합니다.</summary>
    [Theory]
    [InlineData(ActionUserScope.SystemOnly, true)]
    [InlineData(ActionUserScope.Unknown, false)]
    public async Task SystemActionsStillRequireKnownSession(ActionUserScope scope, bool allowed)
    {
        var adapter = new Adapter { Definition = new(ActionId.SystemFiles, ActionScope.System) };
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session with { Scope = scope }, [adapter]);
        var preparation = await service.PrepareAsync(ActionId.SystemFiles, new ActionTarget.Files("fixture"), false, default);
        Assert.Equal(allowed, preparation.Plan is not null);
        if (preparation.Plan is { } plan) { Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded); }
        else { Assert.Equal("ScopeExcluded", preparation.Code); Assert.Equal(0, adapter.Prepares); }
    }

    /// <summary>사용자 전원 API를 시스템 조치로 잘못 등록하여 범위 검증을 우회하지 못합니다.</summary>
    [Fact]
    public void UserPowerCannotBeRegisteredAsSystemAction()
    {
        Assert.Throws<ArgumentException>(() => new ActionCoordinator(new OperationCoordinator(), () => Session,
            [new Adapter { Definition = new(ActionId.Power, ActionScope.System) }]));
    }

    /// <summary>취소를 무시하는 조치도 시간 제한 뒤 반환하되 실제 종료 전 관문은 유지합니다.</summary>
    [Fact]
    public async Task TimeoutPreservesStartedAndBlocksUntilActualCompletion()
    {
        var gate = new OperationCoordinator();
        var adapter = new Adapter { Release = OperationLifetimeTests.Source() };
        var service = new ActionCoordinator(gate, () => Session, [adapter], timeout: TimeSpan.FromMilliseconds(100));
        var plan = (await service.PrepareAsync(ActionId.Power, Target, false, default)).Plan!;
        try
        {
            var result = await service.ExecuteAsync(plan.Id, default).WaitAsync(Bound);
            Assert.True(result.Started);
            Assert.Equal("Draining", result.Code);
            Assert.Null(gate.TryAcquire(OperationKind.Specification));
            adapter.Release.SetResult();
            await OperationLifetimeTests.Idle(gate);
            Assert.True(service.GetResult(plan.Id)!.Succeeded);
        }
        finally { adapter.Release.TrySetResult(); }
    }

    /// <summary>준비가 취소돼도 내부 작업·네이티브 종료를 끝까지 기다리며 늦은 계획을 발급하지 않습니다.</summary>
    [Fact]
    public async Task CancelledPreparationCannotPublishLatePlanOrReleaseNativeWork()
    {
        var gate = new OperationCoordinator();
        var adapter = new Adapter { PrepareRelease = OperationLifetimeTests.Source(), NativeDrain = OperationLifetimeTests.Source() };
        var service = new ActionCoordinator(gate, () => Session, [adapter]);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var prepare = service.PrepareAsync(ActionId.Power, Target, false, cancellation.Token);
            await adapter.PrepareEntered.Task.WaitAsync(Bound);
            cancellation.Cancel();
            var result = await prepare.WaitAsync(Bound);
            Assert.Null(result.Plan);
            Assert.Equal("Cancelled", result.Code);
            Assert.Equal(OperationPhase.Draining, gate.State.Phase);
            adapter.PrepareRelease.SetResult();
            Assert.Null(gate.TryAcquire(OperationKind.Scan));
            var scan = new PcOptimizer.App.Services.ScanService([], [], new PcOptimizer.Core.Models.ScanOptions(), new("test", "test"),
                new PcOptimizer.Tests.Unit.Engine.Fakes.FakeClock(), PcOptimizer.Core.Abstractions.NullAppLogger.Instance, () => true, operations: gate);
            var spec = new PcOptimizer.App.Services.PcSpecService([], new PcOptimizer.Tests.Unit.Engine.Fakes.FakeClock(),
                PcOptimizer.Core.Abstractions.NullAppLogger.Instance, () => scan.CreateContext(false), gate);
            await Assert.ThrowsAsync<InvalidOperationException>(() => scan.RunScanAsync(false, default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => spec.CaptureAsync(default));
            adapter.NativeDrain.SetResult();
            await OperationLifetimeTests.Idle(gate);
            Assert.Equal(0, adapter.Calls);
        }
        finally { adapter.PrepareRelease.TrySetResult(); adapter.NativeDrain.TrySetResult(); }
    }

    /// <summary>발급된 계획만 한 번 쓰고 결과를 보존하며, 타 서비스의 ID는 거절합니다.</summary>
    [Fact]
    public async Task PlanIsSingleUseAndOwnedByItsService()
    {
        var gate = new OperationCoordinator();
        var adapter = new Adapter();
        var service = new ActionCoordinator(gate, () => Session, [adapter]);
        var other = new ActionCoordinator(gate, () => Session, [adapter]);
        var plan = (await service.PrepareAsync(ActionId.Power, Target, false, default)).Plan!;
        Assert.Equal("PlanExpired", (await other.ExecuteAsync(plan.Id, default)).Code);
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Succeeded);
        Assert.True(result.Started);
        Assert.Equal(result, service.GetResult(plan.Id));
        Assert.Equal("PlanExpired", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(1, adapter.Calls);
    }

    /// <summary>준비 전 범위 확인과 닫힌 대상·등록 허용 목록을 강제합니다.</summary>
    [Theory]
    [InlineData(ActionUserScope.SystemOnly)]
    [InlineData(ActionUserScope.Unknown)]
    public async Task UserActionsAreRejectedBeforeAdapter(ActionUserScope scope)
    {
        var adapter = new Adapter();
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session with { Scope = scope }, [adapter]);
        Assert.Equal("ScopeExcluded", (await service.PrepareAsync(ActionId.Power, Target, false, default)).Code);
        Assert.Equal("Unsupported", (await service.PrepareAsync(ActionId.Power, new ActionTarget.Files("cache"), false, default)).Code);
        Assert.Equal("Unsupported", (await service.PrepareAsync(ActionId.Display, Target, false, default)).Code);
        Assert.Equal(0, adapter.Prepares);
    }

    /// <summary>SID·Windows 세션·범위 변경 또는 5분 만료는 실행 전 거절하고 계획을 소비합니다.</summary>
    [Theory]
    [InlineData(0, "SessionChanged")]
    [InlineData(1, "SessionChanged")]
    [InlineData(2, "SessionChanged")]
    [InlineData(3, "PlanExpired")]
    public async Task SessionAndExpiryAreRechecked(int change, string code)
    {
        var current = Session;
        var time = new ManualTimeProvider();
        var adapter = new Adapter();
        var service = new ActionCoordinator(new OperationCoordinator(), () => current, [adapter], time);
        var plan = (await service.PrepareAsync(ActionId.Power, Target, false, default)).Plan!;
        if (change == 3) { time.Advance(TimeSpan.FromMinutes(5)); }
        else { current = change switch { 0 => current with { Sid = "other" }, 1 => current with { SessionId = 2 }, _ => current with { Scope = ActionUserScope.SystemOnly } }; }
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal(code, result.Code);
        Assert.False(result.Started);
        Assert.Equal(0, adapter.Calls);
        Assert.Equal(result, service.GetResult(plan.Id));
    }

    /// <summary>실행 진입 뒤 변경 전 재검증에도 만료·세션 변경을 적용합니다.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MarkStartedRechecksAfterPreflight(bool changeSession)
    {
        var time = new ManualTimeProvider();
        var current = Session;
        var adapter = new Adapter { BeforeStart = () => { if (changeSession) { current = current with { Sid = "other" }; } else { time.Advance(TimeSpan.FromMinutes(5)); } } };
        var service = new ActionCoordinator(new OperationCoordinator(), () => current, [adapter], time);
        var plan = (await service.PrepareAsync(ActionId.Power, Target, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal(changeSession ? "SessionChanged" : "PlanExpired", result.Code);
        Assert.False(result.Started);
    }

    /// <summary>취소 뒤 살아 있는 조치는 관문과 Started 이력을 보존하며, 늦은 실패도 기록합니다.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledApplyOrRestoreRemainsOwnedUntilLateFailure(bool restore)
    {
        var gate = new OperationCoordinator();
        var adapter = new Adapter { Release = OperationLifetimeTests.Source(), FailAfterRelease = true };
        var service = new ActionCoordinator(gate, () => Session, [adapter]);
        var plan = (await service.PrepareAsync(ActionId.Power, Target, restore, default)).Plan!;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var running = service.ExecuteAsync(plan.Id, cancellation.Token);
            await adapter.Entered.Task.WaitAsync(Bound);
            Assert.Equal(restore ? OperationKind.Restore : OperationKind.Apply, gate.State.Kind);
            cancellation.Cancel();
            var result = await running.WaitAsync(Bound);
            Assert.True(result.Started);
            Assert.Equal("Draining", result.Code);
            Assert.Equal(OperationPhase.Draining, gate.State.Phase);
            Assert.Equal("Busy", (await service.PrepareAsync(ActionId.Power, Target, false, default)).Code);
            Assert.Null(gate.TryAcquire(OperationKind.Scan));
            adapter.Release.SetResult();
            await OperationLifetimeTests.Idle(gate);
            Assert.Equal(new ActionResult(plan.Id, true, false, "Failed"), service.GetResult(plan.Id));
        }
        finally { adapter.Release.TrySetResult(); }
    }

    /// <summary>사전 취소는 실행 어댑터를 부르지 않고 결과를 보존합니다.</summary>
    [Fact]
    public async Task CancelledBeforeExecutionDoesNotStart()
    {
        var adapter = new Adapter();
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session, [adapter]);
        var plan = (await service.PrepareAsync(ActionId.Power, Target, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, new CancellationToken(true));
        Assert.False(result.Started);
        // 스케줄되기 전 취소도 내부 검증 작업의 종료까지 소유권을 유지한다.
        Assert.Contains(result.Code, new[] { "Cancelled", "Draining" });
        Assert.Equal(0, adapter.Calls);
    }

    private sealed class Adapter : IActionAdapter
    {
        internal int Calls;
        internal int Prepares;
        internal Action? BeforeStart;
        internal TaskCompletionSource? Release;
        internal TaskCompletionSource? PrepareRelease;
        internal TaskCompletionSource? NativeDrain;
        internal readonly TaskCompletionSource PrepareEntered = OperationLifetimeTests.Source();
        internal readonly TaskCompletionSource Entered = OperationLifetimeTests.Source();
        internal bool FailAfterRelease;
        public ActionDefinition Definition { get; init; } = new(ActionId.Power, ActionScope.CurrentUser, true);
        public async Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
        {
            Prepares++;
            PrepareEntered.TrySetResult();
            if (PrepareRelease is not null) { await PrepareRelease.Task; }
            return new(target, "preview", "impact");
        }
        public Task WaitForDrainAsync() => NativeDrain?.Task ?? Task.CompletedTask;
        public async Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
        {
            Calls++;
            BeforeStart?.Invoke();
            execution.MarkStarted();
            Entered.TrySetResult();
            if (Release is not null) { await Release.Task; }
            if (FailAfterRelease) { throw new InvalidOperationException("private failure"); }
            return new(plan.Id, true, true, "Completed");
        }
    }
}
