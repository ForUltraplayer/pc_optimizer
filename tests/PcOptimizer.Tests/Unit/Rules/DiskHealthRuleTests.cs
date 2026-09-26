/**
 * @file    : DiskHealthRuleTests.cs
 * @author  : rudals252
 * @brief   : 디스크 상태 규칙의 'Windows가 보고한 상태' 문구, Healthy를 SMART 전체 정상으로 확장하지 않음, Warning/Unhealthy 조건부 후보, 알 수 없음 처리, 영속 ID 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="DiskHealthRule"/>을 검증합니다. 디스크 이름·GUID는 테스트용 예시입니다.
/// </summary>
public sealed class DiskHealthRuleTests
{
    private const string DISK_ID = "{00000000-1111-2222-3333-444444444444}";

    private static readonly DiskHealthRule RULE = new();

    /// <summary>Healthy를 확장 해석하는 표현(금지).</summary>
    private static readonly string[] FORBIDDEN_PHRASES = ["SMART 정상", "SMART 검사 완료", "고장 없음", "문제 없음", "정상입니다", "안전해요"];

    /// <summary>
    /// 디스크 목록으로 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(params FakeDisk[] disks)
    {
        return RULE.Evaluate(HardwareRuleTestData.Snapshot(HardwareRuleTestData.DiskResult(disks)));
    }

    /// <summary>Healthy는 'Windows가 보고한 상태' Info이며 SMART 전체 검사가 아님을 밝힌다.</summary>
    [Fact]
    public void Healthy는_Windows가_보고한_상태_정보다()
    {
        var finding = Assert.Single(Evaluate(new FakeDisk(DISK_ID, "테스트 SSD", PhysicalDiskProbeContract.HEALTH_HEALTHY)));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.Storage, finding.Category);
        Assert.Equal(DiskHealthRule.FINDING_ID_PREFIX + DISK_ID, finding.Id);
        Assert.Contains("Windows가 보고한 상태", finding.Title, StringComparison.Ordinal);
        Assert.Contains("Healthy", finding.Title, StringComparison.Ordinal);
        Assert.Contains("SMART 전체 항목을 검사한 결과가 아니", finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>
    /// Warning/Unhealthy 조건부 후보 스냅샷을 만든다. <see cref="CandidateSnapshot"/>과 아래 테스트가 이 메서드 하나를 공유한다.
    /// </summary>
    private static ScanSnapshot CandidateSnapshotFor(long health)
    {
        return HardwareRuleTestData.Snapshot(
            HardwareRuleTestData.DiskResult(new FakeDisk(DISK_ID, "테스트 HDD", health, MediaType: PhysicalDiskProbeContract.MEDIA_TYPE_HDD)));
    }

    /// <summary>Candidate를 내는 스냅샷(Warning 상태 디스크 하나)을 만든다.</summary>
    public static ScanSnapshot CandidateSnapshot()
    {
        return CandidateSnapshotFor(PhysicalDiskProbeContract.HEALTH_WARNING);
    }

    /// <summary>Warning·Unhealthy는 백업·점검을 권하는 조건부 Candidate다.</summary>
    [Theory]
    [InlineData(PhysicalDiskProbeContract.HEALTH_WARNING, "Warning")]
    [InlineData(PhysicalDiskProbeContract.HEALTH_UNHEALTHY, "Unhealthy")]
    public void 경고_상태는_백업_확인_후보다(long health, string name)
    {
        var finding = Assert.Single(RULE.Evaluate(CandidateSnapshotFor(health)));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Contains(name, finding.Title, StringComparison.Ordinal);
        Assert.Contains("Windows가 보고한 상태", finding.Title, StringComparison.Ordinal);
        Assert.Contains("백업", finding.Recommendation!.Text, StringComparison.Ordinal);
        Assert.Contains("제조사 진단", finding.Recommendation.Condition, StringComparison.Ordinal);
    }

    /// <summary>상태가 없거나 알 수 없음(5 등)이면 CannotVerify(Unsupported)다.</summary>
    [Theory]
    [InlineData(5L)]
    [InlineData(null)]
    public void 알_수_없는_상태는_지원_불가다(long? health)
    {
        var finding = Assert.Single(Evaluate(new FakeDisk(DISK_ID, "테스트 SSD", health)));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, finding.CannotVerifyReason);
    }

    /// <summary>제공자 GUID가 없으면 이름과 디스크 번호로 ID를 만들고, 같은 이름의 두 디스크도 서로 다른 ID를 갖는다.</summary>
    [Fact]
    public void 같은_이름_디스크도_ID가_다르다()
    {
        var findings = Evaluate(
            new FakeDisk(null, "같은 SSD", 0, DeviceId: "1"),
            new FakeDisk(null, "같은 SSD", 0, DeviceId: "2"));

        Assert.Equal(2, findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>어떤 상태에서도 Healthy를 SMART 정상·고장 없음으로 확장하지 않는다.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(5L)]
    public void Healthy를_확장하지_않는다(long health)
    {
        foreach (var finding in Evaluate(new FakeDisk(DISK_ID, "테스트", health)))
        {
            var text = RuleTestData.AllText(finding);
            Assert.All(FORBIDDEN_PHRASES, phrase => Assert.DoesNotContain(phrase, text, StringComparison.Ordinal));
        }
    }

    /// <summary>프로브가 실패하면 판정하지 않는다.</summary>
    [Fact]
    public void 실패한_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(PhysicalDiskProbeContract.PROBE_ID, ProbeStatus.Failed));

        Assert.Empty(RULE.Evaluate(snapshot));
    }
}
