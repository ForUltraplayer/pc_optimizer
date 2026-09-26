/**
 * @file    : ExplanationTests.cs
 * @author  : rudals252
 * @brief   : 설명 3줄 레코드의 길이·공백 검증과 Finding 선택 필드 보존을 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Models;

/// <summary>설명 3줄 레코드와 Finding의 설명·안전 수준 필드를 검증합니다.</summary>
public sealed class ExplanationTests
{
    /// <summary>각 줄은 비어 있지 않고 60자 이내여야 한다.</summary>
    [Theory]
    [InlineData("", "효과", "주의")]
    [InlineData("무엇", "", "주의")]
    [InlineData("무엇", "효과", "")]
    [InlineData("가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마", "효과", "주의")]
    public void RejectsEmptyOrLongLines(string what, string effect, string caution)
    {
        Assert.Throws<ArgumentException>(() => new Explanation(what, effect, caution));
    }

    /// <summary>60자 정확히는 허용한다.</summary>
    [Fact]
    public void AllowsSixtyCharacterLine()
    {
        var sixty = new string('가', Explanation.MAX_LINE_LENGTH);
        var explanation = new Explanation(sixty, "효과", "주의");
        Assert.Equal(sixty, explanation.What);
    }

    /// <summary>Finding은 설명과 안전 수준을 선택적으로 보존한다.</summary>
    [Fact]
    public void FindingKeepsOptionalExplanationAndSafety()
    {
        var explanation = new Explanation("이게 뭔가요", "효과", "주의");
        var finding = new Finding(
            "test.id", FindingCategory.Power, "제목", [], "근거", Verdict.Info, null, null, null, null, [],
            explanation, SafetyLevel.Safe);
        Assert.Same(explanation, finding.Explanation);
        Assert.Equal(SafetyLevel.Safe, finding.Safety);

        var without = new Finding("test.id", FindingCategory.Power, "제목", [], "근거", Verdict.Info, null, null, null, null, []);
        Assert.Null(without.Explanation);
        Assert.Null(without.Safety);
    }
}
