/**
 * @file    : ActionWorkflow.cs
 * @author  : rudals252
 * @brief   : 공통 UI와 코드 등록 조치/복구 조율기 연결, 서비스가 발급한 계획만 실행
 */
using System.Collections.Concurrent;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.Services;

/// <summary>화면의 실행 경계입니다. UI가 네이티브 실행기를 직접 호출하지 않습니다.</summary>
public interface IActionWorkflow
{
    /// <summary>검사·사양·변경·복구가 공유하는 실제 작업 관문입니다.</summary>
    IOperationCoordinator Operations { get; }
    /// <summary>코드 등록과 현재 세션 범위가 허용하는지 확인합니다.</summary>
    bool Supports(ActionId id, bool restore);
    /// <summary>서비스가 보관하는 일회성 계획을 준비합니다.</summary>
    Task<ActionPreparation> PrepareAsync(ActionId id, ActionTarget target, bool restore, CancellationToken ct);
    /// <summary>서비스에 보관된 계획 ID만 실행합니다.</summary>
    Task<ActionResult> ExecuteAsync(Guid planId, CancellationToken ct);
    /// <summary>늦은 실제 종료 결과를 조회합니다.</summary>
    ActionResult? GetResult(Guid planId);
    /// <summary>복구 기록과 확인 불가 이슈를 읽습니다.</summary>
    Task<RollbackCatalog> InspectAsync(CancellationToken ct);
}

/// <summary>직접 조치와 원문 보존 조치를 한 UI로 연결합니다. 기본 등록 목록은 비어 있습니다.</summary>
public sealed class ActionWorkflow : IActionWorkflow
{
    private readonly ActionCoordinator _direct;
    private readonly RestoreCoordinator _restore;
    private readonly Dictionary<ActionId, (ActionDefinition Definition, bool Reversible)> _definitions;
    private readonly ConcurrentDictionary<Guid, bool> _plans = new();
    private readonly Func<ActionSession> _session;
    /// <summary>실제 등록 어댑터만 가용하다고 표시합니다. 성공 스텁을 등록하지 않습니다.</summary>
    public ActionWorkflow(IOperationCoordinator operations, IRollbackStore store, Func<ActionSession> session,
        IEnumerable<IActionAdapter> direct, IEnumerable<IReversibleActionAdapter> reversible)
    {
        Operations = operations;
        _session = session;
        var directAdapters = direct.ToArray();
        var reversibleAdapters = reversible.ToArray();
        _definitions = directAdapters.Select(a => (a.Definition, Reversible: false))
            .Concat(reversibleAdapters.Select(a => (a.Definition, Reversible: true))).ToDictionary(a => a.Definition.Id);
        _direct = new(operations, session, directAdapters);
        _restore = new(operations, store, session, reversibleAdapters);
    }
    /// <inheritdoc />
    public IOperationCoordinator Operations { get; }
    /// <inheritdoc />
    public bool Supports(ActionId id, bool restore)
    {
        var session = _session();
        return session.IsKnown && _definitions.TryGetValue(id, out var definition) && (!restore || definition.Reversible)
            && (definition.Definition.Scope == ActionScope.System || session.Scope == ActionUserScope.Full);
    }
    /// <inheritdoc />
    public async Task<ActionPreparation> PrepareAsync(ActionId id, ActionTarget target, bool restore, CancellationToken ct)
    {
        if (!Supports(id, restore)) { return new(null, "Unsupported"); }
        var reversible = _definitions[id].Reversible;
        var prepared = reversible
            ? restore && target is ActionTarget.Restore record
                ? await _restore.PrepareRestoreAsync(id, record.RecordId, ct).ConfigureAwait(false)
                : !restore ? await _restore.PrepareApplyAsync(id, target, ct).ConfigureAwait(false) : new(null, "Unsupported")
            : await _direct.PrepareAsync(id, target, false, ct).ConfigureAwait(false);
        if (prepared.Plan is { } plan) { _plans[plan.Id] = reversible; }
        return prepared;
    }
    /// <inheritdoc />
    public Task<ActionResult> ExecuteAsync(Guid planId, CancellationToken ct) => _plans.TryGetValue(planId, out var reversible)
        ? reversible ? _restore.ExecuteAsync(planId, ct) : _direct.ExecuteAsync(planId, ct)
        : Task.FromResult(new ActionResult(planId, false, false, "PlanExpired"));
    /// <inheritdoc />
    public ActionResult? GetResult(Guid planId) => _plans.TryGetValue(planId, out var reversible)
        ? reversible ? _restore.GetResult(planId) : _direct.GetResult(planId) : null;
    /// <inheritdoc />
    public Task<RollbackCatalog> InspectAsync(CancellationToken ct) => _restore.InspectAsync(ct);
}
