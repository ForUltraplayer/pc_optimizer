/**
 * @file    : ParseReport.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 INI 해석 결과 요약(전체·지원·사유별 미지원 규칙 수, 섹션 밖 무시 줄 수)과 해석 결과(규칙 목록 + 요약) 레코드
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// INI 해석 요약입니다. 리포트에 지원/건너뛴 규칙 수와 사유를 기록하는 데 씁니다(스펙 §5.1).
/// </summary>
/// <param name="TotalRules">섹션(규칙) 수.</param>
/// <param name="SupportedRules">지원 규칙 수.</param>
/// <param name="UnsupportedByReason">사유별 미지원 규칙 수(0인 사유는 없음).</param>
/// <param name="IgnoredLines">섹션 밖에 있어 무시한 주석 아닌 줄 수.</param>
public sealed record ParseReport(
    int TotalRules,
    int SupportedRules,
    IReadOnlyDictionary<UnsupportedRuleReason, int> UnsupportedByReason,
    int IgnoredLines)
{
    /// <summary>미지원 규칙 수.</summary>
    public int UnsupportedRules => TotalRules - SupportedRules;
}

/// <summary>
/// INI 해석 결과입니다.
/// </summary>
/// <param name="Rules">규칙(원본 순서, 미지원 포함).</param>
/// <param name="Report">요약.</param>
public sealed record Winapp2ParseResult(IReadOnlyList<CleaningRule> Rules, ParseReport Report);
