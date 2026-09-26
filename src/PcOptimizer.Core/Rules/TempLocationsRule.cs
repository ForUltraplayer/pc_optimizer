/**
 * @file    : TempLocationsRule.cs
 * @author  : rudals252
 * @brief   : 표준 임시 위치(사용자·Windows 임시, 업데이트 다운로드, 탐색기 캐시, 배달 최적화)별 관측 크기 정보(부분 관측 구분), 없음 정보, 접근 거부(일반 권한 시스템 위치는 관리자 권한 필요)·미관측·해석 실패 확인 불가를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 표준 임시 위치 규칙입니다(스펙 §5 저장소 행 "표준 임시 위치의 관측 크기", "폴더 크기는 삭제 가능량이 아님").
/// 크기는 관측한 논리 크기이며 삭제·확보 가능량으로 표현하지 않고, 후보(Candidate)를 만들지 않습니다.
/// 관측하지 못한 위치는 0바이트가 아니라 사유와 함께 확인 불가로 표시합니다. 제목·근거·상세에 경로를 넣지 않습니다.
/// </summary>
public sealed class TempLocationsRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "storage.tempLocations";

    /// <summary>Finding ID 접두사(뒤에 위치 ID).</summary>
    public const string FINDING_ID_PREFIX = "storage.temp:";

    /// <summary>저장소 설정 URI(설정 URI 허용 목록에 있는 값).</summary>
    public const string STORAGE_SETTINGS_URI = StorageSpaceRule.STORAGE_SETTINGS_URI;

    private const string COUNT_FORMAT = "N0";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(FileScanProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var count = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.TEMP_COUNT) ?? 0;
        var findings = new List<Finding>();
        for (var index = 0; index < count; index++)
        {
            findings.Add(EvaluateLocation(snapshot, result, index));
        }

        return findings;
    }

    /// <summary>
    /// 위치 하나를 판정한다.
    /// </summary>
    private static Finding EvaluateLocation(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string Name(string field) => FileScanProbeContract.Name(FileScanProbeContract.TEMP_PREFIX, index, field);

        var id = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_ID)) ?? index.ToString(CultureInfo.InvariantCulture);
        var state = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_STATE));
        var isSystem = SnapshotValues.Boolean(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_SYSTEM)) ?? false;
        var bytes = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_BYTES));
        var files = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_FILE_COUNT)) ?? 0;
        var duplicates = SnapshotValues.Boolean(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE)) ?? true;
        var measured = SnapshotValues.WithPrefix(result, FileScanProbeContract.ItemPrefix(FileScanProbeContract.TEMP_PREFIX, index));
        var label = Label(id);
        var detail = id == FileScanProbeContract.LOCATION_THUMBNAIL_CACHE ? CoreStrings.Temp_Detail_ThumbnailPattern : null;
        var sizeNote = duplicates ? CoreStrings.FileScan_SizeNote_Estimated : CoreStrings.FileScan_SizeNote_Verified;
        var fileCount = files.ToString(COUNT_FORMAT, CultureInfo.InvariantCulture);

        switch (state)
        {
            case FileScanProbeContract.LOCATION_STATE_OBSERVED when bytes is { } observed:
                return Create(id, SnapshotValues.Format(CoreStrings.Temp_Title_Observed, label, ByteSizeText.Format(observed)), measured,
                    SnapshotValues.Format(CoreStrings.Temp_Evidence_Observed, fileCount, sizeNote), Verdict.Info, null, detail);
            case FileScanProbeContract.LOCATION_STATE_PARTIAL when bytes is { } partial:
                return Create(id, SnapshotValues.Format(CoreStrings.Temp_Title_Partial, label, ByteSizeText.Format(partial)), measured,
                    SnapshotValues.Format(CoreStrings.Temp_Evidence_Partial, fileCount, sizeNote), Verdict.Info, null, detail);
            case FileScanProbeContract.LOCATION_STATE_ABSENT:
                return Create(id, SnapshotValues.Format(CoreStrings.Temp_Title_Absent, label), measured, CoreStrings.Temp_Evidence_Absent, Verdict.Info, null, null);
            case FileScanProbeContract.LOCATION_STATE_ACCESS_DENIED:
                var reason = isSystem && !result.UserContext.IsElevated ? CannotVerifyReason.ElevationRequired : CannotVerifyReason.AccessDenied;
                return CannotVerify(id, label, measured, reason, CannotVerifyTexts.EvidenceFor(reason));
            case FileScanProbeContract.LOCATION_STATE_UNRESOLVED:
                return CannotVerify(id, label, measured, CannotVerifyReason.Unsupported, CoreStrings.Temp_Evidence_Unresolved);
            case FileScanProbeContract.LOCATION_STATE_PROTECTED:
                return CannotVerify(id, label, measured, CannotVerifyReason.Unsupported, CoreStrings.Temp_Evidence_Protected);
            default:
                return CannotVerify(id, label, measured, CannotVerifyReason.PartialData, CoreStrings.Temp_Evidence_NotObserved);
        }
    }

    /// <summary>
    /// 위치 ID의 화면용 이름(경로 아님).
    /// </summary>
    private static string Label(string id)
    {
        return id switch
        {
            FileScanProbeContract.LOCATION_USER_TEMP => CoreStrings.Temp_Label_UserTemp,
            FileScanProbeContract.LOCATION_WINDOWS_TEMP => CoreStrings.Temp_Label_WindowsTemp,
            FileScanProbeContract.LOCATION_UPDATE_DOWNLOAD => CoreStrings.Temp_Label_UpdateDownload,
            FileScanProbeContract.LOCATION_THUMBNAIL_CACHE => CoreStrings.Temp_Label_ThumbnailCache,
            FileScanProbeContract.LOCATION_DELIVERY_OPTIMIZATION => CoreStrings.Temp_Label_DeliveryOptimization,
            _ => CoreStrings.Temp_Label_Unknown,
        };
    }

    /// <summary>
    /// 확인 불가 Finding을 만든다.
    /// </summary>
    private static Finding CannotVerify(string id, string label, IReadOnlyList<Measurement> measured, CannotVerifyReason reason, string evidence)
    {
        return Create(id, SnapshotValues.Format(CoreStrings.Temp_Title_CannotVerify, label), measured, evidence, Verdict.CannotVerify, reason, null);
    }

    /// <summary>
    /// 임시 위치 Finding을 만든다. 상세 보기와 저장소 설정 열기를 붙인다.
    /// </summary>
    private static Finding Create(
        string id, string title, IReadOnlyList<Measurement> measured, string evidence, Verdict verdict, CannotVerifyReason? reason, string? detail)
    {
        return new Finding(
            id: FINDING_ID_PREFIX + id,
            category: FindingCategory.Storage,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: reason,
            detail: detail,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction(), new OpenSettingsAction(STORAGE_SETTINGS_URI)]);
    }
}
