/**
 * @file    : MemorySpeedRuleTests.cs
 * @author  : rudals252
 * @brief   : 메모리 보고 속도/설정 속도 비교 규칙의 모든 판정 분기와 금지 문구 부재를 가짜 측정값으로 검증
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="MemorySpeedRule"/>이 스펙 §5 메모리 행을 그대로 따르는지 검증합니다.
/// </summary>
public sealed class MemorySpeedRuleTests
{
    private const long REPORTED_SPEED = 4800;
    private const long LOWER_CONFIGURED_SPEED = 3600;
    private const long HIGHER_CONFIGURED_SPEED = 5200;

    private static readonly MemorySpeedRule RULE = new();

    /// <summary>
    /// 규칙 결과에서 모듈 판정(프로필 확인 불가 항목 제외)만 고른다.
    /// </summary>
    private static List<Finding> ModuleFindings(IReadOnlyList<Finding> findings)
    {
        return [.. findings.Where(f => f.Id != MemorySpeedRule.PROFILE_FINDING_ID)];
    }

    /// <summary>
    /// 모든 분기의 입력 모음(금지 문구 검사용).
    /// </summary>
    public static TheoryData<FakeModule> AllBranches()
    {
        return
        [
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, LOWER_CONFIGURED_SPEED),
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, REPORTED_SPEED),
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, HIGHER_CONFIGURED_SPEED),
            new FakeModule("DIMM A", "BANK 0", null, REPORTED_SPEED),
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, 0),
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, LOWER_CONFIGURED_SPEED, SpeedUnit: null),
        ];
    }

    /// <summary>설정 속도가 보고 속도보다 낮으면 조건부 Candidate와 필수 문장이 붙는다.</summary>
    [Fact]
    public void 설정_속도가_낮으면_조건부_후보와_필수_문장을_낸다()
    {
        var snapshot = RuleTestData.MemorySnapshot(ProbeStatus.Success, new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, LOWER_CONFIGURED_SPEED));

        var finding = Assert.Single(ModuleFindings(RULE.Evaluate(snapshot)));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(FindingCategory.Memory, finding.Category);
        Assert.Null(finding.CannotVerifyReason);
        Assert.NotNull(finding.Recommendation);
        Assert.False(string.IsNullOrWhiteSpace(finding.Recommendation.Text));
        Assert.False(string.IsNullOrWhiteSpace(finding.Recommendation.Condition));
        Assert.NotNull(finding.Impact);
        Assert.Contains(CoreStrings.Memory_CandidateCaveat, finding.Evidence, StringComparison.Ordinal);
        Assert.Contains(finding.Actions, a => a is KeepAction);
        Assert.Contains(finding.Actions, a => a is ApplyAction);
        Assert.Contains(finding.Actions, a => a is ShowDetailsAction);
        Assert.DoesNotContain(finding.Actions, a => a is OpenSettingsAction);
        Assert.NotEmpty(finding.Measured);
    }

    /// <summary>두 속도가 같으면 Info '보고 속도와 설정 속도 일치'.</summary>
    [Fact]
    public void 두_속도가_같으면_정보를_낸다()
    {
        var snapshot = RuleTestData.MemorySnapshot(ProbeStatus.Success, new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, REPORTED_SPEED));

        var finding = Assert.Single(ModuleFindings(RULE.Evaluate(snapshot)));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Null(finding.Recommendation);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, CoreStrings.Memory_Title_Match, "DIMM A (BANK 0)"),
            finding.Title);
    }

    /// <summary>설정 속도가 더 높게 보고되면 후보가 아니라 Info로만 남긴다.</summary>
    [Fact]
    public void 설정_속도가_더_높으면_후보가_아닌_정보다()
    {
        var snapshot = RuleTestData.MemorySnapshot(ProbeStatus.Success, new FakeModule("DIMM A", null, REPORTED_SPEED, HIGHER_CONFIGURED_SPEED));

        var finding = Assert.Single(ModuleFindings(RULE.Evaluate(snapshot)));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Null(finding.Recommendation);
    }

    /// <summary>한쪽 값이 없으면 CannotVerify(PartialData).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 값이_없으면_부분_데이터로_확인_불가다(bool missingSpeed)
    {
        var module = missingSpeed
            ? new FakeModule("DIMM A", "BANK 0", null, REPORTED_SPEED)
            : new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, null);

        var finding = Assert.Single(ModuleFindings(RULE.Evaluate(RuleTestData.MemorySnapshot(ProbeStatus.Partial, module))));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>0(알 수 없음)으로 보고되면 CannotVerify(Unsupported).</summary>
    [Theory]
    [InlineData(0L, REPORTED_SPEED)]
    [InlineData(REPORTED_SPEED, 0L)]
    public void 영으로_보고되면_지원_안_함으로_확인_불가다(long speed, long configured)
    {
        var snapshot = RuleTestData.MemorySnapshot(ProbeStatus.Success, new FakeModule("DIMM A", "BANK 0", speed, configured));

        var finding = Assert.Single(ModuleFindings(RULE.Evaluate(snapshot)));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, finding.CannotVerifyReason);
    }

    /// <summary>단위가 없거나 서로 다르면 비교하지 않고 CannotVerify(Unsupported).</summary>
    [Theory]
    [InlineData(null, RuleTestData.UNIT)]
    [InlineData(RuleTestData.UNIT, null)]
    [InlineData(MemoryProbeContract.UNIT_MEGAHERTZ, MemoryProbeContract.UNIT_MEGATRANSFERS)]
    public void 단위가_불명확하면_비교하지_않는다(string? speedUnit, string? configuredUnit)
    {
        var module = new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, LOWER_CONFIGURED_SPEED, speedUnit, configuredUnit);

        var finding = Assert.Single(ModuleFindings(RULE.Evaluate(RuleTestData.MemorySnapshot(ProbeStatus.Success, module))));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, finding.CannotVerifyReason);
    }

    /// <summary>XMP/EXPO 활성 여부는 별도 CannotVerify(Unsupported) 하나로 표시한다.</summary>
    [Fact]
    public void 프로필_활성_여부는_별도_확인_불가_하나다()
    {
        var snapshot = RuleTestData.MemorySnapshot(
            ProbeStatus.Success,
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, REPORTED_SPEED),
            new FakeModule("DIMM B", "BANK 1", REPORTED_SPEED, REPORTED_SPEED));

        var profile = Assert.Single(RULE.Evaluate(snapshot), f => f.Id == MemorySpeedRule.PROFILE_FINDING_ID);

        Assert.Equal(Verdict.CannotVerify, profile.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, profile.CannotVerifyReason);
        Assert.Equal(FindingCategory.Memory, profile.Category);
    }

    /// <summary>모듈마다 위치 기반의 서로 다른 ID를 쓴다(표시 순번을 ID로 쓰지 않음).</summary>
    [Fact]
    public void 모듈마다_위치_기반_ID를_쓴다()
    {
        var snapshot = RuleTestData.MemorySnapshot(
            ProbeStatus.Success,
            new FakeModule("DIMM A", "BANK 0", REPORTED_SPEED, REPORTED_SPEED),
            new FakeModule("DIMM B", "BANK 1", REPORTED_SPEED, LOWER_CONFIGURED_SPEED));

        var findings = ModuleFindings(RULE.Evaluate(snapshot));

        Assert.Equal(2, findings.Count);
        Assert.Equal(2, findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(findings, f => f.Id.Contains("DIMM A", StringComparison.Ordinal) && f.Verdict == Verdict.Info);
        Assert.Contains(findings, f => f.Id.Contains("DIMM B", StringComparison.Ordinal) && f.Verdict == Verdict.Candidate);
    }

    /// <summary>수집이 실패·건너뜀·취소되었거나 결과가 없으면 규칙은 Finding을 만들지 않는다(상태 변환기가 CannotVerify를 만든다).</summary>
    [Theory]
    [InlineData(ProbeStatus.Failed)]
    [InlineData(ProbeStatus.Skipped)]
    [InlineData(ProbeStatus.Cancelled)]
    public void 수집이_끝나지_않았으면_판정하지_않는다(ProbeStatus status)
    {
        Assert.Empty(RULE.Evaluate(RuleTestData.MemorySnapshot(status)));
        Assert.Empty(RULE.Evaluate(new ScanSnapshot(Guid.NewGuid(), [])));
    }

    /// <summary>어느 분기에서도 금지 문구가 나오지 않는다.</summary>
    [Theory]
    [MemberData(nameof(AllBranches))]
    public void 어느_분기에서도_금지_문구가_없다(FakeModule module)
    {
        var findings = RULE.Evaluate(RuleTestData.MemorySnapshot(ProbeStatus.Success, module));

        Assert.NotEmpty(findings);
        foreach (var finding in findings)
        {
            var text = RuleTestData.AllText(finding);
            foreach (var phrase in RuleTestData.FORBIDDEN_MEMORY_PHRASES)
            {
                Assert.DoesNotContain(phrase, text, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>후보 필수 문장 리소스가 스펙 §5 메모리 행의 문장과 정확히 같다(스펙 계약).</summary>
    [Fact]
    public void 필수_문장_리소스가_스펙_문장과_같다()
    {
        Assert.Equal(
            "CPU·메인보드·메모리 구성에 따른 정상 제한일 수 있으며, XMP/EXPO 활성 여부는 확인되지 않았습니다",
            CoreStrings.Memory_CandidateCaveat);
    }
}
