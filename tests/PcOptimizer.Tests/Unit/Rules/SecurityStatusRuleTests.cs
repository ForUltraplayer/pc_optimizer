/**
 * @file    : SecurityStatusRuleTests.cs
 * @author  : rudals252
 * @brief   : 보안 상태 규칙의 VBS·메모리 무결성 두 줄 분리, 값 누락/알 수 없는 값 처리, 보안 분류·정보 전용, 끄기 권장 문구 부재 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="SecurityStatusRule"/>을 검증합니다.
/// </summary>
public sealed class SecurityStatusRuleTests
{
    private static readonly SecurityStatusRule RULE = new();

    /// <summary>보안 기능을 끄도록 권하는 표현(어떤 경우에도 금지).</summary>
    private static readonly string[] FORBIDDEN_PHRASES = ["끄", "비활성화 권장", "비활성화하", "해제하세요", "사용 중지하"];

    /// <summary>
    /// DeviceGuard 측정값으로 스냅샷을 만든다. null은 측정값 없음.
    /// </summary>
    private static ScanSnapshot Snapshot(long? vbsStatus, string[]? configured, string[]? running, ProbeStatus status = ProbeStatus.Success)
    {
        var measurements = new List<Measurement>();
        if (vbsStatus is { } vbs)
        {
            measurements.Add(RuleTestData.Integer(SecurityStatusProbeContract.VBS_STATUS, vbs));
        }

        if (configured is not null)
        {
            measurements.Add(RuleTestData.TextList(SecurityStatusProbeContract.SERVICES_CONFIGURED, configured));
        }

        if (running is not null)
        {
            measurements.Add(RuleTestData.TextList(SecurityStatusProbeContract.SERVICES_RUNNING, running));
        }

        return HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(SecurityStatusProbeContract.PROBE_ID, status, measurements));
    }

    /// <summary>
    /// ID로 Finding을 찾는다.
    /// </summary>
    private static Finding Find(IReadOnlyList<Finding> findings, string id)
    {
        return Assert.Single(findings, f => f.Id == id);
    }

    /// <summary>VBS와 메모리 무결성을 서로 다른 두 Finding(두 줄)으로 보여 주며 둘 다 보안 분류 Info다.</summary>
    [Fact]
    public void VBS와_메모리_무결성은_두_줄로_분리한다()
    {
        var findings = RULE.Evaluate(Snapshot(SecurityStatusProbeContract.VBS_STATUS_RUNNING, ["2"], ["2"]));

        Assert.Equal(2, findings.Count);
        var vbs = Find(findings, SecurityStatusRule.VBS_FINDING_ID);
        var hvci = Find(findings, SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID);
        Assert.All(findings, f =>
        {
            Assert.Equal(Verdict.Info, f.Verdict);
            Assert.Equal(FindingCategory.Security, f.Category);
        });
        Assert.Contains(CoreStrings.Security_Vbs_Running, vbs.Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Security_Hvci_Configured, hvci.Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Security_Hvci_Running, hvci.Title, StringComparison.Ordinal);
        Assert.Contains(vbs.Measured, m => m.Name == SecurityStatusProbeContract.VBS_STATUS);
        Assert.DoesNotContain(vbs.Measured, m => m.Name == SecurityStatusProbeContract.SERVICES_RUNNING);
        Assert.DoesNotContain(hvci.Measured, m => m.Name == SecurityStatusProbeContract.VBS_STATUS);
    }

    /// <summary>VBS가 실행 중이어도 메모리 무결성이 설정되지 않았으면 두 값을 따로 표시한다(같은 불리언으로 합치지 않음).</summary>
    [Fact]
    public void VBS_실행과_메모리_무결성_미설정을_구분한다()
    {
        var findings = RULE.Evaluate(Snapshot(SecurityStatusProbeContract.VBS_STATUS_RUNNING, ["1"], ["1"]));

        Assert.Contains(CoreStrings.Security_Vbs_Running, Find(findings, SecurityStatusRule.VBS_FINDING_ID).Title, StringComparison.Ordinal);
        var hvci = Find(findings, SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID);
        Assert.Contains(CoreStrings.Security_Hvci_NotConfigured, hvci.Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Security_Hvci_NotRunning, hvci.Title, StringComparison.Ordinal);
    }

    /// <summary>메모리 무결성이 설정됐지만 실행 중이 아니면 그대로 표시한다.</summary>
    [Fact]
    public void 설정됨과_실행_중_아님을_함께_표시한다()
    {
        var findings = RULE.Evaluate(Snapshot(SecurityStatusProbeContract.VBS_STATUS_ENABLED_NOT_RUNNING, ["2"], []));

        Assert.Contains(CoreStrings.Security_Vbs_EnabledNotRunning, Find(findings, SecurityStatusRule.VBS_FINDING_ID).Title, StringComparison.Ordinal);
        var hvci = Find(findings, SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID);
        Assert.Contains(CoreStrings.Security_Hvci_Configured, hvci.Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Security_Hvci_NotRunning, hvci.Title, StringComparison.Ordinal);
    }

    /// <summary>VBS 값이 없으면 VBS만 CannotVerify(PartialData)이고 메모리 무결성 Info는 유지한다.</summary>
    [Fact]
    public void VBS_누락은_해당_줄만_확인_불가다()
    {
        var findings = RULE.Evaluate(Snapshot(null, ["2"], ["2"], ProbeStatus.Partial));

        var vbs = Find(findings, SecurityStatusRule.VBS_FINDING_ID);
        Assert.Equal(Verdict.CannotVerify, vbs.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, vbs.CannotVerifyReason);
        Assert.Equal(Verdict.Info, Find(findings, SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID).Verdict);
    }

    /// <summary>서비스 목록이 없으면 메모리 무결성만 CannotVerify(PartialData)이고 false로 대체하지 않는다.</summary>
    [Fact]
    public void 서비스_목록_누락은_메모리_무결성만_확인_불가다()
    {
        var findings = RULE.Evaluate(Snapshot(SecurityStatusProbeContract.VBS_STATUS_RUNNING, null, ["2"], ProbeStatus.Partial));

        var hvci = Find(findings, SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID);
        Assert.Equal(Verdict.CannotVerify, hvci.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, hvci.CannotVerifyReason);
        Assert.DoesNotContain(CoreStrings.Security_Hvci_NotConfigured, hvci.Title, StringComparison.Ordinal);
        Assert.Equal(Verdict.Info, Find(findings, SecurityStatusRule.VBS_FINDING_ID).Verdict);
    }

    /// <summary>알려지지 않은 VBS 값은 CannotVerify(Unsupported)다.</summary>
    [Fact]
    public void 알려지지_않은_VBS_값은_지원_불가다()
    {
        var findings = RULE.Evaluate(Snapshot(7, ["2"], ["2"]));

        var vbs = Find(findings, SecurityStatusRule.VBS_FINDING_ID);
        Assert.Equal(Verdict.CannotVerify, vbs.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, vbs.CannotVerifyReason);
    }

    /// <summary>설정 URI 대신 수동 경로 안내를 상세에 준다(코어 격리 URI는 허용 목록에 없음).</summary>
    [Fact]
    public void 수동_경로_안내를_준다()
    {
        var findings = RULE.Evaluate(Snapshot(SecurityStatusProbeContract.VBS_STATUS_RUNNING, ["2"], ["2"]));

        Assert.All(findings, f => Assert.DoesNotContain(f.Actions, a => a is OpenSettingsAction));
        Assert.Contains(CoreStrings.Security_Detail_ManualPath, Find(findings, SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID).Detail, StringComparison.Ordinal);
    }

    /// <summary>프로브가 실패(클래스 없음 등)하면 판정하지 않는다(상태 변환기가 CannotVerify(Unsupported)로 드러냄).</summary>
    [Fact]
    public void 실패한_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(
            SecurityStatusProbeContract.PROBE_ID, ProbeStatus.Failed, issues: [new Issue(CannotVerifyReason.Unsupported, "class missing")]));

        Assert.Empty(RULE.Evaluate(snapshot));
    }

    /// <summary>어떤 값에서도 보안 기능을 끄도록 권하지 않고 Candidate를 만들지 않는다.</summary>
    [Theory]
    [InlineData(0L, new string[0], new string[0])]
    [InlineData(1L, new[] { "2" }, new string[0])]
    [InlineData(2L, new[] { "1", "2" }, new[] { "1", "2" })]
    [InlineData(2L, new[] { "0" }, new[] { "0" })]
    [InlineData(9L, new[] { "2" }, new[] { "2" })]
    public void 끄기_권장이_없다(long vbsStatus, string[] configured, string[] running)
    {
        foreach (var finding in RULE.Evaluate(Snapshot(vbsStatus, configured, running)))
        {
            var text = RuleTestData.AllText(finding);
            Assert.All(FORBIDDEN_PHRASES, phrase => Assert.DoesNotContain(phrase, text, StringComparison.Ordinal));
            Assert.NotEqual(Verdict.Candidate, finding.Verdict);
            Assert.Null(finding.Recommendation);
        }
    }
}
