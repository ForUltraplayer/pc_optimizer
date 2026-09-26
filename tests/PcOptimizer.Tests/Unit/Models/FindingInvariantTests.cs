/**
 * @file    : FindingInvariantTests.cs
 * @author  : rudals252
 * @brief   : Finding 모델 불변식(판정별 사유·권고·근거 필수 조건, Id 필수)을 검증하는 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Models;

/// <summary>
/// Finding 생성 시 데이터 모델 불변식이 강제되는지 확인합니다.
/// </summary>
public class FindingInvariantTests
{
    private const string VALID_ID = "display.refresh-rate:monitor-a";
    private const string VALID_TITLE = "모니터에서 더 높은 주사율 후보를 찾았어요";
    private const string VALID_EVIDENCE = "지원 모드 목록에 더 높은 주사율이 있습니다";

    private static readonly Recommendation VALID_RECOMMENDATION =
        new("설정에서 더 높은 주사율 후보 확인", "동일 해상도 조건 확인 후 선택");

    /// <summary>
    /// 테스트용 Finding을 만든다. 지정하지 않은 값은 유효한 기본값을 쓴다.
    /// </summary>
    private static Finding CreateFinding(
        Verdict verdict,
        CannotVerifyReason? reason = null,
        Recommendation? recommendation = null,
        string evidence = VALID_EVIDENCE,
        string id = VALID_ID)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Display,
            title: VALID_TITLE,
            measured: [],
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: reason,
            detail: null,
            recommendation: recommendation,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }

    /// <summary>
    /// CannotVerify 판정은 사유가 없으면 생성할 수 없다.
    /// </summary>
    [Fact]
    public void CannotVerify는_사유가_없으면_예외를_던진다()
    {
        Assert.Throws<FindingInvariantException>(() => CreateFinding(Verdict.CannotVerify, reason: null));
    }

    /// <summary>
    /// CannotVerify 판정은 사유가 있으면 생성되고 사유를 보존한다.
    /// </summary>
    [Fact]
    public void CannotVerify는_사유가_있으면_생성된다()
    {
        var finding = CreateFinding(Verdict.CannotVerify, reason: CannotVerifyReason.Timeout);

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Timeout, finding.CannotVerifyReason);
    }

    /// <summary>
    /// CannotVerify가 아닌 판정에 사유를 넣으면 생성할 수 없다.
    /// </summary>
    [Theory]
    [InlineData(Verdict.Ok)]
    [InlineData(Verdict.Info)]
    [InlineData(Verdict.Candidate)]
    public void CannotVerify가_아닌_판정에_사유가_있으면_예외를_던진다(Verdict verdict)
    {
        Assert.Throws<FindingInvariantException>(() => CreateFinding(
            verdict,
            reason: CannotVerifyReason.ProbeError,
            recommendation: VALID_RECOMMENDATION));
    }

    /// <summary>
    /// Candidate 판정은 권고가 없으면 생성할 수 없다.
    /// </summary>
    [Fact]
    public void Candidate는_권고가_없으면_예외를_던진다()
    {
        Assert.Throws<FindingInvariantException>(() => CreateFinding(Verdict.Candidate, recommendation: null));
    }

    /// <summary>
    /// Candidate 판정은 근거가 비어 있으면 생성할 수 없다.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Candidate는_근거가_비어_있으면_예외를_던진다(string evidence)
    {
        Assert.Throws<FindingInvariantException>(() => CreateFinding(
            Verdict.Candidate,
            recommendation: VALID_RECOMMENDATION,
            evidence: evidence));
    }

    /// <summary>
    /// Candidate 판정은 권고와 근거가 모두 있으면 생성된다.
    /// </summary>
    [Fact]
    public void Candidate는_권고와_근거가_있으면_생성된다()
    {
        var finding = CreateFinding(Verdict.Candidate, recommendation: VALID_RECOMMENDATION);

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Null(finding.CannotVerifyReason);
        Assert.Equal(VALID_RECOMMENDATION, finding.Recommendation);
    }

    /// <summary>
    /// Id가 비어 있으면 어떤 판정이든 생성할 수 없다.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Id가_비어_있으면_예외를_던진다(string id)
    {
        Assert.Throws<FindingInvariantException>(() => CreateFinding(Verdict.Ok, id: id));
    }

    /// <summary>
    /// 불변식 위반 예외는 ArgumentException 계열이어야 한다.
    /// </summary>
    [Fact]
    public void 불변식_위반_예외는_ArgumentException이다()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateFinding(Verdict.CannotVerify, reason: null));
    }

    /// <summary>
    /// Ok·Info 판정은 권고 없이 생성된다.
    /// </summary>
    [Theory]
    [InlineData(Verdict.Ok)]
    [InlineData(Verdict.Info)]
    public void Ok와_Info는_권고_없이_생성된다(Verdict verdict)
    {
        var finding = CreateFinding(verdict);

        Assert.Equal(verdict, finding.Verdict);
        Assert.Null(finding.Recommendation);
    }
}
