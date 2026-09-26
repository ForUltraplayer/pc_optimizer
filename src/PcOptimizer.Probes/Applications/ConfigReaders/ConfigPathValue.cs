/**
 * @file    : ConfigPathValue.cs
 * @author  : rudals252
 * @brief   : 앱 설정 파일·환경 변수에서 읽은 경로 문자열을 검사하는 공용 도우미(따옴표 제거, 슬래시 통일, 변수 참조·상대 경로는 해석 불가로 거부, UNC는 그대로 두어 분류기가 순회 금지로 표시)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// 설정 경로 값 검사 도우미입니다. 변수 참조(%, $)는 앱마다 해석 규칙이 달라 추측하지 않고 해석 불가로 둡니다.
/// </summary>
internal static class ConfigPathValue
{
    private const string UNC_PREFIX = @"\\";
    private const string PARENT_SEGMENT = "..";
    private const char ALT_SEPARATOR = '/';
    private static readonly char[] QUOTES = ['"', '\''];
    private static readonly char[] VARIABLE_MARKS = ['%', '$'];

    /// <summary>
    /// 원시 값을 경로로 바꿉니다.
    /// </summary>
    /// <param name="raw">설정 값.</param>
    /// <returns>드라이브 절대 경로 또는 UNC 경로(정규화), 해석할 수 없으면 null.</returns>
    public static string? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim().Trim(QUOTES).Trim().Replace(ALT_SEPARATOR, PathScope.SEPARATOR);
        if (value.Length == 0 || value.IndexOfAny(VARIABLE_MARKS) >= 0)
        {
            return null;
        }

        if (value.StartsWith(UNC_PREFIX, StringComparison.Ordinal))
        {
            return value.TrimEnd(PathScope.SEPARATOR);
        }

        return PathScope.IsDriveAbsolute(value) && !PathScope.IsDriveRoot(value) && !Winapp2PathSyntax.Segments(value).Contains(PARENT_SEGMENT)
            ? PathScope.Normalize(value)
            : null;
    }
}
