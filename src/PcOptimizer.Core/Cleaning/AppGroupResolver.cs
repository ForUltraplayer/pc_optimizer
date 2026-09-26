/**
 * @file    : AppGroupResolver.cs
 * @author  : rudals252
 * @brief   : 규칙 목록 전체를 보고 규칙마다 앱 카드 묶음 기준(앱 이름)을 정하는 순수 도우미(숫자가 아니고 범주·제품군 목록에 없으며 공유 규칙 수가 한도 이하인 winapp2 Section= 값, 아니면 규칙 이름)
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 앱 카드 묶음 기준 계산기입니다. Section 값을 공유하는 규칙 수가 필요하므로 규칙 목록을 불러올 때 한 번 계산합니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>winapp2 <c>Section=</c> 텍스트 값은 대부분 앱 하나(예: "Google Chrome Web Browser" 22개 규칙)이지만, 일부는 범주·제품군이라 서로 다른 앱을 묶습니다.</item>
/// <item>숫자 값(<c>LangSecRef</c> 또는 숫자 Section)은 분류 번호라 쓰지 않습니다.</item>
/// <item>조건에 맞지 않으면 규칙 이름(섹션 이름에서 끝의 " *"를 뺀 값)을 씁니다. <see cref="CleaningRule.Section"/>은 해석한 값 그대로 둡니다.</item>
/// </list>
/// </remarks>
public static class AppGroupResolver
{
    /// <summary>
    /// 앱이 아닌 범주·제품군 Section 값(대소문자 무시). 포함 스냅샷 v260915 기준 "Games"는 서로 다른 게임 521개 규칙,
    /// "Adobe"는 Adobe 제품 31개 규칙이 공유합니다. 나머지 66개 값은 앱 하나(가장 큰 값은 "Microsoft PowerToys" 30개)입니다.
    /// </summary>
    public static readonly IReadOnlySet<string> CATEGORY_SECTIONS = new HashSet<string>(["Games", "Adobe"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 한 앱의 Section으로 보는 최대 공유 규칙 수. 스냅샷의 앱별 Section은 최대 30개이며, 새 스냅샷에서 목록에 없는 범주 값이 생겨도 크게 묶지 않도록 둔 한도입니다.
    /// </summary>
    public const int MAX_RULES_PER_APP_SECTION = 100;

    /// <summary>
    /// 규칙 ID별 앱 묶음 기준을 계산합니다.
    /// </summary>
    /// <param name="rules">불러온 규칙 전체(공유 수는 이 목록 안에서 셈).</param>
    /// <returns>규칙 ID → 앱 이름.</returns>
    public static IReadOnlyDictionary<string, string> Resolve(IEnumerable<CleaningRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var list = rules.ToList();
        var counts = list
            .Select(TextSection)
            .OfType<string>()
            .GroupBy(section => section, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in list)
        {
            var section = TextSection(rule);
            result[rule.Id] = section is not null && !CATEGORY_SECTIONS.Contains(section) && counts[section] <= MAX_RULES_PER_APP_SECTION
                ? section
                : rule.Name;
        }

        return result;
    }

    /// <summary>
    /// 숫자가 아닌 Section 텍스트 값(없으면 null).
    /// </summary>
    private static string? TextSection(CleaningRule rule)
    {
        var section = rule.Section?.Trim();
        return string.IsNullOrEmpty(section) || section.All(char.IsAsciiDigit) ? null : section;
    }
}
