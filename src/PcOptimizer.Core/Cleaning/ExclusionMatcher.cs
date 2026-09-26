/**
 * @file    : ExclusionMatcher.cs
 * @author  : rudals252
 * @brief   : 규칙 제외 조건(FILE: 지정 폴더 직접 파일, PATH: 지정 폴더 하위 전체)이 관측 대상·파일·하위 트리에 적용되는지 판정하는 순수 도우미
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 제외 조건 판정 도우미입니다. 제외는 관측을 줄이는 방향으로만 작동합니다.
/// </summary>
public static class ExclusionMatcher
{
    /// <summary>
    /// 제외 조건이 관측 대상 범위 안의 파일에 영향을 줄 수 있는지 확인합니다.
    /// </summary>
    /// <param name="exclusion">제외 조건.</param>
    /// <param name="directory">관측 대상 폴더.</param>
    /// <param name="recurse">관측 대상이 하위 폴더를 포함하는지 여부.</param>
    /// <returns>영향을 줄 수 있으면 true.</returns>
    public static bool AppliesTo(ExclusionSpec exclusion, string directory, bool recurse)
    {
        ArgumentNullException.ThrowIfNull(exclusion);
        ArgumentNullException.ThrowIfNull(directory);
        return exclusion.Kind switch
        {
            ExcludeKind.Path => RulePatternMatcher.IsSameOrUnderPattern(directory, exclusion.DirectoryPattern)
                || (recurse && RulePatternMatcher.CouldMatchStrictlyUnder(exclusion.DirectoryPattern, directory)),
            _ => RulePatternMatcher.DirectoryMatches(exclusion.DirectoryPattern, directory)
                || (recurse && RulePatternMatcher.CouldMatchStrictlyUnder(exclusion.DirectoryPattern, directory)),
        };
    }

    /// <summary>
    /// 파일 하나가 제외되는지 확인합니다.
    /// </summary>
    /// <param name="exclusions">적용할 제외 조건.</param>
    /// <param name="fileDirectory">파일이 든 폴더.</param>
    /// <param name="fileName">파일 이름.</param>
    /// <returns>제외되면 true.</returns>
    public static bool IsExcluded(IReadOnlyList<ExclusionSpec> exclusions, string fileDirectory, string fileName)
    {
        ArgumentNullException.ThrowIfNull(exclusions);
        foreach (var exclusion in exclusions)
        {
            var inScope = exclusion.Kind == ExcludeKind.Path
                ? RulePatternMatcher.IsSameOrUnderPattern(fileDirectory, exclusion.DirectoryPattern)
                : RulePatternMatcher.DirectoryMatches(exclusion.DirectoryPattern, fileDirectory);
            if (inScope && RulePatternMatcher.MatchesAny(exclusion.Patterns, fileName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 폴더 하위 전체가 모든 파일을 빼는 PATH 제외 안인지 확인합니다(들어갈 필요 없음).
    /// </summary>
    /// <param name="exclusions">적용할 제외 조건.</param>
    /// <param name="directory">폴더.</param>
    /// <returns>하위 전체가 제외되면 true.</returns>
    public static bool ExcludesSubtree(IReadOnlyList<ExclusionSpec> exclusions, string directory)
    {
        ArgumentNullException.ThrowIfNull(exclusions);
        return exclusions.Any(exclusion => exclusion.Kind == ExcludeKind.Path
            && RulePatternMatcher.IsAllFiles(exclusion.Patterns)
            && RulePatternMatcher.IsSameOrUnderPattern(directory, exclusion.DirectoryPattern));
    }
}
