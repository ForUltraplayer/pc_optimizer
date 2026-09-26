/**
 * @file    : PowerPlanRuleTests.cs
 * @author  : rudals252
 * @brief   : 전원 계획 규칙의 정보/조건부 후보/확인 불가 분기(노트북·배터리·섀시 불명·배터리 없음)와 금지 문구 부재 검증
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="PowerPlanRule"/>이 스펙 §5 전원 행을 따르는지 검증합니다. 모든 값은 테스트용 예시입니다.
/// </summary>
public sealed class PowerPlanRuleTests
{
    private const string PLAN_NAME = "테스트 계획";
    private const string BALANCED_GUID = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string LAPTOP = "9";
    private const string NOTEBOOK = "10";
    private const string DESKTOP = "3";
    private const string UNKNOWN_CHASSIS = "2";
    private const long AC_OFFLINE = PowerProbeContract.AC_LINE_OFFLINE;
    private const long AC_ONLINE = PowerProbeContract.AC_LINE_ONLINE;
    private const long BATTERY_HIGH = 1;
    private const long NO_BATTERY = PowerProbeContract.BATTERY_FLAG_NO_SYSTEM_BATTERY;

    private static readonly PowerPlanRule RULE = new();

    /// <summary>
    /// 전원 규칙의 모든 분기 입력(금지 문구 검사용).
    /// </summary>
    public static TheoryData<string?, string[]?, long?, long?> AllBranches()
    {
        return new TheoryData<string?, string[]?, long?, long?>
        {
            { PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, [LAPTOP], AC_OFFLINE, BATTERY_HIGH },
            { PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, [DESKTOP], AC_ONLINE, NO_BATTERY },
            { PowerProbeContract.ULTIMATE_PERFORMANCE_SCHEME_GUID, null, null, null },
            { BALANCED_GUID, [UNKNOWN_CHASSIS], AC_ONLINE, BATTERY_HIGH },
            { null, [LAPTOP], AC_OFFLINE, BATTERY_HIGH },
        };
    }

    /// <summary>활성 계획 이름을 담은 Info를 내고 전원 설정 열기를 제공한다.</summary>
    [Fact]
    public void 활성_계획을_정보로_표시한다()
    {
        var snapshot = RuleTestData.PowerSnapshot(BALANCED_GUID, PLAN_NAME, [DESKTOP], AC_ONLINE, NO_BATTERY);

        var finding = Assert.Single(RULE.Evaluate(snapshot));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.Power, finding.Category);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, CoreStrings.Power_Title_Plan, PLAN_NAME), finding.Title);
        Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: PowerPlanRule.POWER_SETTINGS_URI });
        Assert.Contains(CoreStrings.Power_Chassis_Desktop, finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>섀시가 불명·누락·모순이면 '알 수 없음'으로 표시하고 데스크톱으로 추정하지 않는다.</summary>
    [Theory]
    [InlineData(UNKNOWN_CHASSIS)]
    [InlineData(LAPTOP + "," + DESKTOP)]
    [InlineData(null)]
    public void 섀시_불명은_알_수_없음으로_표시한다(string? chassisCodes)
    {
        var chassis = chassisCodes?.Split(',');
        var snapshot = RuleTestData.PowerSnapshot(BALANCED_GUID, PLAN_NAME, chassis, AC_ONLINE, NO_BATTERY);

        var finding = Assert.Single(RULE.Evaluate(snapshot));

        Assert.Contains(CoreStrings.Power_Chassis_Unknown, finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(CoreStrings.Power_Chassis_Desktop, finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>노트북 섀시 + 배터리 있음 + 배터리로 동작 + 고성능/최고 성능 계획이면 조건부 Candidate.</summary>
    [Theory]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, LAPTOP)]
    [InlineData(PowerProbeContract.ULTIMATE_PERFORMANCE_SCHEME_GUID, NOTEBOOK)]
    public void 노트북이_배터리로_고성능이면_조건부_후보다(string schemeGuid, string chassis)
    {
        var snapshot = RuleTestData.PowerSnapshot(schemeGuid, PLAN_NAME, [chassis], AC_OFFLINE, BATTERY_HIGH);

        var findings = RULE.Evaluate(snapshot);

        var candidate = Assert.Single(findings, f => f.Verdict == Verdict.Candidate);
        Assert.NotNull(candidate.Recommendation);
        Assert.False(string.IsNullOrWhiteSpace(candidate.Recommendation.Condition));
        Assert.NotNull(candidate.Impact);
        Assert.Contains(candidate.Actions, a => a is OpenSettingsAction { Uri: PowerPlanRule.POWER_SETTINGS_URI });
        Assert.Contains(candidate.Actions, a => a is KeepAction);
        Assert.Contains(candidate.Actions, a => a is ApplyAction);
        Assert.Single(findings, f => f.Verdict == Verdict.Info);
    }

    /// <summary>후보 조건 중 하나라도 빠지면 Candidate를 내지 않는다(배터리 없는 시스템은 노트북이 아님, UPS 데스크톱 포함).</summary>
    [Theory]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, LAPTOP, AC_ONLINE, BATTERY_HIGH)]
    [InlineData(BALANCED_GUID, LAPTOP, AC_OFFLINE, BATTERY_HIGH)]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, DESKTOP, AC_OFFLINE, BATTERY_HIGH)]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, UNKNOWN_CHASSIS, AC_OFFLINE, BATTERY_HIGH)]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, LAPTOP, AC_OFFLINE, NO_BATTERY)]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, LAPTOP, AC_OFFLINE, PowerProbeContract.BATTERY_FLAG_UNKNOWN)]
    [InlineData(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, LAPTOP, PowerProbeContract.AC_LINE_UNKNOWN, BATTERY_HIGH)]
    public void 조건이_하나라도_빠지면_후보가_아니다(string schemeGuid, string chassis, long acLine, long batteryFlag)
    {
        var snapshot = RuleTestData.PowerSnapshot(schemeGuid, PLAN_NAME, [chassis], acLine, batteryFlag);

        var findings = RULE.Evaluate(snapshot);

        Assert.DoesNotContain(findings, f => f.Verdict == Verdict.Candidate);
        Assert.Single(findings, f => f.Verdict == Verdict.Info);
    }

    /// <summary>배터리 상태를 읽지 못하면 후보를 내지 않는다(false로 대체하지 않음).</summary>
    [Fact]
    public void 배터리_상태를_모르면_후보가_아니다()
    {
        var snapshot = RuleTestData.PowerSnapshot(PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID, PLAN_NAME, [LAPTOP], null, null, ProbeStatus.Partial);

        var findings = RULE.Evaluate(snapshot);

        Assert.DoesNotContain(findings, f => f.Verdict == Verdict.Candidate);
        var info = Assert.Single(findings);
        Assert.Contains(CoreStrings.Power_Supply_Unknown, info.Evidence, StringComparison.Ordinal);
    }

    /// <summary>활성 계획을 읽지 못하면 CannotVerify(PartialData).</summary>
    [Fact]
    public void 활성_계획이_없으면_확인_불가다()
    {
        var snapshot = RuleTestData.PowerSnapshot(null, null, [LAPTOP], AC_OFFLINE, BATTERY_HIGH, ProbeStatus.Partial);

        var finding = Assert.Single(RULE.Evaluate(snapshot));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>이름을 못 읽었어도 GUID가 있으면 대체 이름으로 Info를 낸다.</summary>
    [Fact]
    public void 이름이_없으면_대체_이름을_쓴다()
    {
        var snapshot = RuleTestData.PowerSnapshot(BALANCED_GUID, null, [DESKTOP], AC_ONLINE, NO_BATTERY, ProbeStatus.Partial);

        var finding = Assert.Single(RULE.Evaluate(snapshot));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, CoreStrings.Power_Title_Plan, CoreStrings.Power_PlanNameUnknown),
            finding.Title);
    }

    /// <summary>수집이 실패·건너뜀·취소되었으면 규칙은 판정하지 않는다.</summary>
    [Theory]
    [InlineData(ProbeStatus.Failed)]
    [InlineData(ProbeStatus.Skipped)]
    [InlineData(ProbeStatus.Cancelled)]
    public void 수집이_끝나지_않았으면_판정하지_않는다(ProbeStatus status)
    {
        Assert.Empty(RULE.Evaluate(RuleTestData.PowerSnapshot(null, null, null, null, null, status)));
        Assert.Empty(RULE.Evaluate(new ScanSnapshot(Guid.NewGuid(), [])));
    }

    /// <summary>어느 분기에서도 고성능을 정상/최적으로 단정하는 문구가 없다.</summary>
    [Theory]
    [MemberData(nameof(AllBranches))]
    public void 어느_분기에서도_정상_최적_단정이_없다(string? schemeGuid, string[]? chassis, long? acLine, long? batteryFlag)
    {
        var findings = RULE.Evaluate(RuleTestData.PowerSnapshot(schemeGuid, PLAN_NAME, chassis, acLine, batteryFlag));

        Assert.NotEmpty(findings);
        foreach (var finding in findings)
        {
            var text = RuleTestData.AllText(finding);
            foreach (var phrase in RuleTestData.FORBIDDEN_POWER_PHRASES)
            {
                Assert.DoesNotContain(phrase, text, StringComparison.Ordinal);
            }
        }
    }
}
