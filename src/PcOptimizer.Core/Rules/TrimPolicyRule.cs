/**
 * @file    : TrimPolicyRule.cs
 * @author  : rudals252
 * @brief   : 파일 시스템별 OS 삭제 알림(TRIM) 정책 값(DisableDeleteNotify)을 정보(켜짐)·조건부 확인 후보(꺼짐)·지원 불가(알 수 없는 값)로 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// TRIM 정책 규칙입니다(스펙 §5 저장소 행 중 TRIM).
/// <list type="bullet">
/// <item>0: Info 'OS 삭제 알림(TRIM) 정책: 켜짐'.</item>
/// <item>1: 확인을 권하는 조건부 Candidate(정책 꺼짐).</item>
/// <item>그 밖의 값: CannotVerify(Unsupported). 값이 없는 파일 시스템은 판정하지 않습니다.</item>
/// </list>
/// 모든 문구는 OS 정책 값임을 밝히며, 저장 장치의 TRIM 지원·실제 수행 여부와 구분합니다.
/// 일반 권한에서는 프로브가 건너뛰어지고 상태 변환기가 CannotVerify(ElevationRequired)로 드러냅니다.
/// </summary>
public sealed class TrimPolicyRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "storage.trim";

    /// <summary>파일 시스템별 Finding ID 접두사(뒤에 소문자 파일 시스템 이름이 붙음).</summary>
    public const string FINDING_ID_PREFIX = "trim-policy:";

    /// <summary>Candidate 안전 수준(레지스트리 정책 값 변경, 되돌리기는 가능하나 확인이 필요).</summary>
    private static readonly SafetyLevel CANDIDATE_SAFETY = SafetyLevel.Caution;

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(TrimPolicyProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var findings = new List<Finding>();
        foreach (var fileSystem in TrimPolicyProbeContract.FileSystems)
        {
            var measurement = snapshot.GetMeasurement(TrimPolicyProbeContract.PROBE_ID, TrimPolicyProbeContract.DisableDeleteNotifyName(fileSystem));
            if (measurement?.Value is IntegerValue value)
            {
                findings.Add(EvaluateFileSystem(fileSystem, measurement, value.Value));
            }
        }

        return findings;
    }

    /// <summary>
    /// 파일 시스템 하나의 정책 값을 판정한다.
    /// </summary>
    private static Finding EvaluateFileSystem(string fileSystem, Measurement measurement, long value)
    {
        var id = FINDING_ID_PREFIX + fileSystem.ToLowerInvariant();
        var raw = value.ToString(CultureInfo.InvariantCulture);
        var evidence = SnapshotValues.Format(CoreStrings.Trim_Evidence, fileSystem, raw);

        return value switch
        {
            TrimPolicyProbeContract.DELETE_NOTIFY_ENABLED => Create(
                id,
                SnapshotValues.Format(CoreStrings.Trim_Title_Enabled, fileSystem),
                measurement,
                evidence,
                Verdict.Info,
                cannotVerifyReason: null,
                recommendation: null,
                impact: null),
            TrimPolicyProbeContract.DELETE_NOTIFY_DISABLED => Create(
                id,
                SnapshotValues.Format(CoreStrings.Trim_Title_Disabled, fileSystem),
                measurement,
                evidence,
                Verdict.Candidate,
                cannotVerifyReason: null,
                new Recommendation(CoreStrings.Trim_Recommendation_Text, CoreStrings.Trim_Recommendation_Condition),
                new Impact(CoreStrings.Trim_Impact_Benefit, CoreStrings.Trim_Impact_SideEffect)),
            _ => Create(
                id,
                SnapshotValues.Format(CoreStrings.Trim_Title_Unknown, fileSystem),
                measurement,
                SnapshotValues.Format(CoreStrings.Trim_Evidence_Unknown, fileSystem, raw),
                Verdict.CannotVerify,
                CannotVerifyReason.Unsupported,
                recommendation: null,
                impact: null),
        };
    }

    /// <summary>
    /// TRIM 정책 Finding을 만든다(설정 URI 없음).
    /// </summary>
    private static Finding Create(
        string id,
        string title,
        Measurement measurement,
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
            measured: [measurement],
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: null,
            recommendation: recommendation,
            impact: impact,
            actions: actions,
            explanation: verdict == Verdict.Candidate
                ? new Explanation(CoreStrings.Trim_Explain_What, CoreStrings.Trim_Explain_Effect, CoreStrings.Trim_Explain_Caution)
                : null,
            safety: verdict == Verdict.Candidate ? CANDIDATE_SAFETY : null);
    }
}
