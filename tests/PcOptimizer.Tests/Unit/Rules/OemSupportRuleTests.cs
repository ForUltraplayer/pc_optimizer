/**
 * @file    : OemSupportRuleTests.cs
 * @author  : rudals252
 * @brief   : 제조사 지원 링크 규칙의 정규화 조회(MSI 정식 회사명·Dell Inc.), 표에 없는 제조사 NoRule, 가상 머신 정보, 노트북 제조사 맞춤 드라이버 안내, 제조사 없음·표 없음 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="OemSupportRule"/>을 검증합니다. 제조사·모델은 테스트 예시입니다.
/// </summary>
public sealed class OemSupportRuleTests
{
    private const string DESKTOP_CHASSIS = "3";
    private const string LAPTOP_CHASSIS = "10";

    private static readonly OemSupportRule RULE = new(DriverRuleTestData.CATALOG);

    /// <summary>
    /// 시스템 정보로 규칙을 평가한다.
    /// </summary>
    private static Finding EvaluateSingle(string? manufacturer, string? model, string chassis = DESKTOP_CHASSIS, OemSupportRule? rule = null)
    {
        return Assert.Single((rule ?? RULE).Evaluate(HardwareRuleTestData.Snapshot(DriverRuleTestData.SystemInfoResult(manufacturer, model, chassis))));
    }

    /// <summary>정식 회사명·접미사가 붙은 제조사 이름을 정규화해 표의 지원 페이지로 연결한다.</summary>
    [Theory]
    [InlineData("Micro-Star International Co., Ltd.", "https://www.msi.com/support/download")]
    [InlineData("Dell Inc.", "https://www.dell.com/support/home/")]
    public void 제조사를_정규화해_지원_링크를_준다(string manufacturer, string expectedUrl)
    {
        var finding = EvaluateSingle(manufacturer, "테스트 모델");

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(OemSupportRule.FINDING_ID, finding.Id);
        Assert.Equal(FindingCategory.Driver, finding.Category);
        var link = Assert.Single(finding.Actions.OfType<OpenLinkAction>());
        Assert.Equal(expectedUrl, link.Url);
        Assert.False(string.IsNullOrWhiteSpace(link.Label));
        Assert.Contains("테스트 모델", finding.Detail, StringComparison.Ordinal);
    }

    /// <summary>표에 없는 제조사는 CannotVerify(NoRule)이며 제조사 이름을 상세에 보여 준다.</summary>
    [Fact]
    public void 표에_없는_제조사는_규칙_없음이다()
    {
        var finding = EvaluateSingle("To Be Filled By O.E.M.", "To Be Filled By O.E.M.");

        Assert.Equal(CannotVerifyReason.NoRule, finding.CannotVerifyReason);
        Assert.Contains("To Be Filled By O.E.M.", finding.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(finding.Actions, action => action is OpenLinkAction);
    }

    /// <summary>가상 머신(Microsoft 가상 머신 모델, VMware, QEMU)은 정보 '가상 머신'이며 링크가 없다.</summary>
    [Theory]
    [InlineData("Microsoft Corporation", "Virtual Machine")]
    [InlineData("VMware, Inc.", "VMware7,1")]
    [InlineData("QEMU", "Standard PC (Q35 + ICH9, 2009)")]
    public void 가상_머신은_정보다(string manufacturer, string model)
    {
        var finding = EvaluateSingle(manufacturer, model);

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("가상 머신", finding.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(finding.Actions, action => action is OpenLinkAction);
    }

    /// <summary>가상 머신이 아닌 Microsoft 기기(Surface)는 Microsoft 지원 링크로 연결한다.</summary>
    [Fact]
    public void 가상이_아닌_마이크로소프트는_지원_링크다()
    {
        var finding = EvaluateSingle("Microsoft Corporation", "Surface Laptop 7", LAPTOP_CHASSIS);

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Single(finding.Actions.OfType<OpenLinkAction>());
    }

    /// <summary>노트북은 제조사가 맞춘 드라이버를 먼저 확인하라는 안내를 붙이고 GPU 공급업체 최신을 권하지 않는다.</summary>
    [Fact]
    public void 노트북은_제조사_맞춤_드라이버_안내를_붙인다()
    {
        var finding = EvaluateSingle("Dell Inc.", "테스트 노트북", LAPTOP_CHASSIS);

        Assert.Contains("노트북", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("제조사가 맞춘 드라이버", finding.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("권장", DriverRuleTestData.VisibleText(finding), StringComparison.Ordinal);
    }

    /// <summary>데스크톱에는 노트북 안내가 없다.</summary>
    [Fact]
    public void 데스크톱에는_노트북_안내가_없다()
    {
        var finding = EvaluateSingle("Dell Inc.", "테스트 데스크톱");

        Assert.DoesNotContain("노트북", finding.Detail, StringComparison.Ordinal);
    }

    /// <summary>제조사 값이 없으면 PartialData다.</summary>
    [Fact]
    public void 제조사가_없으면_부분_데이터다()
    {
        var finding = EvaluateSingle(null, "모델");

        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>공식 링크 표를 읽지 못했으면 ProbeError이며 NoRule로 오인하지 않는다.</summary>
    [Fact]
    public void 표가_없으면_수집_오류다()
    {
        var finding = EvaluateSingle("Dell Inc.", "모델", rule: new OemSupportRule(null));

        Assert.Equal(CannotVerifyReason.ProbeError, finding.CannotVerifyReason);
    }

    /// <summary>시스템 정보 프로브가 성공하지 않았으면 빈 결과다.</summary>
    [Fact]
    public void 프로브_실패면_빈_결과다()
    {
        Assert.Empty(RULE.Evaluate(HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(SystemInfoProbeContract.PROBE_ID, ProbeStatus.Failed))));
    }
}
