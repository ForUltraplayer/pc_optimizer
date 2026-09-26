/**
 * @file    : TrimPolicyRuleTests.cs
 * @author  : rudals252
 * @brief   : TRIM 정책 규칙의 파일 시스템별 OS 정책 정보(켜짐)·조건부 후보(꺼짐)·알 수 없는 값, 장치 지원·수행과 구분하는 문구 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="TrimPolicyRule"/>을 검증합니다.
/// </summary>
public sealed class TrimPolicyRuleTests
{
    private static readonly TrimPolicyRule RULE = new();

    /// <summary>장치 지원·수행을 단정하는 표현(금지).</summary>
    private static readonly string[] FORBIDDEN_PHRASES = ["TRIM을 지원해요", "TRIM이 수행되고", "TRIM 지원 확인", "TRIM이 동작 중"];

    /// <summary>
    /// 파일 시스템별 값으로 스냅샷을 만든다. null은 측정값 없음.
    /// </summary>
    private static ScanSnapshot Snapshot(long? ntfs, long? refs)
    {
        var measurements = new List<Measurement>();
        if (ntfs is { } ntfsValue)
        {
            measurements.Add(RuleTestData.Integer(TrimPolicyProbeContract.DisableDeleteNotifyName(TrimPolicyProbeContract.FILE_SYSTEM_NTFS), ntfsValue));
        }

        if (refs is { } refsValue)
        {
            measurements.Add(RuleTestData.Integer(TrimPolicyProbeContract.DisableDeleteNotifyName(TrimPolicyProbeContract.FILE_SYSTEM_REFS), refsValue));
        }

        return HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(TrimPolicyProbeContract.PROBE_ID, ProbeStatus.Success, measurements));
    }

    /// <summary>0은 'OS 삭제 알림(TRIM) 정책: 켜짐' Info이며 장치 지원·수행은 확인하지 않았다고 밝힌다.</summary>
    [Fact]
    public void 값_0은_정책_켜짐_정보다()
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot(0, null)));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.Storage, finding.Category);
        Assert.Equal(TrimPolicyRule.FINDING_ID_PREFIX + "ntfs", finding.Id);
        Assert.Contains("OS 삭제 알림(TRIM) 정책: 켜짐", finding.Title, StringComparison.Ordinal);
        Assert.Contains("NTFS", finding.Title, StringComparison.Ordinal);
        Assert.Contains("정책", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("지원하는지", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("수행되는지", finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>1(삭제 알림 막음)은 확인을 권하는 조건부 Candidate다.</summary>
    [Fact]
    public void 값_1은_확인_후보다()
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot(1, null)));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Contains("꺼짐", finding.Title, StringComparison.Ordinal);
        Assert.NotNull(finding.Recommendation);
    }

    /// <summary>파일 시스템마다 하나씩 판정한다.</summary>
    [Fact]
    public void 파일_시스템별로_판정한다()
    {
        var findings = RULE.Evaluate(Snapshot(0, 1));

        Assert.Equal(Verdict.Info, Assert.Single(findings, f => f.Id == TrimPolicyRule.FINDING_ID_PREFIX + "ntfs").Verdict);
        Assert.Equal(Verdict.Candidate, Assert.Single(findings, f => f.Id == TrimPolicyRule.FINDING_ID_PREFIX + "refs").Verdict);
    }

    /// <summary>알려지지 않은 값은 CannotVerify(Unsupported)다.</summary>
    [Fact]
    public void 알려지지_않은_값은_지원_불가다()
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot(2, null)));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, finding.CannotVerifyReason);
    }

    /// <summary>관리자 권한이 없어 건너뛴 프로브는 판정하지 않는다(상태 변환기가 ElevationRequired로 드러냄).</summary>
    [Fact]
    public void 건너뛴_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(
            TrimPolicyProbeContract.PROBE_ID, ProbeStatus.Skipped, issues: [new Issue(CannotVerifyReason.ElevationRequired, "elevation")]));

        Assert.Empty(RULE.Evaluate(snapshot));
    }

    /// <summary>어떤 값에서도 장치의 TRIM 지원·수행을 단정하지 않는다.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    public void 장치_지원을_단정하지_않는다(long value)
    {
        foreach (var finding in RULE.Evaluate(Snapshot(value, value)))
        {
            var text = RuleTestData.AllText(finding);
            Assert.All(FORBIDDEN_PHRASES, phrase => Assert.DoesNotContain(phrase, text, StringComparison.Ordinal));
        }
    }
}
