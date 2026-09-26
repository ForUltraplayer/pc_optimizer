/**
 * @file    : FileScanSummaryRule.cs
 * @author  : rudals252
 * @brief   : 공유 파일 스캔 요약 정보(관측 루트 수·소요 시간, 관측 용량은 확보 가능량이 아님, 보호 폴더 수, 사유별 건너뜀 개수, 시간 초과 볼륨, 하드링크·중복 가능, 동기화 폴더 감지 범위)와 보호 정책 무효 시 파일 검사 미실행 확인 불가를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 파일 스캔 요약 규칙입니다. 부분 집계와 관측 용량을 구분해 보이고, 합계를 '확보 가능량'으로 부르지 않습니다.
/// 보호 정책이 무효이면(프로브 Failed + 정책 무효 측정값) 파일 검사를 하지 않았다는 CannotVerify(ProbeError)를 상세 사유와 함께 냅니다.
/// </summary>
public sealed class FileScanSummaryRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "storage.fileScanSummary";

    /// <summary>요약 Finding ID.</summary>
    public const string SUMMARY_FINDING_ID = "storage.fileScan:summary";

    /// <summary>정책 무효 Finding ID.</summary>
    public const string POLICY_FINDING_ID = "storage.fileScan:policy";

    private const string LIST_SEPARATOR = ", ";
    private const string DETAIL_SEPARATOR = "\n";
    private const double MILLISECONDS_PER_SECOND = 1000d;
    private const string SECONDS_FORMAT = "0";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(FileScanProbeContract.PROBE_ID, out var result))
        {
            return [];
        }

        var policy = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.POLICY_STATE);
        if (policy == FileScanProbeContract.POLICY_INVALID)
        {
            return [CreatePolicyInvalid(snapshot, result)];
        }

        return result.Status is ProbeStatus.Success or ProbeStatus.Partial ? [CreateSummary(snapshot, result)] : [];
    }

    /// <summary>
    /// 보호 정책 무효로 파일 검사를 하지 않았다는 확인 불가.
    /// </summary>
    private static Finding CreatePolicyInvalid(ScanSnapshot snapshot, ProbeResult result)
    {
        var error = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.POLICY_ERROR) ?? string.Empty;
        return new Finding(
            id: POLICY_FINDING_ID,
            category: FindingCategory.Storage,
            title: CoreStrings.FileScan_Title_PolicyInvalid,
            measured: result.Measurements,
            evidence: CoreStrings.FileScan_Evidence_PolicyInvalid,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: CannotVerifyReason.ProbeError,
            detail: SnapshotValues.Format(CoreStrings.FileScan_Detail_PolicyInvalid, error),
            recommendation: null,
            impact: null,
            actions: []);
    }

    /// <summary>
    /// 관측 범위·건너뜀·한계를 알리는 요약 Info.
    /// </summary>
    private static Finding CreateSummary(ScanSnapshot snapshot, ProbeResult result)
    {
        long Integer(string name) => SnapshotValues.Integer(snapshot, FileScanProbeContract.PROBE_ID, name) ?? 0;

        var rootCount = Integer(FileScanProbeContract.ROOT_COUNT);
        long scanned = 0;
        var skips = new long[6];
        string[] skipFields =
        [
            FileScanProbeContract.FIELD_SKIP_PROTECTED,
            FileScanProbeContract.FIELD_SKIP_ACCESS_DENIED,
            FileScanProbeContract.FIELD_SKIP_IN_USE,
            FileScanProbeContract.FIELD_SKIP_REPARSE,
            FileScanProbeContract.FIELD_SKIP_PLACEHOLDER,
            FileScanProbeContract.FIELD_SKIP_TIMEOUT,
        ];
        for (var index = 0; index < rootCount; index++)
        {
            var state = SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, index, FileScanProbeContract.FIELD_STATE));
            if (state == FileScanProbeContract.ROOT_STATE_SCANNED)
            {
                scanned++;
            }

            for (var field = 0; field < skipFields.Length; field++)
            {
                skips[field] += Integer(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, index, skipFields[field]));
            }
        }

        var timedOut = new List<string>();
        var volumeCount = Integer(FileScanProbeContract.VOLUME_COUNT);
        for (var index = 0; index < volumeCount; index++)
        {
            if (SnapshotValues.Boolean(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.Name(FileScanProbeContract.VOLUME_PREFIX, index, FileScanProbeContract.FIELD_TIMED_OUT)) == true)
            {
                timedOut.Add(SnapshotValues.Text(snapshot, FileScanProbeContract.PROBE_ID, FileScanProbeContract.Name(FileScanProbeContract.VOLUME_PREFIX, index, FileScanProbeContract.FIELD_VOLUME)) ?? string.Empty);
            }
        }

        var detail = new List<string>
        {
            SnapshotValues.Format(CoreStrings.FileScan_Detail_Skips, [.. skips.Select(value => (object)value.ToString(CultureInfo.InvariantCulture))]),
        };
        if (timedOut.Count > 0)
        {
            detail.Add(SnapshotValues.Format(CoreStrings.FileScan_Detail_TimedOutVolumes, string.Join(LIST_SEPARATOR, timedOut)));
        }

        detail.Add(SnapshotValues.Format(CoreStrings.FileScan_Detail_HardLinks, Integer(FileScanProbeContract.HARD_LINK_DUPLICATE_COUNT).ToString(CultureInfo.InvariantCulture)));
        detail.Add(CoreStrings.FileScan_Detail_SyncCoverage);

        var seconds = (Integer(FileScanProbeContract.ELAPSED_MS) / MILLISECONDS_PER_SECOND).ToString(SECONDS_FORMAT, CultureInfo.InvariantCulture);
        Measurement[] measured = [.. result.Measurements.Where(m => !m.Name.StartsWith(FileScanProbeContract.UNCLASSIFIED_PREFIX, StringComparison.Ordinal)
            && !m.Name.StartsWith(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, StringComparison.Ordinal)
            && !m.Name.StartsWith(FileScanProbeContract.TEMP_PREFIX, StringComparison.Ordinal))];
        return new Finding(
            id: SUMMARY_FINDING_ID,
            category: FindingCategory.Storage,
            title: SnapshotValues.Format(CoreStrings.FileScan_Title_Summary, scanned.ToString(CultureInfo.InvariantCulture), seconds),
            measured: measured,
            evidence: SnapshotValues.Format(CoreStrings.FileScan_Evidence_Summary, Integer(FileScanProbeContract.PROTECTED_ROOT_COUNT).ToString(CultureInfo.InvariantCulture)),
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: string.Join(DETAIL_SEPARATOR, detail),
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
