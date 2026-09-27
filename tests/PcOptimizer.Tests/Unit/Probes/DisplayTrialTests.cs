/**
 * @file    : DisplayTrialTests.cs
 * @author  : rudals252
 * @brief   : 사용자 화면을 바꾸지 않는 유지·기한·복구·기록·관문 회귀
 */
using System.Buffers.Binary;
using System.IO;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Display;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>모든 변경은 메모리 대역에서만 수행합니다.</summary>
public sealed class DisplayTrialTests
{
    internal static readonly ActionSession Session = new("S-1-5-21-1", 1, ActionUserScope.Full);
    internal static DisplayFrame Frame(int hz)
    {
        var bytes = new byte[220];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(68), 220);
        foreach (var (offset, value) in new[] { (168, 32), (172, 1920), (176, 1080), (184, hz) })
        { BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value); }
        return new(new DisplayMode(1920, 1080, 0, 32, false, hz), bytes);
    }
    private sealed class Fixture
    {
        internal readonly Platform Platform = new();
        internal readonly Store Store = new();
        internal readonly Clock Clock = new();
        internal readonly OperationCoordinator Gate = new();
        internal ActionSession Owner = Session;
        internal DisplayTrialCoordinator Coordinator;
        internal Fixture() { Coordinator = Create(); }
        internal DisplayTrialCoordinator Create() => new(Platform, Gate, Store, () => Owner, Clock);
        internal async Task<Guid> Prepare() => (await Coordinator.PrepareAsync("monitor", 120, default)).Id!.Value;
        internal async Task<(Guid Id, Task<DisplayTrialResult> Running)> Start(CancellationToken ct = default)
        {
            var id = await Prepare(); var running = Coordinator.StartAsync(id, ct);
            await Until(() => Coordinator.Status is not null && Clock.HasTimer);
            Assert.True(Gate.State.IsBusy); Assert.Equal(RollbackState.Pending, Store.Record!.State);
            Assert.Equal(120, Platform.Current.Mode.RefreshHz); Assert.Equal(60, Platform.Registered.Mode.RefreshHz);
            return (id, running);
        }
    }
    private static async Task Until(Func<bool> condition)
    {
        var stop = DateTime.UtcNow.AddSeconds(5);
        while (!condition()) { if (DateTime.UtcNow >= stop) { throw new TimeoutException("fixture wait"); } await Task.Delay(1); }
    }
    /// <summary>유지 전에는 프로필을 쓰지 않고 유지 뒤 원본으로 되돌릴 수 있습니다.</summary>
    [Fact] public async Task KeepPersistsOnlyAfterDecisionAndCanUndo()
    {
        var f = new Fixture(); var (id, running) = await f.Start();
        Assert.True(f.Coordinator.Keep(id)); Assert.False(f.Coordinator.Revert(id));
        var result = await running; Assert.True(result.Kept); Assert.False(result.Restored);
        Assert.Equal(120, f.Platform.Registered.Mode.RefreshHz); Assert.Equal(RollbackState.Applied, f.Store.Record!.State);
        var restored = await f.Create().RestoreAsync(id, default);
        Assert.True(restored.Restored); Assert.Equal(60, f.Platform.Current.Mode.RefreshHz); Assert.Equal(60, f.Platform.Registered.Mode.RefreshHz);
        Assert.Equal(RollbackState.Restored, f.Store.Record.State);
    }
    /// <summary>UI 호출이 전혀 없어도 단조 15초 경과가 원복합니다.</summary>
    [Fact] public async Task TimeoutWithoutUiRestoresAndRejectsLateKeep()
    {
        var f = new Fixture(); var (id, running) = await f.Start();
        f.Clock.Advance(TimeSpan.FromSeconds(15)); Assert.False(f.Coordinator.Keep(id));
        var result = await running; Assert.True(result.Restored); Assert.False(result.Kept);
        Assert.Equal("TrialExpired", result.Code); Assert.Equal(60, f.Platform.Current.Mode.RefreshHz);
        Assert.Null(f.Coordinator.Status); Assert.Equal(RollbackState.Restored, f.Store.Record!.State);
    }
    /// <summary>취소/창 닫기는 복구 토큰을 취소하지 않습니다.</summary>
    [Theory] [InlineData(true)] [InlineData(false)] public async Task CancelOrCloseRestores(bool cancel)
    {
        var f = new Fixture(); using var cts = new CancellationTokenSource(); var (id, running) = await f.Start(cts.Token);
        if (cancel) { cts.Cancel(); } else { Assert.True(f.Coordinator.Revert(id)); }
        Assert.True((await running).Restored); Assert.Equal(60, f.Platform.Current.Mode.RefreshHz);
    }
    /// <summary>기한을 넘긴 느린 네이티브 적용 후 새 15초를 부여하지 않습니다.</summary>
    [Fact] public async Task SlowApplyConsumesTrialDeadline()
    {
        var f = new Fixture(); f.Platform.AfterApply = () => f.Clock.Advance(TimeSpan.FromSeconds(16));
        var result = await f.Coordinator.StartAsync(await f.Prepare(), default);
        Assert.True(result.Restored); Assert.Equal("TrialExpired", result.Code);
    }
    /// <summary>지원 검사 실패는 원본 저장/쓰기 전에 끝납니다.</summary>
    [Fact] public async Task TestFailureNeverWrites()
    {
        var f = new Fixture(); f.Platform.TestCode = -2;
        Assert.Null((await f.Coordinator.PrepareAsync("monitor", 120, default)).Id);
        Assert.Null(f.Store.Record); Assert.Equal(0, f.Platform.Writes);
    }
    /// <summary>부분 적용/재시작 요구 반환은 성공이 아니며 실제 상태를 복구합니다.</summary>
    [Theory] [InlineData(-1)] [InlineData(1)] public async Task ApplyErrorOrRestartIsRolledBack(int code)
    {
        var f = new Fixture(); f.Platform.ApplyCode = code;
        var result = await f.Coordinator.StartAsync(await f.Prepare(), default);
        Assert.False(result.Kept); Assert.True(result.Restored); Assert.Equal("DisplayApplyFailed", result.Code);
    }
    /// <summary>저장이 실패해도 바뀐 프로필과 활성 모드를 각각 복구합니다.</summary>
    [Theory] [InlineData(-1)] [InlineData(1)] public async Task PersistFailureRestoresBothStates(int code)
    {
        var f = new Fixture(); var (id, running) = await f.Start(); f.Platform.PersistCode = code; f.Coordinator.Keep(id);
        var result = await running; Assert.False(result.Kept); Assert.True(result.Restored);
        Assert.Equal(60, f.Platform.Registered.Mode.RefreshHz); Assert.Equal("DisplaySaveFailed", result.Code);
    }
    /// <summary>복구 실패는 내구 기록을 남기고 새 조율기로 재시도할 수 있습니다.</summary>
    [Fact] public async Task FailedRollbackCanRecoverAfterRestart()
    {
        var f = new Fixture(); var (id, running) = await f.Start(); f.Platform.RecoveryFails = true; f.Coordinator.Revert(id);
        Assert.False((await running).Restored); Assert.Equal(RollbackState.Restoring, f.Store.Record!.State);
        f.Platform.RecoveryFails = false;
        Assert.True((await f.Create().RestoreAsync(id, default)).Restored); Assert.Equal(60, f.Platform.Current.Mode.RefreshHz);
    }
    /// <summary>장치 제거 또는 외부 주사율 변경을 덮어쓰지 않습니다.</summary>
    [Theory] [InlineData(true)] [InlineData(false)] public async Task ExternalChangeOrDisconnectRemainsUnresolved(bool disconnect)
    {
        var f = new Fixture(); var (id, running) = await f.Start();
        if (disconnect) { f.Platform.Disconnected = true; } else { f.Platform.Current = Frame(144); }
        f.Coordinator.Revert(id); var result = await running;
        Assert.False(result.Restored); Assert.Equal(1, f.Platform.Writes); Assert.True(f.Store.Record!.NeedsRecovery);
    }
    /// <summary>Pending 기록 실패 시 표시 설정을 쓰지 않습니다.</summary>
    [Fact] public async Task InitialJournalFailurePreventsWrite()
    {
        var f = new Fixture(); f.Store.FailState = RollbackState.Pending;
        var result = await f.Coordinator.StartAsync(await f.Prepare(), default);
        Assert.False(result.Started); Assert.Equal(0, f.Platform.Writes);
    }
    /// <summary>복구 기록 실패와 실제 원복 성공은 따로 표시합니다.</summary>
    [Theory] [InlineData(RollbackState.Restoring)] [InlineData(RollbackState.Restored)]
    public async Task JournalFailureDoesNotPreventActualRollback(RollbackState failure)
    {
        var f = new Fixture(); var (id, running) = await f.Start(); f.Store.FailState = failure; f.Coordinator.Revert(id);
        var result = await running; Assert.True(result.Restored); Assert.Equal("RecoveryRecordFailed", result.Code); Assert.True(f.Store.Record!.NeedsRecovery);
    }
    /// <summary>유지 기록 실패도 시험 성공으로 오인하지 않고 원복합니다.</summary>
    [Fact] public async Task AppliedJournalFailureRestores()
    {
        var f = new Fixture(); var (id, running) = await f.Start(); f.Store.FailState = RollbackState.Applied; f.Coordinator.Keep(id);
        var result = await running; Assert.False(result.Kept); Assert.True(result.Restored);
    }
    /// <summary>관문 공유·계획 소비·범위·만료를 실제 호출 경계에서 검사합니다.</summary>
    [Fact] public async Task BusyScopeSingleUseAndExpiryAreEnforced()
    {
        var f = new Fixture(); f.Owner = Session with { Scope = ActionUserScope.SystemOnly };
        Assert.Equal("ScopeExcluded", (await f.Coordinator.PrepareAsync("monitor", 120, default)).Code);
        Assert.Equal(0, f.Platform.Captures); f.Owner = Session;
        var id = await f.Prepare(); f.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("PlanExpired", (await f.Coordinator.StartAsync(id, default)).Code); Assert.Equal(0, f.Platform.Writes);
        var (trialId, running) = await f.Start();
        Assert.Equal("Busy", (await f.Coordinator.PrepareAsync("monitor", 120, default)).Code);
        Assert.Null(f.Gate.TryAcquire(OperationKind.Scan)); f.Coordinator.Revert(trialId); await running;
        Assert.Equal("PlanExpired", (await f.Coordinator.StartAsync(trialId, default)).Code);
    }
    /// <summary>실행 직전 느린 재검사 중 만료된 계획도 적용하지 않습니다.</summary>
    [Fact] public async Task ExpiryDuringRecheckPreventsWrite()
    {
        var f = new Fixture(); var id = await f.Prepare(); f.Platform.AfterTest = () => f.Clock.Advance(TimeSpan.FromMinutes(5));
        var result = await f.Coordinator.StartAsync(id, default); Assert.Equal("PlanExpired", result.Code); Assert.Equal(0, f.Platform.Writes);
    }
    /// <summary>취소를 무시하는 네이티브 호출 종료 전까지 관문을 보존합니다.</summary>
    [Fact] public async Task BlockingApplyKeepsGateUntilItReallyReturns()
    {
        var f = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        f.Platform.AfterApply = () => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5))); };
        using var cts = new CancellationTokenSource(); var running = f.Coordinator.StartAsync(await f.Prepare(), cts.Token);
        await Until(() => entered.IsSet); cts.Cancel(); Assert.True(f.Gate.State.IsBusy); Assert.False(running.IsCompleted);
        release.Set(); Assert.True((await running).Restored);
    }
    /// <summary>유지 클릭 후 네이티브 재검사 중 기한이 지났으면 저장을 시작하지 않습니다.</summary>
    [Fact] public async Task KeepRacingDeadlineCannotPersistLate()
    {
        var f = new Fixture(); var (id, running) = await f.Start();
        f.Platform.BeforePersist = () => f.Clock.Advance(TimeSpan.FromSeconds(15)); f.Coordinator.Keep(id);
        var result = await running; Assert.False(result.Kept); Assert.True(result.Restored);
        Assert.Equal("TrialExpired", result.Code); Assert.Equal(60, f.Platform.Registered.Mode.RefreshHz);
    }
    /// <summary>재식별 후 쓰기 직전 거절은 Started로 표시하지 않습니다.</summary>
    [Fact] public async Task FinalNativeGuardRejectionIsNotStarted()
    {
        var f = new Fixture(); f.Platform.RejectWrite = true;
        var result = await f.Coordinator.StartAsync(await f.Prepare(), default);
        Assert.False(result.Started); Assert.Equal(0, f.Platform.Writes); Assert.True(result.Restored);
    }
    /// <summary>타 사용자·손상 원문·외부 JSON 값은 복구 쓰기로 연결되지 않습니다.</summary>
    [Theory] [InlineData("sid")] [InlineData("bytes")] [InlineData("applied")]
    public async Task InvalidRecoveryRecordNeverWrites(string kind)
    {
        var f = new Fixture(); var (id, running) = await f.Start(); f.Coordinator.Keep(id); await running;
        var record = f.Store.Record!;
        f.Store.Record = kind switch
        {
            "sid" => record with { Sid = "other" },
            "bytes" => record with { Before = new(true, 1, "{}"u8.ToArray()) },
            _ => record with { Applied = new(true, 1, "{}"u8.ToArray()) }
        };
        var before = f.Platform.Writes; Assert.False((await f.Create().RestoreAsync(id, default)).Started); Assert.Equal(before, f.Platform.Writes);
    }
    /// <summary>미해결 원복이 있으면 새 시험을 차단하며 조회 실패도 기록을 지우지 않습니다.</summary>
    [Fact] public async Task UnresolvedRecoveryBlocksAnotherTrialAndRemainsVisible()
    {
        var f = new Fixture(); var (id, running) = await f.Start(); f.Platform.RecoveryFails = true; f.Coordinator.Revert(id); await running;
        var next = await f.Prepare(); var writes = f.Platform.Writes;
        Assert.Equal("RecoveryRequired", (await f.Coordinator.StartAsync(next, default)).Code); Assert.Equal(writes, f.Platform.Writes);
        f.Platform.ChoicesFail = true;
        var catalog = await f.Create().InspectAsync(default);
        Assert.True(Assert.Single(catalog.Records).NeedsRecovery); Assert.NotEqual("Ready", catalog.Code);
    }
    internal sealed class Platform : IDisplaySettingsPlatform
    {
        internal DisplayFrame Current = Frame(60), Registered = Frame(60);
        internal int Writes, Captures, TestCode, ApplyCode, PersistCode;
        internal bool RecoveryFails, Disconnected, RejectWrite, ChoicesFail;
        internal Action? AfterTest, AfterApply, BeforePersist;
        public IReadOnlyList<DisplayTrialChoice> Choices() => ChoicesFail ? throw new ActionUnavailableException("DisplayReadFailed") : [new("monitor", "fixture 60 → 120 Hz", 60, 120)];
        public DisplayChange Capture(string monitorPath, int hz) { Captures++; return new(new("monitor", "adapter", "gdi", 1, 1, 1), Current, Registered, Frame(hz)); }
        public DisplayObservation Observe(DisplayIdentity identity) => Disconnected ? throw new ActionUnavailableException("DisplayDeviceChanged") : new(Current, Registered);
        public int Test(DisplayIdentity identity, DisplayFrame frame) { AfterTest?.Invoke(); return TestCode; }
        public int Apply(DisplayIdentity identity, DisplayFrame frame, bool persist, DisplayObservation expected, Action beforeWrite)
        {
            if (!Current.SameMode(expected.Current) || !Registered.SameMode(expected.Registered)) { throw new ActionUnavailableException("CurrentValueChanged"); }
            if (RejectWrite) { throw new ActionUnavailableException("CurrentValueChanged"); }
            if (persist) { BeforePersist?.Invoke(); }
            beforeWrite(); Writes++;
            if (frame.Mode.RefreshHz == 60 && RecoveryFails) { return -1; }
            Current = frame; if (persist) { Registered = frame; return PersistCode; }
            if (frame.Mode.RefreshHz == 120) { AfterApply?.Invoke(); return ApplyCode; }
            return 0;
        }
        public int SaveProfileOnly(DisplayIdentity identity, DisplayFrame frame, DisplayObservation expected, Action beforeWrite) { beforeWrite(); Writes++; Registered = frame; return 0; }
    }
    private sealed class Store : IRollbackStore, IRollbackTransaction
    {
        internal RollbackRecord? Record;
        internal RollbackState? FailState;
        public IRollbackTransaction Open(ActionSession session) => this;
        public RollbackRecord? Read(Guid id) => Record?.Id == id ? Record : null;
        public RollbackCatalog ReadAll() => new(Record is null ? [] : [Record], []);
        public void Save(RollbackRecord record)
        {
            if (record.State == FailState) { throw new IOException("fixture"); }
            RollbackCodec.CheckTransition(Record, record); Record = record;
        }
        public int PruneCompleted(DateTimeOffset olderThan) => 0;
        public void Dispose() { }
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        private readonly List<Timer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        internal bool HasTimer { get { lock (_timers) { return _timers.Count > 0; } } }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(callback, state, GetTimestamp() + dueTime.Ticks);
            lock (_timers) { _timers.Add(timer); } return timer;
        }
        internal void Advance(TimeSpan span)
        {
            Interlocked.Add(ref _ticks, span.Ticks); Timer[] timers;
            lock (_timers) { timers = _timers.ToArray(); }
            foreach (var timer in timers) { if (!timer.Disposed && GetTimestamp() >= timer.Due) { timer.Dispose(); timer.Callback(timer.State); } }
        }
        private sealed class Timer(TimerCallback callback, object? state, long due) : ITimer
        {
            internal readonly TimerCallback Callback = callback;
            internal readonly object? State = state;
            internal readonly long Due = due;
            internal bool Disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
