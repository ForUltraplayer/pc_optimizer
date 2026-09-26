/**
 * @file    : GpuVendorLinkRuleTests.cs
 * @author  : rudals252
 * @brief   : AMD/Intel 어댑터의 설치 버전 정보 + 공식 페이지·도구 링크 Info와 별도 '최신 여부는 공식 도구에서 확인' CannotVerify(Unsupported), NVIDIA·가상 어댑터 제외 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="GpuVendorLinkRule"/>을 검증합니다. 이름·버전·ID는 테스트 예시입니다.
/// </summary>
public sealed class GpuVendorLinkRuleTests
{
    private const string AMD_PNP = @"PCI\VEN_1002&DEV_0002&SUBSYS_00000000&REV_C1\4&TEST&0&0019";
    private const string INTEL_PNP = @"PCI\VEN_8086&DEV_0003&SUBSYS_00000000&REV_04\3&TEST&0&10";

    private static readonly GpuVendorLinkRule RULE = new(DriverRuleTestData.CATALOG);

    /// <summary>
    /// 어댑터 목록으로 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(params FakeAdapter[] adapters)
    {
        return RULE.Evaluate(HardwareRuleTestData.Snapshot(HardwareRuleTestData.GpuResult(adapters)));
    }

    /// <summary>AMD는 설치 버전 Info + 공식 드라이버 페이지·자동 감지 도구 링크, 그리고 최신 비교 Unsupported 한 줄이다.</summary>
    [Fact]
    public void AMD는_공식_링크와_최신_비교_미지원을_보여_준다()
    {
        var findings = Evaluate(new FakeAdapter("테스트 AMD GPU", "31.0.24033.1003", AMD_PNP, "2026-03-04"));

        Assert.Equal(2, findings.Count);
        var info = Assert.Single(findings, f => f.Verdict == Verdict.Info);
        Assert.Equal(GpuVendorLinkRule.LINK_FINDING_ID_PREFIX + AMD_PNP, info.Id);
        Assert.Contains("31.0.24033.1003", info.Evidence, StringComparison.Ordinal);
        Assert.Contains("2026-03-04", info.Evidence, StringComparison.Ordinal);
        Assert.Equal(
            ["https://www.amd.com/en/support/download/drivers.html", "https://www.amd.com/en/resources/support-articles/faqs/GPU-131.html"],
            info.Actions.OfType<OpenLinkAction>().Select(link => link.Url));
        Assert.All(info.Actions.OfType<OpenLinkAction>(), link => Assert.False(string.IsNullOrWhiteSpace(link.Label)));

        var unsupported = Assert.Single(findings, f => f.Verdict == Verdict.CannotVerify);
        Assert.Equal(CannotVerifyReason.Unsupported, unsupported.CannotVerifyReason);
        Assert.Contains("최신 여부는 공식 도구에서 확인", unsupported.Title, StringComparison.Ordinal);
        Assert.Equal(GpuVendorLinkRule.LATEST_FINDING_ID_PREFIX + AMD_PNP, unsupported.Id);
    }

    /// <summary>Intel도 같은 방식이며 표에 있는 Intel 항목만 링크로 준다.</summary>
    [Fact]
    public void Intel도_공식_링크를_준다()
    {
        var findings = Evaluate(new FakeAdapter("테스트 Intel GPU", "32.0.101.6078", INTEL_PNP));

        var info = Assert.Single(findings, f => f.Verdict == Verdict.Info);
        Assert.Equal(["https://www.intel.com/content/www/us/en/support/detect.html"], info.Actions.OfType<OpenLinkAction>().Select(link => link.Url));
        Assert.Single(findings, f => f.CannotVerifyReason == CannotVerifyReason.Unsupported);
    }

    /// <summary>NVIDIA·가상·기타 PCI 어댑터에는 새 카드를 만들지 않는다(설치 정보 카드는 기존 규칙이 유지).</summary>
    [Fact]
    public void NVIDIA와_가상_어댑터는_새_카드가_없다()
    {
        var findings = Evaluate(
            new FakeAdapter("테스트 NVIDIA", "32.0.16.1656", DriverRuleTestData.NVIDIA_PNP),
            new FakeAdapter("테스트 가상", "0.45.0.0", @"ROOT\DISPLAY\0000"),
            new FakeAdapter("기타 PCI", "1.0.0.0", @"PCI\VEN_1234&DEV_0001\3&TEST&0&10"));

        Assert.Empty(findings);
    }

    /// <summary>공식 링크 표가 없으면 링크 없이 정보와 미지원 줄만 만든다(임의 URL을 만들지 않음).</summary>
    [Fact]
    public void 표가_없으면_링크_없이_정보만_준다()
    {
        var findings = new GpuVendorLinkRule(null).Evaluate(
            HardwareRuleTestData.Snapshot(HardwareRuleTestData.GpuResult(new FakeAdapter("테스트 AMD GPU", "31.0.24033.1003", AMD_PNP))));

        Assert.Equal(2, findings.Count);
        Assert.All(findings, finding => Assert.DoesNotContain(finding.Actions, action => action is OpenLinkAction));
    }

    /// <summary>설치 GPU 프로브가 성공하지 않았으면 빈 결과다.</summary>
    [Fact]
    public void 프로브_실패면_빈_결과다()
    {
        Assert.Empty(RULE.Evaluate(HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(GpuProbeContract.PROBE_ID, ProbeStatus.Failed))));
    }
}
