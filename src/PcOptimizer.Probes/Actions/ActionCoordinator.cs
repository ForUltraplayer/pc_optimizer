/**
 * @file    : ActionCoordinator.cs
 * @author  : rudals252
 * @brief   : 코드 등록 조치의 세션 귀속·5분 만료·일회성 소비·실제 실행 및 늦은 종료 결과 보존
 */
using System.Collections.Concurrent;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions;

/// <summary>검사·사양과 같은 관문을 사용하는 조치 서비스입니다. 기본 등록 조치는 없습니다.</summary>
public sealed class ActionCoordinator
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly IOperationCoordinator _operations;
    private readonly Func<ActionSession> _session;
    private readonly TimeProvider _time;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _timeout;
    private readonly IReadOnlyDictionary<ActionId, IActionAdapter> _adapters;
    private readonly ConcurrentDictionary<Guid, (ActionPlan Plan, long Created)> _plans = new();
    private readonly ConcurrentDictionary<Guid, ActionResult> _results = new();

    /// <summary>실행 관문·현재 세션 공급자·코드 어댑터를 연결합니다. JSON으로 실행기를 등록하지 않습니다.</summary>
    public ActionCoordinator(IOperationCoordinator operations, Func<ActionSession> session, IEnumerable<IActionAdapter> adapters, TimeProvider? time = null, IAppLogger? logger = null, TimeSpan? timeout = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _adapters = adapters.ToDictionary(a => a.Definition.Id);
        foreach (var definition in _adapters.Values.Select(a => a.Definition))
        {
            if (!Enum.IsDefined(definition.Id) || !Enum.IsDefined(definition.Scope)
                || (definition.Id is ActionId.Startup or ActionId.Power or ActionId.Display or ActionId.UserFiles or ActionId.AppFiles or ActionId.OfficialCache or ActionId.SteamShaderCache && definition.Scope != ActionScope.CurrentUser)
                || (definition.Id is ActionId.MachineStartup or ActionId.UpdateServices or ActionId.WindowsUpdateCache && definition.Scope != ActionScope.System))
            { throw new ArgumentException("조치 정의의 사용자 범위가 올바르지 않습니다.", nameof(adapters)); }
        }
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullAppLogger.Instance;
        _timeout = timeout ?? TimeSpan.FromMinutes(2);
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromDays(1)) { throw new ArgumentOutOfRangeException(nameof(timeout)); }
    }
    /// <summary>진행/최종 결과를 조회합니다. 창이 닫혀도 이 서비스의 기록은 보존됩니다.</summary>
    public ActionResult? GetResult(Guid planId) => _results.GetValueOrDefault(planId);

    /// <summary>검증한 미리보기만 발급합니다. 복구 계획도 같은 관문과 세션 규칙을 따릅니다.</summary>
    public async Task<ActionPreparation> PrepareAsync(ActionId id, ActionTarget target, bool restore, CancellationToken ct)
    {
        using var lease = _operations.TryAcquire(OperationKind.Prepare);
        if (lease is null) { return new(null, "Busy"); }
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!_adapters.TryGetValue(id, out var adapter) || !Matches(id, target) || (target is ActionTarget.Restore && !restore) || (restore && !adapter.Definition.SupportsRestore)) { return new(null, "Unsupported"); }
            var session = _session();
            if (!Allowed(session, adapter.Definition)) { return new(null, "ScopeExcluded"); }
            foreach (var entry in _plans.Where(p => Expired(p.Value.Created))) { _plans.TryRemove(entry.Key, out _); }
            var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(_timeout);
            var token = deadline.Token;
            var running = Task.Run(async () =>
            {
                try { return await adapter.PrepareAsync(target, restore, token).ConfigureAwait(false); }
                finally { await ObserveDrainAsync(adapter).ConfigureAwait(false); }
            }, CancellationToken.None);
            lease.Track(running);
            var preview = await WaitOwnedAsync(running, deadline, ct).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (preview is null || !Matches(id, preview.Target)) { return new(null, "Blocked"); }
            if (_session() != session) { return new(null, "SessionChanged"); }
            ct.ThrowIfCancellationRequested();
            var plan = new ActionPlan(Guid.NewGuid(), adapter.Definition, preview, session, _time.GetUtcNow() + Lifetime, restore);
            _plans[plan.Id] = (plan, _time.GetTimestamp());
            return new(plan, null);
        }
        catch (OperationCanceledException) { return new(null, ct.IsCancellationRequested ? "Cancelled" : "TimedOut"); }
        catch (TimeoutException) { return new(null, "TimedOut"); }
        catch (ActionUnavailableException ex) { return new(null, ex.Code); }
        catch (Exception ex) { LogFailure("PrepareFailed", ex); return new(null, "PreparationFailed"); }
    }

    /// <summary>서비스가 보존한 원본 계획 ID만 소비합니다. 취소 뒤에도 실제 작업이 끝나야 관문이 풀립니다.</summary>
    public async Task<ActionResult> ExecuteAsync(Guid planId, CancellationToken ct)
    {
        if (!_plans.TryGetValue(planId, out var pending)) { return new(planId, false, false, "PlanExpired"); }
        using var lease = _operations.TryAcquire(pending.Plan.IsRestore ? OperationKind.Restore : OperationKind.Apply);
        if (lease is null) { return new(planId, false, false, "Busy"); }
        if (!_plans.TryRemove(planId, out var entry)) { return new(planId, false, false, "PlanExpired"); }
        var plan = entry.Plan;
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_timeout);
        var token = deadline.Token;
        var execution = new Execution(lease, () => Validate(entry.Created, plan, token),
            () => _results[planId] = new(planId, true, false, "Running"));
        _results[planId] = new(planId, false, false, "Validating");
        async Task<ActionResult> Run()
        {
            ActionResult result;
            try
            {
                Validate(entry.Created, plan, token);
                result = await _adapters[plan.Definition.Id].ExecuteAsync(plan, execution, token).ConfigureAwait(false);
                // 어댑터가 임의 결과 ID/Started 값을 반환해도 서비스의 시작 기록을 기준으로 삼는다.
                if (result.Succeeded && !execution.Started) { result = result with { Code = "StartNotRecorded" }; }
                result = result with { PlanId = planId, Started = execution.Started, Succeeded = execution.Started && result.Succeeded };
            }
            catch (ActionRejected ex) { result = new(planId, execution.Started, false, ex.Code); }
            catch (ActionUnavailableException ex) { result = new(planId, execution.Started, false, ex.Code); }
            catch (OperationCanceledException) { result = new(planId, execution.Started, false, ct.IsCancellationRequested ? "Cancelled" : "TimedOut"); }
            catch (Exception ex) { LogFailure("ExecuteFailed", ex); result = new(planId, execution.Started, false, "Failed"); }
            _results[planId] = result with { Succeeded = false, Code = result.Succeeded ? "Draining" : result.Code };
            await ObserveDrainAsync(_adapters[plan.Definition.Id]).ConfigureAwait(false);
            _results[planId] = result;
            return result;
        }
        var running = Task.Run(Run, CancellationToken.None);
        lease.Track(running);
        try { return await WaitOwnedAsync(running, deadline, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        { return GetResult(planId)! with { Succeeded = false, Code = running.IsCompleted ? ct.IsCancellationRequested ? "Cancelled" : "TimedOut" : "Draining" }; }
    }

    private async Task<T> WaitOwnedAsync<T>(Task<T> running, CancellationTokenSource deadline, CancellationToken ct)
    {
        try { return await running.WaitAsync(_timeout, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            try { deadline.Cancel(); } catch (AggregateException error) { LogFailure("CancelCallbackFailed", error); }
            throw;
        }
        finally
        {
            // 취소를 무시하는 실행도 토큰 등록을 사용할 수 있도록 실제 완료 때 소스를 해제한다.
            _ = running.ContinueWith(_ => deadline.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private async Task ObserveDrainAsync(IActionAdapter adapter)
    {
        var logged = false;
        while (true)
        {
            try { await adapter.WaitForDrainAsync().ConfigureAwait(false); return; }
            catch (Exception ex)
            {
                // 종료 확인 실패는 종료 증명이 아니다. 관문을 유지하며 다시 관측한다.
                if (!logged) { LogFailure("DrainObservation", ex); logged = true; }
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        }
    }

    private void LogFailure(string stage, Exception ex)
    {
        try { _logger.Warn(nameof(ActionCoordinator), $"{stage} type={ex.GetType().Name}"); }
        catch { /* 기록 장치 실패가 실행 결과·소유권을 잃게 만들지 않는다. */ }
    }
    private void Validate(long created, ActionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Expired(created)) { throw new ActionRejected("PlanExpired"); }
        var current = _session();
        if (current != plan.Session) { throw new ActionRejected("SessionChanged"); }
        if (!Allowed(current, plan.Definition)) { throw new ActionRejected("ScopeExcluded"); }
    }
    private bool Expired(long created) => _time.GetElapsedTime(created) >= Lifetime;
    private static bool Allowed(ActionSession session, ActionDefinition definition) => session.IsKnown
        && (definition.Scope == ActionScope.System || session.Scope == ActionUserScope.Full);
    private static bool Matches(ActionId id, ActionTarget target) => (id, target) switch
    {
        (_, ActionTarget.Restore t) => t.RecordId != Guid.Empty,
        (ActionId.UserFiles or ActionId.SystemFiles or ActionId.AppFiles or ActionId.WindowsUpdateCache or ActionId.SteamShaderCache, ActionTarget.Files) => true,
        (ActionId.Startup or ActionId.MachineStartup, ActionTarget.Startup) => true,
        (ActionId.Power, ActionTarget.Power) => true,
        (ActionId.Display, ActionTarget.Display) => true,
        (ActionId.OfficialCache, ActionTarget.OfficialTool t) => Enum.IsDefined(t.Tool),
        (ActionId.DeliveryOptimization, ActionTarget.DeliveryCache) => true,
        _ => false,
    };
    private sealed class ActionRejected(string code) : Exception { internal string Code { get; } = code; }
    private sealed class Execution(IOperationLease lease, Action validate, Action onStarted) : IActionExecution
    {
        private int _started;
        internal bool Started => Volatile.Read(ref _started) != 0;
        public void MarkStarted()
        {
            validate();
            RecordStarted();
        }
        public bool TryStartProcess(Func<bool> start)
        {
            validate();
            if (Started) { throw new InvalidOperationException("AlreadyStarted"); }
            if (!start()) { return false; }
            RecordStarted();
            return true;
        }
        private void RecordStarted()
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) { throw new InvalidOperationException("조치는 한 번만 시작할 수 있습니다."); }
            onStarted();
        }
        public void Track(Task task) => lease.Track(task);
    }
}
