/**
 * @file    : RestoreCoordinator.cs
 * @author  : rudals252
 * @brief   : 변경 전 영속 Pending·현재 값 비교 복구·중단 기록 관측을 공통 실행 관문에 연결
 */
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions;

/// <summary>신뢰된 코드 어댑터만 등록하는 복구 조율기입니다. 자동 복구는 기본적으로 실행하지 않습니다.</summary>
public sealed class RestoreCoordinator
{
    private readonly ActionCoordinator _actions;
    private readonly IOperationCoordinator _operations;
    private readonly IRollbackStore _store;
    private readonly Func<ActionSession> _session;
    private readonly TimeProvider _time;

    /// <summary>실제 앱과 동일한 실행 관문·세션 공급자를 전달해야 합니다.</summary>
    public RestoreCoordinator(IOperationCoordinator operations, IRollbackStore store, Func<ActionSession> session,
        IEnumerable<IReversibleActionAdapter> adapters, TimeProvider? time = null, TimeSpan? timeout = null)
    {
        _operations = operations;
        _store = store;
        _session = session;
        _time = time ?? TimeProvider.System;
        _actions = new ActionCoordinator(operations, session, adapters.Select(a => new JournalAdapter(a, store, session, _time)), _time, timeout: timeout);
    }
    /// <summary>코드 어댑터가 검증한 적용 미리보기를 발급합니다.</summary>
    public Task<ActionPreparation> PrepareApplyAsync(ActionId id, ActionTarget target, CancellationToken ct)
        => _actions.PrepareAsync(id, target, false, ct);
    /// <summary>사용자에게 보여 줄 기록 ID만 받습니다. 조치 ID와 실제 기록이 다르면 거절합니다.</summary>
    public Task<ActionPreparation> PrepareRestoreAsync(ActionId id, Guid recordId, CancellationToken ct)
        => _actions.PrepareAsync(id, new ActionTarget.Restore(recordId), true, ct);
    /// <summary>일회성 계획을 실행하며 실제 작업 종료까지 저장소 잠금과 공통 관문을 유지합니다.</summary>
    public Task<ActionResult> ExecuteAsync(Guid planId, CancellationToken ct) => _actions.ExecuteAsync(planId, ct);
    /// <summary>취소/타임아웃 뒤 늦게 도착한 실제 결과도 조회합니다.</summary>
    public ActionResult? GetResult(Guid planId) => _actions.GetResult(planId);

    /// <summary>재시작 후 복구 필요 목록을 읽습니다. 이 API는 설정을 자동 변경하지 않습니다.</summary>
    public async Task<RollbackCatalog> InspectAsync(CancellationToken ct)
    {
        using var lease = _operations.TryAcquire(OperationKind.Prepare) ?? throw new InvalidOperationException("Busy");
        var session = _session();
        var running = Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var transaction = _store.Open(session);
            if (_session() != session) { throw new InvalidOperationException("SessionChanged"); }
            transaction.PruneCompleted(_time.GetUtcNow() - TimeSpan.FromDays(30));
            return transaction.ReadAll();
        }, CancellationToken.None);
        lease.Track(running);
        return await running.WaitAsync(ct).ConfigureAwait(false);
    }

    private sealed class JournalAdapter(IReversibleActionAdapter adapter, IRollbackStore store, Func<ActionSession> session, TimeProvider time) : IActionAdapter
    {
        public ActionDefinition Definition => adapter.Definition with { SupportsRestore = true };
        public async Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
        {
            if (!restore) { return await adapter.PrepareAsync(target, ct).ConfigureAwait(false); }
            if (target is not ActionTarget.Restore selected) { return null; }
            var currentSession = session();
            using var transaction = store.Open(currentSession);
            try
            {
                var record = transaction.Read(selected.RecordId);
                if (record is null || !Matches(record, currentSession) || Completed(record)
                    || !await adapter.ValidateAsync(record, ct).ConfigureAwait(false)) { return null; }
                if (record.ActionId is ActionId.Startup or ActionId.MachineStartup && StartupRegistration.Name(record.TargetKey) is { } name)
                {
                    var scopeLabel = StartupRegistration.Label(StartupRegistration.SourceOfKey(record.TargetKey)!);
                    return new(target, $"'{name}'의 원래 자동 실행 등록 복원 · {scopeLabel}", "표시한 사용자 범위에서 다음 로그인 때 원래 등록 명령을 다시 사용할 수 있게 합니다. 작업 관리자의 사용/사용 안 함 상태는 변경하지 않으며 앱을 지금 실행하지 않습니다.",
                        new(name + " · " + scopeLabel, "선택한 출처에 같은 이름의 새 등록이 없을 때만 원래 형식과 내용을 복원합니다. 다른 앱이 다시 등록한 값은 덮어쓰지 않습니다.", RequiresRestart: false));
                }
                return new(target, "이 앱이 변경한 설정 되돌리기", "현재 값이 앱의 적용 값과 같을 때만 이전 값으로 되돌립니다.");
            }
            finally { await DrainAsync().ConfigureAwait(false); }
        }
        public async Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
        {
            using var transaction = store.Open(plan.Session);
            try
            {
                if (plan.IsRestore) { return await RestoreAsync(transaction, plan, execution, ct).ConfigureAwait(false); }
                var change = await adapter.CaptureAsync(plan, ct).ConfigureAwait(false);
                var now = time.GetUtcNow();
                // 복사본을 저장/검증한다. 어댑터가 보유한 배열을 나중에 바꾸어 기록을 바꿀 수 없다.
                var record = RollbackCodec.Decode(RollbackCodec.Encode(new(1, plan.Id, plan.Session.Sid,
                    Definition.Id, Definition.Scope, change.TargetKey, change.Purpose, change.Before, change.Applied,
                    now, now, RollbackState.Pending, 0), plan.Session.Sid), plan.Session.Sid);
                if (!Matches(record, session()) || !await adapter.ValidateAsync(record, ct).ConfigureAwait(false)) { return Result(plan, false, false, "TargetRejected"); }
                if (!(await adapter.ReadCurrentAsync(record, ct).ConfigureAwait(false)).SameAs(record.Before)) { return Result(plan, false, false, "CurrentValueChanged"); }
                if (record.Before.SameAs(record.Applied)) { return Result(plan, false, false, "AlreadyApplied"); }
                ct.ThrowIfCancellationRequested();
                transaction.Save(record); // 내구성 확보 실패 시 아래 변경 호출에 도달하지 않는다.
                execution.MarkStarted();
                if (!await adapter.CompareExchangeAsync(record, record.Before, record.Applied, ct).ConfigureAwait(false))
                { return Result(plan, true, false, "CurrentValueChanged"); }
                await DrainAsync().ConfigureAwait(false);
                if (!(await adapter.ReadCurrentAsync(record, ct).ConfigureAwait(false)).SameAs(record.Applied))
                { return Result(plan, true, false, "VerificationFailed"); }
                transaction.Save(Advance(record, RollbackState.Applied));
                return Result(plan, true, true, "Applied");
            }
            finally { await DrainAsync().ConfigureAwait(false); }
        }
        private async Task<ActionResult> RestoreAsync(IRollbackTransaction transaction, ActionPlan plan, IActionExecution execution, CancellationToken ct)
        {
            if (plan.Preview.Target is not ActionTarget.Restore selected) { return Result(plan, false, false, "TargetRejected"); }
            var record = transaction.Read(selected.RecordId);
            if (record is null || !Matches(record, session()) || Completed(record)
                || !await adapter.ValidateAsync(record, ct).ConfigureAwait(false)) { return Result(plan, false, false, "TargetRejected"); }
            var current = await adapter.ReadCurrentAsync(record, ct).ConfigureAwait(false);
            if (current.SameAs(record.Before))
            {
                transaction.Save(Advance(record, RollbackState.Unchanged));
                return Result(plan, false, false, "AlreadyOriginal");
            }
            if (!current.SameAs(record.Applied)) { return Result(plan, false, false, "CurrentValueChanged"); }
            ct.ThrowIfCancellationRequested();
            if (record.State != RollbackState.Restoring) { record = Advance(record, RollbackState.Restoring); transaction.Save(record); }
            execution.MarkStarted();
            if (!await adapter.CompareExchangeAsync(record, record.Applied, record.Before, ct).ConfigureAwait(false))
            { return Result(plan, true, false, "CurrentValueChanged"); }
            await DrainAsync().ConfigureAwait(false);
            if (!(await adapter.ReadCurrentAsync(record, ct).ConfigureAwait(false)).SameAs(record.Before))
            { return Result(plan, true, false, "VerificationFailed"); }
            transaction.Save(Advance(record, RollbackState.Restored));
            return Result(plan, true, true, "Restored");
        }
        private bool Matches(RollbackRecord record, ActionSession current) => record.ActionId == Definition.Id && record.Scope == Definition.Scope
            && current.IsKnown && record.Sid == current.Sid && (record.Scope == ActionScope.System || current.Scope == ActionUserScope.Full);
        private static bool Completed(RollbackRecord record) => record.State is RollbackState.Restored or RollbackState.Unchanged;
        private RollbackRecord Advance(RollbackRecord r, RollbackState state)
        {
            var now = time.GetUtcNow();
            return r with { State = state, Revision = checked(r.Revision + 1), UpdatedAt = now < r.UpdatedAt ? r.UpdatedAt : now };
        }
        private static ActionResult Result(ActionPlan plan, bool started, bool success, string code) => new(plan.Id, started, success, code);
        public Task WaitForDrainAsync() => adapter.WaitForDrainAsync();
        private async Task DrainAsync()
        {
            while (true)
            {
                try { await adapter.WaitForDrainAsync().ConfigureAwait(false); return; }
                catch { await Task.Delay(1000).ConfigureAwait(false); } // 실제 종료 미확인은 저장소 잠금 해제 근거가 아니다.
            }
        }
    }
}
