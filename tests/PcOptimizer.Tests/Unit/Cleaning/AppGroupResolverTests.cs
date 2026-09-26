/**
 * @file    : AppGroupResolverTests.cs
 * @author  : rudals252
 * @brief   : 앱 카드 묶음 기준 계산(숫자가 아닌 Section 값, 숫자 LangSecRef·Section 무시, 범주 Games·Adobe 제외, 공유 규칙 수 100개 한도)과 포함 스냅샷의 묶음 결과를 검증
 */

// 기본 패키지
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications;

namespace PcOptimizer.Tests.Unit.Cleaning;

/// <summary>
/// <see cref="AppGroupResolver"/>를 검증합니다. 구성 규칙은 winapp2 형식 문자열을 실제 파서로 해석해 만듭니다.
/// </summary>
public sealed class AppGroupResolverTests
{
    /// <summary>
    /// 같은 Section 값을 가진 규칙 N개를 만든다.
    /// </summary>
    private static IReadOnlyList<CleaningRule> SharedSection(string section, int count)
    {
        var text = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            text.Append("[Tool ").Append(index).Append(" *]\n")
                .Append("Section=").Append(section).Append('\n')
                .Append("DetectFile=%LocalAppData%\\Tool").Append(index).Append('\n')
                .Append("FileKey1=%LocalAppData%\\Tool").Append(index).Append("|*.log\n\n");
        }

        return Winapp2Parser.Parse(text.ToString(), RuleOrigin.Community).Rules;
    }

    /// <summary>
    /// Section 텍스트 값이 있으면 그 값(LangSecRef가 함께 있어도), 숫자 LangSecRef만 있거나 Section 값이 숫자면 규칙 이름(" *" 제거)이다.
    /// </summary>
    [Fact]
    public void 숫자가_아닌_Section_값_또는_규칙_이름이다()
    {
        const string TEXT = """
            [Google Chrome Caches *]
            Section=Google Chrome Web Browser
            DetectFile=%LocalAppData%\Google\Chrome*
            FileKey1=%LocalAppData%\Google\Chrome*\User Data|*-journal|RECURSE

            [Both Keys *]
            LangSecRef=3021
            Section=Vendor Suite
            DetectFile=%LocalAppData%\Vendor
            FileKey1=%LocalAppData%\Vendor|*.log

            [Lang Only *]
            LangSecRef=3022
            DetectFile=%LocalAppData%\Vendor
            FileKey1=%LocalAppData%\Vendor|*.tmp

            [Numeric Section *]
            Section=3021
            DetectFile=%LocalAppData%\Vendor
            FileKey1=%LocalAppData%\Vendor|*.bak
            """;

        var groups = AppGroupResolver.Resolve(Winapp2Parser.Parse(TEXT, RuleOrigin.Community).Rules);

        Assert.Equal("Google Chrome Web Browser", groups["winapp2:Google Chrome Caches"]);
        Assert.Equal("Vendor Suite", groups["winapp2:Both Keys"]);
        Assert.Equal("Lang Only", groups["winapp2:Lang Only"]);
        Assert.Equal("Numeric Section", groups["winapp2:Numeric Section"]);
    }

    /// <summary>범주·제품군 Section 값(Games·Adobe, 대소문자 무시)은 공유 수가 적어도 앱 기준이 아니고 규칙 이름을 쓴다.</summary>
    [Theory]
    [InlineData("Games")]
    [InlineData("Adobe")]
    [InlineData("games")]
    public void 범주_Section은_앱_기준이_아니다(string section)
    {
        var groups = AppGroupResolver.Resolve(SharedSection(section, 3));

        Assert.Equal(["Tool 0", "Tool 1", "Tool 2"], groups.Values.Order(StringComparer.Ordinal));
    }

    /// <summary>같은 Section 값을 공유하는 규칙이 100개면 한 앱으로 묶고, 101개면 범주로 보고 규칙 이름을 쓴다.</summary>
    [Theory]
    [InlineData(AppGroupResolver.MAX_RULES_PER_APP_SECTION, 1)]
    [InlineData(AppGroupResolver.MAX_RULES_PER_APP_SECTION + 1, AppGroupResolver.MAX_RULES_PER_APP_SECTION + 1)]
    public void 공유_규칙_수가_한도_이하일_때만_Section으로_묶는다(int count, int expectedKeys)
    {
        var groups = AppGroupResolver.Resolve(SharedSection("Vendor Suite", count));

        Assert.Equal(count, groups.Count);
        Assert.Equal(expectedKeys, groups.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(count == AppGroupResolver.MAX_RULES_PER_APP_SECTION, groups.Values.All(value => value == "Vendor Suite"));
    }

    /// <summary>
    /// 포함 스냅샷: Games(521개)·Adobe(31개)는 묶음 기준이 되지 않고(이름이 "Adobe"인 규칙 하나만 자기 이름을 씀), 앱 Section인 Chrome(22개)·PowerToys(30개)는 묶이며, 어떤 묶음도 한도를 넘지 않는다.
    /// </summary>
    [Fact]
    public void 포함_스냅샷의_범주_Section은_묶지_않는다()
    {
        var catalog = RuleCatalogLoader.CreateEmbedded().Load();
        var groups = catalog.AppGroups;

        Assert.True(catalog.IsVerified);
        Assert.Equal(catalog.Rules.Count, groups.Count);
        Assert.All(groups.Where(pair => AppGroupResolver.CATEGORY_SECTIONS.Contains(pair.Value)), pair => Assert.Equal(CleaningRule.COMMUNITY_ID_PREFIX + pair.Value, pair.Key));
        Assert.Equal(22, groups.Values.Count(value => value == "Google Chrome Web Browser"));
        Assert.Equal(30, groups.Values.Count(value => value == "Microsoft PowerToys"));
        Assert.All(groups.Values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase), group => Assert.True(group.Count() <= AppGroupResolver.MAX_RULES_PER_APP_SECTION, group.Key));
    }
}
