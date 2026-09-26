/**
 * @file    : SecurityStatusRule.cs
 * @author  : rudals252
 * @brief   : Win32_DeviceGuard 값으로 VBS 상태와 메모리 무결성(HVCI) 설정/실행 상태를 서로 다른 두 정보 줄로 내는 순수 판정 규칙(끄기 권장 없음)
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 보안 상태 규칙입니다(스펙 §5 보안 상태 행).
/// <list type="bullet">
/// <item>VBS 상태와 메모리 무결성(HVCI)은 서로 다른 Finding 두 개로 내며 같은 불리언으로 합치지 않습니다.</item>
/// <item>확인한 상태는 Info만. 값 누락은 해당 줄만 CannotVerify(PartialData), 알 수 없는 VBS 값은 CannotVerify(Unsupported).</item>
/// <item>보안 기능을 끄라는 권장은 어떤 경우에도 하지 않으며 Candidate를 만들지 않습니다.</item>
/// <item>코어 격리 설정 URI는 허용 목록에 없으므로 설정 열기 대신 수동 경로를 상세에 안내합니다.</item>
/// </list>
/// 클래스 없음·미지원으로 프로브가 실패하면 상태 변환기가 CannotVerify(Unsupported)로 드러내므로 여기서는 판정하지 않습니다.
/// </summary>
public sealed class SecurityStatusRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "security.status";

    /// <summary>VBS Finding ID.</summary>
    public const string VBS_FINDING_ID = "security-status:vbs";

    /// <summary>메모리 무결성 Finding ID.</summary>
    public const string MEMORY_INTEGRITY_FINDING_ID = "security-status:memory-integrity";

    private const string LIST_SEPARATOR = ", ";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(SecurityStatusProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        return [EvaluateVbs(snapshot), EvaluateMemoryIntegrity(snapshot)];
    }

    /// <summary>
    /// VBS 상태 줄을 만든다.
    /// </summary>
    private static Finding EvaluateVbs(ScanSnapshot snapshot)
    {
        var measurement = snapshot.GetMeasurement(SecurityStatusProbeContract.PROBE_ID, SecurityStatusProbeContract.VBS_STATUS);
        Measurement[] measured = measurement is null ? [] : [measurement];
        if (measurement?.Value is not IntegerValue status)
        {
            return Create(VBS_FINDING_ID, CoreStrings.Security_Title_VbsUnknown, measured, CoreStrings.Security_Evidence_VbsMissing, Verdict.CannotVerify, CannotVerifyReason.PartialData);
        }

        var raw = status.Value.ToString(CultureInfo.InvariantCulture);
        var state = status.Value switch
        {
            SecurityStatusProbeContract.VBS_STATUS_NOT_ENABLED => CoreStrings.Security_Vbs_NotEnabled,
            SecurityStatusProbeContract.VBS_STATUS_ENABLED_NOT_RUNNING => CoreStrings.Security_Vbs_EnabledNotRunning,
            SecurityStatusProbeContract.VBS_STATUS_RUNNING => CoreStrings.Security_Vbs_Running,
            _ => null,
        };

        if (state is null)
        {
            return Create(
                VBS_FINDING_ID,
                CoreStrings.Security_Title_VbsUnknown,
                measured,
                SnapshotValues.Format(CoreStrings.Security_Evidence_VbsUnknownValue, raw),
                Verdict.CannotVerify,
                CannotVerifyReason.Unsupported);
        }

        return Create(
            VBS_FINDING_ID,
            SnapshotValues.Format(CoreStrings.Security_Title_Vbs, state),
            measured,
            SnapshotValues.Format(CoreStrings.Security_Evidence_Vbs, raw),
            Verdict.Info,
            cannotVerifyReason: null);
    }

    /// <summary>
    /// 메모리 무결성(HVCI) 설정/실행 줄을 만든다. 서비스 목록 하나라도 없으면 false로 대체하지 않고 확인 불가다.
    /// </summary>
    private static Finding EvaluateMemoryIntegrity(ScanSnapshot snapshot)
    {
        var configuredMeasurement = snapshot.GetMeasurement(SecurityStatusProbeContract.PROBE_ID, SecurityStatusProbeContract.SERVICES_CONFIGURED);
        var runningMeasurement = snapshot.GetMeasurement(SecurityStatusProbeContract.PROBE_ID, SecurityStatusProbeContract.SERVICES_RUNNING);
        Measurement[] measured = [.. new[] { configuredMeasurement, runningMeasurement }.OfType<Measurement>()];

        if (configuredMeasurement?.Value is not TextListValue configured || runningMeasurement?.Value is not TextListValue running)
        {
            return Create(
                MEMORY_INTEGRITY_FINDING_ID,
                CoreStrings.Security_Title_MemoryIntegrityUnknown,
                measured,
                CoreStrings.Security_Evidence_MemoryIntegrityMissing,
                Verdict.CannotVerify,
                CannotVerifyReason.PartialData);
        }

        var isConfigured = configured.Values.Contains(SecurityStatusProbeContract.SERVICE_HVCI, StringComparer.Ordinal);
        var isRunning = running.Values.Contains(SecurityStatusProbeContract.SERVICE_HVCI, StringComparer.Ordinal);
        return Create(
            MEMORY_INTEGRITY_FINDING_ID,
            SnapshotValues.Format(
                CoreStrings.Security_Title_MemoryIntegrity,
                isConfigured ? CoreStrings.Security_Hvci_Configured : CoreStrings.Security_Hvci_NotConfigured,
                isRunning ? CoreStrings.Security_Hvci_Running : CoreStrings.Security_Hvci_NotRunning),
            measured,
            SnapshotValues.Format(CoreStrings.Security_Evidence_MemoryIntegrity, ListText(configured.Values), ListText(running.Values)),
            Verdict.Info,
            cannotVerifyReason: null);
    }

    /// <summary>
    /// 코드 목록을 표시 문자열로 만든다(빈 목록은 '없음').
    /// </summary>
    private static string ListText(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? CoreStrings.Security_ListEmpty : string.Join(LIST_SEPARATOR, values);
    }

    /// <summary>
    /// 보안 분류 Finding을 만든다. 권고·설정 열기 없이 수동 경로 안내만 상세에 둔다.
    /// </summary>
    private static Finding Create(
        string id,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Security,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: CoreStrings.Security_Detail_ManualPath,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
