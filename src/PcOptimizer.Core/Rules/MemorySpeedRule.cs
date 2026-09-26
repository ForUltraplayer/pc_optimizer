/**
 * @file    : MemorySpeedRule.cs
 * @author  : rudals252
 * @brief   : 모듈별 SMBIOS 보고 속도(Speed)와 설정 속도(ConfiguredClockSpeed)를 비교하는 순수 판정 규칙(조건부 후보/일치 정보/확인 불가)
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 메모리 속도 규칙입니다(스펙 §5 메모리 행).
/// <list type="bullet">
/// <item>유효하고 비교 가능한 설정 속도 &lt; 보고 속도: Candidate '메모리 속도 설정 확인 가능' + 필수 문장(정상 제한 가능성, XMP/EXPO 미확인).</item>
/// <item>두 속도 일치: Info '보고 속도와 설정 속도 일치'.</item>
/// <item>설정 속도 &gt; 보고 속도: 판정하지 않는 Info(스펙에 정의되지 않은 경우이므로 후보로 만들지 않음).</item>
/// <item>값 누락: CannotVerify(PartialData). 0(알 수 없음)·단위 불명확: CannotVerify(Unsupported).</item>
/// <item>XMP/EXPO 활성 여부는 알 수 없으므로 별도 CannotVerify(Unsupported) 하나.</item>
/// </list>
/// 보고 속도를 광고상 정격·보장 속도로 표현하지 않으며, XMP 활성화를 무조건 권하지 않습니다.
/// 수집이 끝나지 않은(실패·건너뜀·취소) 결과는 상태 변환기가 CannotVerify로 드러내므로 여기서는 판정하지 않습니다.
/// </summary>
public sealed class MemorySpeedRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "memory.speed";

    /// <summary>모듈 판정 Finding ID 접두사(뒤에 모듈 위치 식별자가 붙음).</summary>
    public const string MODULE_FINDING_ID_PREFIX = "memory-speed:";

    /// <summary>XMP/EXPO 활성 여부 확인 불가 Finding ID.</summary>
    public const string PROFILE_FINDING_ID = "memory-profile:xmp-expo";

    private const string LOCATOR_SEPARATOR = "|";
    private const string INDEX_ID_FORMAT = "index-{0}";
    private const int DISPLAY_INDEX_OFFSET = 1;

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(MemoryProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var findings = new List<Finding>();
        var moduleCount = snapshot.GetMeasurement(MemoryProbeContract.PROBE_ID, MemoryProbeContract.MODULE_COUNT)?.Value is IntegerValue count
            ? count.Value
            : 0;

        for (var index = 0; index < moduleCount; index++)
        {
            findings.Add(EvaluateModule(snapshot, result, index));
        }

        findings.Add(CreateProfileUnknownFinding());
        return findings;
    }

    /// <summary>
    /// 모듈 하나의 보고 속도와 설정 속도를 비교한다.
    /// </summary>
    private static Finding EvaluateModule(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        var prefix = MemoryProbeContract.ModuleMeasurementPrefix(index);
        Measurement[] measured = [.. result.Measurements.Where(m => m.Name.StartsWith(prefix, StringComparison.Ordinal))];

        var locator = TextOf(snapshot, index, MemoryProbeContract.FIELD_DEVICE_LOCATOR);
        var bank = TextOf(snapshot, index, MemoryProbeContract.FIELD_BANK_LABEL);
        var id = MODULE_FINDING_ID_PREFIX + ModuleIdentity(locator, bank, index);
        var label = ModuleLabel(locator, bank, index);

        var speed = snapshot.GetMeasurement(MemoryProbeContract.PROBE_ID, MemoryProbeContract.ModuleMeasurementName(index, MemoryProbeContract.FIELD_SPEED));
        var configured = snapshot.GetMeasurement(
            MemoryProbeContract.PROBE_ID, MemoryProbeContract.ModuleMeasurementName(index, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED));

        if (speed?.Value is not IntegerValue speedValue || configured?.Value is not IntegerValue configuredValue)
        {
            return CreateCannotCompare(id, label, measured, CannotVerifyReason.PartialData, CoreStrings.Memory_Evidence_Missing);
        }

        if (speedValue.Value <= 0 || configuredValue.Value <= 0)
        {
            return CreateCannotCompare(id, label, measured, CannotVerifyReason.Unsupported, CoreStrings.Memory_Evidence_Zero);
        }

        if (string.IsNullOrWhiteSpace(speed.Unit)
            || string.IsNullOrWhiteSpace(configured.Unit)
            || !string.Equals(speed.Unit, configured.Unit, StringComparison.Ordinal))
        {
            return CreateCannotCompare(id, label, measured, CannotVerifyReason.Unsupported, CoreStrings.Memory_Evidence_UnitUnclear);
        }

        var unit = speed.Unit;
        if (configuredValue.Value < speedValue.Value)
        {
            return CreateCandidate(id, label, measured, speedValue.Value, configuredValue.Value, unit);
        }

        if (configuredValue.Value == speedValue.Value)
        {
            return CreateInfo(
                id,
                Format(CoreStrings.Memory_Title_Match, label),
                measured,
                Format(CoreStrings.Memory_Evidence_Match, speedValue.Value, unit));
        }

        return CreateInfo(
            id,
            Format(CoreStrings.Memory_Title_ConfiguredHigher, label),
            measured,
            Format(CoreStrings.Memory_Evidence_ConfiguredHigher, configuredValue.Value, speedValue.Value, unit));
    }

    /// <summary>
    /// 설정 속도가 더 낮게 보고된 모듈의 조건부 Candidate를 만든다. 근거에 스펙 필수 문장을 붙인다.
    /// </summary>
    private static Finding CreateCandidate(string id, string label, Measurement[] measured, long speed, long configured, string unit)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Memory,
            title: Format(CoreStrings.Memory_Title_Candidate, label),
            measured: measured,
            evidence: Format(CoreStrings.Memory_Evidence_Candidate, configured, speed, unit, CoreStrings.Memory_CandidateCaveat),
            verdict: Verdict.Candidate,
            cannotVerifyReason: null,
            detail: null,
            recommendation: new Recommendation(CoreStrings.Memory_Recommendation_Text, CoreStrings.Memory_Recommendation_Condition),
            impact: new Impact(CoreStrings.Memory_Impact_Benefit, CoreStrings.Memory_Impact_SideEffect),
            actions: [new ShowDetailsAction(), new KeepAction(), new ApplyAction()]);
    }

    /// <summary>
    /// 판정 없이 사실만 전하는 Info를 만든다.
    /// </summary>
    private static Finding CreateInfo(string id, string title, Measurement[] measured, string evidence)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Memory,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }

    /// <summary>
    /// 두 속도를 비교할 수 없는 모듈의 CannotVerify를 만든다.
    /// </summary>
    private static Finding CreateCannotCompare(
        string id,
        string label,
        Measurement[] measured,
        CannotVerifyReason reason,
        string evidence)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Memory,
            title: Format(CoreStrings.Memory_Title_CannotCompare, label),
            measured: measured,
            evidence: evidence,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: reason,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }

    /// <summary>
    /// XMP/EXPO 활성 여부를 확인할 수 없음을 알리는 CannotVerify(Unsupported)를 만든다.
    /// </summary>
    private static Finding CreateProfileUnknownFinding()
    {
        return new Finding(
            id: PROFILE_FINDING_ID,
            category: FindingCategory.Memory,
            title: CoreStrings.Memory_Title_ProfileUnknown,
            measured: [],
            evidence: CoreStrings.Memory_Evidence_ProfileUnknown,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: CannotVerifyReason.Unsupported,
            detail: null,
            recommendation: null,
            impact: null,
            actions: []);
    }

    /// <summary>
    /// 모듈 필드의 문자열 값을 읽는다. 없거나 공백이면 null.
    /// </summary>
    private static string? TextOf(ScanSnapshot snapshot, int index, string field)
    {
        var measurement = snapshot.GetMeasurement(MemoryProbeContract.PROBE_ID, MemoryProbeContract.ModuleMeasurementName(index, field));
        return measurement?.Value is TextValue text && !string.IsNullOrWhiteSpace(text.Value) ? text.Value.Trim() : null;
    }

    /// <summary>
    /// 모듈의 내부 식별자를 만든다. 위치 정보가 있으면 그것을, 없을 때만 인덱스를 쓴다.
    /// </summary>
    private static string ModuleIdentity(string? locator, string? bank, int index)
    {
        if (locator is null && bank is null)
        {
            return string.Format(CultureInfo.InvariantCulture, INDEX_ID_FORMAT, index);
        }

        return (locator ?? string.Empty) + LOCATOR_SEPARATOR + (bank ?? string.Empty);
    }

    /// <summary>
    /// 사용자에게 보여 줄 모듈 이름을 만든다(예: "DIMM 1 (P0 CHANNEL A)").
    /// </summary>
    private static string ModuleLabel(string? locator, string? bank, int index)
    {
        return (locator, bank) switch
        {
            (not null, not null) => $"{locator} ({bank})",
            (not null, null) => locator,
            (null, not null) => bank,
            _ => $"{CoreStrings.Memory_ModuleFallbackName} {(index + DISPLAY_INDEX_OFFSET).ToString(CultureInfo.CurrentCulture)}",
        };
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채운다.
    /// </summary>
    private static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
