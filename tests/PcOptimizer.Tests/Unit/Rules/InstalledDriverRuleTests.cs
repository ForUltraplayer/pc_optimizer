/**
 * @file    : InstalledDriverRuleTests.cs
 * @author  : rudals252
 * @brief   : 설치 GPU 드라이버 규칙의 어댑터별 정보(NVIDIA 표기 변환·AMD/Intel 변환 없음)·가상 어댑터·버전 누락 분기와 '최신' 비교 문구 부재 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="InstalledDriverRule"/>을 검증합니다. 이름·버전·ID는 테스트용 예시입니다.
/// </summary>
public sealed class InstalledDriverRuleTests
{
    private const string NVIDIA_PNP = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1\4&TEST&0&0009";
    private const string AMD_PNP = @"PCI\VEN_1002&DEV_0002&SUBSYS_00000000&REV_C1\4&TEST&0&0019";
    private const string INTEL_PNP = @"PCI\VEN_8086&DEV_0003&SUBSYS_00000000&REV_04\3&TEST&0&10";
    private const string VIRTUAL_PNP = @"ROOT\DISPLAY\0000";

    private static readonly InstalledDriverRule RULE = new();

    /// <summary>1차에서 다루지 않는 최신 비교 표현.</summary>
    private static readonly string[] FORBIDDEN_PHRASES = ["최신", "업데이트 후보", "권장 드라이버", "호환성 검증"];

    /// <summary>
    /// 어댑터 목록으로 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(params FakeAdapter[] adapters)
    {
        return RULE.Evaluate(HardwareRuleTestData.Snapshot(HardwareRuleTestData.GpuResult(adapters)));
    }

    /// <summary>NVIDIA 어댑터는 Windows 버전과 NVIDIA 표기 버전·날짜·하드웨어 ID를 Info로 보여 준다.</summary>
    [Fact]
    public void NVIDIA는_표기_버전을_함께_보여_준다()
    {
        var finding = Assert.Single(Evaluate(new FakeAdapter("테스트 NVIDIA", "32.0.16.1656", NVIDIA_PNP, "2026-01-02", [@"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1"])));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.Driver, finding.Category);
        Assert.Equal(InstalledDriverRule.FINDING_ID_PREFIX + NVIDIA_PNP, finding.Id);
        Assert.Contains("616.56", finding.Title, StringComparison.Ordinal);
        Assert.Contains("32.0.16.1656", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("2026-01-02", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains(@"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1", finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(finding.Actions, a => a is OpenLinkAction);
    }

    /// <summary>AMD/Intel은 NVIDIA식 변환을 하지 않고 '변환 없음'으로 표시한다.</summary>
    [Theory]
    [InlineData(AMD_PNP)]
    [InlineData(INTEL_PNP)]
    public void AMD와_Intel은_변환하지_않는다(string pnpId)
    {
        var finding = Assert.Single(Evaluate(new FakeAdapter("테스트 GPU", "31.0.101.4502", pnpId)));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains(CoreStrings.Gpu_VendorVersion_None, finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("31.0.101.4502", finding.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("145.02", finding.Title + finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>PCI 장치가 아닌 어댑터는 Info '가상 어댑터'다.</summary>
    [Fact]
    public void 가상_어댑터는_가상_정보다()
    {
        var finding = Assert.Single(Evaluate(new FakeAdapter("테스트 가상 어댑터", "0.45.0.0", VIRTUAL_PNP)));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("가상 어댑터", finding.Title, StringComparison.Ordinal);
        Assert.Equal(InstalledDriverRule.FINDING_ID_PREFIX + VIRTUAL_PNP, finding.Id);
    }

    /// <summary>드라이버 버전이 없으면 CannotVerify(PartialData)다.</summary>
    [Fact]
    public void 버전이_없으면_부분_데이터다()
    {
        var finding = Assert.Single(Evaluate(new FakeAdapter("테스트 GPU", null, NVIDIA_PNP)));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>어댑터마다 하나씩, PnP ID 기반의 서로 다른 ID로 Finding을 만든다.</summary>
    [Fact]
    public void 어댑터별로_하나씩_만든다()
    {
        var findings = Evaluate(
            new FakeAdapter("가상", "0.45.0.0", VIRTUAL_PNP),
            new FakeAdapter("NVIDIA", "32.0.16.1656", NVIDIA_PNP));

        Assert.Equal(2, findings.Count);
        Assert.Equal(2, findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>프로브가 실패하면 판정하지 않는다.</summary>
    [Fact]
    public void 실패한_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(GpuProbeContract.PROBE_ID, ProbeStatus.Failed));

        Assert.Empty(RULE.Evaluate(snapshot));
    }

    /// <summary>최신 여부·업데이트 권고 문구는 1차 설치 정보 규칙에 없다.</summary>
    [Fact]
    public void 최신_비교_문구가_없다()
    {
        var findings = Evaluate(
            new FakeAdapter("가상", "0.45.0.0", VIRTUAL_PNP),
            new FakeAdapter("NVIDIA", "32.0.16.1656", NVIDIA_PNP),
            new FakeAdapter("AMD", "31.0.21001.45002", AMD_PNP),
            new FakeAdapter("버전 없음", null, INTEL_PNP));

        foreach (var finding in findings)
        {
            var text = RuleTestData.AllText(finding);
            Assert.All(FORBIDDEN_PHRASES, phrase => Assert.DoesNotContain(phrase, text, StringComparison.Ordinal));
            Assert.NotEqual(Verdict.Candidate, finding.Verdict);
        }
    }
}
