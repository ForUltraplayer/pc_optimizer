/**
 * @file    : UnclassifiedFolderRule.cs
 * @author  : rudals252
 * @brief   : 미분류 대용량 폴더 후보마다 CannotVerify(NoRule)(제목은 마지막 폴더 이름만, 확장자 분포·관측 파일의 최근 수정 시각은 상세, 전체 경로는 측정값에만), 선정 기준 요약 정보, 부분 관측 대용량 폴더 CannotVerify(PartialData)를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 미분류 대용량 폴더 규칙입니다(스펙 §5 미분류 행, 2차 LLM 슬롯).
/// <list type="bullet">
/// <item>후보마다 CannotVerify(NoRule): 판정 규칙이 없다는 사실만 알리고 삭제·정리 권고를 하지 않습니다.</item>
/// <item>최근 시각은 '관측 파일의 수정 시각'이며 마지막 사용 시각이 아니라고 밝힙니다.</item>
/// <item>제목에는 폴더의 마지막 이름만 쓰고, 근거·상세에는 경로를 쓰지 않습니다(전체 경로는 상세 보기 측정값에만).</item>
/// <item>요약 Info 하나: 후보 수와 선정 기준(완전 관측, 1,000,000,000바이트 이상, 상위 20개), 상위 폴더 잔여 생략 가능성.</item>
/// <item>부분 관측 대용량 폴더가 있으면 CannotVerify(PartialData) 하나로 순위 밖임을 알립니다.</item>
/// </list>
/// </summary>
public sealed class UnclassifiedFolderRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "unclassified.largeFolders";

    /// <summary>Finding ID 접두사(후보는 뒤에 전체 경로 — 기본 내보내기에서 토큰화).</summary>
    public const string FINDING_ID_PREFIX = "unclassified:";

    /// <summary>요약 Finding ID.</summary>
    public const string SUMMARY_FINDING_ID = FINDING_ID_PREFIX + "summary";

    /// <summary>부분 관측 폴더 Finding ID.</summary>
    public const string PARTIAL_FINDING_ID = FINDING_ID_PREFIX + "partial";

    private const string LIST_SEPARATOR = ", ";
    private const string COUNT_FORMAT = "N0";
    private const string DETAIL_SEPARATOR = " · ";
    private const string SHARE_FORMAT = "{0} {1}";
    private const string PARTIAL_ITEM_FORMAT = "'{0}' {1}";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(FileScanProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial)
            || SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.UNCLASSIFIED_COUNT) is not { } count)
        {
            return [];
        }

        var findings = new List<Finding> { CreateSummary(snapshot, result, count) };
        for (var index = 0; index < count; index++)
        {
            if (CreateCandidate(snapshot, result, index) is { } candidate)
            {
                findings.Add(candidate);
            }
        }

        var partialCount = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.PARTIAL_FOLDER_COUNT) ?? 0;
        if (partialCount > 0)
        {
            findings.Add(CreatePartial(snapshot, result, partialCount));
        }

        return findings;
    }

    /// <summary>
    /// 선정 기준과 후보 수를 알리는 요약 Info.
    /// </summary>
    private static Finding CreateSummary(ScanSnapshot snapshot, ProbeResult result, long count)
    {
        var qualifying = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.UNCLASSIFIED_QUALIFYING_COUNT) ?? count;
        var title = count == 0
            ? CoreStrings.Unclassified_Title_None
            : SnapshotValues.Format(CoreStrings.Unclassified_Title_Summary, qualifying.ToString(CultureInfo.InvariantCulture), count.ToString(CultureInfo.InvariantCulture));
        Measurement[] measured = [.. result.Measurements.Where(m =>
            m.Name is FileScanProbeContract.UNCLASSIFIED_COUNT or FileScanProbeContract.UNCLASSIFIED_QUALIFYING_COUNT or FileScanProbeContract.UNCLASSIFIED_MIN_BYTES)];
        return Create(SUMMARY_FINDING_ID, title, measured, CoreStrings.Unclassified_Evidence_Summary, Verdict.Info, null, null);
    }

    /// <summary>
    /// 후보 하나의 CannotVerify(NoRule). 경로가 없으면 만들지 않는다.
    /// </summary>
    private static Finding? CreateCandidate(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string Name(string field) => FileScanProbeContract.Name(FileScanProbeContract.UNCLASSIFIED_PREFIX, index, field);

        if (SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_PATH)) is not { } path)
        {
            return null;
        }

        var bytes = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_BYTES)) ?? 0;
        var files = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_FILE_COUNT)) ?? 0;
        var extensions = SnapshotValues.TextList(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_TOP_EXTENSIONS)) ?? [];
        var newest = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_NEWEST_WRITE_UTC));
        var duplicates = SnapshotValues.Boolean(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE)) ?? true;
        var excludesChildren = SnapshotValues.Boolean(snapshot, FileScanProbeContract.PROBE_ID, Name(FileScanProbeContract.FIELD_EXCLUDES_REPORTED_CHILDREN)) ?? false;

        var detail = SnapshotValues.Format(
            CoreStrings.Unclassified_Detail_Candidate,
            FormatExtensions(extensions),
            files.ToString(COUNT_FORMAT, CultureInfo.InvariantCulture),
            FormatDate(newest));
        if (excludesChildren)
        {
            detail += DETAIL_SEPARATOR + CoreStrings.Unclassified_Detail_ExcludesChildren;
        }

        return Create(
            FINDING_ID_PREFIX + path,
            SnapshotValues.Format(CoreStrings.Unclassified_Title_Candidate, PathScope.GetLeafName(path), ByteSizeText.Format(bytes)),
            SnapshotValues.WithPrefix(result, FileScanProbeContract.ItemPrefix(FileScanProbeContract.UNCLASSIFIED_PREFIX, index)),
            SnapshotValues.Format(CoreStrings.Unclassified_Evidence_Candidate, duplicates ? CoreStrings.FileScan_SizeNote_Estimated : CoreStrings.FileScan_SizeNote_Verified),
            Verdict.CannotVerify,
            CannotVerifyReason.NoRule,
            detail);
    }

    /// <summary>
    /// 부분 관측 대용량 폴더 CannotVerify(PartialData). 상세에는 마지막 폴더 이름과 관측된 크기만 쓴다.
    /// </summary>
    private static Finding CreatePartial(ScanSnapshot snapshot, ProbeResult result, long count)
    {
        var total = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.PARTIAL_FOLDER_TOTAL_COUNT) ?? count;
        var items = new List<string>();
        for (var index = 0; index < count; index++)
        {
            var path = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.Name(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, index, FileScanProbeContract.FIELD_PATH));
            var bytes = SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.Name(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, index, FileScanProbeContract.FIELD_BYTES));
            if (path is not null && bytes is { } observed)
            {
                items.Add(string.Format(CultureInfo.InvariantCulture, PARTIAL_ITEM_FORMAT, PathScope.GetLeafName(path), ByteSizeText.Format(observed)));
            }
        }

        return Create(
            PARTIAL_FINDING_ID,
            SnapshotValues.Format(CoreStrings.Unclassified_Title_Partial, total.ToString(CultureInfo.InvariantCulture)),
            SnapshotValues.WithPrefix(result, FileScanProbeContract.PARTIAL_FOLDER_PREFIX),
            CoreStrings.Unclassified_Evidence_Partial,
            Verdict.CannotVerify,
            CannotVerifyReason.PartialData,
            SnapshotValues.Format(CoreStrings.Unclassified_Detail_Partial, string.Join(LIST_SEPARATOR, items)));
    }

    /// <summary>
    /// "확장자=바이트" 목록을 "확장자 크기" 문장으로 바꾼다.
    /// </summary>
    private static string FormatExtensions(IReadOnlyList<string> extensions)
    {
        var parts = new List<string>();
        foreach (var item in extensions)
        {
            var separator = item.LastIndexOf(FileScanProbeContract.EXTENSION_SEPARATOR);
            if (separator < 0 || !long.TryParse(item[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            {
                continue;
            }

            var extension = item[..separator];
            parts.Add(string.Format(
                CultureInfo.InvariantCulture,
                SHARE_FORMAT,
                extension.Length == 0 ? CoreStrings.Unclassified_NoExtension : extension,
                ByteSizeText.Format(bytes)));
        }

        return parts.Count == 0 ? CoreStrings.Unclassified_NoValue : string.Join(LIST_SEPARATOR, parts);
    }

    /// <summary>
    /// ISO 8601 UTC 문자열을 날짜(UTC)로 바꾼다. 없거나 해석하지 못하면 '없음'.
    /// </summary>
    private static string FormatDate(string? iso)
    {
        return iso is not null && DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? string.Format(CultureInfo.InvariantCulture, CoreStrings.Unclassified_DateUtcFormat, value.UtcDateTime)
            : CoreStrings.Unclassified_NoValue;
    }

    /// <summary>
    /// 미분류 Finding을 만든다(상세 보기만 제공, 정리·적용 동작 없음).
    /// </summary>
    private static Finding Create(
        string id, string title, IReadOnlyList<Measurement> measured, string evidence, Verdict verdict, CannotVerifyReason? reason, string? detail)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Unclassified,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: reason,
            detail: detail,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
