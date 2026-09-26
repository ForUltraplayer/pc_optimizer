/**
 * @file    : StorageSpaceRule.cs
 * @author  : rudals252
 * @brief   : 드라이브 문자가 있는 고정 볼륨의 여유율 10% 미만 또는 여유 10GiB 미만을 '여유 공간 적음' 후보(제품 휴리스틱 명시)로, 그 밖은 정보로, Windows.old 존재를 정보로 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 저장소 여유 공간 규칙입니다(스펙 §5 저장소 행).
/// <list type="bullet">
/// <item>드라이브 문자가 있는 고정 볼륨만 판정합니다(복구·EFI처럼 문자 없는 시스템 파티션은 사용자가 정리할 대상이 아니므로 제외).</item>
/// <item>여유율 &lt; <see cref="LOW_FREE_PERCENT"/>% 또는 여유 &lt; <see cref="LOW_FREE_BYTES"/>: Candidate '여유 공간 적음'. 기준은 제품 휴리스틱임을 근거에 밝힙니다.</item>
/// <item>그 밖: Info. 크기·남은 공간 누락 또는 크기 0: CannotVerify(PartialData).</item>
/// <item>시스템 드라이브에 Windows.old가 있으면 Info(크기는 재지 않음, 삭제 가능 여부 판단 없음).</item>
/// </list>
/// </summary>
public sealed class StorageSpaceRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "storage.space";

    /// <summary>볼륨별 Finding ID 접두사(뒤에 "C:" 같은 드라이브 이름이 붙음).</summary>
    public const string FINDING_ID_PREFIX = "storage-space:";

    /// <summary>Windows.old Finding ID.</summary>
    public const string WINDOWS_OLD_FINDING_ID = "storage-windows-old:system-drive";

    /// <summary>저장소 설정 URI.</summary>
    public const string STORAGE_SETTINGS_URI = "ms-settings:storagesense";

    /// <summary>'여유 공간 적음' 여유율 기준(%, 제품 휴리스틱). 이 값 미만이면 후보.</summary>
    public const double LOW_FREE_PERCENT = 10.0;

    /// <summary>'여유 공간 적음' 여유 공간 기준(바이트, 10 GiB, 제품 휴리스틱). 이 값 미만이면 후보.</summary>
    public const long LOW_FREE_BYTES = 10L * BYTES_PER_GIB;

    private const long BYTES_PER_GIB = 1024L * 1024 * 1024;
    private const double PERCENT_SCALE = 100.0;
    private const string GIB_FORMAT = "0.0";
    private const string PERCENT_FORMAT = "0.0";
    private const string DRIVE_SUFFIX = ":";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(VolumeProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var findings = new List<Finding>();
        var count = SnapshotValues.Integer(snapshot, VolumeProbeContract.PROBE_ID, VolumeProbeContract.VOLUME_COUNT) ?? 0;
        for (var index = 0; index < count; index++)
        {
            if (EvaluateVolume(snapshot, result, index) is { } finding)
            {
                findings.Add(finding);
            }
        }

        if (SnapshotValues.Boolean(snapshot, VolumeProbeContract.PROBE_ID, VolumeProbeContract.WINDOWS_OLD_EXISTS) == true)
        {
            findings.Add(CreateWindowsOld(snapshot));
        }

        return findings;
    }

    /// <summary>
    /// 볼륨 하나를 판정한다. 판정 대상이 아니면 null.
    /// </summary>
    private static Finding? EvaluateVolume(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string Name(string field) => VolumeProbeContract.VolumeMeasurementName(index, field);

        var letter = SnapshotValues.Text(snapshot, VolumeProbeContract.PROBE_ID, Name(VolumeProbeContract.FIELD_DRIVE_LETTER));
        var driveType = SnapshotValues.Integer(snapshot, VolumeProbeContract.PROBE_ID, Name(VolumeProbeContract.FIELD_DRIVE_TYPE));
        if (letter is null || driveType != VolumeProbeContract.DRIVE_TYPE_FIXED)
        {
            return null;
        }

        var drive = letter.TrimEnd(DRIVE_SUFFIX[0]) + DRIVE_SUFFIX;
        var id = FINDING_ID_PREFIX + drive;
        Measurement[] measured = SnapshotValues.WithPrefix(result, VolumeProbeContract.VolumeMeasurementPrefix(index));
        var size = SnapshotValues.Integer(snapshot, VolumeProbeContract.PROBE_ID, Name(VolumeProbeContract.FIELD_SIZE));
        var free = SnapshotValues.Integer(snapshot, VolumeProbeContract.PROBE_ID, Name(VolumeProbeContract.FIELD_SIZE_REMAINING));

        if (size is not { } total || free is not { } remaining || total <= 0 || remaining < 0)
        {
            return Create(
                id,
                SnapshotValues.Format(CoreStrings.Storage_Title_SpaceUnknown, drive),
                measured,
                CoreStrings.Storage_Evidence_SpaceUnknown,
                Verdict.CannotVerify,
                CannotVerifyReason.PartialData,
                recommendation: null,
                impact: null);
        }

        var percent = remaining * PERCENT_SCALE / total;
        var freeText = Gib(remaining);
        var percentText = percent.ToString(PERCENT_FORMAT, CultureInfo.CurrentCulture);
        var evidence = SnapshotValues.Format(
            CoreStrings.Storage_Evidence_Space,
            freeText,
            Gib(total),
            percentText,
            LOW_FREE_PERCENT.ToString(CultureInfo.CurrentCulture),
            (LOW_FREE_BYTES / BYTES_PER_GIB).ToString(CultureInfo.CurrentCulture));

        if (percent < LOW_FREE_PERCENT || remaining < LOW_FREE_BYTES)
        {
            return Create(
                id,
                SnapshotValues.Format(CoreStrings.Storage_Title_LowSpace, drive),
                measured,
                evidence,
                Verdict.Candidate,
                cannotVerifyReason: null,
                new Recommendation(CoreStrings.Storage_Recommendation_Text, CoreStrings.Storage_Recommendation_Condition),
                new Impact(CoreStrings.Storage_Impact_Benefit, CoreStrings.Storage_Impact_SideEffect));
        }

        return Create(
            id,
            SnapshotValues.Format(CoreStrings.Storage_Title_Space, drive, freeText, percentText),
            measured,
            evidence,
            Verdict.Info,
            cannotVerifyReason: null,
            recommendation: null,
            impact: null);
    }

    /// <summary>
    /// Windows.old 존재 Info를 만든다.
    /// </summary>
    private static Finding CreateWindowsOld(ScanSnapshot snapshot)
    {
        var drive = SnapshotValues.Text(snapshot, VolumeProbeContract.PROBE_ID, VolumeProbeContract.SYSTEM_DRIVE) ?? string.Empty;
        Measurement[] measured =
        [
            .. new[]
            {
                snapshot.GetMeasurement(VolumeProbeContract.PROBE_ID, VolumeProbeContract.SYSTEM_DRIVE),
                snapshot.GetMeasurement(VolumeProbeContract.PROBE_ID, VolumeProbeContract.WINDOWS_OLD_EXISTS),
            }.OfType<Measurement>(),
        ];

        return Create(
            WINDOWS_OLD_FINDING_ID,
            CoreStrings.Storage_Title_WindowsOld,
            measured,
            SnapshotValues.Format(CoreStrings.Storage_Evidence_WindowsOld, drive),
            Verdict.Info,
            cannotVerifyReason: null,
            recommendation: null,
            impact: null);
    }

    /// <summary>
    /// 바이트를 GiB 문자열(소수 첫째 자리)로 만든다.
    /// </summary>
    private static string Gib(long bytes)
    {
        return ((double)bytes / BYTES_PER_GIB).ToString(GIB_FORMAT, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 저장소 Finding을 만든다. 모든 판정에 상세 보기와 저장소 설정 열기를, 후보에는 유지를 붙인다.
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
            ? [new ShowDetailsAction(), new OpenSettingsAction(STORAGE_SETTINGS_URI), new KeepAction(), new ApplyAction()]
            : [new ShowDetailsAction(), new OpenSettingsAction(STORAGE_SETTINGS_URI)];

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
