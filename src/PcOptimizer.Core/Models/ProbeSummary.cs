/**
 * @file    : ProbeSummary.cs
 * @author  : rudals252
 * @brief   : 리포트에 담는 프로브별 실행 요약(상태·소요 시간·Issue 수·종료 중 여부) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 리포트에 담는 프로브별 실행 요약입니다.
/// </summary>
/// <param name="ProbeId">프로브 ID.</param>
/// <param name="Status">수집 상태.</param>
/// <param name="Duration">소요 시간.</param>
/// <param name="IssueCount">Issue 수.</param>
/// <param name="IsStillRunning">
/// 리포트 작성 시점에 호출이 아직 끝나지 않았는지 여부(타임아웃·취소 후 종료 중). true이면 "취소/중단 완료"로 표시하지 않는다.
/// </param>
public sealed record ProbeSummary(
    string ProbeId,
    ProbeStatus Status,
    TimeSpan Duration,
    int IssueCount,
    bool IsStillRunning);
