/**
 * @file    : OfficialCacheActionTests.cs
 * @author  : rudals252
 * @brief   : 공식 도구 어댑터의 시작 전 거절·실제 시작·늦은 종료·세션 경계를 검증
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>공식 명령 대신 대역 backend를 사용합니다.</summary>
public sealed class OfficialCacheActionTests
{
    private static readonly ActionTarget Target = new ActionTarget.OfficialTool(OfficialCacheTool.Pip);
    private static ActionCoordinator Service(Backend backend, OperationCoordinator? gate = null, Func<ActionSession>? session = null)
        => new(gate ?? new(), session ?? (() => ActionCenterTests.Session), [new OfficialCacheActionAdapter(backend, session ?? (() => ActionCenterTests.Session))]);
    /// <summary>프로세스 시작 실패는 실행 전 거절로 남으며 논리 크기를 실제 공간으로 표시하지 않습니다.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task StartedTracksActualStartOnly(bool starts)
    {
        var backend = new Backend { Starts = starts }; var service = Service(backend);
        var plan = (await service.PrepareAsync(ActionId.OfficialCache, Target, false, default)).Plan!;
        Assert.Contains("개별 파일", plan.Preview.Impact);
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal(starts, result.Started); Assert.Equal(starts, result.Succeeded);
        Assert.Null(result.Effect?.FreeSpaceDeltaBytes);
        if (starts) { Assert.Equal(100, result.Effect!.BeforeLogicalBytes); Assert.Equal(0, result.Effect.AfterLogicalBytes); }
        else { Assert.Equal("StartFailed", result.Code); }
    }
    /// <summary>시작 직전 세션이 바뀌면 실제 시작 콜백까지 도달하지 않습니다.</summary>
    [Fact]
    public async Task ChangedSessionAtProcessBoundaryDoesNotStart()
    {
        var owner = ActionCenterTests.Session; var backend = new Backend { BeforeStart = () => owner = owner with { SessionId = 99 } };
        var service = Service(backend, session: () => owner);
        var plan = (await service.PrepareAsync(ActionId.OfficialCache, Target, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal(0, backend.StartCalls); Assert.Equal("SessionChanged", result.Code);
    }
    /// <summary>지문/보호 조건이 달라지면 정리 호출을 하지 않습니다.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task ChangedOrBlockedTargetRejectsWithoutStart(bool changed)
    {
        var backend = new Backend(); var service = Service(backend);
        var plan = (await service.PrepareAsync(ActionId.OfficialCache, Target, false, default)).Plan!;
        if (changed) { backend.Location = backend.Location with { Fingerprint = "changed" }; } else { backend.Allowed = false; }
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal(0, backend.StartCalls);
        Assert.Equal(changed ? "TargetChanged" : "Protected", result.Code);
    }
    /// <summary>시작 후 관측 실패는 실행 전 거절이나 성공으로 숨기지 않습니다.</summary>
    [Fact]
    public async Task ObservationFailurePreservesStartedAndBefore()
    {
        var backend = new Backend { AfterStart = () => throw new InvalidOperationException("fixture observation") };
        var service = Service(backend);
        var plan = (await service.PrepareAsync(ActionId.OfficialCache, Target, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Started); Assert.False(result.Succeeded); Assert.Equal("ObservationFailed", result.Code);
        Assert.Equal(100, result.Effect!.BeforeLogicalBytes); Assert.Null(result.Effect.AfterLogicalBytes);
    }
    /// <summary>취소 응답 뒤에도 실제 프로세스 종료까지 관문을 유지합니다.</summary>
    [Fact]
    public async Task LateProcessDrainKeepsGateUntilExit()
    {
        var backend = new Backend(); var gate = new OperationCoordinator(); var service = Service(backend, gate);
        var plan = (await service.PrepareAsync(ActionId.OfficialCache, Target, false, default)).Plan!;
        backend.Drain = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var running = service.ExecuteAsync(plan.Id, cancel.Token);
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancel.Cancel();
        var pending = await running; Assert.Equal("Draining", pending.Code); Assert.True(gate.State.IsBusy);
        backend.Drain.SetResult(); await OperationLifetimeTests.Idle(gate);
        Assert.True(service.GetResult(plan.Id)!.Started);
    }
    internal sealed class Backend : ICacheToolActionBackend
    {
        internal CacheToolLocation Location = new(CacheTool.Pip, @"C:\Program Files\fixture\python.exe", @"C:\Users\fixture\cache", "fingerprint");
        internal bool Starts = true, Allowed = true;
        internal int StartCalls;
        internal Action? BeforeStart, AfterStart;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? Drain;
        public Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct) => Task.FromResult<CacheToolLocation?>(Location);
        public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct)
        { if (StartCalls > 0) { AfterStart?.Invoke(); } return Task.FromResult(new CacheInspection(Allowed, StartCalls > 0 ? 0 : 100, Allowed ? null : "Protected")); }
        public Task<CacheToolExecution> ClearAsync(CacheToolLocation location, CancellationToken ct) => throw new NotSupportedException("LegacyGateMustNotBeUsed");
        public Task<CacheToolExecution> ClearAsync(CacheToolLocation location, IActionExecution execution, CancellationToken ct)
        {
            BeforeStart?.Invoke();
            var started = execution.TryStartProcess(() => { StartCalls++; return Starts; });
            Entered.TrySetResult(); return Task.FromResult(new CacheToolExecution(started, started, started ? "Completed" : "StartFailed"));
        }
        public Task WaitForDrainAsync() => Drain?.Task ?? Task.CompletedTask;
    }
}
