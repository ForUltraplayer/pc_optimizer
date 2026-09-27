/**
 * @file    : OperationLifetimeTests.cs
 * @author  : rudals252
 * @brief   : UI를 우회한 검사·사양·준비·적용·복구 상호 배제와 실제 작업/프로세스 종료 경계 검증
 */
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 설정이나 사용자 파일을 변경하지 않는 수명 회귀입니다.</summary>
public sealed class OperationLifetimeTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
    /// <summary>다섯 종류의 모든 조합에서 두 번째 소유권을 거절하고, 이전 lease 재해제가 새 소유자를 풀지 않습니다.</summary>
    [Fact]
    public void EveryOperationPairIsExclusiveAndOldLeaseCannotReleaseNewOwner()
    {
        var operations = new OperationCoordinator();
        foreach (var first in Enum.GetValues<OperationKind>())
        {
            var lease = operations.TryAcquire(first)!;
            foreach (var second in Enum.GetValues<OperationKind>()) { Assert.Null(operations.TryAcquire(second)); }
            lease.Dispose();
            using var next = operations.TryAcquire(OperationKind.Apply)!;
            lease.Dispose();
            Assert.True(operations.State.IsBusy);
            Assert.Throws<ObjectDisposedException>(() => lease.Track(Task.CompletedTask));
        }
    }

    /// <summary>화면 호출이 끝나도 자식 작업·늦은 실패를 관측한 뒤에만 소유권이 해제됩니다.</summary>
    [Fact]
    public async Task ReturnedLeaseRetainsParentAndChildIncludingLateFailure()
    {
        var operations = new OperationCoordinator();
        var parent = Source();
        var child = Source();
        var lease = operations.TryAcquire(OperationKind.Apply)!;
        lease.Track(parent.Task);
        lease.Dispose();
        Assert.Equal(OperationPhase.Draining, operations.State.Phase);
        lease.Track(child.Task);
        parent.SetException(new InvalidOperationException("private failure"));
        Assert.Null(operations.TryAcquire(OperationKind.Restore));
        child.SetResult();
        await Idle(operations);
        Assert.NotNull(parent.Task.Exception);
    }

    /// <summary>검사와 사양은 직접 호출해도 동시 실행 및 타임아웃 뒤 재진입을 양방향으로 막습니다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScanAndSpecShareGateThroughTimeout(bool scanFirst)
    {
        var gate = new OperationCoordinator();
        var probe = new BlockingProbe();
        var scan = new ScanService([probe], [], new ScanOptions(), new("test", "test"), new FakeClock(), NullAppLogger.Instance, () => true, operations: gate);
        var spec = new PcSpecService([probe], new FakeClock(), NullAppLogger.Instance, () => scan.CreateContext(false), gate);
        try
        {
            Task first = scanFirst ? scan.RunScanAsync(false, default) : spec.CaptureAsync(default);
            await probe.Entered.Task.WaitAsync(Bound);
            await Assert.ThrowsAsync<InvalidOperationException>(() => scanFirst ? (Task)spec.CaptureAsync(default) : scan.RunScanAsync(false, default));
            await first.WaitAsync(Bound);
            Assert.Equal(OperationPhase.Draining, gate.State.Phase);
            await Assert.ThrowsAsync<InvalidOperationException>(() => scan.RunScanAsync(false, default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => spec.CaptureAsync(default));
            Assert.Null(gate.TryAcquire(OperationKind.Apply));
            Assert.Equal(1, probe.Calls);
            probe.Release.SetResult();
            await Idle(gate);
            await spec.WaitForDrainAsync(default).WaitAsync(Bound);
            await spec.CaptureAsync(default).WaitAsync(Bound);
            Assert.Equal(2, probe.Calls);
        }
        finally { probe.Release.TrySetResult(); }
    }

    /// <summary>실행 결과가 반환돼도 종료 실패 자식 프로세스가 남아 있으면 검사를 막습니다.</summary>
    [Fact]
    public async Task CacheCleanupRetainsNativeDrainAndBlocksOtherServices()
    {
        var gate = new OperationCoordinator();
        var backend = new CacheBackend();
        var cleanup = new CacheCleanupService(backend, operations: gate);
        var plan = (await cleanup.PrepareAsync(CacheTool.Npm, default)).Plan!;
        backend.Drain = Source();
        var result = await cleanup.ExecuteAsync(plan.Id, default);
        Assert.True(result.Started);
        Assert.Equal(OperationPhase.Draining, gate.State.Phase);
        Assert.Equal("Busy", (await cleanup.PrepareAsync(CacheTool.Pip, default)).Reason);
        var scan = new ScanService([], [], new ScanOptions(), new("test", "test"), new FakeClock(), NullAppLogger.Instance, () => true, operations: gate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scan.RunScanAsync(false, default));
        backend.Drain.SetResult();
        await Idle(gate);
        await scan.RunScanAsync(false, default);
    }

    /// <summary>기존 공식 도구 계획도 SID·세션·범위가 바뀌면 소비하고 거절합니다.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExistingCachePlanIsBoundToSession(int change)
    {
        var current = new ActionSession("sid1", 1, ActionUserScope.Full);
        var backend = new CacheBackend();
        var cleanup = new CacheCleanupService(backend, session: () => current);
        var plan = (await cleanup.PrepareAsync(CacheTool.Npm, default)).Plan!;
        current = change switch { 0 => current with { Sid = "sid2" }, 1 => current with { SessionId = 2 }, _ => current with { Scope = ActionUserScope.SystemOnly } };
        Assert.Equal("SessionChanged", (await cleanup.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal("PlanExpired", (await cleanup.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(0, backend.Clears);
    }

    internal static TaskCompletionSource Source() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static async Task Idle(IOperationCoordinator gate)
    {
        using var timeout = new CancellationTokenSource(Bound);
        while (gate.State.IsBusy) { await Task.Delay(1, timeout.Token); }
    }
    private sealed class BlockingProbe : IProbe
    {
        internal readonly TaskCompletionSource Entered = Source();
        internal readonly TaskCompletionSource Release = Source();
        internal int Calls;
        public string Id => SystemDetailsProbeContract.PROBE_ID;
        public FindingCategory Category => FindingCategory.Unclassified;
        public bool RequiresElevation => false;
        public bool RequiresNetwork => false;
        public ProbeScope Scope => ProbeScope.System;
        public TimeSpan DefaultTimeout => TimeSpan.FromMilliseconds(60);
        public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            Release.Task.GetAwaiter().GetResult(); // 동기 차단과 취소 무시를 함께 재현
            return Task.FromResult(new ProbeResult(Id, ProbeStatus.Success, [], [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext));
        }
    }
    private sealed class CacheBackend : ICacheToolBackend
    {
        internal TaskCompletionSource? Drain;
        internal int Clears;
        public Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct) => Task.FromResult<CacheToolLocation?>(new(tool, "tool", "cache", "fingerprint"));
        public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct) => Task.FromResult(new CacheInspection(true, 10, null));
        public Task<CacheToolExecution> ClearAsync(CacheToolLocation location, CancellationToken ct) { Clears++; return Task.FromResult(new CacheToolExecution(true, true, "Completed")); }
        public Task WaitForDrainAsync() => Drain?.Task ?? Task.CompletedTask;
    }
}
