/**
 * @file    : ObservationPlanner.cs
 * @author  : rudals252
 * @brief   : 펼친 관측 후보에 보호 정책·규칙 제외·우선순위를 적용해 관측 대상을 정하는 순수 계획기(보호 루트 제외, 겹치는 위치 병합, 하위 전체 대상 분리, 파일 단위 중복 확인 필요 표시)
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 관측 계획기입니다(스펙 §5.1 "보호 정책 > 규칙의 제외 경로 > 검토된 supplement/앱 설정 경로 > 기본 커뮤니티 경로", "겹치는 파일은 합계에서 한 번만 세고 출처 규칙 목록을 보존").
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>보호 루트와 같거나 그 아래인 후보는 관측하지 않고 보호 제외로 남깁니다.</item>
/// <item>모든 파일을 빼는 PATH 제외 안의 후보는 규칙 제외로 남깁니다. 제외 조건은 어느 규칙이 적었든 모든 대상에 적용합니다(넓은 탐색 금지).</item>
/// <item>검토 경로를 커뮤니티 경로보다 먼저, 같은 우선순위는 입력 순서로 처리합니다.</item>
/// <item>앞선 전체 대상(모든 파일·하위 포함·제외 없음)과 같거나 그 아래인 후보, 앞선 대상과 폴더·패턴·하위 포함이 같은 후보는 병합합니다.</item>
/// <item>남은 대상은 자기 아래의 앞선 전체 대상을 하위 제외로 가지며, 앞선 필터 대상과 파일이 겹칠 수 있으면 그 사실을 표시합니다.</item>
/// </list>
/// 일부 파일이 맞았다고 상위 폴더 전체를 대상으로 넓히지 않습니다.
/// </remarks>
public static class ObservationPlanner
{
    /// <summary>
    /// 관측 계획을 만듭니다.
    /// </summary>
    /// <param name="candidates">관측 후보(입력 순서가 같은 우선순위 안의 순서).</param>
    /// <param name="exclusions">탐지된 지원 규칙의 제외 조건(모든 대상에 적용).</param>
    /// <param name="protectedRoots">보호 루트 정규화 경로.</param>
    /// <returns>계획.</returns>
    public static ObservationPlan Plan(
        IReadOnlyList<ObservationCandidate> candidates,
        IReadOnlyList<ExclusionSpec> exclusions,
        IReadOnlyList<string> protectedRoots)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(exclusions);
        ArgumentNullException.ThrowIfNull(protectedRoots);

        var dropped = new List<DroppedCandidate>();
        var ordered = new List<ObservationCandidate>();
        foreach (var candidate in candidates)
        {
            var directory = PathScope.Normalize(candidate.Directory);
            if (protectedRoots.Any(root => PathScope.IsSameOrUnder(directory, root)))
            {
                dropped.Add(new DroppedCandidate(candidate.RuleId, directory, candidate.Source, DroppedCandidateReason.Protected, null));
            }
            else if (ExclusionMatcher.ExcludesSubtree(exclusions, directory))
            {
                dropped.Add(new DroppedCandidate(candidate.RuleId, directory, candidate.Source, DroppedCandidateReason.RuleExcluded, null));
            }
            else
            {
                ordered.Add(candidate with { Directory = directory });
            }
        }

        var targets = new List<PlannedTarget>();
        foreach (var candidate in ordered.OrderBy(candidate => candidate.Precedence))
        {
            var owner = targets.FirstOrDefault(target => Covers(target, candidate));
            if (owner is not null)
            {
                owner.AddContributor(candidate.RuleId);
                dropped.Add(new DroppedCandidate(candidate.RuleId, candidate.Directory, candidate.Source, DroppedCandidateReason.Merged, owner.Order));
                continue;
            }

            targets.Add(CreateTarget(candidate, targets, exclusions));
        }

        return new ObservationPlan([.. targets.Select(target => target.ToTarget())], dropped.AsReadOnly());
    }

    /// <summary>
    /// 앞선 대상이 후보의 파일을 모두 이미 세는지 확인한다(전체 대상 아래이거나 범위가 똑같음).
    /// </summary>
    private static bool Covers(PlannedTarget target, ObservationCandidate candidate)
    {
        if (target.IsWhole && PathScope.IsSameOrUnder(candidate.Directory, target.Directory))
        {
            return true;
        }

        return string.Equals(target.Directory, candidate.Directory, StringComparison.OrdinalIgnoreCase)
            && target.Recurse == candidate.Recurse
            && target.Patterns.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(candidate.Patterns);
    }

    /// <summary>
    /// 새 대상을 만든다(적용할 제외, 하위 제외, 앞선 필터 대상과의 겹침).
    /// </summary>
    private static PlannedTarget CreateTarget(ObservationCandidate candidate, List<PlannedTarget> earlier, IReadOnlyList<ExclusionSpec> exclusions)
    {
        var applicable = exclusions.Where(exclusion => ExclusionMatcher.AppliesTo(exclusion, candidate.Directory, candidate.Recurse)).ToList();
        var isWhole = candidate.Recurse && RulePatternMatcher.IsAllFiles(candidate.Patterns) && applicable.Count == 0;
        var carveOuts = candidate.Recurse
            ? earlier.Where(target => target.IsWhole && PathScope.IsStrictlyUnder(target.Directory, candidate.Directory)).Select(target => target.Directory).ToList()
            : [];
        var overlaps = earlier.Any(target => !target.IsWhole && Overlaps(target, candidate));
        return new PlannedTarget(earlier.Count, candidate, isWhole, applicable, carveOuts, overlaps);
    }

    /// <summary>
    /// 앞선 필터 대상과 후보가 같은 파일을 볼 수 있는지 확인한다.
    /// </summary>
    private static bool Overlaps(PlannedTarget earlier, ObservationCandidate candidate)
    {
        if (string.Equals(earlier.Directory, candidate.Directory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (candidate.Recurse && PathScope.IsStrictlyUnder(earlier.Directory, candidate.Directory))
            || (earlier.Recurse && PathScope.IsStrictlyUnder(candidate.Directory, earlier.Directory));
    }

    /// <summary>
    /// 계획 중인 대상(기여 규칙을 모은다).
    /// </summary>
    private sealed class PlannedTarget(
        int order, ObservationCandidate candidate, bool isWhole, List<ExclusionSpec> exclusions, List<string> carveOuts, bool overlapsEarlierFiltered)
    {
        private readonly List<string> _contributors = [candidate.RuleId];

        /// <summary>처리 순서.</summary>
        public int Order { get; } = order;

        /// <summary>폴더.</summary>
        public string Directory => candidate.Directory;

        /// <summary>패턴.</summary>
        public IReadOnlyList<string> Patterns => candidate.Patterns;

        /// <summary>하위 포함 여부.</summary>
        public bool Recurse => candidate.Recurse;

        /// <summary>전체 대상 여부.</summary>
        public bool IsWhole { get; } = isWhole;

        /// <summary>
        /// 기여 규칙을 더한다(중복 없음).
        /// </summary>
        public void AddContributor(string ruleId)
        {
            if (!_contributors.Contains(ruleId, StringComparer.Ordinal))
            {
                _contributors.Add(ruleId);
            }
        }

        /// <summary>
        /// 결과 레코드로 바꾼다.
        /// </summary>
        public ObservationTarget ToTarget()
        {
            return new ObservationTarget(
                Order,
                candidate.Directory,
                candidate.Patterns,
                candidate.Recurse,
                candidate.RuleId,
                candidate.Precedence,
                candidate.Source,
                _contributors.AsReadOnly(),
                IsWhole,
                exclusions.AsReadOnly(),
                carveOuts.AsReadOnly(),
                overlapsEarlierFiltered);
        }
    }
}
