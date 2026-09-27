/**
 * @file    : DisplayRefreshRule.cs
 * @author  : rudals252
 * @brief   : 활성 디스플레이별 현재 모드와 같은 조건(해상도·방향·색 깊이·순차 주사) 보고 모드를 비교해 조건부 후보/현재 모드 정보/모호·부분 데이터 확인 불가를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 디스플레이 주사율 규칙입니다(스펙 §5 디스플레이 행).
/// <list type="bullet">
/// <item>같은 해상도·방향·색 깊이·순차 주사에서 현재보다 1Hz 넘게 높은 보고 모드가 있으면 Candidate '더 높은 주사율 후보'.</item>
/// <item>59.94/60처럼 1Hz 이하 차이는 개선으로 분류하지 않고 Info '현재 모드'.</item>
/// <item>원격 세션·복제 표시·가상(비PCI) 어댑터·인터레이스 현재 모드·신호/모드 주사율 불일치(DRR 등)는 CannotVerify(Ambiguous), 사유는 Detail에.</item>
/// <item>현재 모드나 같은 조건 모드 목록이 없으면 CannotVerify(PartialData).</item>
/// </list>
/// 후보 조건에는 HDR·색상 확인을, 노트북형 섀시면 '전원 연결 시' 확인을 붙이며, 전원 공급 상태를 근거에 함께 보여 줍니다.
/// 드라이버가 보고한 모드일 뿐이므로 구동 가능·안정성을 확정하는 표현을 쓰지 않습니다.
/// Finding ID는 모니터 장치 경로(없으면 어댑터 경로 + 대상 ID)이며 GDI 순번("DISPLAY3")은 표시용으로만 씁니다.
/// </summary>
public sealed class DisplayRefreshRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "display.refresh";

    /// <summary>대상별 Finding ID 접두사(뒤에 모니터 장치 경로가 붙음).</summary>
    public const string FINDING_ID_PREFIX = "display-refresh:";

    /// <summary>디스플레이 설정 URI.</summary>
    public const string DISPLAY_SETTINGS_URI = "ms-settings:display";

    private const string FALLBACK_ID_FORMAT = "adapter:{0}|target:{1}";
    private const string INDEX_ID_FORMAT = "index-{0}";
    private const string GDI_PREFIX = @"\\.\";
    private const string VIRTUAL_ADAPTER_KEYWORD = "Virtual";
    private const string PCI_DEVICE_PATH_MARKER = "PCI#VEN_";
    private const string RATE_SEPARATOR = ", ";
    private const string DETAIL_SEPARATOR = " / ";
    private const string CONDITION_SEPARATOR = " ";
    private const string HZ_SUFFIX = "Hz";
    private const string HZ_FORMAT = "0.##";
    private const int DISPLAY_INDEX_OFFSET = 1;

    /// <summary>표시가 끊기거나 원복을 확인하지 못할 수 있는 설정 변경입니다.</summary>
    private static readonly SafetyLevel CANDIDATE_SAFETY = SafetyLevel.Caution;

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(DisplayProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var count = SnapshotValues.Integer(snapshot, DisplayProbeContract.PROBE_ID, DisplayProbeContract.TARGET_COUNT) ?? 0;
        var context = PowerContext.From(snapshot);
        var remote = SnapshotValues.Boolean(snapshot, DisplayProbeContract.PROBE_ID, DisplayProbeContract.REMOTE_SESSION) == true;
        var remoteMeasurement = snapshot.GetMeasurement(DisplayProbeContract.PROBE_ID, DisplayProbeContract.REMOTE_SESSION);

        var findings = new List<Finding>();
        for (var index = 0; index < count; index++)
        {
            findings.Add(EvaluateTarget(snapshot, result, index, remote, remoteMeasurement, context));
        }

        return findings;
    }

    /// <summary>
    /// 대상 하나를 판정한다.
    /// </summary>
    private static Finding EvaluateTarget(
        ScanSnapshot snapshot,
        ProbeResult result,
        int index,
        bool remote,
        Measurement? remoteMeasurement,
        PowerContext power)
    {
        var target = TargetValues.Read(snapshot, index);
        var measured = new List<Measurement>(SnapshotValues.WithPrefix(result, DisplayProbeContract.TargetMeasurementPrefix(index)));
        if (remoteMeasurement is not null)
        {
            measured.Add(remoteMeasurement);
        }

        measured.AddRange(power.Measurements);

        var id = FINDING_ID_PREFIX + target.Identity(index);
        var label = target.Label(index);

        if (target.Width is not { } width || target.Height is not { } height || target.RefreshHz is not { } currentHz || currentHz <= 1 || target.SameModeRates is null)
        {
            return Create(
                id,
                Format(CoreStrings.Display_Title_Partial, label),
                measured,
                CoreStrings.Display_Evidence_ModeMissing,
                Verdict.CannotVerify,
                CannotVerifyReason.PartialData,
                detail: null,
                recommendation: null,
                impact: null,
                candidate: false);
        }

        var maxText = target.MaxSameModeHz is { } knownMax ? knownMax.ToString(CultureInfo.CurrentCulture) + HZ_SUFFIX : CoreStrings.Display_ValueUnknown;
        var ambiguity = AmbiguityReasons(target, remote);
        if (ambiguity.Count > 0)
        {
            return Create(
                id,
                Format(CoreStrings.Display_Title_Ambiguous, label),
                measured,
                Format(CoreStrings.Display_Evidence_Ambiguous, width, height, currentHz, maxText, power.SupplyText),
                Verdict.CannotVerify,
                CannotVerifyReason.Ambiguous,
                string.Join(DETAIL_SEPARATOR, ambiguity),
                recommendation: null,
                impact: null,
                candidate: false);
        }

        if (target.MaxSameModeHz is { } maxHz && DisplayModeMatcher.IsMeaningfullyHigher(maxHz, currentHz))
        {
            var condition = power.Chassis == ChassisKind.Laptop
                ? CoreStrings.Display_Recommendation_Condition + CONDITION_SEPARATOR + CoreStrings.Display_Recommendation_LaptopCondition
                : CoreStrings.Display_Recommendation_Condition;
            return Create(
                id,
                Format(CoreStrings.Display_Title_Candidate, label),
                measured,
                Format(CoreStrings.Display_Evidence_Candidate, width, height, currentHz, maxHz, string.Join(RATE_SEPARATOR, target.SameModeRates), power.SupplyText),
                Verdict.Candidate,
                cannotVerifyReason: null,
                detail: null,
                new Recommendation(Format(CoreStrings.Display_Recommendation_Text, maxHz), condition),
                new Impact(CoreStrings.Display_Impact_Benefit, CoreStrings.Display_Impact_SideEffect),
                candidate: true);
        }

        return Create(
            id,
            Format(CoreStrings.Display_Title_Current, label, width, height, currentHz),
            measured,
            Format(CoreStrings.Display_Evidence_Current, maxText, power.SupplyText),
            Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            candidate: false);
    }

    /// <summary>
    /// 비교 결과를 모호하게 만드는 표시 상태를 모은다(원격·복제·가상 어댑터·인터레이스·신호/모드 주사율 불일치).
    /// </summary>
    private static List<string> AmbiguityReasons(TargetValues target, bool remote)
    {
        var reasons = new List<string>();
        if (remote)
        {
            reasons.Add(CoreStrings.Display_Detail_Remote);
        }

        if (target.Cloned == true)
        {
            reasons.Add(CoreStrings.Display_Detail_Cloned);
        }

        if (target.IsVirtualAdapter())
        {
            reasons.Add(Format(CoreStrings.Display_Detail_Virtual, target.AdapterName ?? CoreStrings.Display_AdapterUnknown));
        }

        if (target.Interlaced == true)
        {
            reasons.Add(CoreStrings.Display_Detail_Interlaced);
        }

        if (target.SignalHz is { } signal
            && target.RefreshHz is { } refresh
            && Math.Abs(signal - refresh) > DisplayModeMatcher.REFRESH_TOLERANCE_HZ)
        {
            reasons.Add(Format(
                CoreStrings.Display_Detail_SignalMismatch,
                signal.ToString(HZ_FORMAT, CultureInfo.CurrentCulture),
                refresh.ToString(CultureInfo.CurrentCulture)));
        }

        return reasons;
    }

    /// <summary>
    /// 디스플레이 Finding을 만든다. 모든 판정에 상세 보기와 디스플레이 설정 열기를, 후보에는 유지·적용(비활성)을 붙인다.
    /// </summary>
    private static Finding Create(
        string id,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason,
        string? detail,
        Recommendation? recommendation,
        Impact? impact,
        bool candidate)
    {
        IReadOnlyList<FindingAction> actions = candidate
            ? [new ShowDetailsAction(), new OpenSettingsAction(DISPLAY_SETTINGS_URI), new KeepAction(), new ApplyAction()]
            : [new ShowDetailsAction(), new OpenSettingsAction(DISPLAY_SETTINGS_URI)];

        return new Finding(
            id: id,
            category: FindingCategory.Display,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: detail,
            recommendation: recommendation,
            impact: impact,
            actions: actions,
            explanation: candidate
                ? new Explanation(CoreStrings.Display_Explain_What, CoreStrings.Display_Explain_Effect, CoreStrings.Display_Explain_Caution)
                : null,
            safety: candidate ? CANDIDATE_SAFETY : null);
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채운다.
    /// </summary>
    private static string Format(string template, params object[] args)
    {
        return SnapshotValues.Format(template, args);
    }

    /// <summary>
    /// 전원 프로브에서 읽은 섀시·전원 공급 상태와 근거용 측정값입니다(없으면 알 수 없음).
    /// </summary>
    private sealed record PowerContext(ChassisKind Chassis, string SupplyText, IReadOnlyList<Measurement> Measurements)
    {
        /// <summary>
        /// 스냅샷의 전원 프로브 측정값으로 만든다. 전원 프로브가 없거나 값이 없으면 알 수 없음이다.
        /// </summary>
        public static PowerContext From(ScanSnapshot snapshot)
        {
            var chassisMeasurement = snapshot.GetMeasurement(PowerProbeContract.PROBE_ID, PowerProbeContract.CHASSIS_TYPES);
            var acMeasurement = snapshot.GetMeasurement(PowerProbeContract.PROBE_ID, PowerProbeContract.AC_LINE_STATUS);
            var supply = PowerSupplyClassifier.Classify(acMeasurement?.Value is IntegerValue ac ? ac.Value : null);
            var measurements = new List<Measurement>();
            if (acMeasurement is not null)
            {
                measurements.Add(acMeasurement);
            }

            if (chassisMeasurement is not null)
            {
                measurements.Add(chassisMeasurement);
            }

            return new PowerContext(ChassisClassifier.Classify(chassisMeasurement), PowerSupplyClassifier.DisplayName(supply), measurements);
        }
    }

    /// <summary>
    /// 대상 하나의 측정값 묶음입니다. null은 "값 없음"입니다.
    /// </summary>
    private sealed record TargetValues(
        string? DevicePath,
        string? MonitorName,
        string? GdiName,
        string? AdapterName,
        string? AdapterPath,
        long? TargetId,
        long? OutputTechnology,
        long? Width,
        long? Height,
        long? RefreshHz,
        bool? Interlaced,
        bool? Cloned,
        double? SignalHz,
        IReadOnlyList<string>? SameModeRates,
        long? MaxSameModeHz)
    {
        /// <summary>
        /// 스냅샷에서 대상 측정값을 읽는다.
        /// </summary>
        public static TargetValues Read(ScanSnapshot snapshot, int index)
        {
            string Name(string field) => DisplayProbeContract.TargetMeasurementName(index, field);
            string? Text(string field) => SnapshotValues.Text(snapshot, DisplayProbeContract.PROBE_ID, Name(field));
            long? Integer(string field) => SnapshotValues.Integer(snapshot, DisplayProbeContract.PROBE_ID, Name(field));
            bool? Boolean(string field) => SnapshotValues.Boolean(snapshot, DisplayProbeContract.PROBE_ID, Name(field));

            return new TargetValues(
                Text(DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH),
                Text(DisplayProbeContract.FIELD_MONITOR_NAME),
                Text(DisplayProbeContract.FIELD_GDI_DEVICE_NAME),
                Text(DisplayProbeContract.FIELD_ADAPTER_NAME),
                Text(DisplayProbeContract.FIELD_ADAPTER_DEVICE_PATH),
                Integer(DisplayProbeContract.FIELD_TARGET_ID),
                Integer(DisplayProbeContract.FIELD_OUTPUT_TECHNOLOGY),
                Integer(DisplayProbeContract.FIELD_WIDTH),
                Integer(DisplayProbeContract.FIELD_HEIGHT),
                Integer(DisplayProbeContract.FIELD_REFRESH_HZ),
                Boolean(DisplayProbeContract.FIELD_INTERLACED),
                Boolean(DisplayProbeContract.FIELD_CLONED),
                SnapshotValues.Decimal(snapshot, DisplayProbeContract.PROBE_ID, Name(DisplayProbeContract.FIELD_SIGNAL_REFRESH_HZ)),
                SnapshotValues.TextList(snapshot, DisplayProbeContract.PROBE_ID, Name(DisplayProbeContract.FIELD_SAME_MODE_REFRESH_RATES)),
                Integer(DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ));
        }

        /// <summary>
        /// 영속 식별자: 모니터 장치 경로, 없으면 어댑터 경로 + 대상 ID, 둘 다 없을 때만 인덱스.
        /// </summary>
        public string Identity(int index)
        {
            if (DevicePath is not null)
            {
                return DevicePath;
            }

            if (AdapterPath is not null && TargetId is { } targetId)
            {
                return string.Format(CultureInfo.InvariantCulture, FALLBACK_ID_FORMAT, AdapterPath, targetId);
            }

            return string.Format(CultureInfo.InvariantCulture, INDEX_ID_FORMAT, index);
        }

        /// <summary>
        /// 사용자 표시 이름(모니터 이름 + GDI 레이블).
        /// </summary>
        public string Label(int index)
        {
            var name = MonitorName
                ?? SnapshotValues.Format(CoreStrings.Display_FallbackName, (index + DISPLAY_INDEX_OFFSET).ToString(CultureInfo.CurrentCulture));
            var gdiLabel = GdiName?.Replace(GDI_PREFIX, string.Empty, StringComparison.Ordinal);
            return gdiLabel is null ? name : SnapshotValues.Format(CoreStrings.Display_LabelFormat, name, gdiLabel);
        }

        /// <summary>
        /// 가상 또는 PCI가 아닌 어댑터인지 판단한다(어댑터 이름에 "Virtual", 어댑터 경로에 PCI 공급업체 없음, 간접 가상 출력).
        /// </summary>
        public bool IsVirtualAdapter()
        {
            var virtualName = AdapterName?.Contains(VIRTUAL_ADAPTER_KEYWORD, StringComparison.OrdinalIgnoreCase) == true;
            var nonPciPath = AdapterPath is not null && !AdapterPath.Contains(PCI_DEVICE_PATH_MARKER, StringComparison.OrdinalIgnoreCase);
            var indirectVirtual = OutputTechnology == DisplayProbeContract.OUTPUT_TECHNOLOGY_INDIRECT_VIRTUAL;
            return virtualName || nonPciPath || indirectVirtual;
        }
    }
}
