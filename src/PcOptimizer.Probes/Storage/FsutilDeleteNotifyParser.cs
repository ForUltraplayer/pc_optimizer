/**
 * @file    : FsutilDeleteNotifyParser.cs
 * @author  : rudals252
 * @brief   : fsutil behavior query DisableDeleteNotify 출력(한국어·영어)에서 파일 시스템별 값만 키 이름과 끝 숫자로 읽는 순수 파서
 */

// 기본 패키지
using System.Globalization;
using System.Text.RegularExpressions;

// 사용자 패키지
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// fsutil DisableDeleteNotify 출력 파서입니다. 지역화된 설명 문구는 해석하지 않고,
/// 줄 맨 앞의 "NTFS"/"ReFS" + "DisableDeleteNotify" + "= 숫자"만 읽습니다(대소문자 무시).
/// "설정되지 않음" 줄처럼 숫자가 없는 줄은 값으로 만들지 않으며 0으로 대체하지 않습니다.
/// </summary>
public static partial class FsutilDeleteNotifyParser
{
    private const string FILE_SYSTEM_GROUP = "fs";
    private const string VALUE_GROUP = "value";
    private const int REGEX_TIMEOUT_MILLISECONDS = 200;
    private const char NULL_CHAR = '\0';

    /// <summary>
    /// 출력을 파싱합니다. 알려진 파일 시스템(<see cref="TrimPolicyProbeContract.FileSystems"/>)만 담으며 같은 파일 시스템은 첫 값을 씁니다.
    /// </summary>
    /// <param name="output">fsutil 표준 출력(없으면 null).</param>
    /// <returns>파일 시스템 이름(계약 표기) → DisableDeleteNotify 원시 값.</returns>
    public static IReadOnlyDictionary<string, long> Parse(string? output)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(output))
        {
            return values;
        }

        var text = output.Replace(NULL_CHAR.ToString(), string.Empty, StringComparison.Ordinal);
        foreach (Match match in DeleteNotifyLine().Matches(text))
        {
            var fileSystem = CanonicalFileSystem(match.Groups[FILE_SYSTEM_GROUP].Value);
            if (fileSystem is null
                || values.ContainsKey(fileSystem)
                || !long.TryParse(match.Groups[VALUE_GROUP].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            values[fileSystem] = value;
        }

        return values;
    }

    /// <summary>
    /// 출력의 파일 시스템 이름을 계약 표기로 바꾼다. 알 수 없으면 null.
    /// </summary>
    private static string? CanonicalFileSystem(string raw)
    {
        return TrimPolicyProbeContract.FileSystems.FirstOrDefault(name => string.Equals(name, raw, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 줄 맨 앞(공백 허용)의 "파일 시스템 DisableDeleteNotify = 숫자"를 찾는 정규식.
    /// </summary>
    [GeneratedRegex(
        @"^[ \t]*(?<fs>[A-Za-z0-9]+)[ \t]+DisableDeleteNotify[ \t]*=[ \t]*(?<value>[0-9]+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex DeleteNotifyLine();
}
