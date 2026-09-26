/**
 * @file    : LocationObserver.cs
 * @author  : rudals252
 * @brief   : 표준 임시 위치의 관측 상태·크기를 순회 결과(중첩 위치는 바깥 루트의 디렉터리별 합계, 이름 패턴 위치는 패턴 집계)와 존재 확인으로 정하는 도우미
 */

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 표준 임시 위치 관측 도우미입니다. 순회하지 못한 위치는 존재 확인으로 없음·접근 거부·미관측을 구분하며 0바이트로 바꾸지 않습니다.
/// </summary>
internal static class LocationObserver
{
    /// <summary>
    /// 계획의 임시 위치마다 관측 결과를 만든다.
    /// </summary>
    public static List<LocationObservation> Observe(
        ScanPlan plan, TraversalResult traversal, ResolvedProtection protection, IDirectoryEntrySource source)
    {
        return [.. plan.Locations.Select(location => Observe(location, traversal, protection, source))];
    }

    /// <summary>
    /// 위치 하나를 관측한다.
    /// </summary>
    private static LocationObservation Observe(
        ScanLocationPlan location, TraversalResult traversal, ResolvedProtection protection, IDirectoryEntrySource source)
    {
        if (location.Path is not { } path)
        {
            return Without(location, LocationState.Unresolved);
        }

        if (protection.IsProtected(path))
        {
            return Without(location, LocationState.Protected);
        }

        if (location.FilePatterns.Count > 0)
        {
            var tally = traversal.Patterns.FirstOrDefault(pattern => pattern.Id == location.Id);
            if (tally is { Enumerated: true })
            {
                if (tally.Failure == ScanSkipReason.AccessDenied && tally.FileCount == 0)
                {
                    return Without(location, LocationState.AccessDenied);
                }

                var state = tally.Failure is null ? LocationState.Observed : LocationState.Partial;
                return new LocationObservation(location.Id, path, location.IsSystem, state, tally.Bytes, tally.FileCount, tally.DuplicatesPossible);
            }

            return Without(location, FromPresence(source.ProbeRoot(path)));
        }

        if (traversal.TryGetDirectoryTotals(path, out var totals))
        {
            if (traversal.GetEnumerationFailure(path) == ScanSkipReason.AccessDenied && totals.FileCount == 0 && totals.DirectoryCount == 0)
            {
                return Without(location, LocationState.AccessDenied);
            }

            var state = totals.IsPartial ? LocationState.Partial : LocationState.Observed;
            return new LocationObservation(location.Id, path, location.IsSystem, state, totals.Bytes, totals.FileCount, totals.DuplicatesPossible);
        }

        return Without(location, FromPresence(source.ProbeRoot(path)));
    }

    /// <summary>
    /// 순회하지 못한 위치의 상태를 존재 확인 결과로 정한다.
    /// </summary>
    private static LocationState FromPresence(RootPresence presence)
    {
        return presence switch
        {
            RootPresence.Missing or RootPresence.NotDirectory => LocationState.Absent,
            RootPresence.AccessDenied => LocationState.AccessDenied,
            _ => LocationState.NotObserved,
        };
    }

    /// <summary>
    /// 크기 없는 관측 결과를 만든다.
    /// </summary>
    private static LocationObservation Without(ScanLocationPlan location, LocationState state)
    {
        return new LocationObservation(location.Id, location.Path, location.IsSystem, state, null, null, false);
    }
}
