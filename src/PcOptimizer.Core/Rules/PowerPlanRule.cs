/**
 * @file    : PowerPlanRule.cs
 * @author  : rudals252
 * @brief   : 활성 전원 계획·섀시 종류·AC/배터리 상태로 정보와 (노트북·배터리 동작·고성능 계열일 때만) 조건부 후보를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 전원 계획 규칙입니다(스펙 §5 전원 행).
/// <list type="bullet">
/// <item>활성 계획은 Info로만 알립니다. 고성능을 정상/최적으로 단정하지 않습니다.</item>
/// <item>섀시가 불명·누락·모순이면 '알 수 없음'이며 데스크톱으로 추정하지 않습니다.</item>
/// <item>노트북형 섀시 AND 시스템 배터리 있음 AND 배터리로 동작 AND 고성능/최고의 성능 계획일 때만 조건부 Candidate.</item>
/// <item>배터리가 있다는 사실만으로(예: UPS) 노트북으로 보지 않고, 배터리가 없는 시스템은 노트북 조건을 만족하지 않습니다.</item>
/// <item>활성 계획을 읽지 못하면 CannotVerify(PartialData).</item>
/// </list>
/// </summary>
public sealed class PowerPlanRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "power.plan";

    /// <summary>활성 계획 Finding ID.</summary>
    public const string PLAN_FINDING_ID = "power-plan:active";

    /// <summary>배터리 동작 중 고성능 계열 계획 후보 Finding ID.</summary>
    public const string BATTERY_HIGH_PERFORMANCE_FINDING_ID = "power-plan:battery-high-performance";

    /// <summary>Windows 전원 및 절전 설정 URI.</summary>
    public const string POWER_SETTINGS_URI = "ms-settings:powersleep";

    /// <summary>고성능 계열로 보는 기본 제공 계획 GUID.</summary>
    private static readonly HashSet<string> HIGH_PERFORMANCE_SCHEME_GUIDS = new(StringComparer.OrdinalIgnoreCase)
    {
        PowerProbeContract.HIGH_PERFORMANCE_SCHEME_GUID,
        PowerProbeContract.ULTIMATE_PERFORMANCE_SCHEME_GUID,
    };

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(PowerProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var schemeGuid = TextOf(snapshot, PowerProbeContract.ACTIVE_SCHEME_GUID);
        var schemeName = TextOf(snapshot, PowerProbeContract.ACTIVE_SCHEME_NAME);
        var chassis = ChassisClassifier.Classify(snapshot.GetMeasurement(PowerProbeContract.PROBE_ID, PowerProbeContract.CHASSIS_TYPES));
        var supply = PowerSupplyClassifier.Classify(IntegerOf(snapshot, PowerProbeContract.AC_LINE_STATUS));
        var chassisText = ChassisClassifier.DisplayName(chassis);
        var supplyText = PowerSupplyClassifier.DisplayName(supply);
        Measurement[] measured = [.. result.Measurements];

        if (schemeGuid is null)
        {
            return
            [
                new Finding(
                    id: PLAN_FINDING_ID,
                    category: FindingCategory.Power,
                    title: CoreStrings.Power_Title_PlanUnknown,
                    measured: measured,
                    evidence: Format(CoreStrings.Power_Evidence_PlanUnknown, chassisText, supplyText),
                    verdict: Verdict.CannotVerify,
                    cannotVerifyReason: CannotVerifyReason.PartialData,
                    detail: null,
                    recommendation: null,
                    impact: null,
                    actions: [new ShowDetailsAction(), new OpenSettingsAction(POWER_SETTINGS_URI)]),
            ];
        }

        var planName = schemeName ?? CoreStrings.Power_PlanNameUnknown;
        var findings = new List<Finding>
        {
            new(
                id: PLAN_FINDING_ID,
                category: FindingCategory.Power,
                title: Format(CoreStrings.Power_Title_Plan, planName),
                measured: measured,
                evidence: Format(CoreStrings.Power_Evidence_Plan, chassisText, supplyText),
                verdict: Verdict.Info,
                cannotVerifyReason: null,
                detail: null,
                recommendation: null,
                impact: null,
                actions: [new ShowDetailsAction(), new OpenSettingsAction(POWER_SETTINGS_URI)]),
        };

        if (chassis == ChassisKind.Laptop
            && supply == PowerSupplyKind.Battery
            && HasSystemBattery(IntegerOf(snapshot, PowerProbeContract.BATTERY_FLAG))
            && HIGH_PERFORMANCE_SCHEME_GUIDS.Contains(schemeGuid))
        {
            findings.Add(new Finding(
                id: BATTERY_HIGH_PERFORMANCE_FINDING_ID,
                category: FindingCategory.Power,
                title: CoreStrings.Power_Title_BatteryHighPerformance,
                measured: measured,
                evidence: Format(CoreStrings.Power_Evidence_BatteryHighPerformance, planName),
                verdict: Verdict.Candidate,
                cannotVerifyReason: null,
                detail: null,
                recommendation: new Recommendation(CoreStrings.Power_Recommendation_Text, CoreStrings.Power_Recommendation_Condition),
                impact: new Impact(CoreStrings.Power_Impact_Benefit, CoreStrings.Power_Impact_SideEffect),
                actions: [new ShowDetailsAction(), new OpenSettingsAction(POWER_SETTINGS_URI), new KeepAction(), new ApplyAction()]));
        }

        return findings;
    }

    /// <summary>
    /// BatteryFlag 원시 값으로 시스템 배터리가 있는지 판단한다. 값이 없거나 "배터리 없음"·"알 수 없음"이면 false.
    /// </summary>
    private static bool HasSystemBattery(long? batteryFlag)
    {
        return batteryFlag is { } flag
            && flag != PowerProbeContract.BATTERY_FLAG_NO_SYSTEM_BATTERY
            && flag != PowerProbeContract.BATTERY_FLAG_UNKNOWN;
    }

    /// <summary>
    /// 문자열 측정값을 읽는다. 없거나 공백이면 null.
    /// </summary>
    private static string? TextOf(ScanSnapshot snapshot, string name)
    {
        return snapshot.GetMeasurement(PowerProbeContract.PROBE_ID, name)?.Value is TextValue text && !string.IsNullOrWhiteSpace(text.Value)
            ? text.Value.Trim()
            : null;
    }

    /// <summary>
    /// 정수 측정값을 읽는다. 없으면 null(0과 구분).
    /// </summary>
    private static long? IntegerOf(ScanSnapshot snapshot, string name)
    {
        return snapshot.GetMeasurement(PowerProbeContract.PROBE_ID, name)?.Value is IntegerValue value ? value.Value : null;
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채운다.
    /// </summary>
    private static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
