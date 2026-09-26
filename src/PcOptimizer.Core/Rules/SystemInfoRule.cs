/**
 * @file    : SystemInfoRule.cs
 * @author  : rudals252
 * @brief   : 제조사·모델·제품군·섀시·BIOS 버전을 '시스템 정보' 정보로 내는 순수 판정 규칙(OEM 지원 링크 없음)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 시스템 정보 규칙입니다(스펙 §5 노트북 OEM 행 중 수집 부분).
/// 제조사·모델 중 하나라도 있으면 Info '시스템 정보', 둘 다 없으면 CannotVerify(PartialData)입니다.
/// 섀시는 전원 규칙과 같은 <see cref="ChassisClassifier"/> 해석을 쓰며 불명은 '알 수 없음'입니다.
/// OEM 지원 페이지 링크는 공식 링크 허용 표가 생긴 뒤 추가하므로 여기서는 만들지 않습니다.
/// </summary>
public sealed class SystemInfoRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "system.info";

    /// <summary>시스템 정보 Finding ID.</summary>
    public const string FINDING_ID = "system-info:computer";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(SystemInfoProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        string? Text(string name) => SnapshotValues.Text(snapshot, SystemInfoProbeContract.PROBE_ID, name);

        var manufacturer = Text(SystemInfoProbeContract.MANUFACTURER);
        var model = Text(SystemInfoProbeContract.MODEL);
        var family = Text(SystemInfoProbeContract.SYSTEM_FAMILY) ?? CoreStrings.System_ValueUnknown;
        var bios = Text(SystemInfoProbeContract.BIOS_VERSION) ?? CoreStrings.System_ValueUnknown;
        var chassis = ChassisClassifier.DisplayName(
            ChassisClassifier.Classify(snapshot.GetMeasurement(SystemInfoProbeContract.PROBE_ID, SystemInfoProbeContract.CHASSIS_TYPES)));
        Measurement[] measured = [.. result.Measurements];

        if (manufacturer is null && model is null)
        {
            return
            [
                Create(
                    CoreStrings.System_Title_Unknown,
                    measured,
                    SnapshotValues.Format(CoreStrings.System_Evidence_Unknown, chassis, bios),
                    Verdict.CannotVerify,
                    CannotVerifyReason.PartialData),
            ];
        }

        var manufacturerText = manufacturer ?? CoreStrings.System_ValueUnknown;
        var modelText = model ?? CoreStrings.System_ValueUnknown;
        return
        [
            Create(
                SnapshotValues.Format(CoreStrings.System_Title, manufacturerText, modelText),
                measured,
                SnapshotValues.Format(CoreStrings.System_Evidence, manufacturerText, modelText, family, chassis, bios),
                Verdict.Info,
                cannotVerifyReason: null),
        ];
    }

    /// <summary>
    /// 시스템 정보 Finding을 만든다(드라이버 분류: 제조사·모델은 이후 공식 드라이버 지원 링크의 기준이 됨).
    /// </summary>
    private static Finding Create(string title, IReadOnlyList<Measurement> measured, string evidence, Verdict verdict, CannotVerifyReason? cannotVerifyReason)
    {
        return new Finding(
            id: FINDING_ID,
            category: FindingCategory.Driver,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
