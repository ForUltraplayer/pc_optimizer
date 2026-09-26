/**
 * @file    : PersonalDataScrubber.cs
 * @author  : rudals252
 * @brief   : 문자열에서 사용자 프로필 경로·사용자명·PC명을 자리표시자로 바꾸는 개인정보 치환기(내보내기·로그 공용)
 */

// 기본 패키지
using System.Text.RegularExpressions;

namespace PcOptimizer.App.Services;

/// <summary>
/// 사용자 프로필 경로, 사용자명, PC명을 자리표시자로 바꿉니다.
/// 경로는 대소문자와 구분자(\ 또는 /)를 가리지 않고, 이름은 영문·숫자 단어 경계에서만 바꿔 영문 단어를 망가뜨리지 않습니다
/// (한국어 조사가 붙은 이름은 치환합니다).
/// 경로 해시로 익명화했다고 간주하지 않고 원문 자체를 없앱니다.
/// </summary>
public sealed class PersonalDataScrubber
{
    /// <summary>사용자 프로필 경로 자리표시자.</summary>
    public const string PROFILE_PLACEHOLDER = "%USERPROFILE%";

    /// <summary>사용자명 자리표시자.</summary>
    public const string USER_PLACEHOLDER = "<user>";

    /// <summary>PC명 자리표시자.</summary>
    public const string MACHINE_PLACEHOLDER = "<machine>";

    /// <summary>정규식 시간 제한(비정상 입력에서 멈추지 않도록).</summary>
    private static readonly TimeSpan REGEX_TIMEOUT = TimeSpan.FromSeconds(1);

    // 경계는 영문·숫자·밑줄만 본다. 한국어 조사("이름의")처럼 이름 뒤에 붙는 글자가 있어도 치환되도록 한다.
    private const string WORD_BEFORE = @"(?<![A-Za-z0-9_])";
    private const string WORD_AFTER = @"(?![A-Za-z0-9_])";
    private const string ANY_SEPARATOR = @"[\\/]";
    private const RegexOptions MATCH_OPTIONS = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private readonly Regex[] _patterns;
    private readonly string[] _replacements;

    /// <summary>
    /// 치환할 값으로 치환기를 만듭니다. null·공백 값은 치환하지 않습니다.
    /// </summary>
    /// <param name="profilePath">사용자 프로필 경로(예: C:\Users\이름).</param>
    /// <param name="userName">사용자명.</param>
    /// <param name="machineName">PC명.</param>
    public PersonalDataScrubber(string? profilePath, string? userName, string? machineName)
    {
        var patterns = new List<Regex>();
        var replacements = new List<string>();

        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            var trimmed = profilePath.TrimEnd('\\', '/');
            var parts = trimmed.Split(['\\', '/']).Select(Regex.Escape);
            patterns.Add(new Regex(string.Join(ANY_SEPARATOR, parts) + WORD_AFTER, MATCH_OPTIONS, REGEX_TIMEOUT));
            replacements.Add(PROFILE_PLACEHOLDER);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            patterns.Add(new Regex(WORD_BEFORE + Regex.Escape(userName.Trim()) + WORD_AFTER, MATCH_OPTIONS, REGEX_TIMEOUT));
            replacements.Add(USER_PLACEHOLDER);
        }

        if (!string.IsNullOrWhiteSpace(machineName))
        {
            patterns.Add(new Regex(WORD_BEFORE + Regex.Escape(machineName.Trim()) + WORD_AFTER, MATCH_OPTIONS, REGEX_TIMEOUT));
            replacements.Add(MACHINE_PLACEHOLDER);
        }

        _patterns = [.. patterns];
        _replacements = [.. replacements];
    }

    /// <summary>
    /// 현재 사용자·PC 환경 값으로 치환기를 만듭니다.
    /// </summary>
    /// <returns>치환기.</returns>
    public static PersonalDataScrubber FromEnvironment()
    {
        return new PersonalDataScrubber(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.UserName,
            Environment.MachineName);
    }

    /// <summary>
    /// 문자열의 개인 정보를 자리표시자로 바꿉니다. 프로필 경로 → 사용자명 → PC명 순서로 바꿉니다.
    /// </summary>
    /// <param name="text">원문.</param>
    /// <returns>치환한 문자열.</returns>
    public string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = text;
        for (var index = 0; index < _patterns.Length; index++)
        {
            var replacement = _replacements[index];
            result = _patterns[index].Replace(result, _ => replacement);
        }

        return result;
    }
}
