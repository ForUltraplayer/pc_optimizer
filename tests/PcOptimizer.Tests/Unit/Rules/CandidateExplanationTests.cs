/**
 * @file    : CandidateExplanationTests.cs
 * @author  : rudals252
 * @brief   : Candidate를 내는 규칙 8개가 설명 3줄과 안전 수준을 채우고, 각 줄이 60자 이내이며 금지 문구가 없는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>Candidate 규칙의 설명 3줄·안전 수준을 검증합니다.</summary>
public sealed class CandidateExplanationTests
{
    private static readonly string[] FORBIDDEN = ["근거:", "http", "난이도", "정격 미달", "XMP 꺼짐", "확보 가능"];

    /// <summary>규칙별 Candidate 스냅샷을 만드는 fixture. 각 규칙 테스트 파일의 헬퍼를 호출한다.</summary>
    public static TheoryData<string, IRule, ScanSnapshot> Candidates => new()
    {
        { "display", new DisplayRefreshRule(), DisplayRefreshRuleTests.CandidateSnapshot() },
        { "memory", new MemorySpeedRule(), MemorySpeedRuleTests.CandidateSnapshot() },
        { "power", new PowerPlanRule(), PowerPlanRuleTests.CandidateSnapshot() },
        { "storage", new StorageSpaceRule(), StorageSpaceRuleTests.CandidateSnapshot() },
        { "diskHealth", new DiskHealthRule(), DiskHealthRuleTests.CandidateSnapshot() },
        { "trim", new TrimPolicyRule(), TrimPolicyRuleTests.CandidateSnapshot() },
        { "driverUpdate", DriverUpdateRuleTests.CreateRule(), DriverUpdateRuleTests.CandidateSnapshot() },
        { "wuDriver", new WindowsUpdateDriverRule(), WindowsUpdateDriverRuleTests.CandidateSnapshot() },
    };

    /// <summary>Candidate마다 설명 3줄과 안전 수준이 있고 각 줄이 60자 이내이며 금지 문구가 없다.</summary>
    [Theory]
    [MemberData(nameof(Candidates))]
    public void CandidateHasExplanationAndSafety(string name, IRule rule, ScanSnapshot snapshot)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        var candidates = rule.Evaluate(snapshot).Where(f => f.Verdict == Verdict.Candidate).ToList();
        Assert.NotEmpty(candidates);
        foreach (var finding in candidates)
        {
            Assert.NotNull(finding.Explanation);
            Assert.NotNull(finding.Safety);
            foreach (var line in new[] { finding.Explanation!.What, finding.Explanation.Effect, finding.Explanation.Caution })
            {
                Assert.InRange(line.Length, 1, Explanation.MAX_LINE_LENGTH);
                Assert.DoesNotContain(FORBIDDEN, forbidden => line.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
