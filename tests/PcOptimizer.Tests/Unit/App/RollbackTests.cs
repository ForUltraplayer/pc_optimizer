/**
 * @file    : RollbackTests.cs
 * @author  : rudals252
 * @brief   : 복구 저장 실패·중단 경계·세션·현재 값 충돌·실제 종료 수명을 소유 대역으로 검증
 */
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>네이티브 설정을 바꾸지 않고 복구 계약의 실패 경계를 검증합니다.</summary>
public sealed class RollbackTests
{
    private static readonly ActionSession Session = new("S-1-5-21-fixture", 1, ActionUserScope.Full);
    private static readonly ActionTarget Target = new ActionTarget.Power(Guid.NewGuid());
    private static RollbackValue Value(byte b) => new(true, 3, [b]);
    internal static RollbackRecord Record(string sid, RollbackState state = RollbackState.Pending, int revision = 0) => new(1, Guid.NewGuid(), sid,
        ActionId.Power, ActionScope.CurrentUser, "fixture-power", RollbackPurpose.UserUndo, new(false, 0, []), Value(1),
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-01T00:00:00Z"), state, revision);

    /// <summary>값 없음과 전체 바이너리/형식은 JSON 왕복으로 바뀌지 않습니다.</summary>
    [Fact]
    public void CodecPreservesAbsenceTypeAndEveryByte()
    {
        var original = Record(Session.Sid) with { Applied = new(true, 11, Enumerable.Range(0, 256).Select(i => (byte)i).ToArray()) };
        var loaded = RollbackCodec.Decode(RollbackCodec.Encode(original, Session.Sid), Session.Sid);
        Assert.True(original.Before.SameAs(loaded.Before));
        Assert.True(original.Applied.SameAs(loaded.Applied));
        Assert.DoesNotContain("CanExpire", Encoding.UTF8.GetString(RollbackCodec.Encode(original, Session.Sid)));
    }

    /// <summary>다른 계정/구형/임의 실행 필드/누락/중복/상한/원래 값 없음 위조는 거절합니다.</summary>
    [Theory]
    [InlineData("sid")]
    [InlineData("version")]
    [InlineData("action")]
    [InlineData("extra")]
    [InlineData("computed")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("oversize")]
    [InlineData("absence")]
    [InlineData("partial")]
    public void CodecRejectsUntrustedRecords(string corruption)
    {
        var node = JsonNode.Parse(RollbackCodec.Encode(Record(Session.Sid), Session.Sid))!.AsObject();
        switch (corruption)
        {
            case "sid": node["Sid"] = "other-user"; break;
            case "version": node["Version"] = 0; break;
            case "action": node["ActionId"] = 999; break;
            case "extra": node["Command"] = "ignored.exe"; break;
            case "computed": node["NeedsRecovery"] = false; break;
            case "missing": node.Remove("Revision"); break;
            case "absence": node["Before"]!["Data"] = "AQ=="; break;
        }
        var json = node.ToJsonString();
        if (corruption == "duplicate") { json = json.Insert(1, "\"Version\":1,"); }
        if (corruption == "oversize") { json = new string(' ', RollbackCodec.MaxBytes + 1); }
        if (corruption == "partial") { json = "{\"Version\":"; }
        Assert.ThrowsAny<Exception>(() => RollbackCodec.Decode(Encoding.UTF8.GetBytes(json), Session.Sid));
    }

    /// <summary>기존 원문 변경/리비전 재사용/완료 후 재활성화는 저장 단계에서 거절합니다.</summary>
    [Fact]
    public void StateTransitionsCannotReplaceOriginalOrReplayOldRevision()
    {
        var record = Record(Session.Sid);
        RollbackCodec.CheckTransition(null, record);
        var applied = record with { State = RollbackState.Applied, Revision = 1 };
        RollbackCodec.CheckTransition(record, applied);
        Assert.Throws<InvalidDataException>(() => RollbackCodec.CheckTransition(applied, applied));
        Assert.Throws<InvalidDataException>(() => RollbackCodec.CheckTransition(record, applied with { Before = Value(8) }));
        Assert.Throws<InvalidDataException>(() => RollbackCodec.CheckTransition(applied with { State = RollbackState.Restored }, applied with { Revision = 2 }));
    }

    /// <summary>최초 Pending 원자 저장 실패면 실제 변경은 호출하지 않습니다.</summary>
    [Fact]
    public async Task PendingMustBeDurableBeforeMutation()
    {
        var store = new Store { FailOnSave = 1 };
        var adapter = new Adapter(store);
        var coordinator = Create(store, adapter);
        var plan = (await coordinator.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        var result = await coordinator.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started);
        Assert.False(result.Succeeded);
        Assert.Equal(0, adapter.Writes);
        Assert.Empty(store.Records);
    }

    /// <summary>복구의 Restoring 저장 실패도 이전 적용 값을 변경하지 않습니다.</summary>
    [Fact]
    public async Task RestoringMustBeDurableBeforeUndo()
    {
        var store = new Store { FailOnSave = 3 };
        var adapter = new Adapter(store);
        var coordinator = Create(store, adapter);
        var apply = (await coordinator.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        Assert.True((await coordinator.ExecuteAsync(apply.Id, default)).Succeeded);
        var restore = (await coordinator.PrepareRestoreAsync(ActionId.Power, apply.Id, default)).Plan!;
        var result = await coordinator.ExecuteAsync(restore.Id, default);
        Assert.False(result.Started);
        Assert.False(result.Succeeded);
        Assert.Equal(RollbackState.Applied, store.Only.State);
        Assert.True(adapter.Current.SameAs(Value(1)));
        Assert.Equal(1, adapter.Writes);
    }

    /// <summary>정상 적용·되돌리기는 없음 상태까지 정확히 복원하고 완료 기록을 재사용하지 않습니다.</summary>
    [Fact]
    public async Task SuccessfulUndoPreservesMissingValueAndCannotRepeat()
    {
        var store = new Store();
        var adapter = new Adapter(store);
        var coordinator = Create(store, adapter);
        var apply = (await coordinator.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        Assert.True((await coordinator.ExecuteAsync(apply.Id, default)).Succeeded);
        var restore = (await coordinator.PrepareRestoreAsync(ActionId.Power, apply.Id, default)).Plan!;
        Assert.True((await coordinator.ExecuteAsync(restore.Id, default)).Succeeded);
        Assert.Equal(RollbackState.Restored, store.Only.State);
        Assert.False(adapter.Current.Exists);
        Assert.Equal(0, adapter.Current.NativeType);
        Assert.Empty(adapter.Current.Data);
        Assert.Equal("PlanExpired", (await coordinator.ExecuteAsync(restore.Id, default)).Code);
        Assert.Equal(2, adapter.Writes);
    }

    /// <summary>적용/복구 후 상태 저장 실패는 성공으로 숨기지 않고 실제 원문으로 다시 수습합니다.</summary>
    [Theory]
    [InlineData(2, RollbackState.Pending)]
    [InlineData(4, RollbackState.Restoring)]
    public async Task InterruptedAfterMutationIsRecoverable(int failOnSave, RollbackState expected)
    {
        var store = new Store { FailOnSave = failOnSave };
        var adapter = new Adapter(store);
        var coordinator = Create(store, adapter);
        var apply = (await coordinator.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        var first = await coordinator.ExecuteAsync(apply.Id, default);
        if (failOnSave == 4)
        {
            Assert.True(first.Succeeded);
            var restore = (await coordinator.PrepareRestoreAsync(ActionId.Power, apply.Id, default)).Plan!;
            first = await coordinator.ExecuteAsync(restore.Id, default);
        }
        Assert.True(first.Started);
        Assert.False(first.Succeeded);
        Assert.Equal(expected, store.Only.State);
        Assert.True(store.Only.NeedsRecovery);
        store.FailOnSave = 0;
        // 메모리 계획을 잃은 새 조율기로 재시작을 재현한다.
        coordinator = Create(store, adapter);
        var recovery = (await coordinator.PrepareRestoreAsync(ActionId.Power, apply.Id, default)).Plan!;
        var result = await coordinator.ExecuteAsync(recovery.Id, default);
        Assert.Equal(failOnSave == 2 ? "Restored" : "AlreadyOriginal", result.Code);
        Assert.True(adapter.Current.SameAs(new(false, 0, [])));
        Assert.False(store.Only.NeedsRecovery);
    }

    /// <summary>Pending 저장 뒤 변경 전에 중단되면 원래 값을 관측하고 변경 없이 종료합니다.</summary>
    [Fact]
    public async Task InterruptedBeforeMutationAndDuplicateRestoreDoNotWrite()
    {
        var store = new Store();
        var record = Record(Session.Sid);
        store.Seed(record);
        var adapter = new Adapter(store);
        var coordinator = Create(store, adapter);
        var plan = (await coordinator.PrepareRestoreAsync(ActionId.Power, record.Id, default)).Plan!;
        var result = await coordinator.ExecuteAsync(plan.Id, default);
        Assert.Equal("AlreadyOriginal", result.Code);
        Assert.False(result.Started);
        Assert.Equal(0, adapter.Writes);
        Assert.Equal(RollbackState.Unchanged, store.Only.State);
        Assert.Equal("PlanExpired", (await coordinator.ExecuteAsync(plan.Id, default)).Code);
        Assert.Null((await coordinator.PrepareRestoreAsync(ActionId.Power, record.Id, default)).Plan);
    }

    /// <summary>사용자가 나중에 바꾼 값은 덮어쓰지 않으며 복구 전에 다시 바뀐 값도 보호합니다.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterChangesAreNeverOverwritten(bool atWriteBoundary)
    {
        var store = new Store();
        var adapter = new Adapter(store);
        var coordinator = Create(store, adapter);
        var apply = (await coordinator.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        Assert.True((await coordinator.ExecuteAsync(apply.Id, default)).Succeeded);
        var plan = (await coordinator.PrepareRestoreAsync(ActionId.Power, apply.Id, default)).Plan!;
        if (atWriteBoundary) { adapter.BeforeExchange = () => adapter.Current = Value(9); }
        else { adapter.Current = Value(9); }
        var result = await coordinator.ExecuteAsync(plan.Id, default);
        Assert.False(result.Succeeded);
        Assert.Equal("CurrentValueChanged", result.Code);
        Assert.True(adapter.Current.SameAs(Value(9)));
        Assert.Equal(1, adapter.Writes);
    }

    /// <summary>기록만으로 코드를 고르지 않으며 다른 SID·범위·대상 검증 실패는 어댑터 변경 전 차단합니다.</summary>
    [Theory]
    [InlineData("sid")]
    [InlineData("scope")]
    [InlineData("id")]
    [InlineData("target")]
    public async Task RecoveryRequiresCurrentIdentityAndCodeAdapter(string reason)
    {
        var store = new Store();
        var adapter = new Adapter(store) { Current = Value(1) };
        var record = Record(Session.Sid);
        store.Seed(record);
        var current = Session;
        var coordinator = new RestoreCoordinator(new OperationCoordinator(), store, () => current, [adapter]);
        if (reason == "sid") { current = current with { Sid = "other" }; }
        if (reason == "scope") { current = current with { Scope = ActionUserScope.SystemOnly }; }
        if (reason == "target") { adapter.Valid = false; }
        var plan = await coordinator.PrepareRestoreAsync(reason == "id" ? ActionId.Startup : ActionId.Power, record.Id, default);
        Assert.Null(plan.Plan);
        Assert.Equal(0, adapter.Writes);
    }

    /// <summary>중단 기록은 30일이 지나도 남고 정상 기록만 제거합니다. 내부 서비스 책임은 사용자 undo와 다릅니다.</summary>
    [Fact]
    public async Task StartupInspectionKeepsUnfinishedAndInternalRecovery()
    {
        var store = new Store();
        var pending = Record(Session.Sid);
        var applied = Record(Session.Sid, RollbackState.Applied, 1);
        var service = Record(Session.Sid, RollbackState.Applied, 1) with { ActionId = ActionId.SystemFiles, Scope = ActionScope.System, Purpose = RollbackPurpose.ServiceRecovery };
        store.Seed(pending); store.Seed(applied); store.Seed(service);
        var adapter = new Adapter(store);
        var catalog = await Create(store, adapter).InspectAsync(default);
        Assert.Equal(2, catalog.Records.Count);
        Assert.All(catalog.Records, r => Assert.True(r.NeedsRecovery));
        Assert.Equal(0, adapter.Writes);
    }

    /// <summary>취소를 무시하는 실제 변경은 잠금과 실행 관문을 붙잡고 늦은 실패도 Pending으로 남습니다.</summary>
    [Fact]
    public async Task TimeoutRetainsCrossInstanceLockUntilNativeWorkEnds()
    {
        var store = new Store();
        var adapter = new Adapter(store) { WaitOnExchange = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var gate = new OperationCoordinator();
        var coordinator = new RestoreCoordinator(gate, store, () => Session, [adapter], timeout: TimeSpan.FromMilliseconds(100));
        var plan = (await coordinator.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        var result = await coordinator.ExecuteAsync(plan.Id, default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Draining", result.Code);
        Assert.True(store.Opened);
        Assert.Null(gate.TryAcquire(OperationKind.Scan));
        Assert.Throws<IOException>(() => store.Open(Session));
        adapter.WaitOnExchange.SetException(new IOException("late failure"));
        await OperationLifetimeTests.Idle(gate);
        Assert.False(store.Opened);
        Assert.Equal("Failed", coordinator.GetResult(plan.Id)!.Code);
        Assert.Equal(RollbackState.Pending, store.Only.State);
    }

    private static RestoreCoordinator Create(Store store, Adapter adapter) => new(new OperationCoordinator(), store, () => Session, [adapter]);
    private sealed class Adapter(Store store) : IReversibleActionAdapter
    {
        public ActionDefinition Definition => new(ActionId.Power, ActionScope.CurrentUser, true);
        internal RollbackValue Current = new(false, 0, []);
        internal bool Valid = true;
        internal int Writes;
        internal Action? BeforeExchange;
        internal TaskCompletionSource? WaitOnExchange;
        public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct) => Task.FromResult<ActionPreview?>(new(target, "fixture", "fixture"));
        public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct) => Task.FromResult(new RollbackChange("fixture-power", RollbackPurpose.UserUndo, Current, Value(1)));
        public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct) => Task.FromResult(Valid && record.TargetKey == "fixture-power");
        public Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct) => Task.FromResult(Current);
        public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
        {
            Assert.True(store.Opened);
            Assert.Contains(store.Only.State, new[] { RollbackState.Pending, RollbackState.Restoring });
            if (WaitOnExchange is not null) { await WaitOnExchange.Task; }
            BeforeExchange?.Invoke();
            if (!Current.SameAs(expected)) { return false; }
            Writes++; Current = desired;
            return true;
        }
    }
    private sealed class Store : IRollbackStore
    {
        internal readonly Dictionary<Guid, byte[]> Records = [];
        internal int FailOnSave;
        internal int Saves;
        internal bool Opened;
        internal RollbackRecord Only => RollbackCodec.Decode(Records.Single().Value, Session.Sid);
        internal void Seed(RollbackRecord record) => Records[record.Id] = RollbackCodec.Encode(record, record.Sid);
        public IRollbackTransaction Open(ActionSession session)
        {
            if (Opened) { throw new IOException("Busy"); }
            Opened = true;
            return new Transaction(this, session.Sid);
        }
        private sealed class Transaction(Store owner, string sid) : IRollbackTransaction
        {
            public RollbackRecord? Read(Guid id) => owner.Records.TryGetValue(id, out var bytes) ? RollbackCodec.Decode(bytes, sid) : null;
            public RollbackCatalog ReadAll() => new(owner.Records.Keys.Select(id => Read(id)!).ToArray(), []);
            public void Save(RollbackRecord record)
            {
                if (++owner.Saves == owner.FailOnSave) { throw new IOException("fixture commit failure"); }
                RollbackCodec.CheckTransition(Read(record.Id), record);
                owner.Records[record.Id] = RollbackCodec.Encode(record, sid);
            }
            public int PruneCompleted(DateTimeOffset olderThan)
            {
                var ids = ReadAll().Records.Where(r => r.CanExpire && r.UpdatedAt < olderThan).Select(r => r.Id).ToArray();
                foreach (var id in ids) { owner.Records.Remove(id); }
                return ids.Length;
            }
            public void Dispose() { owner.Opened = false; }
        }
    }
}
