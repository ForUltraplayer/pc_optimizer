/**
 * @file    : Finding.cs
 * @author  : rudals252
 * @brief   : 모든 검사 결과를 표현하는 Finding 모델과 생성 시 불변식 검증. 초보자용 설명 3줄과 안전 수준을 선택적으로 담는다
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 결과 하나입니다. 측정값과 판정·권고를 분리해 담습니다.
/// 생성자에서 불변식을 검증하며, 속성은 생성 후 바꿀 수 없습니다(with 식으로 불변식을 우회할 수 없음).
/// </summary>
/// <remarks>
/// 불변식:
/// <list type="bullet">
/// <item>Id·Title은 비어 있을 수 없다.</item>
/// <item>CannotVerify이면 CannotVerifyReason이 필수이고, 그 외 판정이면 null이어야 한다.</item>
/// <item>Candidate이면 Recommendation과 Evidence(공백이 아닌 문자열)가 필수다.</item>
/// <item>Candidate이면 설명 3줄(<see cref="Explanation"/>)과 안전 수준(<see cref="SafetyLevel"/>)이 필수다. 그 외 판정에서는 선택 필드다.</item>
/// </list>
/// 위반하면 <see cref="FindingInvariantException"/>을 던진다.
/// </remarks>
public sealed record Finding
{
    /// <summary>
    /// 불변식을 검증하며 Finding을 만듭니다.
    /// </summary>
    /// <param name="id">규칙 ID + 장치/경로 내부 식별자. 표시 순번을 영속 ID로 쓰지 않는다.</param>
    /// <param name="category">검사 분류.</param>
    /// <param name="title">한 문장 제목.</param>
    /// <param name="measured">판정에 사용한 측정값.</param>
    /// <param name="evidence">판단 근거 한 줄. Candidate이면 필수.</param>
    /// <param name="verdict">판정.</param>
    /// <param name="cannotVerifyReason">확인 불가 사유. CannotVerify일 때만 값이 있다.</param>
    /// <param name="detail">사용자용 상세 사유. 원시 예외·전체 개인 경로를 담지 않는다.</param>
    /// <param name="recommendation">권고. Candidate이면 필수.</param>
    /// <param name="impact">영향.</param>
    /// <param name="actions">사용자 동작 목록.</param>
    /// <param name="explanation">초보자용 설명 3줄. Candidate이면 필수.</param>
    /// <param name="safety">안전 수준. Candidate이면 필수.</param>
    /// <exception cref="FindingInvariantException">불변식을 어긴 경우.</exception>
    public Finding(
        string id,
        FindingCategory category,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason,
        string? detail,
        Recommendation? recommendation,
        Impact? impact,
        IReadOnlyList<FindingAction> actions,
        Explanation? explanation = null,
        SafetyLevel? safety = null)
    {
        ValidateInvariants(
            id, title, measured, evidence, verdict, cannotVerifyReason, recommendation, actions, explanation, safety);

        Id = id;
        Category = category;
        Title = title;
        Measured = [.. measured];
        Evidence = evidence;
        Verdict = verdict;
        CannotVerifyReason = cannotVerifyReason;
        Detail = detail;
        Recommendation = recommendation;
        Impact = impact;
        Actions = [.. actions];
        Explanation = explanation;
        Safety = safety;
    }

    /// <summary>규칙 ID + 장치/경로 내부 식별자.</summary>
    public string Id { get; }

    /// <summary>검사 분류.</summary>
    public FindingCategory Category { get; }

    /// <summary>한 문장 제목.</summary>
    public string Title { get; }

    /// <summary>판정에 사용한 측정값.</summary>
    public IReadOnlyList<Measurement> Measured { get; }

    /// <summary>판단 근거 한 줄.</summary>
    public string Evidence { get; }

    /// <summary>판정.</summary>
    public Verdict Verdict { get; }

    /// <summary>확인 불가 사유. CannotVerify일 때만 값이 있다.</summary>
    public CannotVerifyReason? CannotVerifyReason { get; }

    /// <summary>사용자용 상세 사유.</summary>
    public string? Detail { get; }

    /// <summary>권고.</summary>
    public Recommendation? Recommendation { get; }

    /// <summary>영향.</summary>
    public Impact? Impact { get; }

    /// <summary>사용자 동작 목록.</summary>
    public IReadOnlyList<FindingAction> Actions { get; }

    /// <summary>초보자용 설명 3줄. Candidate이면 필수.</summary>
    public Explanation? Explanation { get; }

    /// <summary>안전 수준. Candidate이면 필수.</summary>
    public SafetyLevel? Safety { get; }

    /// <summary>
    /// 생성 인자가 데이터 모델 불변식을 지키는지 검사한다.
    /// </summary>
    private static void ValidateInvariants(
        string id,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason,
        Recommendation? recommendation,
        IReadOnlyList<FindingAction> actions,
        Explanation? explanation,
        SafetyLevel? safety)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new FindingInvariantException("Finding Id는 비어 있을 수 없습니다.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new FindingInvariantException("Finding Title은 비어 있을 수 없습니다.", nameof(title));
        }

        if (measured is null)
        {
            throw new FindingInvariantException("Measured 목록은 null일 수 없습니다.", nameof(measured));
        }

        if (evidence is null)
        {
            throw new FindingInvariantException("Evidence는 null일 수 없습니다.", nameof(evidence));
        }

        if (actions is null)
        {
            throw new FindingInvariantException("Actions 목록은 null일 수 없습니다.", nameof(actions));
        }

        if (verdict == Verdict.CannotVerify && cannotVerifyReason is null)
        {
            throw new FindingInvariantException(
                "CannotVerify 판정에는 CannotVerifyReason이 필요합니다.", nameof(cannotVerifyReason));
        }

        if (verdict != Verdict.CannotVerify && cannotVerifyReason is not null)
        {
            throw new FindingInvariantException(
                $"{verdict} 판정에는 CannotVerifyReason을 지정할 수 없습니다.", nameof(cannotVerifyReason));
        }

        if (verdict == Verdict.Candidate && recommendation is null)
        {
            throw new FindingInvariantException(
                "Candidate 판정에는 Recommendation이 필요합니다.", nameof(recommendation));
        }

        if (verdict == Verdict.Candidate && string.IsNullOrWhiteSpace(evidence))
        {
            throw new FindingInvariantException(
                "Candidate 판정에는 Evidence가 필요합니다.", nameof(evidence));
        }

        if (verdict == Verdict.Candidate && explanation is null)
        {
            throw new FindingInvariantException(
                "Candidate에는 설명 3줄(Explanation)이 필요합니다.", nameof(explanation));
        }

        if (verdict == Verdict.Candidate && safety is null)
        {
            throw new FindingInvariantException(
                "Candidate에는 안전 수준(Safety)이 필요합니다.", nameof(safety));
        }
    }
}
