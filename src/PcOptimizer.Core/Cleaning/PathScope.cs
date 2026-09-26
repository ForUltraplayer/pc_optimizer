/**
 * @file    : PathScope.cs
 * @author  : rudals252
 * @brief   : Windows 경로 문자열의 순수 비교 도우미(구분자 통일·끝 구분자 제거, 대소문자 무시·디렉터리 경계 단위 포함 판정, 부모·마지막 이름 추출). 파일 시스템에 접근하지 않는다
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 경로 문자열 비교 도우미입니다(스펙 §5.1 "대소문자 무시·디렉터리 경계 단위로 비교").
/// 실제 정규화(전체 경로·긴 이름 변환)는 Probes가 하고, 여기서는 이미 절대 경로인 문자열만 다룹니다.
/// </summary>
public static class PathScope
{
    /// <summary>Windows 디렉터리 구분자.</summary>
    public const char SEPARATOR = '\\';

    private const char ALT_SEPARATOR = '/';
    private const char DRIVE_SEPARATOR = ':';
    private const int DRIVE_ROOT_LENGTH = 3;
    private const int DRIVE_LETTER_INDEX = 0;
    private const int DRIVE_COLON_INDEX = 1;
    private const string UNC_PREFIX = @"\\";

    /// <summary>
    /// '/'를 '\'로 바꾸고 앞뒤 공백과 끝 구분자를 없앱니다(드라이브 루트는 "C:\" 형태로 유지).
    /// </summary>
    /// <param name="path">절대 경로.</param>
    /// <returns>비교용 경로.</returns>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmed = path.Replace(ALT_SEPARATOR, SEPARATOR).Trim().TrimEnd(SEPARATOR);
        return IsDriveOnly(trimmed) ? trimmed + SEPARATOR : trimmed;
    }

    /// <summary>
    /// "C:\" 같은 드라이브 루트인지 확인합니다.
    /// </summary>
    /// <param name="path">경로.</param>
    /// <returns>드라이브 루트이면 true.</returns>
    public static bool IsDriveRoot(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return IsDriveOnly(path.Replace(ALT_SEPARATOR, SEPARATOR).Trim().TrimEnd(SEPARATOR));
    }

    /// <summary>
    /// 드라이브 문자로 시작하는 절대 경로인지 확인합니다(UNC·장치 경로·상대 경로는 false).
    /// </summary>
    /// <param name="path">경로.</param>
    /// <returns>드라이브 절대 경로이면 true.</returns>
    public static bool IsDriveAbsolute(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var unified = path.Replace(ALT_SEPARATOR, SEPARATOR).Trim();
        return unified.Length >= DRIVE_ROOT_LENGTH
            && !unified.StartsWith(UNC_PREFIX, StringComparison.Ordinal)
            && char.IsAsciiLetter(unified[DRIVE_LETTER_INDEX])
            && unified[DRIVE_COLON_INDEX] == DRIVE_SEPARATOR
            && unified[DRIVE_ROOT_LENGTH - 1] == SEPARATOR;
    }

    /// <summary>
    /// 경로가 기준 경로와 같거나 그 아래인지 디렉터리 경계 단위로 확인합니다(대소문자 무시).
    /// "C:\Users\ab"는 "C:\Users\a" 아래가 아닙니다.
    /// </summary>
    /// <param name="path">확인할 경로.</param>
    /// <param name="root">기준 경로.</param>
    /// <returns>같거나 아래이면 true.</returns>
    public static bool IsSameOrUnder(string path, string root)
    {
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(root);
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || IsStrictlyUnderNormalized(normalizedPath, normalizedRoot);
    }

    /// <summary>
    /// 경로가 기준 경로의 하위(같은 경로 제외)인지 확인합니다.
    /// </summary>
    /// <param name="path">확인할 경로.</param>
    /// <param name="root">기준 경로.</param>
    /// <returns>하위이면 true.</returns>
    public static bool IsStrictlyUnder(string path, string root)
    {
        return IsStrictlyUnderNormalized(Normalize(path), Normalize(root));
    }

    /// <summary>
    /// 부모 경로를 돌려줍니다. 드라이브 루트나 구분자가 없는 경로는 null입니다.
    /// </summary>
    /// <param name="path">경로.</param>
    /// <returns>부모 경로 또는 null.</returns>
    public static string? GetParent(string path)
    {
        var normalized = Normalize(path);
        if (IsDriveOnly(normalized.TrimEnd(SEPARATOR)))
        {
            return null;
        }

        var index = normalized.LastIndexOf(SEPARATOR);
        if (index < 0)
        {
            return null;
        }

        return index == DRIVE_ROOT_LENGTH - 1 ? normalized[..DRIVE_ROOT_LENGTH] : normalized[..index];
    }

    /// <summary>
    /// 마지막 구성 요소(폴더·파일 이름)를 돌려줍니다. 드라이브 루트는 그대로입니다.
    /// </summary>
    /// <param name="path">경로.</param>
    /// <returns>마지막 이름.</returns>
    public static string GetLeafName(string path)
    {
        var normalized = Normalize(path);
        if (IsDriveOnly(normalized.TrimEnd(SEPARATOR)))
        {
            return normalized;
        }

        var index = normalized.LastIndexOf(SEPARATOR);
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    /// <summary>
    /// 끝 구분자가 없는 "C:" 형태인지 확인한다.
    /// </summary>
    private static bool IsDriveOnly(string trimmed)
    {
        return trimmed.Length == DRIVE_COLON_INDEX + 1
            && char.IsAsciiLetter(trimmed[DRIVE_LETTER_INDEX])
            && trimmed[DRIVE_COLON_INDEX] == DRIVE_SEPARATOR;
    }

    /// <summary>
    /// 정규화된 두 경로에서 하위 관계를 확인한다.
    /// </summary>
    private static bool IsStrictlyUnderNormalized(string path, string root)
    {
        if (path.Length <= root.Length || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return root[^1] == SEPARATOR || path[root.Length] == SEPARATOR;
    }
}
