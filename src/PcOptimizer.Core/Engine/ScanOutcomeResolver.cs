/**
 * @file    : ScanOutcomeResolver.cs
 * @author  : rudals252
 * @brief   : 프로브 결과·규칙 실패·사용자 취소 여부로 검사 단위 결과(Completed/Partial/Cancelled)를 도출
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 검사 단위 결과를 도출합니다.
/// 사용자 취소면 Cancelled, 실패·타임아웃·부분 수집·비정책 건너뜀·판정 규칙 실패가 하나라도 있으면 Partial, 그 밖에는 Completed입니다.
/// </summary>
public static class ScanOutcomeResolver
{
    /// <summary>
    /// 검사가 불완전하다고 보지 않는 "정책상 건너뜀" 사유입니다(권한 필요·온라인 미요청·지원 안 함).
    /// </summary>
    private static readonly CannotVerifyReason[] POLICY_SKIP_REASONS =
    [
        CannotVerifyReason.ElevationRequired,
        CannotVerifyReason.NotRequested,
        CannotVerifyReason.Unsupported,
    ];

    /// <summary>
    /// 검사 단위 결과를 도출합니다.
    /// </summary>
    /// <param name="cancellationRequested">사용자가 검사를 취소했는지 여부.</param>
    /// <param name="results">프로브 결과 목록.</param>
    /// <param name="ruleFailuresOccurred">판정 규칙이 하나라도 실패했는지 여부.</param>
    /// <returns>검사 단위 결과.</returns>
    public static ScanOutcome Resolve(bool cancellationRequested, IEnumerable<ProbeResult> results, bool ruleFailuresOccurred)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (cancellationRequested)
        {
            return ScanOutcome.Cancelled;
        }

        if (ruleFailuresOccurred)
        {
            return ScanOutcome.Partial;
        }

        return results.All(IsComplete) ? ScanOutcome.Completed : ScanOutcome.Partial;
    }

    /// <summary>
    /// 결과 하나가 "의도대로 끝까지 수집됨"인지 판단한다.
    /// </summary>
    private static bool IsComplete(ProbeResult result)
    {
        return result.Status switch
        {
            ProbeStatus.Success => true,
            ProbeStatus.Skipped => result.Issues.All(issue => POLICY_SKIP_REASONS.Contains(issue.Reason)),
            _ => false,
        };
    }
}
