/**
 * @file    : StorageSpaceRuleTests.cs
 * @author  : rudals252
 * @brief   : 저장소 여유 공간 규칙의 10%·10GiB 경계값, 제품 휴리스틱 명시, 고정·문자 있는 볼륨만 판정, 값 누락, Windows.old 존재 정보 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="StorageSpaceRule"/>을 검증합니다. 크기는 테스트용 예시입니다.
/// </summary>
public sealed class StorageSpaceRuleTests
{
    private const long GIB = HardwareRuleTestData.GIB;
    private const long REMOVABLE = 2;

    private static readonly StorageSpaceRule RULE = new();

    /// <summary>
    /// 볼륨 목록으로 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(bool? windowsOld, params FakeVolume[] volumes)
    {
        return RULE.Evaluate(HardwareRuleTestData.Snapshot(HardwareRuleTestData.VolumeResult(windowsOld, volumes)));
    }

    /// <summary>임계값 상수는 스펙 값(10%, 10GiB)이다.</summary>
    [Fact]
    public void 임계값은_10퍼센트와_10GiB다()
    {
        Assert.Equal(10.0, StorageSpaceRule.LOW_FREE_PERCENT);
        Assert.Equal(10 * GIB, StorageSpaceRule.LOW_FREE_BYTES);
    }

    /// <summary>여유율 경계: 정확히 10%는 후보가 아니고, 10% 미만이면 후보다(여유 바이트는 기준 이상).</summary>
    [Theory]
    [InlineData(100L * GIB, false)]
    [InlineData((100L * GIB) - 1, true)]
    [InlineData(500L * GIB, false)]
    public void 여유율_경계(long free, bool expectCandidate)
    {
        var finding = Assert.Single(Evaluate(null, new FakeVolume("C", 1000 * GIB, free)));

        Assert.Equal(expectCandidate ? Verdict.Candidate : Verdict.Info, finding.Verdict);
    }

    /// <summary>여유 바이트 경계: 정확히 10GiB는 후보가 아니고, 10GiB 미만이면 후보다(여유율은 기준 이상).</summary>
    [Theory]
    [InlineData(10L * GIB, false)]
    [InlineData((10L * GIB) - 1, true)]
    [InlineData(20L * GIB, false)]
    public void 여유_바이트_경계(long free, bool expectCandidate)
    {
        var finding = Assert.Single(Evaluate(null, new FakeVolume("D", 50 * GIB, free)));

        Assert.Equal(expectCandidate ? Verdict.Candidate : Verdict.Info, finding.Verdict);
    }

    /// <summary>후보는 '여유 공간 적음'이며 제품 휴리스틱임을 근거에 밝히고 저장소 설정 열기를 제공한다.</summary>
    [Fact]
    public void 후보는_휴리스틱임을_밝힌다()
    {
        var finding = Assert.Single(Evaluate(null, new FakeVolume("C", 100 * GIB, 5 * GIB)));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(FindingCategory.Storage, finding.Category);
        Assert.Equal(StorageSpaceRule.FINDING_ID_PREFIX + "C:", finding.Id);
        Assert.Contains("여유 공간 적음", finding.Title, StringComparison.Ordinal);
        Assert.Contains("휴리스틱", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: StorageSpaceRule.STORAGE_SETTINGS_URI });
        Assert.NotNull(finding.Recommendation);
    }

    /// <summary>고정 디스크가 아니거나 드라이브 문자가 없는 볼륨(복구·EFI 파티션 등)은 판정하지 않는다.</summary>
    [Fact]
    public void 고정_문자_볼륨만_판정한다()
    {
        var findings = Evaluate(
            null,
            new FakeVolume(null, GIB, GIB / 100),
            new FakeVolume("E", 100 * GIB, GIB, DriveType: REMOVABLE),
            new FakeVolume("C", 100 * GIB, 50 * GIB));

        var finding = Assert.Single(findings);
        Assert.Equal(StorageSpaceRule.FINDING_ID_PREFIX + "C:", finding.Id);
    }

    /// <summary>크기나 남은 공간이 없거나 크기가 0이면 CannotVerify(PartialData)다.</summary>
    [Theory]
    [InlineData(null, 10L)]
    [InlineData(100L, null)]
    [InlineData(0L, 0L)]
    public void 크기_누락은_부분_데이터다(long? size, long? free)
    {
        var finding = Assert.Single(Evaluate(null, new FakeVolume("C", size, free)));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>Windows.old가 있으면 크기를 재지 않았다는 Info를 내고, 없으면 만들지 않는다.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void WindowsOld_존재를_알린다(bool exists, bool expectFinding)
    {
        var findings = Evaluate(exists, new FakeVolume("C", 100 * GIB, 50 * GIB));

        var windowsOld = findings.Where(f => f.Id == StorageSpaceRule.WINDOWS_OLD_FINDING_ID).ToList();
        Assert.Equal(expectFinding ? 1 : 0, windowsOld.Count);
        if (expectFinding)
        {
            Assert.Equal(Verdict.Info, windowsOld[0].Verdict);
            Assert.Contains("크기", windowsOld[0].Evidence, StringComparison.Ordinal);
        }
    }

    /// <summary>프로브가 실패하면 판정하지 않는다.</summary>
    [Fact]
    public void 실패한_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(VolumeProbeContract.PROBE_ID, ProbeStatus.Failed));

        Assert.Empty(RULE.Evaluate(snapshot));
    }
}
