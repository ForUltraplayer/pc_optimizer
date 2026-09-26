/**
 * @file    : Winapp2Parser.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 INI 텍스트를 정리 규칙 목록과 해석 요약으로 바꾸는 순수 파서(섹션·주석·BOM·줄 끝 처리, 중복 섹션 거부, 사유별 미지원 집계). 키 해석은 Winapp2RuleBuilder가 맡고 I/O는 없다
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// winapp2 형식 파서입니다(스펙 §5.1 "winapp2 전체를 완벽하게 해석한다고 가정하지 않는다").
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>지원: 섹션 <c>[이름 *]</c>, LangSecRef/Section, Detect·DetectFile(번호 변형, OR), Default, Warning, FileKey(세미콜론 패턴·RECURSE·REMOVESELF),
/// ExcludeKey(FILE/PATH), RegKey(효과 미지원으로 개수만 기록).</item>
/// <item>알 수 없는 키, DetectOS, SpecialDetect, FILE/PATH 외 제외, 표에 없는 변수, 볼륨 루트·UNC·장치 경로, 형식 오류가 있으면 그 규칙 전체를 미지원으로 둡니다(일부만 적용하지 않음).</item>
/// <item>같은 이름의 섹션이 다시 나오면 뒤의 섹션을 형식 오류로 둡니다.</item>
/// </list>
/// </remarks>
public static class Winapp2Parser
{
    /// <summary>경로 하나에 허용하는 와일드카드 구성 요소 최대 개수.</summary>
    public const int MAX_WILDCARD_SEGMENTS = 4;

    /// <summary>섹션 이름 끝의 winapp2 표시(" *").</summary>
    public const string SECTION_SUFFIX = " *";

    private const char COMMENT_MARK = ';';
    private const char SECTION_START = '[';
    private const char SECTION_END = ']';
    private const char LINE_FEED = '\n';
    private const char BYTE_ORDER_MARK = '\uFEFF';
    private const string SECTION_DETAIL = "Section";

    /// <summary>
    /// INI 텍스트를 해석합니다.
    /// </summary>
    /// <param name="text">INI 텍스트(UTF-8로 읽은 문자열).</param>
    /// <param name="origin">규칙 출처.</param>
    /// <returns>해석 결과(원본 순서).</returns>
    public static Winapp2ParseResult Parse(string text, RuleOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(text);

        var rules = new List<CleaningRule>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ignoredLines = 0;
        Winapp2RuleBuilder? current = null;
        foreach (var rawLine in text.TrimStart(BYTE_ORDER_MARK).Split(LINE_FEED))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == COMMENT_MARK)
            {
                continue;
            }

            if (line[0] == SECTION_START && line[^1] == SECTION_END)
            {
                if (current is not null)
                {
                    rules.Add(current.Build());
                }

                var name = SectionName(line);
                current = new Winapp2RuleBuilder(name, origin);
                if (name.Length == 0 || !names.Add(name))
                {
                    current.Fail(UnsupportedRuleReason.MalformedEntry, SECTION_DETAIL);
                }

                continue;
            }

            if (current is null)
            {
                ignoredLines++;
                continue;
            }

            current.AddLine(line);
        }

        if (current is not null)
        {
            rules.Add(current.Build());
        }

        return new Winapp2ParseResult(rules.AsReadOnly(), Summarize(rules, ignoredLines));
    }

    /// <summary>
    /// 섹션 줄에서 이름을 꺼낸다(대괄호와 끝의 " *" 제거).
    /// </summary>
    private static string SectionName(string line)
    {
        var inner = line[1..^1].Trim();
        return inner.EndsWith(SECTION_SUFFIX, StringComparison.Ordinal) ? inner[..^SECTION_SUFFIX.Length].TrimEnd() : inner;
    }

    /// <summary>
    /// 사유별 미지원 수를 센다.
    /// </summary>
    private static ParseReport Summarize(List<CleaningRule> rules, int ignoredLines)
    {
        var byReason = rules
            .Where(rule => rule.UnsupportedReason is not null)
            .GroupBy(rule => rule.UnsupportedReason!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        return new ParseReport(rules.Count, rules.Count(rule => rule.IsSupported), byReason, ignoredLines);
    }
}
