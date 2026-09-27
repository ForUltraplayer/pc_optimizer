/**
 * @file    : DeliveryOptimizationAdapter.cs
 * @author  : rudals252
 * @brief   : 미리보기에서 고정한 비고정 캐시 ID만 Windows 제공자로 정리하고 결과 재조회
 */
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

/// <summary>서비스 중지·폴더 삭제 없이 Windows가 관리하는 배달 최적화 캐시만 처리합니다.</summary>
public sealed class DeliveryOptimizationAdapter : IActionAdapter
{
    private readonly IDeliveryCachePlatform _platform;
    private readonly Func<ActionSession> _session;
    private readonly ConditionalWeakTable<ActionPreview, Snapshot> _snapshots = new();
    private sealed record Snapshot(ActionSession Session, IReadOnlyList<DeliveryCacheEntry> Files);
    /// <summary>앱과 같은 실행 범위 공급자를 사용합니다.</summary>
    public DeliveryOptimizationAdapter(Func<ActionSession> session) : this(new DeliveryCachePlatform(), session) { }
    internal DeliveryOptimizationAdapter(IDeliveryCachePlatform platform, Func<ActionSession> session) { _platform = platform; _session = session; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.DeliveryOptimization, ActionScope.System);
    private IReadOnlyList<DeliveryCacheEntry> Read(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var files = _platform.Read(ct);
        if (files.Count > 1024 || files.Any(f => string.IsNullOrWhiteSpace(f.Id) || f.Id.Length > 512 || f.Id.Any(char.IsControl) || f.Bytes < 0 || f.Status > 3)
            || files.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != files.Count) { throw new ActionUnavailableException("DeliveryStateUnavailable"); }
        return files;
    }
    private static void CheckIdle(IReadOnlyList<DeliveryCacheEntry> files)
    {
        if (files.Any(f => f.Status is 0 or 3)) { throw new ActionUnavailableException("DeliveryBusy"); }
    }
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
    {
        var session = _session();
        if (restore || target is not ActionTarget.DeliveryCache || !session.IsKnown) { return Task.FromResult<ActionPreview?>(null); }
        var all = Read(ct); CheckIdle(all);
        var files = all.Where(f => f.Status == 2 && !f.Pinned && f.Bytes > 0).ToArray();
        if (files.Length == 0) { throw new ActionUnavailableException("NoEligibleFiles"); }
        var preview = new ActionPreview(target, $"Windows 배달 최적화의 캐시 {files.Length:N0}개를 정리합니다.",
            "이 PC의 모든 사용자에게 해당하는 Windows·Store 다운로드 캐시입니다. 필요하면 다시 다운로드하므로 네트워크 사용량이 늘 수 있습니다. 되돌릴 수 없습니다.",
            new("Windows 배달 최적화 · 미리보기에서 확인한 캐시", "고정 보관·다운로드·일시 중지 항목을 처리하지 않습니다. 서비스는 중지하지 않으며 Windows 업데이트 설치 파일 폴더를 직접 지우지 않습니다.", files.Sum(f => f.Bytes), false));
        _snapshots.Add(preview, new(session, files));
        return Task.FromResult<ActionPreview?>(preview);
    }
    /// <inheritdoc />
    public Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
    {
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { return Task.FromResult(new ActionResult(plan.Id, false, false, "PlanExpired")); }
        _snapshots.Remove(plan.Preview);
        if (_session() != snapshot.Session || plan.Session != snapshot.Session) { return Task.FromResult(new ActionResult(plan.Id, false, false, "SessionChanged")); }
        var beforeBytes = snapshot.Files.Sum(f => f.Bytes);
        var changed = 0; var skipped = 0; var failed = 0; var started = false; long? remaining = null;
        var code = "Completed";
        try
        {
            foreach (var item in snapshot.Files)
            {
                ct.ThrowIfCancellationRequested();
                if (_session() != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
                var current = Read(ct); CheckIdle(current);
                var found = current.FirstOrDefault(f => f.Id == item.Id);
                if (found is null) { skipped++; continue; }
                if (found != item) { throw new ActionUnavailableException("TargetChanged"); }
                _platform.Delete(item.Id, () =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (_session() != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
                    var latest = Read(ct); CheckIdle(latest);
                    if (latest.FirstOrDefault(f => f.Id == item.Id) != item) { throw new ActionUnavailableException("TargetChanged"); }
                    if (!started) { execution.MarkStarted(); started = true; }
                });
                // 호출 성공만으로 삭제 완료라고 하지 않는다. 새로 생긴 캐시는 계획에 추가하지 않는다.
                var after = Read(ct);
                if (after.Any(f => f.Id == item.Id && f.Bytes > 0)) { failed++; code = "DeliveryVerificationFailed"; break; }
                changed++;
            }
            var final = Read(ct);
            remaining = final.Where(f => snapshot.Files.Any(x => x.Id == f.Id)).Sum(f => f.Bytes);
        }
        catch (OperationCanceledException) { code = "Cancelled"; }
        catch (ActionUnavailableException ex) { code = ex.Code; failed += started && changed + skipped < snapshot.Files.Count ? 1 : 0; }
        catch (Exception) { code = "DeliveryStateUnavailable"; failed += started ? 1 : 0; }
        var success = started && code == "Completed";
        if (!started && code == "Completed") { code = "NoEligibleFiles"; }
        return Task.FromResult(new ActionResult(plan.Id, started, success, code,
            new(null, changed, skipped, failed, beforeBytes, remaining)));
    }
}
