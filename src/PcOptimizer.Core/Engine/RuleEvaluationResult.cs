/**
 * @file    : RuleEvaluationResult.cs
 * @author  : rudals252
 * @brief   : 규칙 평가 결과(모은 Finding과 실패한 규칙 수) 레코드
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 규칙 평가 결과입니다. 실패한 규칙 수는 검사 결과(ScanOutcome) 도출에 쓰입니다.
/// </summary>
/// <param name="Findings">규칙 순서대로 모은 Finding(실패한 규칙의 CannotVerify 포함, null 항목 없음).</param>
/// <param name="FailedRuleCount">예외·null 목록·null 항목으로 실패한 규칙 수.</param>
public sealed record RuleEvaluationResult(IReadOnlyList<Finding> Findings, int FailedRuleCount)
{
    /// <summary>실패한 규칙이 하나라도 있는지 여부.</summary>
    public bool HasFailures => FailedRuleCount > 0;
}
