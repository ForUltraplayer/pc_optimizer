/**
 * @file    : RulePatternMatcher.cs
 * @author  : rudals252
 * @brief   : 규칙 패턴 비교 도우미(파일 이름 Win32 와일드카드 일치, "모든 파일" 패턴 판별, 와일드카드가 든 폴더 패턴과 실제 폴더의 구성 요소 단위 일치·포함 판정). 파일 시스템에 접근하지 않는다
 */

// 기본 패키지
using System.IO.Enumeration;

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 규칙 패턴 비교 도우미입니다. 파일 이름 패턴은 CCleaner 형식 관례대로 Windows(FindFirstFile) 규칙으로 비교합니다(예: "*.*"는 확장자 없는 이름도 포함).
/// </summary>
public static class RulePatternMatcher
{
    /// <summary>모든 파일 패턴.</summary>
    public const string ALL_FILES = "*";

    /// <summary>모든 파일 패턴(Windows 관례).</summary>
    public const string ALL_FILES_DOTTED = "*.*";

    /// <summary>
    /// 이름이 Win32 와일드카드 패턴과 맞는지 확인합니다(대소문자 무시).
    /// </summary>
    /// <param name="pattern">패턴.</param>
    /// <param name="name">파일·폴더 이름.</param>
    /// <returns>맞으면 true.</returns>
    public static bool Matches(string pattern, string name)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(name);
        return FileSystemName.MatchesWin32Expression(FileSystemName.TranslateWin32Expression(pattern), name, ignoreCase: true);
    }

    /// <summary>
    /// 이름이 패턴 중 하나와 맞는지 확인합니다.
    /// </summary>
    /// <param name="patterns">패턴 목록.</param>
    /// <param name="name">이름.</param>
    /// <returns>하나라도 맞으면 true.</returns>
    public static bool MatchesAny(IReadOnlyList<string> patterns, string name)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns.Any(pattern => Matches(pattern, name));
    }

    /// <summary>
    /// 패턴 목록이 모든 파일을 뜻하는지 확인합니다("*" 또는 "*.*").
    /// </summary>
    /// <param name="patterns">패턴 목록.</param>
    /// <returns>모든 파일이면 true.</returns>
    public static bool IsAllFiles(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns.Any(pattern => pattern is ALL_FILES or ALL_FILES_DOTTED);
    }

    /// <summary>
    /// 폴더 패턴과 실제 폴더가 구성 요소 수가 같고 각 구성 요소가 맞는지 확인합니다.
    /// </summary>
    /// <param name="directoryPattern">폴더 패턴(구성 요소에 와일드카드 가능).</param>
    /// <param name="directory">실제 폴더.</param>
    /// <returns>맞으면 true.</returns>
    public static bool DirectoryMatches(string directoryPattern, string directory)
    {
        var pattern = Winapp2PathSyntax.Segments(directoryPattern);
        var actual = Winapp2PathSyntax.Segments(directory);
        return pattern.Length == actual.Length && PrefixMatches(pattern, actual, pattern.Length);
    }

    /// <summary>
    /// 실제 폴더가 폴더 패턴과 같거나 그 아래인지 확인합니다.
    /// </summary>
    /// <param name="directory">실제 폴더.</param>
    /// <param name="directoryPattern">폴더 패턴.</param>
    /// <returns>같거나 아래이면 true.</returns>
    public static bool IsSameOrUnderPattern(string directory, string directoryPattern)
    {
        var pattern = Winapp2PathSyntax.Segments(directoryPattern);
        var actual = Winapp2PathSyntax.Segments(directory);
        return actual.Length >= pattern.Length && PrefixMatches(pattern, actual, pattern.Length);
    }

    /// <summary>
    /// 폴더 패턴이 실제 폴더의 하위(같은 폴더 제외)에 있는 어떤 폴더와 맞을 수 있는지 확인합니다.
    /// </summary>
    /// <param name="directoryPattern">폴더 패턴.</param>
    /// <param name="directory">실제 폴더.</param>
    /// <returns>하위에서 맞을 수 있으면 true.</returns>
    public static bool CouldMatchStrictlyUnder(string directoryPattern, string directory)
    {
        var pattern = Winapp2PathSyntax.Segments(directoryPattern);
        var actual = Winapp2PathSyntax.Segments(directory);
        return pattern.Length > actual.Length && PrefixMatches(pattern, actual, actual.Length);
    }

    /// <summary>
    /// 앞쪽 구성 요소 count개가 서로 맞는지 확인한다.
    /// </summary>
    private static bool PrefixMatches(string[] pattern, string[] actual, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var matches = Winapp2PathSyntax.HasWildcard(pattern[index])
                ? Matches(pattern[index], actual[index])
                : string.Equals(pattern[index], actual[index], StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }
}
