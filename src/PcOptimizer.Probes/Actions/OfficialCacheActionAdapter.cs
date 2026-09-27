/**
 * @file    : OfficialCacheActionAdapter.cs
 * @author  : rudals252
 * @brief   : 기존 공식 도구 backend를 중복 계획·관문 없이 공통 조율기에 연결
 */
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions;

/// <summary>Start 성공을 공통 실행 기록에 연결하는 공식 도구 경계입니다.</summary>
public interface ICacheToolActionBackend : ICacheToolBackend
{
    /// <summary>프로세스 시작 직전 재검증과 시작 직후 기록을 실행 컨텍스트로 수행합니다.</summary>
    Task<CacheToolExecution> ClearAsync(CacheToolLocation location, IActionExecution execution, CancellationToken ct);
}

/// <summary>npm·pip·NuGet HTTP만 지원합니다. 직접 파일 삭제 계획과 구별합니다.</summary>
public sealed class OfficialCacheActionAdapter(ICacheToolActionBackend backend, Func<ActionSession> session) : IActionAdapter
{
    private readonly ConditionalWeakTable<ActionPreview, CacheToolLocation> _locations = new();
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.OfficialCache, ActionScope.CurrentUser);
    /// <inheritdoc />
    public async Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
    {
        var owner = session();
        if (restore || !owner.IsKnown || owner.Scope != ActionUserScope.Full || target is not ActionTarget.OfficialTool selected || !Enum.IsDefined(selected.Tool)) { return null; }
        try
        {
            var location = await backend.LocateAsync(ToBackend(selected.Tool), ct).ConfigureAwait(false)
                ?? throw new ActionUnavailableException("ToolUnavailable");
            var inspection = await backend.InspectAsync(location, ct).ConfigureAwait(false);
            if (!inspection.Allowed) { throw new ActionUnavailableException(inspection.Reason ?? "Blocked"); }
            var preview = new ActionPreview(target, $"{selected.Tool} 공식 도구로 캐시를 정리합니다.",
                "캐시를 다시 내려받아야 할 수 있습니다. 되돌릴 수 없습니다. 실행 시 공식 도구가 처리하는 캐시 전체이며 미리보기의 개별 파일 목록으로 한정되지 않습니다.",
                new(location.CachePath, "실행 직전 경로·실행 파일 지문·보호 정책을 다시 확인합니다. NuGet은 HTTP 캐시만 포함합니다.", inspection.Bytes, false));
            _locations.Add(preview, location); return preview;
        }
        catch (CacheToolUnavailableException ex) { throw new ActionUnavailableException(ex.Code); }
    }
    /// <inheritdoc />
    public async Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
    {
        if (!_locations.TryGetValue(plan.Preview, out var location)) { return new(plan.Id, false, false, "PlanExpired"); }
        _locations.Remove(plan.Preview);
        if (session() != plan.Session) { return new(plan.Id, false, false, "SessionChanged"); }
        try
        {
            if (await backend.LocateAsync(location.Tool, ct).ConfigureAwait(false) != location) { return new(plan.Id, false, false, "TargetChanged"); }
            var before = await backend.InspectAsync(location, ct).ConfigureAwait(false);
            if (!before.Allowed) { return new(plan.Id, false, false, before.Reason ?? "Blocked"); }
            var result = await backend.ClearAsync(location, execution, ct).ConfigureAwait(false);
            if (!result.Started) { return new(plan.Id, false, false, result.Code); }
            await backend.WaitForDrainAsync().ConfigureAwait(false);
            CacheInspection? after = null;
            try { after = await backend.InspectAsync(location, ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* 시작 사실을 후속 관측 실패로 잃지 않는다. */ }
            var observed = after?.Allowed == true;
            return new(plan.Id, true, result.Succeeded && observed, observed ? result.Code : "ObservationFailed",
                new(null, BeforeLogicalBytes: before.Bytes, AfterLogicalBytes: observed ? after!.Bytes : null));
        }
        catch (CacheToolUnavailableException ex) { throw new ActionUnavailableException(ex.Code); }
    }
    private static CacheTool ToBackend(OfficialCacheTool tool) => tool switch
    {
        OfficialCacheTool.Npm => CacheTool.Npm, OfficialCacheTool.Pip => CacheTool.Pip,
        OfficialCacheTool.NuGetHttp => CacheTool.NuGetHttp, _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };
    /// <inheritdoc />
    public Task WaitForDrainAsync() => backend.WaitForDrainAsync();
}
