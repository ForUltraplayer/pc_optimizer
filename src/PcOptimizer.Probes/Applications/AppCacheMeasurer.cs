/**
 * @file    : AppCacheMeasurer.cs
 * @author  : rudals252
 * @brief   : 관측 계획의 대상을 우선순위 순서로 관측하는 측정기(전체 대상이 공유 스캔 루트 안이면 공유 스캔 폴더 합계에서 앞선 하위 대상 몫을 빼서 쓰고, 그 밖은 대상 열거로 파일 경로 단위 중복을 거름, 대상당 5초·전체 예산)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 앱 캐시 측정기입니다(스펙 §4 "앱 캐시는 공유 스캔 결과를 재사용", §5.1 "겹치는 파일은 합계에서 한 번만 센다").
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>공유 스캔 합계는 모든 파일·하위 포함·제외 없음이고 앞선 필터 대상과 겹치지 않는 대상에만 씁니다. 앞선 하위 전체 대상이 센 몫은 빼므로 한 파일은 한 번만 셉니다.</item>
/// <item>그 밖의 대상은 <see cref="TargetedEnumerator"/>로 열거하며, 앞선 대상이 센 파일 경로는 다시 세지 않습니다(경로 단위이므로 하드링크 중복 가능으로 표시).</item>
/// <item>전체 예산을 넘긴 뒤의 대상은 시작하지 않고 시간 초과로 남깁니다.</item>
/// </list>
/// </remarks>
public sealed class AppCacheMeasurer
{
    /// <summary>대상 하나의 대상 열거 시간 예산.</summary>
    public static readonly TimeSpan APP_CACHE_TARGET_BUDGET = TimeSpan.FromSeconds(5);

    private readonly TargetedEnumerator _enumerator;
    private readonly TimeProvider _time;

    /// <summary>
    /// 측정기를 만듭니다.
    /// </summary>
    /// <param name="source">디렉터리 항목 열거.</param>
    /// <param name="time">시간 공급자.</param>
    public AppCacheMeasurer(IDirectoryEntrySource source, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(time);
        _enumerator = new TargetedEnumerator(source, time);
        _time = time;
    }

    /// <summary>
    /// 계획의 대상을 차례로 관측합니다.
    /// </summary>
    /// <param name="plan">관측 계획.</param>
    /// <param name="shared">공유 파일 스캔 결과(유효 정책).</param>
    /// <param name="isProtected">보호 루트 확인 함수.</param>
    /// <param name="overallBudget">대상 열거 전체 예산.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>대상별 결과(계획 순서).</returns>
    public IReadOnlyList<TargetMeasurement> Measure(
        ObservationPlan plan, DirectoryScanResult shared, Func<string, bool> isProtected, TimeSpan overallBudget, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(isProtected);

        var start = _time.GetTimestamp();
        bool OverallExpired() => _time.GetElapsedTime(start) > overallBudget;
        var counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<TargetMeasurement>();
        var byDirectory = new Dictionary<string, TargetMeasurement>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in plan.Targets)
        {
            ct.ThrowIfCancellationRequested();
            TargetMeasurement result;
            if (target.IsWhole && !target.OverlapsEarlierFiltered && shared.TryGetDirectoryTotals(target.Directory, out var totals))
            {
                result = FromTotals(target, totals, byDirectory);
            }
            else if (OverallExpired())
            {
                result = new TargetMeasurement(target.Order, TargetState.TimedOut, 0, 0, default(SkipCounts).Increment(ScanSkipReason.Timeout), false, false);
            }
            else
            {
                result = _enumerator.Measure(target, isProtected, counted, APP_CACHE_TARGET_BUDGET, OverallExpired, ct);
            }

            results.Add(result);
            if (target.IsWhole)
            {
                byDirectory[target.Directory] = result;
            }
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// 공유 스캔 폴더 합계에서 앞선 하위 전체 대상이 센 몫을 뺀다.
    /// </summary>
    private static TargetMeasurement FromTotals(ObservationTarget target, DirectoryTotals totals, Dictionary<string, TargetMeasurement> earlier)
    {
        var bytes = totals.Bytes;
        var files = totals.FileCount;
        var skips = totals.Skips;
        foreach (var carveOut in target.CarveOuts)
        {
            if (earlier.TryGetValue(carveOut, out var inner))
            {
                bytes -= inner.Bytes;
                files -= inner.FileCount;
                skips = Subtract(skips, inner.Skips);
            }
        }

        var state = skips.Incomplete > 0 ? TargetState.Partial : TargetState.Observed;
        return new TargetMeasurement(target.Order, state, Math.Max(0, bytes), Math.Max(0, files), skips, totals.DuplicatesPossible, FromSharedScan: true);
    }

    /// <summary>
    /// 건너뜀 개수를 뺀다(0 아래로 내려가지 않음).
    /// </summary>
    private static SkipCounts Subtract(SkipCounts total, SkipCounts part)
    {
        return new SkipCounts(
            Math.Max(0, total.ProtectedExcluded - part.ProtectedExcluded),
            Math.Max(0, total.AccessDenied - part.AccessDenied),
            Math.Max(0, total.InUse - part.InUse),
            Math.Max(0, total.Reparse - part.Reparse),
            Math.Max(0, total.Placeholder - part.Placeholder),
            Math.Max(0, total.Timeout - part.Timeout));
    }
}
