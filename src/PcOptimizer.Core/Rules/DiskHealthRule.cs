/**
 * @file    : DiskHealthRule.cs
 * @author  : rudals252
 * @brief   : 물리 디스크별 'Windows가 보고한 상태'(Healthy 정보, Warning/Unhealthy 조건부 백업·점검 후보, 알 수 없음 확인 불가)를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 디스크 상태 규칙입니다(스펙 §5 저장소 행 중 디스크 상태).
/// <list type="bullet">
/// <item>Healthy: Info 'Windows가 보고한 상태: Healthy'. SMART 전체 정상·고장 없음으로 확장하지 않습니다.</item>
/// <item>Warning/Unhealthy: 백업 확인·제조사 진단을 권하는 조건부 Candidate.</item>
/// <item>상태 없음·알 수 없음(5 등): CannotVerify(Unsupported).</item>
/// </list>
/// Finding ID는 저장소 제공자의 물리 디스크 GUID이며, 없으면 이름과 디스크 번호를 씁니다.
/// </summary>
public sealed class DiskHealthRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "storage.diskHealth";

    /// <summary>디스크별 Finding ID 접두사.</summary>
    public const string FINDING_ID_PREFIX = "disk-health:";

    private const string HEALTHY_NAME = "Healthy";
    private const string WARNING_NAME = "Warning";
    private const string UNHEALTHY_NAME = "Unhealthy";
    private const string MEDIA_HDD_NAME = "HDD";
    private const string MEDIA_SSD_NAME = "SSD";
    private const string MEDIA_SCM_NAME = "SCM";
    private const string FALLBACK_ID_FORMAT = "name:{0}|disk:{1}";
    private const string GIB_FORMAT = "0.0";
    private const long BYTES_PER_GIB = 1024L * 1024 * 1024;

    /// <summary>알려진 버스 종류 이름(MSFT_PhysicalDisk.BusType).</summary>
    private static readonly Dictionary<long, string> BUS_TYPE_NAMES = new()
    {
        [1] = "SCSI",
        [3] = "ATA",
        [7] = "USB",
        [8] = "RAID",
        [10] = "SAS",
        [11] = "SATA",
        [12] = "SD",
        [17] = "NVMe",
    };

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(PhysicalDiskProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var count = SnapshotValues.Integer(snapshot, PhysicalDiskProbeContract.PROBE_ID, PhysicalDiskProbeContract.DISK_COUNT) ?? 0;
        var findings = new List<Finding>();
        for (var index = 0; index < count; index++)
        {
            findings.Add(EvaluateDisk(snapshot, result, index));
        }

        return findings;
    }

    /// <summary>
    /// 디스크 하나를 판정한다.
    /// </summary>
    private static Finding EvaluateDisk(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string Name(string field) => PhysicalDiskProbeContract.DiskMeasurementName(index, field);
        string? Text(string field) => SnapshotValues.Text(snapshot, PhysicalDiskProbeContract.PROBE_ID, Name(field));
        long? Integer(string field) => SnapshotValues.Integer(snapshot, PhysicalDiskProbeContract.PROBE_ID, Name(field));

        Measurement[] measured = SnapshotValues.WithPrefix(result, PhysicalDiskProbeContract.DiskMeasurementPrefix(index));
        var providerId = Text(PhysicalDiskProbeContract.FIELD_PROVIDER_DISK_ID);
        var deviceId = Text(PhysicalDiskProbeContract.FIELD_DEVICE_ID) ?? index.ToString(CultureInfo.InvariantCulture);
        var friendlyName = Text(PhysicalDiskProbeContract.FIELD_FRIENDLY_NAME);
        var label = friendlyName ?? SnapshotValues.Format(CoreStrings.Disk_FallbackName, deviceId);
        var id = FINDING_ID_PREFIX + (providerId ?? string.Format(CultureInfo.InvariantCulture, FALLBACK_ID_FORMAT, friendlyName, deviceId));
        var health = Integer(PhysicalDiskProbeContract.FIELD_HEALTH_STATUS);

        var healthName = health switch
        {
            PhysicalDiskProbeContract.HEALTH_HEALTHY => HEALTHY_NAME,
            PhysicalDiskProbeContract.HEALTH_WARNING => WARNING_NAME,
            PhysicalDiskProbeContract.HEALTH_UNHEALTHY => UNHEALTHY_NAME,
            _ => null,
        };

        if (healthName is null)
        {
            return Create(
                id,
                SnapshotValues.Format(CoreStrings.Disk_Title_Unknown, label),
                measured,
                SnapshotValues.Format(CoreStrings.Disk_Evidence_Unknown, health?.ToString(CultureInfo.InvariantCulture) ?? CoreStrings.Disk_ValueUnknown),
                Verdict.CannotVerify,
                CannotVerifyReason.Unsupported,
                recommendation: null,
                impact: null);
        }

        var evidence = SnapshotValues.Format(
            CoreStrings.Disk_Evidence,
            MediaName(Integer(PhysicalDiskProbeContract.FIELD_MEDIA_TYPE)),
            BusName(Integer(PhysicalDiskProbeContract.FIELD_BUS_TYPE)),
            Integer(PhysicalDiskProbeContract.FIELD_SIZE) is { } size
                ? ((double)size / BYTES_PER_GIB).ToString(GIB_FORMAT, CultureInfo.CurrentCulture)
                : CoreStrings.Disk_ValueUnknown);

        if (health == PhysicalDiskProbeContract.HEALTH_HEALTHY)
        {
            return Create(
                id,
                SnapshotValues.Format(CoreStrings.Disk_Title, label, healthName),
                measured,
                evidence,
                Verdict.Info,
                cannotVerifyReason: null,
                recommendation: null,
                impact: null);
        }

        return Create(
            id,
            SnapshotValues.Format(CoreStrings.Disk_Title_Attention, label, healthName),
            measured,
            evidence,
            Verdict.Candidate,
            cannotVerifyReason: null,
            new Recommendation(CoreStrings.Disk_Recommendation_Text, CoreStrings.Disk_Recommendation_Condition),
            new Impact(CoreStrings.Disk_Impact_Benefit, CoreStrings.Disk_Impact_SideEffect));
    }

    /// <summary>
    /// 매체 종류 원시 값의 표시 이름.
    /// </summary>
    private static string MediaName(long? mediaType)
    {
        return mediaType switch
        {
            PhysicalDiskProbeContract.MEDIA_TYPE_HDD => MEDIA_HDD_NAME,
            PhysicalDiskProbeContract.MEDIA_TYPE_SSD => MEDIA_SSD_NAME,
            PhysicalDiskProbeContract.MEDIA_TYPE_SCM => MEDIA_SCM_NAME,
            null => CoreStrings.Disk_ValueUnknown,
            { } other => SnapshotValues.Format(CoreStrings.Disk_CodeFormat, other.ToString(CultureInfo.InvariantCulture)),
        };
    }

    /// <summary>
    /// 버스 종류 원시 값의 표시 이름.
    /// </summary>
    private static string BusName(long? busType)
    {
        if (busType is not { } code)
        {
            return CoreStrings.Disk_ValueUnknown;
        }

        return BUS_TYPE_NAMES.TryGetValue(code, out var name)
            ? name
            : SnapshotValues.Format(CoreStrings.Disk_CodeFormat, code.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 디스크 상태 Finding을 만든다(설정 URI 없음: 권고가 있으면 화면이 수동 경로 안내를 보여 줌).
    /// </summary>
    private static Finding Create(
        string id,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason,
        Recommendation? recommendation,
        Impact? impact)
    {
        IReadOnlyList<FindingAction> actions = verdict == Verdict.Candidate
            ? [new ShowDetailsAction(), new KeepAction()]
            : [new ShowDetailsAction()];

        return new Finding(
            id: id,
            category: FindingCategory.Storage,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: null,
            recommendation: recommendation,
            impact: impact,
            actions: actions);
    }
}
