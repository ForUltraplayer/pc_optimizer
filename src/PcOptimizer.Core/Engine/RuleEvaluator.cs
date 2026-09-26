/**
 * @file    : RuleEvaluator.cs
 * @author  : rudals252
 * @brief   : 등록된 모든 판정 규칙을 스냅샷에 적용해 Finding을 모으고, 규칙 결함을 CannotVerify로 격리함
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 등록된 판정 규칙을 스냅샷에 적용해 Finding을 모읍니다.
/// 한 규칙이 예외를 던지거나 null을 돌려줘도 다른 규칙의 결과는 유지하고, 실패한 규칙은 CannotVerify로 드러냅니다.
/// </summary>
public sealed class RuleEvaluator
{
    /// <summary>규칙 실패 Finding ID 접두사.</summary>
    public const string RULE_FAILURE_FINDING_ID_PREFIX = "rule-failure:";

    private const string LOG_CATEGORY = nameof(RuleEvaluator);

    private readonly IReadOnlyList<IRule> _rules;
    private readonly IAppLogger _logger;

    /// <summary>
    /// 규칙 평가기를 만듭니다.
    /// </summary>
    /// <param name="rules">판정 규칙 목록.</param>
    /// <param name="logger">공용 로거.</param>
    public RuleEvaluator(IEnumerable<IRule> rules, IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(logger);

        _rules = [.. rules];
        _logger = logger;
    }

    /// <summary>
    /// 모든 규칙을 스냅샷에 적용해 Finding을 모읍니다.
    /// 예외·null 목록·null 항목을 낸 규칙은 실패로 세고 그 규칙마다 CannotVerify 하나를 추가합니다(null 항목은 버리고 나머지는 유지).
    /// </summary>
    /// <param name="snapshot">검사 스냅샷.</param>
    /// <returns>규칙 순서대로 모은 Finding과 실패한 규칙 수.</returns>
    public RuleEvaluationResult Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var findings = new List<Finding>();
        var failedRuleCount = 0;
        foreach (var rule in _rules)
        {
            try
            {
                var ruleFindings = rule.Evaluate(snapshot);
                if (ruleFindings is null)
                {
                    _logger.Error(LOG_CATEGORY, $"RuleReturnedNull rule={rule.Id} scan={snapshot.ScanId}");
                    findings.Add(CreateRuleFailureFinding(rule.Id));
                    failedRuleCount++;
                    continue;
                }

                var validFindings = ruleFindings.Where(finding => finding is not null).ToArray();
                findings.AddRange(validFindings);
                if (validFindings.Length != ruleFindings.Count)
                {
                    _logger.Error(LOG_CATEGORY, $"RuleReturnedNullItem rule={rule.Id} scan={snapshot.ScanId}");
                    findings.Add(CreateRuleFailureFinding(rule.Id));
                    failedRuleCount++;
                }
            }
            catch (Exception ex)
            {
                // 규칙 결함 하나가 다른 규칙 결과를 버리지 않도록 모든 예외를 CannotVerify로 격리한다.
                // 예외 원문에는 개인 경로가 들어갈 수 있으므로 형식 이름만 기록한다.
                _logger.Error(LOG_CATEGORY, $"RuleFailed rule={rule.Id} scan={snapshot.ScanId} error={ex.GetType().Name}");
                findings.Add(CreateRuleFailureFinding(rule.Id));
                failedRuleCount++;
            }
        }

        return new RuleEvaluationResult(findings.AsReadOnly(), failedRuleCount);
    }

    /// <summary>
    /// 실행에 실패한 규칙을 드러내는 CannotVerify를 만든다.
    /// </summary>
    private static Finding CreateRuleFailureFinding(string ruleId)
    {
        return new Finding(
            id: RULE_FAILURE_FINDING_ID_PREFIX + ruleId,
            category: FindingCategory.Unclassified,
            title: CoreStrings.CannotVerify_Title_RuleFailed,
            measured: [],
            evidence: CoreStrings.CannotVerify_Evidence_RuleFailed,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: CannotVerifyReason.ProbeError,
            detail: null,
            recommendation: null,
            impact: null,
            actions: []);
    }
}
