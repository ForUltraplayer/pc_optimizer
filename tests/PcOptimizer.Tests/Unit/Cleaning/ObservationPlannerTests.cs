/**
 * @file    : ObservationPlannerTests.cs
 * @author  : rudals252
 * @brief   : 관측 계획기의 우선순위(보호 정책 > 규칙 제외 > 검토 경로 > 커뮤니티 경로), 같은·하위 위치 병합, 하위 전체 대상 분리, 필터 대상 겹침 표시, 제외 조건(FILE·PATH·와일드카드) 판정과 Win32 패턴 비교를 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Tests.Unit.Cleaning;

/// <summary>
/// <see cref="ObservationPlanner"/>, <see cref="ExclusionMatcher"/>, <see cref="RulePatternMatcher"/>를 검증합니다. 경로는 모두 가짜입니다.
/// </summary>
public sealed class ObservationPlannerTests
{
    private const string LOCAL = @"C:\Users\tester\AppData\Local";
    private const string DOCUMENTS = @"C:\Users\tester\Documents";
    private const string SUPPLEMENT = "supplement:Vendor Cache";
    private const string COMMUNITY = "winapp2:Vendor";
    private const string OTHER = "winapp2:Other";

    /// <summary>
    /// 모든 파일·하위 포함 후보.
    /// </summary>
    private static ObservationCandidate Whole(string ruleId, string directory, ObservationPrecedence precedence, TargetSource source = TargetSource.Default)
    {
        return new ObservationCandidate(ruleId, precedence, directory, ["*"], Recurse: true, source);
    }

    /// <summary>
    /// 패턴 후보.
    /// </summary>
    private static ObservationCandidate Filtered(string ruleId, string directory, bool recurse, params string[] patterns)
    {
        return new ObservationCandidate(ruleId, ObservationPrecedence.Community, directory, patterns, recurse, TargetSource.Default);
    }

    /// <summary>보호 루트 안의 후보(예: 사용자 설정 NuGet 경로가 문서 폴더)는 관측 대상이 아니라 보호 제외로 남는다. 보호가 모든 우선순위보다 먼저다.</summary>
    [Fact]
    public void 보호_루트_안의_후보는_보호_제외로_남는다()
    {
        var plan = ObservationPlanner.Plan(
            [Whole(SUPPLEMENT, DOCUMENTS + @"\nuget-packages", ObservationPrecedence.Reviewed, TargetSource.UserConfig), Whole(SUPPLEMENT, LOCAL + @"\Vendor\Cache", ObservationPrecedence.Reviewed)],
            [],
            [DOCUMENTS]);

        var target = Assert.Single(plan.Targets);
        Assert.Equal(LOCAL + @"\Vendor\Cache", target.Directory);
        var dropped = Assert.Single(plan.Dropped);
        Assert.Equal(DroppedCandidateReason.Protected, dropped.Reason);
        Assert.Equal(TargetSource.UserConfig, dropped.Source);
    }

    /// <summary>검토 경로가 커뮤니티 경로보다 먼저 처리되고, 커뮤니티의 상위 전체 대상은 검토 대상 폴더를 하위 제외로 가진다.</summary>
    [Fact]
    public void 검토_경로가_먼저이고_상위_커뮤니티_대상은_그_폴더를_빼고_센다()
    {
        var plan = ObservationPlanner.Plan(
            [Whole(COMMUNITY, LOCAL + @"\Vendor", ObservationPrecedence.Community), Whole(SUPPLEMENT, LOCAL + @"\Vendor\Cache", ObservationPrecedence.Reviewed)],
            [],
            []);

        Assert.Equal(2, plan.Targets.Count);
        Assert.Equal(SUPPLEMENT, plan.Targets[0].OwnerRuleId);
        Assert.Equal(COMMUNITY, plan.Targets[1].OwnerRuleId);
        Assert.Equal([LOCAL + @"\Vendor\Cache"], plan.Targets[1].CarveOuts);
        Assert.True(plan.Targets[1].IsWhole);
        Assert.False(plan.Targets[1].OverlapsEarlierFiltered);
    }

    /// <summary>같은 위치나 앞선 전체 대상 아래의 후보는 병합되어 한 번만 세며, 기여 규칙 목록에 남는다(입력 순서와 무관하게 검토 경로가 소유).</summary>
    [Fact]
    public void 같은_위치와_하위_위치는_병합된다()
    {
        var plan = ObservationPlanner.Plan(
            [
                Whole(COMMUNITY, LOCAL + @"\vendor\cache", ObservationPrecedence.Community),
                Filtered(OTHER, LOCAL + @"\Vendor\Cache\Sub", false, "*.log"),
                Whole(SUPPLEMENT, LOCAL + @"\Vendor\Cache\", ObservationPrecedence.Reviewed),
            ],
            [],
            []);

        var target = Assert.Single(plan.Targets);
        Assert.Equal(SUPPLEMENT, target.OwnerRuleId);
        Assert.Equal([SUPPLEMENT, COMMUNITY, OTHER], target.ContributingRuleIds);
        Assert.Equal(2, plan.Dropped.Count(d => d.Reason == DroppedCandidateReason.Merged && d.MergedIntoOrder == 0));
    }

    /// <summary>커뮤니티 규칙의 제외는 검토 경로에도 적용된다(규칙 제외 > 검토 경로). 적용되는 제외가 있으면 전체 대상이 아니다.</summary>
    [Fact]
    public void 규칙_제외는_검토_경로보다_우선한다()
    {
        var keep = new ExclusionSpec(COMMUNITY, ExcludeKind.Path, LOCAL + @"\Vendor\Cache\Keep", ["*"]);

        var plan = ObservationPlanner.Plan([Whole(SUPPLEMENT, LOCAL + @"\Vendor\Cache", ObservationPrecedence.Reviewed)], [keep], []);

        var target = Assert.Single(plan.Targets);
        Assert.False(target.IsWhole);
        Assert.Equal([keep], target.Exclusions);
    }

    /// <summary>모든 파일을 빼는 PATH 제외 안의 후보는 규칙 제외로 빠진다.</summary>
    [Fact]
    public void 모든_파일을_빼는_PATH_제외_안의_후보는_빠진다()
    {
        var saved = new ExclusionSpec(COMMUNITY, ExcludeKind.Path, LOCAL + @"\Vendor\*\Saved", ["*.*"]);

        var plan = ObservationPlanner.Plan([Whole(COMMUNITY, LOCAL + @"\Vendor\Game1\Saved\Slot1", ObservationPrecedence.Community)], [saved], []);

        Assert.Empty(plan.Targets);
        Assert.Equal(DroppedCandidateReason.RuleExcluded, Assert.Single(plan.Dropped).Reason);
    }

    /// <summary>관련 없는 위치의 제외나 하위 폴더를 보지 않는 대상의 하위 FILE 제외는 적용하지 않는다.</summary>
    [Fact]
    public void 관련_없는_제외는_적용하지_않는다()
    {
        var elsewhere = new ExclusionSpec(OTHER, ExcludeKind.Path, LOCAL + @"\OtherVendor", ["*"]);
        var deeperFile = new ExclusionSpec(OTHER, ExcludeKind.File, LOCAL + @"\Vendor\Logs\Keep", ["keep.log"]);

        var plan = ObservationPlanner.Plan([Filtered(COMMUNITY, LOCAL + @"\Vendor\Logs", false, "*.log")], [elsewhere, deeperFile], []);

        Assert.Empty(Assert.Single(plan.Targets).Exclusions);
    }

    /// <summary>앞선 필터 대상과 겹치는 뒤 대상은 파일 단위 중복 확인이 필요하다고 표시한다. 하위 폴더를 보지 않는 대상은 하위 전체 대상을 하위 제외로 갖지 않는다.</summary>
    [Fact]
    public void 필터_대상과_겹치면_표시한다()
    {
        var plan = ObservationPlanner.Plan(
            [
                new ObservationCandidate(SUPPLEMENT, ObservationPrecedence.Reviewed, LOCAL + @"\Vendor", ["*.log"], true, TargetSource.Default),
                Whole(COMMUNITY, LOCAL + @"\Vendor\Cache", ObservationPrecedence.Community),
                Filtered(OTHER, LOCAL + @"\Vendor\Cache", false, "*.tmp"),
                Filtered(OTHER, LOCAL + @"\Unrelated", true, "*.tmp"),
            ],
            [],
            []);

        Assert.Equal(3, plan.Targets.Count);
        Assert.False(plan.Targets[0].IsWhole);
        Assert.True(plan.Targets[1].OverlapsEarlierFiltered);
        Assert.Empty(plan.Targets[0].CarveOuts);
        Assert.False(plan.Targets[2].OverlapsEarlierFiltered);
        Assert.Contains(plan.Dropped, d => d.Reason == DroppedCandidateReason.Merged && d.RuleId == OTHER && d.MergedIntoOrder == 1);
    }

    /// <summary>"*.*"도 모든 파일로 보고, 하위 폴더를 보지 않는 대상은 전체 대상이 아니다.</summary>
    [Fact]
    public void 모든_파일_패턴과_하위_포함_여부로_전체_대상을_정한다()
    {
        var plan = ObservationPlanner.Plan(
            [
                new ObservationCandidate(COMMUNITY, ObservationPrecedence.Community, LOCAL + @"\A", ["*.*"], true, TargetSource.Default),
                new ObservationCandidate(COMMUNITY, ObservationPrecedence.Community, LOCAL + @"\B", ["*"], false, TargetSource.Default),
            ],
            [],
            []);

        Assert.True(plan.Targets[0].IsWhole);
        Assert.False(plan.Targets[1].IsWhole);
    }

    /// <summary>FILE 제외는 지정 폴더의 직접 파일만, PATH 제외는 하위 전체의 패턴 일치 파일을 뺀다. 폴더 구성 요소 와일드카드를 지원한다.</summary>
    [Fact]
    public void 제외_조건을_파일에_적용한다()
    {
        List<ExclusionSpec> exclusions =
        [
            new(COMMUNITY, ExcludeKind.File, LOCAL + @"\Vendor", ["Filters.csv"]),
            new(COMMUNITY, ExcludeKind.Path, LOCAL + @"\Vendor\*\cfg", ["subconf_*.ejs"]),
        ];

        Assert.True(ExclusionMatcher.IsExcluded(exclusions, LOCAL + @"\Vendor", "filters.CSV"));
        Assert.False(ExclusionMatcher.IsExcluded(exclusions, LOCAL + @"\Vendor\Sub", "Filters.csv"));
        Assert.True(ExclusionMatcher.IsExcluded(exclusions, LOCAL + @"\Vendor\JD2\cfg\deep", "subconf_a.ejs"));
        Assert.False(ExclusionMatcher.IsExcluded(exclusions, LOCAL + @"\Vendor\JD2\cfg", "other.ejs"));
        Assert.False(ExclusionMatcher.ExcludesSubtree(exclusions, LOCAL + @"\Vendor\JD2\cfg"));
        Assert.True(ExclusionMatcher.ExcludesSubtree([new(COMMUNITY, ExcludeKind.Path, LOCAL + @"\Vendor\Keep", ["*"])], LOCAL + @"\vendor\keep\x"));
    }

    /// <summary>Win32 패턴 비교: "*.*"는 확장자 없는 이름도 포함하고, 대소문자를 무시하며, 폴더 경계 단위로 비교한다.</summary>
    [Fact]
    public void Win32_패턴과_폴더_경계를_지킨다()
    {
        Assert.True(RulePatternMatcher.Matches("*.*", "LOG"));
        Assert.True(RulePatternMatcher.Matches("*Cache", "GPUCache"));
        Assert.False(RulePatternMatcher.Matches("*.log", "a.log.bak"));
        Assert.True(RulePatternMatcher.IsSameOrUnderPattern(@"C:\A\Discord\Cache", @"C:\A\Discord*"));
        Assert.False(RulePatternMatcher.IsSameOrUnderPattern(@"C:\A\DiscordX", @"C:\A\Discord"));
        Assert.True(RulePatternMatcher.CouldMatchStrictlyUnder(@"C:\A\*\Keep", @"C:\A"));
        Assert.False(RulePatternMatcher.CouldMatchStrictlyUnder(@"C:\A", @"C:\A"));
    }
}
