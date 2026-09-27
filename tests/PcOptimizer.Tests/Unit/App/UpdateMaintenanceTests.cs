/**
 * @file    : UpdateMaintenanceTests.cs
 * @author  : rudals252
 * @brief   : 대역 서비스에서 선행 원본 기록·부분 중지·취소·복구 실패 보존 검증
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.SystemCleanup;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>Windows 서비스 제어 API를 호출하지 않는 작업 흐름 회귀입니다.</summary>
public sealed class UpdateMaintenanceTests
{
    /// <summary>원래 중지된 서비스는 시작하지 않으며 변경 전 기록을 남깁니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RestoreOnlyServicesOriginallyRunning(bool bitsStopped)
    {
        var store = new ActionCenterTests.Store(); var platform = new Platform(store);
        if (bitsStopped) { platform.States[UpdateService.Bits] = UpdateServiceState.Stopped; }
        var session = SystemActionSession.Read(); var plan = Plan(session); var execution = new Execution();
        var result = await new UpdateServiceMaintenance(store, platform, () => session).RunAsync(plan, execution, (e, ct) =>
        {
            Assert.All(platform.States.Values, s => Assert.Equal(UpdateServiceState.Stopped, s)); e.MarkStarted();
            return Task.FromResult(new ActionResult(plan.Id, true, true, "Completed", new(10, 1)));
        }, () => { }, default);
        Assert.True(result.Succeeded); Assert.True(result.ServiceRecoveryCompleted); Assert.Equal(1, execution.Starts);
        Assert.Equal(bitsStopped ? 2 : 4, platform.Changes);
        Assert.Equal(bitsStopped ? UpdateServiceState.Stopped : UpdateServiceState.Running, platform.States[UpdateService.Bits]);
        Assert.All(store.Catalog.Records, r => Assert.Equal(RollbackState.Restored, r.State));
    }
    /// <summary>첫/둘째 중지 실패에서도 먼저 중지한 서비스는 복구합니다.</summary>
    [Theory]
    [InlineData(1)][InlineData(2)]
    public async Task FailedStopRestoresEarlierServices(int failAt)
    {
        var store = new ActionCenterTests.Store(); var platform = new Platform(store) { FailStop = failAt };
        var session = SystemActionSession.Read(); var plan = Plan(session); var work = false;
        var result = await new UpdateServiceMaintenance(store, platform, () => session).RunAsync(plan, new Execution(), (e, ct) =>
        { work = true; return Task.FromResult(new ActionResult(plan.Id, true, true, "Completed")); }, () => { }, default);
        Assert.False(work); Assert.False(result.Succeeded); Assert.True(result.ServiceRecoveryCompleted);
        Assert.All(platform.States.Values, s => Assert.Equal(UpdateServiceState.Running, s));
        Assert.DoesNotContain(store.Catalog.Records, r => r.NeedsRecovery);
    }
    /// <summary>사용자 취소 토큰을 복구에 재사용하지 않습니다.</summary>
    [Fact]
    public async Task CancelledWorkStillRestoresBothServices()
    {
        var store = new ActionCenterTests.Store(); var platform = new Platform(store); using var cancel = new CancellationTokenSource();
        var session = SystemActionSession.Read(); var plan = Plan(session);
        var result = await new UpdateServiceMaintenance(store, platform, () => session).RunAsync(plan, new Execution(), (e, ct) =>
        { cancel.Cancel(); ct.ThrowIfCancellationRequested(); throw new InvalidOperationException(); }, () => { }, cancel.Token);
        Assert.Equal("Cancelled", result.Code); Assert.True(result.Started); Assert.True(result.ServiceRecoveryCompleted);
        Assert.All(platform.States.Values, s => Assert.Equal(UpdateServiceState.Running, s));
    }
    /// <summary>복구 실패 시 처리량과 미완료 원본을 유지하고 다음 정리를 거절합니다.</summary>
    [Fact]
    public async Task FailedRecoveryPreservesEffectAndBlocksNextRun()
    {
        var store = new ActionCenterTests.Store(); var platform = new Platform(store) { FailRestore = true };
        var session = SystemActionSession.Read(); var plan = Plan(session); var maintenance = new UpdateServiceMaintenance(store, platform, () => session);
        Task<ActionResult> Work(IActionExecution e, CancellationToken ct) => Task.FromResult(new ActionResult(plan.Id, true, true, "Completed", new(25, 2)));
        var result = await maintenance.RunAsync(plan, new Execution(), Work, () => { }, default);
        Assert.Equal("UpdateRecoveryFailed", result.Code); Assert.False(result.ServiceRecoveryCompleted); Assert.Equal(2, result.Effect!.ChangedFiles);
        Assert.Contains(store.Catalog.Records, r => r.NeedsRecovery); var changes = platform.Changes;
        var next = await maintenance.RunAsync(Plan(session), new Execution(), Work, () => { }, default);
        Assert.Equal("UpdateRecoveryRequired", next.Code); Assert.False(next.Started); Assert.Equal(changes, platform.Changes);
    }
    private static ActionPlan Plan(ActionSession session) => new(Guid.NewGuid(), new(ActionId.WindowsUpdateCache, ActionScope.System),
        new(new ActionTarget.Files("fixture"), "fixture", "fixture"), session, DateTimeOffset.UtcNow.AddMinutes(1), false);
    private sealed class Execution : IActionExecution
    {
        internal int Starts;
        public void MarkStarted() => Starts++;
        public void Track(Task task) { }
    }
    private sealed class Platform(ActionCenterTests.Store store) : IUpdateServicePlatform
    {
        internal readonly Dictionary<UpdateService, UpdateServiceState> States = new() { [UpdateService.WindowsUpdate] = UpdateServiceState.Running, [UpdateService.Bits] = UpdateServiceState.Running };
        internal int Changes, FailStop, Stops; internal bool FailRestore;
        public UpdateServiceStatus Read(UpdateService service) => new(States[service], true);
        public Task<UpdateServiceStatus> ReadStableAsync(UpdateService service, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Read(service)); }
        public Task<bool> ChangeAsync(UpdateService service, UpdateServiceState expected, UpdateServiceState desired, Action beforeWrite, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal(expected, States[service]);
            var record = Assert.Single(store.Catalog.Records, r => r.TargetKey == UpdateServiceRecoveryAdapter.Key(service));
            Assert.Equal(desired == UpdateServiceState.Stopped ? RollbackState.Pending : RollbackState.Restoring, record.State);
            if (desired == UpdateServiceState.Stopped && ++Stops == FailStop || desired == UpdateServiceState.Running && FailRestore) { throw new InvalidOperationException("owned fixture"); }
            beforeWrite(); States[service] = desired; Changes++; return Task.FromResult(true);
        }
    }
}
