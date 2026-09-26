/**
 * @file    : SystemInfoRuleTests.cs
 * @author  : rudals252
 * @brief   : 시스템 정보 규칙의 제조사·모델·섀시 정보 표시, 섀시 불명 처리, 값 누락 시 확인 불가, OEM 링크 부재 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="SystemInfoRule"/>을 검증합니다. 제조사·모델은 테스트용 예시입니다.
/// </summary>
public sealed class SystemInfoRuleTests
{
    private static readonly SystemInfoRule RULE = new();

    /// <summary>
    /// 시스템 정보 결과를 만든다. null은 측정값 없음.
    /// </summary>
    private static ScanSnapshot Snapshot(string? manufacturer, string? model, string[]? chassis, string? bios = "F1")
    {
        var measurements = new List<Measurement>();
        if (manufacturer is not null)
        {
            measurements.Add(RuleTestData.Text(SystemInfoProbeContract.MANUFACTURER, manufacturer));
        }

        if (model is not null)
        {
            measurements.Add(RuleTestData.Text(SystemInfoProbeContract.MODEL, model));
        }

        if (chassis is not null)
        {
            measurements.Add(RuleTestData.TextList(SystemInfoProbeContract.CHASSIS_TYPES, chassis));
        }

        if (bios is not null)
        {
            measurements.Add(RuleTestData.Text(SystemInfoProbeContract.BIOS_VERSION, bios));
        }

        return HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(SystemInfoProbeContract.PROBE_ID, ProbeStatus.Success, measurements));
    }

    /// <summary>제조사·모델·섀시를 Info '시스템 정보'로 보여 주고 OEM 링크는 없다.</summary>
    [Fact]
    public void 제조사와_모델을_정보로_보여_준다()
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot("테스트 제조사", "테스트 모델", ["3"])));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(SystemInfoRule.FINDING_ID, finding.Id);
        Assert.Contains("시스템 정보", finding.Title, StringComparison.Ordinal);
        Assert.Contains("테스트 제조사", finding.Title, StringComparison.Ordinal);
        Assert.Contains("테스트 모델", finding.Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Power_Chassis_Desktop, finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(finding.Actions, a => a is OpenLinkAction);
    }

    /// <summary>노트북형 섀시 코드는 노트북형으로 표시한다(전원 규칙과 같은 해석).</summary>
    [Fact]
    public void 노트북형_섀시를_표시한다()
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot("제조사", "모델", ["10"])));

        Assert.Contains(CoreStrings.Power_Chassis_Laptop, finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>섀시 정보가 없거나 모순이면 '알 수 없음'이며 데스크톱으로 추정하지 않는다.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("9,3")]
    public void 섀시_불명은_알_수_없음이다(string? codes)
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot("제조사", "모델", codes?.Split(','))));

        Assert.Contains(CoreStrings.Power_Chassis_Unknown, finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(CoreStrings.Power_Chassis_Desktop, finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>제조사와 모델을 모두 읽지 못하면 CannotVerify(PartialData)다.</summary>
    [Fact]
    public void 제조사와_모델이_없으면_부분_데이터다()
    {
        var finding = Assert.Single(RULE.Evaluate(Snapshot(null, null, ["3"])));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>프로브가 실패하면 판정하지 않는다.</summary>
    [Fact]
    public void 실패한_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(SystemInfoProbeContract.PROBE_ID, ProbeStatus.Failed));

        Assert.Empty(RULE.Evaluate(snapshot));
    }
}
