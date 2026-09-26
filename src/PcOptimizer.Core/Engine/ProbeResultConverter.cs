/**
 * @file    : ProbeResultConverter.cs
 * @author  : rudals252
 * @brief   : 프로브 상태(건너뜀/실패/취소/부분)를 CannotVerify Finding으로 변환해 실패가 빈 결과나 Ok로 사라지지 않게 함
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 프로브 수집 상태를 화면용 Finding으로 변환합니다.
/// 건너뜀·실패·취소는 프로브마다 정확히 하나의 CannotVerify가 되고, 부분 수집은 CannotVerify(PartialData)를 추가로 만듭니다.
/// 성공 결과는 규칙 평가에 맡기며 여기서는 Finding을 만들지 않습니다.
/// </summary>
public static class ProbeResultConverter
{
    /// <summary>프로브 상태 Finding ID 접두사.</summary>
    public const string STATUS_FINDING_ID_PREFIX = "probe-status:";

    /// <summary>부분 수집 Finding ID 접두사.</summary>
    public const string PARTIAL_FINDING_ID_PREFIX = "probe-partial:";

    /// <summary>
    /// 스냅샷의 프로브 상태를 Finding 목록으로 변환합니다.
    /// </summary>
    /// <param name="snapshot">검사 스냅샷.</param>
    /// <param name="probeCategories">프로브 ID별 분류. 없으면 Unclassified로 표시합니다.</param>
    /// <returns>상태 변환 Finding 목록(스냅샷 순서).</returns>
    public static IReadOnlyList<Finding> Convert(
        ScanSnapshot snapshot,
        IReadOnlyDictionary<string, FindingCategory> probeCategories)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(probeCategories);

        var findings = new List<Finding>();
        foreach (var result in snapshot.ProbeResults)
        {
            var category = probeCategories.TryGetValue(result.ProbeId, out var known)
                ? known
                : FindingCategory.Unclassified;

            switch (result.Status)
            {
                case ProbeStatus.Skipped:
                case ProbeStatus.Failed:
                case ProbeStatus.Cancelled:
                    findings.Add(CreateStatusFinding(result, category));
                    break;
                case ProbeStatus.Partial:
                    findings.Add(CreatePartialFinding(result, category));
                    break;
                case ProbeStatus.Success:
                default:
                    break;
            }
        }

        return findings;
    }

    /// <summary>
    /// 건너뜀·실패·취소 결과의 CannotVerify를 만든다. 사유는 첫 Issue를 따르고, Issue가 없으면 상태별 기본 사유를 쓴다.
    /// </summary>
    private static Finding CreateStatusFinding(ProbeResult result, FindingCategory category)
    {
        var reason = result.Issues.Count > 0 ? result.Issues[0].Reason : DefaultReasonFor(result.Status);
        var title = result.Status switch
        {
            ProbeStatus.Skipped => CannotVerifyTexts.TITLE_SKIPPED,
            ProbeStatus.Cancelled => CannotVerifyTexts.TITLE_CANCELLED,
            _ => CannotVerifyTexts.TITLE_FAILED,
        };

        return new Finding(
            id: STATUS_FINDING_ID_PREFIX + result.ProbeId,
            category: category,
            title: title,
            measured: result.Measurements,
            evidence: CannotVerifyTexts.EvidenceFor(reason),
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: reason,
            detail: null,
            recommendation: null,
            impact: null,
            actions: []);
    }

    /// <summary>
    /// 부분 수집 결과의 CannotVerify(PartialData)를 만든다. 수집된 측정값은 규칙이 따로 판정한다.
    /// </summary>
    private static Finding CreatePartialFinding(ProbeResult result, FindingCategory category)
    {
        return new Finding(
            id: PARTIAL_FINDING_ID_PREFIX + result.ProbeId,
            category: category,
            title: CannotVerifyTexts.TITLE_PARTIAL,
            measured: [],
            evidence: CannotVerifyTexts.EVIDENCE_PARTIAL_DATA,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: CannotVerifyReason.PartialData,
            detail: null,
            recommendation: null,
            impact: null,
            actions: []);
    }

    /// <summary>
    /// Issue가 없는 비정상 상태의 기본 사유를 정한다.
    /// </summary>
    private static CannotVerifyReason DefaultReasonFor(ProbeStatus status)
    {
        return status switch
        {
            ProbeStatus.Cancelled => CannotVerifyReason.Cancelled,
            ProbeStatus.Skipped => CannotVerifyReason.Unsupported,
            _ => CannotVerifyReason.ProbeError,
        };
    }
}
