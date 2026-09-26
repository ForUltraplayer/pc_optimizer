/**
 * @file    : Winapp2PathSyntax.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 경로 템플릿의 정적 검사(UNC·장치 경로, 표에 없는 변수, 상대 경로·"..", 볼륨 루트, 와일드카드 구성 요소 수)와 경로 구성 요소·와일드카드 판별 도우미(순수 문자열 처리)
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 경로 템플릿 정적 검사 결과입니다.
/// </summary>
/// <param name="Reason">미지원 사유(문제가 없으면 null).</param>
/// <param name="Detail">보조 정보(변수 이름 등).</param>
public readonly record struct PathSyntaxCheck(UnsupportedRuleReason? Reason, string? Detail)
{
    /// <summary>문제가 없는 결과.</summary>
    public static PathSyntaxCheck Ok => default;

    /// <summary>문제가 없는지 여부.</summary>
    public bool IsOk => Reason is null;
}

/// <summary>
/// 경로 템플릿 정적 검사 도우미입니다. 실행 시 변수 값을 펼친 뒤의 볼륨 루트·UNC 검사는 Probes의 경로 해석기가 다시 합니다.
/// </summary>
public static class Winapp2PathSyntax
{
    /// <summary>와일드카드 문자.</summary>
    public static readonly char[] WILDCARDS = ['*', '?'];

    private const string UNC_PREFIX = @"\\";
    private const string ALT_UNC_PREFIX = "//";
    private const string PARENT_SEGMENT = "..";
    private const char DRIVE_SEPARATOR = ':';
    private const int DRIVE_PREFIX_LENGTH = 2;
    private static readonly char[] SEPARATORS = ['\\', '/'];

    /// <summary>
    /// 경로 템플릿을 검사합니다.
    /// </summary>
    /// <param name="template">끝 구분자를 뗀 경로 템플릿.</param>
    /// <param name="keyName">문제 보고용 키 이름(예: "FileKey").</param>
    /// <returns>검사 결과.</returns>
    public static PathSyntaxCheck Check(string template, string keyName)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Length == 0)
        {
            return new PathSyntaxCheck(UnsupportedRuleReason.MalformedEntry, keyName);
        }

        if (template.StartsWith(UNC_PREFIX, StringComparison.Ordinal) || template.StartsWith(ALT_UNC_PREFIX, StringComparison.Ordinal))
        {
            return new PathSyntaxCheck(UnsupportedRuleReason.UncOrDevicePath, keyName);
        }

        if (!Winapp2Variables.TryGetNames(template, out var names))
        {
            return new PathSyntaxCheck(UnsupportedRuleReason.MalformedEntry, keyName);
        }

        var unknown = names.FirstOrDefault(name => !Winapp2Variables.IsSupported(name));
        if (unknown is not null)
        {
            return new PathSyntaxCheck(UnsupportedRuleReason.UnresolvedVariable, unknown);
        }

        var segments = Segments(template);
        if (!StartsAtRoot(template) || segments.Any(segment => segment == PARENT_SEGMENT))
        {
            return new PathSyntaxCheck(UnsupportedRuleReason.MalformedEntry, keyName);
        }

        if (IsVolumeRoot(segments))
        {
            return new PathSyntaxCheck(UnsupportedRuleReason.VolumeRootPath, keyName);
        }

        return segments.Count(HasWildcard) > Winapp2Parser.MAX_WILDCARD_SEGMENTS
            ? new PathSyntaxCheck(UnsupportedRuleReason.WildcardBoundExceeded, keyName)
            : PathSyntaxCheck.Ok;
    }

    /// <summary>
    /// 경로를 구성 요소로 나눕니다(빈 구성 요소 제외).
    /// </summary>
    /// <param name="path">경로.</param>
    /// <returns>구성 요소.</returns>
    public static string[] Segments(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 구성 요소에 와일드카드(* ?)가 있는지 확인합니다.
    /// </summary>
    /// <param name="segment">구성 요소.</param>
    /// <returns>있으면 true.</returns>
    public static bool HasWildcard(string segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return segment.IndexOfAny(WILDCARDS) >= 0;
    }

    /// <summary>
    /// 끝 구분자를 뗀 템플릿을 돌려줍니다.
    /// </summary>
    /// <param name="template">템플릿.</param>
    /// <returns>정리한 템플릿.</returns>
    public static string TrimTrailingSeparators(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Trim().TrimEnd(SEPARATORS);
    }

    /// <summary>
    /// 변수(%이름%)나 드라이브 문자("C:")로 시작하는지 확인한다.
    /// </summary>
    private static bool StartsAtRoot(string template)
    {
        if (template[0] == Winapp2Variables.MARK)
        {
            return true;
        }

        return template.Length >= DRIVE_PREFIX_LENGTH && char.IsAsciiLetter(template[0]) && template[1] == DRIVE_SEPARATOR;
    }

    /// <summary>
    /// 구성 요소가 하나뿐이고 %SystemDrive% 또는 "C:"이면 볼륨 루트다.
    /// </summary>
    private static bool IsVolumeRoot(string[] segments)
    {
        if (segments.Length != 1)
        {
            return false;
        }

        var only = segments[0];
        var systemDrive = Winapp2Variables.MARK + Winapp2Variables.SYSTEM_DRIVE + Winapp2Variables.MARK;
        return string.Equals(only, systemDrive, StringComparison.OrdinalIgnoreCase)
            || (only.Length == DRIVE_PREFIX_LENGTH && char.IsAsciiLetter(only[0]) && only[1] == DRIVE_SEPARATOR);
    }
}
