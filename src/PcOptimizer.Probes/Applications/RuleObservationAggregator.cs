/**
 * @file    : RuleObservationAggregator.cs
 * @author  : rudals252
 * @brief   : 관측 대상별 결과를 규칙별 관측(상태·소유 대상 크기 합·부분 여부·보호/병합/제외 위치 수·같은 위치를 가리킨 규칙·앱 설정 경로 출처)으로 모으는 집계기
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 규칙 하나의 관측 결과입니다.
/// </summary>
/// <param name="Rule">규칙.</param>
/// <param name="Metadata">검토 메타데이터.</param>
/// <param name="State">상태(<c>AppCacheProbeContract.RULE_STATE_*</c>).</param>
/// <param name="Bytes">관측 논리 크기(관측·부분일 때만).</param>
/// <param name="FileCount">관측 파일 수.</param>
/// <param name="Partial">부분 관측 여부.</param>
/// <param name="DuplicatesPossible">중복 가능 여부.</param>
/// <param name="Paths">소유 대상 폴더.</param>
/// <param name="ProtectedTargets">보호 위치 수.</param>
/// <param name="ExcludedTargets">규칙 제외 위치 수.</param>
/// <param name="MergedTargets">다른 규칙에서 센 위치 수.</param>
/// <param name="SharedWith">같은 위치를 가리킨 다른 규칙 ID.</param>
/// <param name="Skips">건너뜀 합.</param>
/// <param name="ConfigSource">앱 설정 경로 출처(<c>CONFIG_SOURCE_*</c>).</param>
internal sealed record RuleObservation(
    CleaningRule Rule,
    RuleMetadata? Metadata,
    string State,
    long? Bytes,
    long FileCount,
    bool Partial,
    bool DuplicatesPossible,
    IReadOnlyList<string> Paths,
    int ProtectedTargets,
    int ExcludedTargets,
    int MergedTargets,
    IReadOnlyList<string> SharedWith,
    SkipCounts Skips,
    string ConfigSource);

/// <summary>
/// 규칙별 관측 집계기입니다. 한 파일은 소유 대상 하나에서만 셌으므로 규칙 크기의 합이 중복 없이 맞습니다.
/// </summary>
internal static class RuleObservationAggregator
{
    /// <summary>
    /// 규칙별 관측을 만듭니다(크기 내림차순, 같으면 이름순).
    /// </summary>
    /// <param name="rules">경로를 펼친 규칙(런타임 미지원·폴더 이름 규칙 제외).</param>
    /// <param name="plan">관측 계획.</param>
    /// <param name="results">대상별 결과.</param>
    /// <returns>규칙별 관측.</returns>
    public static IReadOnlyList<RuleObservation> Aggregate(IEnumerable<RuleTargets> rules, ObservationPlan plan, IReadOnlyList<TargetMeasurement> results)
    {
        var byOrder = results.ToDictionary(result => result.Order);
        var ownerByOrder = plan.Targets.ToDictionary(target => target.Order, target => target.OwnerRuleId);
        return [.. rules
            .Select(rule => Aggregate(rule, plan, byOrder, ownerByOrder))
            .OrderByDescending(observation => observation.Bytes ?? -1)
            .ThenBy(observation => observation.Rule.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// 규칙 하나를 집계한다.
    /// </summary>
    private static RuleObservation Aggregate(
        RuleTargets rule, ObservationPlan plan, Dictionary<int, TargetMeasurement> byOrder, Dictionary<int, string> ownerByOrder)
    {
        var id = rule.Rule.Id;
        var owned = plan.Targets.Where(target => target.OwnerRuleId == id).ToList();
        var measured = owned.Select(target => byOrder[target.Order]).ToList();
        var dropped = plan.Dropped.Where(item => item.RuleId == id).ToList();
        var protectedTargets = dropped.Count(item => item.Reason == DroppedCandidateReason.Protected) + rule.ProtectedWildcards
            + measured.Count(result => result.State == TargetState.Protected);
        var excluded = dropped.Count(item => item.Reason == DroppedCandidateReason.RuleExcluded);
        var merged = dropped.Where(item => item.Reason == DroppedCandidateReason.Merged).ToList();
        var sharedWith = owned.SelectMany(target => target.ContributingRuleIds)
            .Concat(merged.Select(item => item.MergedIntoOrder is { } order ? ownerByOrder[order] : id))
            .Where(other => other != id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var skips = measured.Aggregate(default(SkipCounts), (sum, result) => sum.Add(result.Skips));
        for (var index = 0; index < rule.IncompleteExpansions; index++)
        {
            skips = skips.Increment(ScanSkipReason.AccessDenied);
        }

        var observed = measured.Where(result => result.IsObserved).ToList();
        var partial = observed.Count > 0
            && (rule.IncompleteExpansions > 0 || measured.Any(result => result.State is TargetState.Partial or TargetState.AccessDenied or TargetState.TimedOut or TargetState.Error));
        var files = observed.Sum(result => result.FileCount);
        var state = observed.Count == 0 ? StateWithoutObservation(measured, protectedTargets, merged.Count, excluded)
            : partial ? AppCacheProbeContract.RULE_STATE_PARTIAL
            : files == 0 ? AppCacheProbeContract.RULE_STATE_ABSENT
            : AppCacheProbeContract.RULE_STATE_OBSERVED;
        var configSource = rule.Metadata?.ConfigReader is null
            ? AppCacheProbeContract.CONFIG_SOURCE_NOT_APPLICABLE
            : rule.Config?.State == AppCacheProbeContract.CONFIG_STATE_APPLIED ? AppCacheProbeContract.CONFIG_SOURCE_USER : AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY;

        return new RuleObservation(
            rule.Rule,
            rule.Metadata,
            state,
            observed.Count > 0 ? observed.Sum(result => result.Bytes) : null,
            files,
            partial,
            observed.Any(result => result.DuplicatesPossible),
            [.. owned.Select(target => target.Directory)],
            protectedTargets,
            excluded,
            merged.Count,
            sharedWith.AsReadOnly(),
            skips,
            configSource);
    }

    /// <summary>
    /// 관측한 파일이 없을 때의 상태.
    /// </summary>
    private static string StateWithoutObservation(List<TargetMeasurement> measured, int protectedTargets, int merged, int excluded)
    {
        if (measured.Any(result => result.State == TargetState.AccessDenied))
        {
            return AppCacheProbeContract.RULE_STATE_ACCESS_DENIED;
        }

        if (measured.Any(result => result.State == TargetState.TimedOut))
        {
            return AppCacheProbeContract.RULE_STATE_TIMED_OUT;
        }

        if (measured.Any(result => result.State == TargetState.Error))
        {
            return AppCacheProbeContract.RULE_STATE_NOT_OBSERVED;
        }

        // 일부 위치를 다른 규칙에서 셌으면 병합으로 알린다(보호·없음 위치 수는 상세에 남음).
        if (merged > 0)
        {
            return AppCacheProbeContract.RULE_STATE_MERGED;
        }

        if (protectedTargets > 0)
        {
            return AppCacheProbeContract.RULE_STATE_PROTECTED;
        }

        if (measured.Count > 0)
        {
            return AppCacheProbeContract.RULE_STATE_ABSENT;
        }

        return excluded > 0 ? AppCacheProbeContract.RULE_STATE_EXCLUDED : AppCacheProbeContract.RULE_STATE_ABSENT;
    }
}
