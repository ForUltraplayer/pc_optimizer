/**
 * @file    : DisplayTrialCoordinator.cs
 * @author  : rudals252
 * @brief   : Pending 선행·15초 단조 확인·독립 원복·내구 기록을 공유 관문과 연결
 */
using System.Collections.Concurrent;
using System.Text.Json;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Display;

/// <summary>UI가 멈추거나 닫혀도 워커가 기한 원복을 수행합니다. 프로세스 강제 종료 중 타이머 실행은 보장하지 않습니다.</summary>
public sealed class DisplayTrialCoordinator
{
    private readonly IDisplaySettingsPlatform _platform;
    private readonly IOperationCoordinator _operations;
    private readonly IRollbackStore _store;
    private readonly Func<ActionSession> _session;
    private readonly TimeProvider _time;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<Guid, Plan> _plans = new();
    private readonly object _decisionLock = new();
    private Active? _active;
    private sealed record Plan(Guid Id, DisplayChange Change, ActionSession Session, long Created);
    private sealed class Active(Guid id, long since)
    {
        internal Guid Id = id;
        internal long Since = since;
        internal string State = "Waiting";
        internal TaskCompletionSource<bool> Decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    /// <summary>본 실행기는 실환경 시험이 끝난 뒤 제품 조립에서 등록합니다.</summary>
    public DisplayTrialCoordinator(IOperationCoordinator operations, IRollbackStore store, Func<ActionSession> session)
        : this(new DisplaySettingsPlatform(), operations, store, session) { }
    internal DisplayTrialCoordinator(IDisplaySettingsPlatform platform, IOperationCoordinator operations, IRollbackStore store,
        Func<ActionSession> session, TimeProvider? time = null, TimeSpan? window = null)
    {
        _platform = platform; _operations = operations; _store = store; _session = session; _time = time ?? TimeProvider.System;
        _window = window ?? TimeSpan.FromSeconds(15);
        if (_window <= TimeSpan.Zero || _window > TimeSpan.FromSeconds(15)) { throw new ArgumentOutOfRangeException(nameof(window)); }
    }
    /// <summary>현재 시험의 남은 시간을 단조 시계로 계산합니다. UI 타이머는 원복의 소유자가 아닙니다.</summary>
    public DisplayTrialStatus? Status
    {
        get { lock (_decisionLock) { return _active is { } a ? new(a.Id, a.State, Remaining(a.Since)) : null; } }
    }
    private TimeSpan Remaining(long since) => TimeSpan.FromTicks(Math.Max(0, (_window - _time.GetElapsedTime(since)).Ticks));
    private ActionSession Session()
    {
        var session = _session();
        if (!session.IsKnown || session.Scope != ActionUserScope.Full) { throw new ActionUnavailableException("ScopeExcluded"); }
        return session;
    }
    /// <summary>지원 모드와 기록을 같은 관문 안에서 읽습니다. 실제 적용은 하지 않습니다.</summary>
    public async Task<DisplayTrialCatalog> InspectAsync(CancellationToken ct)
    {
        using var lease = _operations.TryAcquire(OperationKind.Prepare);
        if (lease is null) { return new([], [], "Busy"); }
        var running = Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested(); var session = Session();
                using var transaction = _store.Open(session); var catalog = transaction.ReadAll();
                var records = new List<DisplayTrialRecord>(); var invalid = catalog.Issues.Count > 0;
                foreach (var record in catalog.Records.Where(r => r.ActionId == ActionId.Display))
                {
                    try
                    {
                        var change = ReadChange(record, session);
                        records.Add(new(record.Id, change.Before.Mode.RefreshHz, change.Desired.Mode.RefreshHz, record.NeedsRecovery,
                            record.State is RollbackState.Pending or RollbackState.Applied or RollbackState.Restoring));
                    }
                    catch { invalid = true; }
                }
                ct.ThrowIfCancellationRequested();
                IReadOnlyList<DisplayTrialChoice> choices;
                try { choices = _platform.Choices(); }
                catch (Exception ex) { return new DisplayTrialCatalog([], records, Code(ex)); }
                if (Session() != session) { throw new ActionUnavailableException("SessionChanged"); }
                return new DisplayTrialCatalog(choices, records, invalid ? "RecordsUnavailable" : "Ready");
            }
            catch (Exception ex) { return new DisplayTrialCatalog([], [], Code(ex)); }
        }, CancellationToken.None);
        lease.Track(running); return await running.ConfigureAwait(false);
    }
    /// <summary>같은 해상도의 실제 지원 모드를 읽고 CDS_TEST를 통과한 계획만 발급합니다.</summary>
    public async Task<DisplayTrialPreparation> PrepareAsync(string monitorPath, int hz, CancellationToken ct)
    {
        using var lease = _operations.TryAcquire(OperationKind.Prepare);
        if (lease is null) { return new(null, null, null, "Busy"); }
        var running = Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested(); var session = Session();
                var change = _platform.Capture(monitorPath, hz);
                ValidateChange(change);
                if (_platform.Test(change.Identity, change.Desired) != 0) { return new DisplayTrialPreparation(null, null, null, "DisplayTestFailed"); }
                ct.ThrowIfCancellationRequested(); if (Session() != session) { throw new ActionUnavailableException("SessionChanged"); }
                foreach (var old in _plans.Where(p => _time.GetElapsedTime(p.Value.Created) >= TimeSpan.FromMinutes(5))) { _plans.TryRemove(old.Key, out _); }
                var plan = new Plan(Guid.NewGuid(), Clone(change), session, _time.GetTimestamp()); _plans[plan.Id] = plan;
                return new(plan.Id, change.Before.Mode.RefreshHz, change.Desired.Mode.RefreshHz, "Prepared");
            }
            catch (Exception ex) { return new(null, null, null, Code(ex)); }
        }, CancellationToken.None);
        lease.Track(running); return await running.ConfigureAwait(false);
    }
    /// <summary>같은 계획의 15초 기한 안에서 첫 유지 결정만 받습니다. 늦은 클릭은 무효입니다.</summary>
    public bool Keep(Guid id)
    {
        lock (_decisionLock)
        {
            return _active is { State: "Waiting" } a && a.Id == id && Remaining(a.Since) > TimeSpan.Zero && a.Decision.TrySetResult(true);
        }
    }
    /// <summary>확인창 닫기·취소에 사용합니다. 원복은 실행 워커에서 실제 종료까지 계속합니다.</summary>
    public bool Revert(Guid id)
    {
        lock (_decisionLock) { return _active is { State: "Waiting" } a && a.Id == id && a.Decision.TrySetResult(false); }
    }
    /// <summary>원본 저장 뒤 시험 적용하고 유지/기한/취소 결과를 기다립니다. 실행 중 호출 스레드를 막지 않습니다.</summary>
    public async Task<DisplayTrialResult> StartAsync(Guid id, CancellationToken ct)
    {
        using var lease = _operations.TryAcquire(OperationKind.Apply);
        if (lease is null) { return new(id, false, false, false, "Busy"); }
        if (!_plans.TryRemove(id, out var plan)) { return new(id, false, false, false, "PlanExpired"); }
        var running = Task.Run(() => RunAsync(plan, ct), CancellationToken.None);
        lease.Track(running); return await running.ConfigureAwait(false);
    }
    private async Task<DisplayTrialResult> RunAsync(Plan plan, CancellationToken ct)
    {
        var started = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (Session() != plan.Session) { throw new ActionUnavailableException("SessionChanged"); }
            if (_time.GetElapsedTime(plan.Created) >= TimeSpan.FromMinutes(5)) { throw new ActionUnavailableException("PlanExpired"); }
            using var transaction = _store.Open(plan.Session);
            var catalog = transaction.ReadAll();
            if (catalog.Issues.Count > 0) { throw new ActionUnavailableException("RecordsUnavailable"); }
            if (catalog.Records.Any(r => r.ActionId == ActionId.Display && r.NeedsRecovery)) { throw new ActionUnavailableException("RecoveryRequired"); }
            var change = plan.Change;
            var observed = _platform.Observe(change.Identity);
            if (!Original(change, observed)) { throw new ActionUnavailableException("CurrentValueChanged"); }
            if (_platform.Test(change.Identity, change.Desired) != 0) { throw new ActionUnavailableException("DisplayTestFailed"); }
            var now = _time.GetUtcNow();
            var record = new RollbackRecord(1, plan.Id, plan.Session.Sid, ActionId.Display, ActionScope.CurrentUser, "display-refresh-v1",
                RollbackPurpose.UserUndo, new(true, 1, JsonSerializer.SerializeToUtf8Bytes(change)), new(true, 1, JsonSerializer.SerializeToUtf8Bytes(change.Desired)),
                now, now, RollbackState.Pending, 0);
            transaction.Save(record);
            var failure = "Cancelled";
            try
            {
                ct.ThrowIfCancellationRequested(); if (Session() != plan.Session) { throw new ActionUnavailableException("SessionChanged"); }
                if (_time.GetElapsedTime(plan.Created) >= TimeSpan.FromMinutes(5)) { throw new ActionUnavailableException("PlanExpired"); }
                var trialSince = _time.GetTimestamp();
                if (_platform.Apply(change.Identity, change.Desired, false, observed, () =>
                    {
                        ct.ThrowIfCancellationRequested(); if (Session() != plan.Session) { throw new ActionUnavailableException("SessionChanged"); }
                        if (_time.GetElapsedTime(plan.Created) >= TimeSpan.FromMinutes(5)) { throw new ActionUnavailableException("PlanExpired"); }
                        trialSince = _time.GetTimestamp(); started = true;
                    }) != 0) { throw new ActionUnavailableException("DisplayApplyFailed"); }
                observed = _platform.Observe(change.Identity);
                if (!observed.Current.SameMode(change.Desired) || !observed.Registered.SameMode(change.RegisteredBefore)) { throw new ActionUnavailableException("DisplayVerificationFailed"); }
                var active = new Active(plan.Id, trialSince);
                lock (_decisionLock) { _active = active; }
                using var cancel = ct.Register(() => Revert(plan.Id));
                using var deadlineCancel = new CancellationTokenSource();
                var timeout = Task.Delay(Remaining(active.Since), _time, deadlineCancel.Token);
                await Task.WhenAny(active.Decision.Task, timeout).ConfigureAwait(false);
                bool keep;
                lock (_decisionLock)
                {
                    keep = !ct.IsCancellationRequested && Remaining(active.Since) > TimeSpan.Zero && active.Decision.Task.IsCompletedSuccessfully && active.Decision.Task.Result;
                    active.State = keep ? "Persisting" : "Reverting";
                }
                deadlineCancel.Cancel();
                if (keep)
                {
                    if (Session() != plan.Session) { throw new ActionUnavailableException("SessionChanged"); }
                    observed = _platform.Observe(change.Identity);
                    if (!observed.Current.SameMode(change.Desired) || !observed.Registered.SameMode(change.RegisteredBefore)) { throw new ActionUnavailableException("CurrentValueChanged"); }
                    if (_platform.Apply(change.Identity, change.Desired, true, observed, () =>
                    {
                        ct.ThrowIfCancellationRequested(); if (Session() != plan.Session) { throw new ActionUnavailableException("SessionChanged"); }
                        if (Remaining(active.Since) <= TimeSpan.Zero) { throw new ActionUnavailableException("TrialExpired"); }
                    }) != 0) { throw new ActionUnavailableException("DisplaySaveFailed"); }
                    observed = _platform.Observe(change.Identity);
                    if (!observed.Current.SameMode(change.Desired) || !observed.Registered.SameMode(change.Desired)) { throw new ActionUnavailableException("DisplayVerificationFailed"); }
                    transaction.Save(Advance(record, RollbackState.Applied));
                    return new(plan.Id, true, true, false, "Kept");
                }
                failure = ct.IsCancellationRequested ? "Cancelled" : active.Decision.Task.IsCompletedSuccessfully ? "Reverted" : "TrialExpired";
            }
            catch (Exception ex) { failure = Code(ex); }
            return RollBack(transaction, record, change, plan.Session, started, failure);
        }
        catch (Exception ex) { return new(plan.Id, started, false, false, Code(ex)); }
        finally { lock (_decisionLock) { if (_active?.Id == plan.Id) { _active = null; } } }
    }
    /// <summary>같은 사용자·장치의 중단/유지 기록을 재식별한 뒤 원래 모드로 복원합니다. 원문 JSON은 UI에서 받지 않습니다.</summary>
    public async Task<DisplayTrialResult> RestoreAsync(Guid id, CancellationToken ct)
    {
        using var lease = _operations.TryAcquire(OperationKind.Restore);
        if (lease is null) { return new(id, false, false, false, "Busy"); }
        var running = Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested(); var session = Session(); using var transaction = _store.Open(session);
                var record = transaction.Read(id) ?? throw new ActionUnavailableException("TargetRejected");
                var change = ReadChange(record, session);
                if (record.State is RollbackState.Restored or RollbackState.Unchanged) { throw new ActionUnavailableException("TargetRejected"); }
                ct.ThrowIfCancellationRequested();
                return RollBack(transaction, record, change, session, false, "Restored");
            }
            catch (Exception ex) { return new(id, false, false, false, Code(ex)); }
        }, CancellationToken.None);
        lease.Track(running); return await running.ConfigureAwait(false);
    }
    private DisplayTrialResult RollBack(IRollbackTransaction transaction, RollbackRecord record, DisplayChange change, ActionSession owner, bool started, string reason)
    {
        try
        {
            if (Session() != owner) { throw new ActionUnavailableException("SessionChanged"); }
            var current = _platform.Observe(change.Identity);
            if (Original(change, current))
            {
                try { transaction.Save(Advance(record, RollbackState.Unchanged)); }
                catch { return new(record.Id, started, false, true, "RecoveryRecordFailed"); }
                return new(record.Id, started, false, true, reason);
            }
            if ((!current.Current.SameMode(change.Desired) && !current.Current.SameMode(change.Before))
                || (!current.Registered.SameMode(change.Desired) && !current.Registered.SameMode(change.RegisteredBefore)))
            { throw new ActionUnavailableException("CurrentValueChanged"); }
            // 복구는 취소 토큰을 사용하지 않는다. Pending이 이미 원본을 보존하며 저장 실패도 원복 시도를 막지 않는다.
            var journalOk = true;
            try { if (record.State != RollbackState.Restoring) { var next = Advance(record, RollbackState.Restoring); transaction.Save(next); record = next; } }
            catch { journalOk = false; }
            void BeforeWrite() { if (Session() != owner) { throw new ActionUnavailableException("SessionChanged"); } started = true; }
            if (!current.Registered.SameMode(change.RegisteredBefore) && _platform.SaveProfileOnly(change.Identity, change.RegisteredBefore, current, BeforeWrite) != 0)
            { throw new ActionUnavailableException("DisplayRecoveryFailed"); }
            current = _platform.Observe(change.Identity);
            if (!current.Current.SameMode(change.Desired) && !current.Current.SameMode(change.Before)) { throw new ActionUnavailableException("CurrentValueChanged"); }
            if (_platform.Apply(change.Identity, change.Before, false, current, BeforeWrite) != 0 || !Original(change, _platform.Observe(change.Identity)))
            { throw new ActionUnavailableException("DisplayRecoveryFailed"); }
            if (journalOk) { try { transaction.Save(Advance(record, RollbackState.Restored)); } catch { journalOk = false; } }
            return new(record.Id, started, false, true, journalOk ? reason : "RecoveryRecordFailed");
        }
        catch (Exception ex) { return new(record.Id, started, false, false, Code(ex)); }
    }
    private RollbackRecord Advance(RollbackRecord record, RollbackState state) => record with
    { State = state, Revision = checked(record.Revision + 1), UpdatedAt = _time.GetUtcNow() < record.UpdatedAt ? record.UpdatedAt : _time.GetUtcNow() };
    private static bool Original(DisplayChange change, DisplayObservation observed) => observed.Current.SameMode(change.Before) && observed.Registered.SameMode(change.RegisteredBefore);
    private static DisplayChange ReadChange(RollbackRecord record, ActionSession session)
    {
        if (record.Sid != session.Sid || record.ActionId != ActionId.Display || record.TargetKey != "display-refresh-v1" || record.Scope != ActionScope.CurrentUser
            || record.Purpose != RollbackPurpose.UserUndo || !record.Before.Exists || record.Before.NativeType != 1 || !record.Applied.Exists || record.Applied.NativeType != 1)
        { throw new ActionUnavailableException("TargetRejected"); }
        var change = JsonSerializer.Deserialize<DisplayChange>(record.Before.Data) ?? throw new ActionUnavailableException("TargetRejected");
        ValidateChange(change);
        if (!record.Applied.Data.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(change.Desired))) { throw new ActionUnavailableException("TargetRejected"); }
        return change;
    }
    private static DisplayChange Clone(DisplayChange value) => value with
    { Before = value.Before with { Native = value.Before.Native.ToArray() }, RegisteredBefore = value.RegisteredBefore with { Native = value.RegisteredBefore.Native.ToArray() }, Desired = value.Desired with { Native = value.Desired.Native.ToArray() } };
    private static void ValidateChange(DisplayChange change)
    {
        if (change.Identity is null || string.IsNullOrWhiteSpace(change.Identity.MonitorPath) || string.IsNullOrWhiteSpace(change.Identity.AdapterPath)
            || string.IsNullOrWhiteSpace(change.Identity.GdiName)) { throw new ActionUnavailableException("TargetRejected"); }
        DisplaySettingsPlatform.ValidateFrame(change.Before); DisplaySettingsPlatform.ValidateFrame(change.RegisteredBefore); DisplaySettingsPlatform.ValidateFrame(change.Desired);
        if (!DisplaySettingsPlatform.SameShape(change.Before.Mode, change.Desired.Mode) || !DisplaySettingsPlatform.SameShape(change.Before.Mode, change.RegisteredBefore.Mode))
        { throw new ActionUnavailableException("TargetRejected"); }
    }
    private static string Code(Exception ex) => ex is ActionUnavailableException known ? known.Code : ex is OperationCanceledException ? "Cancelled" : "DisplayFailed";
}
