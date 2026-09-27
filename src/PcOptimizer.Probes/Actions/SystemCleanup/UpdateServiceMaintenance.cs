/**
 * @file    : UpdateServiceMaintenance.cs
 * @author  : rudals252
 * @brief   : 정리 작업의 서비스별 선행 원본 기록·중지·부분 실패/취소 후 독립 복구를 소유
 */
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

// T9-B 정리 실행기가 공통 ActionCoordinator 워커 안에서 사용할 기반이다.
// 아직 제품에서 이 중지 진입점을 호출하지 않으며, 상태 조회만으로 호출 허용을 추정하지 않는다.
internal sealed class UpdateServiceMaintenance(IRollbackStore store, IUpdateServicePlatform platform, Func<ActionSession> session, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    internal async Task<ActionResult> RunAsync(ActionPlan plan, IActionExecution execution,
        Func<IActionExecution, CancellationToken, Task<ActionResult>> work, Action checkActivity, CancellationToken ct)
    {
        void CheckSession()
        {
            if (!plan.Session.IsKnown || session() != plan.Session || SystemActionSession.Read(plan.Session.Scope) != plan.Session)
            { throw new ActionUnavailableException("SessionChanged"); }
        }
        CheckSession(); ct.ThrowIfCancellationRequested(); checkActivity();
        using var transaction = store.Open(plan.Session);
        var catalog = transaction.ReadAll();
        if (catalog.Issues.Count > 0 || catalog.Records.Any(r => r.Purpose == RollbackPurpose.ServiceRecovery && r.NeedsRecovery))
        { return new(plan.Id, false, false, "UpdateRecoveryRequired"); }
        var services = new[] { UpdateService.WindowsUpdate, UpdateService.Bits };
        var originals = services.ToDictionary(s => s, s => platform.Read(s));
        if (originals.Values.Any(s => s.State is not (UpdateServiceState.Stopped or UpdateServiceState.Running)
            || (s.State == UpdateServiceState.Running && !s.AcceptsStop)))
        { return new(plan.Id, false, false, "UpdateServiceTransitionFailed"); }
        var records = new List<Guid>();
        var started = new StartOnce(execution);
        ActionResult result = new(plan.Id, false, false, "UpdateServiceChangeFailed");
        var recoveryFailed = false;
        try
        {
            foreach (var service in services)
            {
                CheckSession(); ct.ThrowIfCancellationRequested(); checkActivity();
                var original = originals[service];
                if (platform.Read(service).State != original.State) { throw new ActionUnavailableException("CurrentValueChanged"); }
                if (original.State == UpdateServiceState.Stopped) { continue; }
                var now = _time.GetUtcNow();
                var record = new RollbackRecord(1, Guid.NewGuid(), plan.Session.Sid, ActionId.UpdateServices, ActionScope.System,
                    UpdateServiceRecoveryAdapter.Key(service), RollbackPurpose.ServiceRecovery,
                    UpdateServiceRecoveryAdapter.Value(UpdateServiceState.Running), UpdateServiceRecoveryAdapter.Value(UpdateServiceState.Stopped),
                    now, now, RollbackState.Pending, 0);
                transaction.Save(record); records.Add(record.Id); // 반드시 서비스 제어 전에 내구 원본 확보
                if (!await platform.ChangeAsync(service, UpdateServiceState.Running, UpdateServiceState.Stopped, () =>
                    { CheckSession(); ct.ThrowIfCancellationRequested(); checkActivity(); started.MarkStarted(); }, ct).ConfigureAwait(false))
                { throw new ActionUnavailableException("CurrentValueChanged"); }
                transaction.Save(Advance(record, RollbackState.Applied));
            }
            CheckSession(); ct.ThrowIfCancellationRequested();
            if (services.Any(s => platform.Read(s).State != UpdateServiceState.Stopped)) { throw new ActionUnavailableException("CurrentValueChanged"); }
            // 후속 파일 실행기도 매 삭제 직전에 서비스 재시작·대상 변경을 확인해야 한다.
            result = await work(started, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { result = new(plan.Id, started.Started, false, "Cancelled"); }
        catch (ActionUnavailableException ex) { result = new(plan.Id, started.Started, false, ex.Code); }
        catch (Exception) { result = new(plan.Id, started.Started, false, "UpdateMaintenanceFailed"); }
        finally
        {
            foreach (var id in records.AsEnumerable().Reverse())
            {
                // 취소된 작업 토큰을 복구에 재사용하지 않는다. 한 서비스 실패 뒤에도 다른 서비스를 복구한다.
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                try
                {
                    CheckSession();
                    var record = transaction.Read(id) ?? throw new InvalidDataException("MissingRecoveryRecord");
                    var adapter = new UpdateServiceRecoveryAdapter(platform, session);
                    var current = await adapter.ReadCurrentAsync(record, recovery.Token).ConfigureAwait(false);
                    if (current.SameAs(record.Before)) { transaction.Save(Advance(record, RollbackState.Unchanged)); continue; }
                    if (!current.SameAs(record.Applied)) { throw new ActionUnavailableException("CurrentValueChanged"); }
                    record = Advance(record, RollbackState.Restoring); transaction.Save(record);
                    if (!await adapter.CompareExchangeAsync(record, record.Applied, record.Before, recovery.Token).ConfigureAwait(false)
                        || !(await adapter.ReadCurrentAsync(record, recovery.Token).ConfigureAwait(false)).SameAs(record.Before))
                    { throw new ActionUnavailableException("UpdateRecoveryFailed"); }
                    transaction.Save(Advance(record, RollbackState.Restored));
                }
                catch (Exception) { recoveryFailed = true; } // 미완료 기록은 삭제하거나 완료로 바꾸지 않는다.
            }
        }
        return result with { Started = result.Started || started.Started, Succeeded = result.Succeeded && !recoveryFailed,
            Code = recoveryFailed ? "UpdateRecoveryFailed" : result.Code };
    }
    private RollbackRecord Advance(RollbackRecord record, RollbackState state)
    {
        var now = _time.GetUtcNow();
        return record with { State = state, Revision = checked(record.Revision + 1), UpdatedAt = now < record.UpdatedAt ? record.UpdatedAt : now };
    }
    private sealed class StartOnce(IActionExecution inner) : IActionExecution
    {
        internal bool Started { get; private set; }
        public void MarkStarted() { if (!Started) { inner.MarkStarted(); Started = true; } }
        public void Track(Task task) => inner.Track(task);
    }
}
