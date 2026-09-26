/**
 * @file    : PathTokenizer.cs
 * @author  : rudals252
 * @brief   : 기본(익명화) 내보내기 한 번 안에서 프로필 자리표시자 아래 경로(%USERPROFILE%\...)를 처음 본 순서의 folder-N 토큰으로 바꾸고, 같은 Finding의 문장에 작은따옴표로 인용된 그 경로의 마지막 폴더 이름도 같은 토큰으로 바꾸는 치환기
 */

// 기본 패키지
using System.Globalization;
using System.Text.RegularExpressions;

namespace PcOptimizer.App.Services;

/// <summary>
/// 프로필 하위 경로 토큰화기입니다(스펙 §8 "폴더 상세 이름/전체 경로는 기본 내보내기에 넣지 않음", 경로 해시로 익명화했다고 보지 않음).
/// <see cref="PersonalDataScrubber"/>가 프로필 경로를 <c>%USERPROFILE%</c>로 바꾼 뒤에 적용하며, 프로필 밖(ProgramData·시스템) 경로는 그대로 둡니다.
/// 같은 경로(대소문자·구분자·끝 구분자 무시)는 한 내보내기 안에서 같은 토큰이 되고, 내보내기마다 새 인스턴스를 씁니다.
/// </summary>
/// <remarks>
/// 문장 속 경로는 따옴표·파이프·꺾쇠·와일드카드·콜론·세미콜론·줄바꿈·탭에서 끝나며, 구성 요소는 공백·마침표로 끝나지 않습니다(Windows 이름 규칙).
/// 공백이 든 폴더 이름을 지키려고 경로 뒤에 공백으로 이어진 낱말까지 경로로 볼 수 있으나, 이는 더 많이 가리는 쪽입니다.
/// </remarks>
public sealed partial class PathTokenizer
{
    /// <summary>토큰 접두사.</summary>
    public const string TOKEN_PREFIX = "folder-";

    private const string SEPARATOR = @"\";
    private const string TOKEN_FORMAT = "{0}{1}";
    private const int FIRST_TOKEN_NUMBER = 1;
    private const int REGEX_TIMEOUT_MILLISECONDS = 1000;
    private const string GROUP_RELATIVE = "rel";
    private const string GROUP_TOKEN = "token";
    private const string QUOTE = "'";
    private static readonly TimeSpan REGEX_TIMEOUT = TimeSpan.FromMilliseconds(REGEX_TIMEOUT_MILLISECONDS);
    private static readonly char[] SEPARATORS = ['\\', '/'];

    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _leafByToken = new(StringComparer.Ordinal);

    /// <summary>
    /// 문자열 안의 프로필 하위 경로를 <c>%USERPROFILE%\folder-N</c>으로 바꿉니다.
    /// </summary>
    /// <param name="text">개인정보 치환을 마친 문자열.</param>
    /// <returns>치환한 문자열.</returns>
    public string Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ProfilePathPattern().Replace(
            text, match => PersonalDataScrubber.PROFILE_PLACEHOLDER + SEPARATOR + TokenFor(match.Groups[GROUP_RELATIVE].Value));
    }

    /// <summary>
    /// 문자열에 들어 있는 경로 토큰(folder-N)을 찾습니다.
    /// </summary>
    /// <param name="text">토큰화한 문자열.</param>
    /// <returns>이 인스턴스가 만든 토큰 목록.</returns>
    public IReadOnlyCollection<string> FindTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. TokenPattern().Matches(text)
            .Select(match => match.Groups[GROUP_TOKEN].Value)
            .Where(_leafByToken.ContainsKey)
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// 지정한 토큰이 가리키는 경로의 마지막 이름이 문장에서 작은따옴표로 인용된 곳('이름')만 토큰으로 바꿉니다(대소문자 무시).
    /// 규칙 문장은 폴더·항목 이름을 작은따옴표로 인용하므로, 인용하지 않은 고정 문구(예: 제품 이름 "OneDrive")는 바꾸지 않습니다.
    /// 같은 Finding 안의 문장(제목·근거·상세)에만 적용해 다른 항목의 이름을 건드리지 않습니다.
    /// </summary>
    /// <param name="text">문장.</param>
    /// <param name="tokens">같은 Finding에서 찾은 토큰.</param>
    /// <returns>치환한 문장.</returns>
    public string ReplaceLeafNames(string text, IReadOnlyCollection<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(tokens);

        var result = text;
        foreach (var token in tokens.Where(_leafByToken.ContainsKey).OrderByDescending(t => _leafByToken[t].Length))
        {
            var pattern = new Regex(QUOTE + Regex.Escape(_leafByToken[token]) + QUOTE, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, REGEX_TIMEOUT);
            result = pattern.Replace(result, _ => QUOTE + token + QUOTE);
        }

        return result;
    }

    /// <summary>
    /// 상대 경로의 토큰을 돌려준다. 처음 보면 다음 번호를 붙이고 마지막 이름을 기억한다.
    /// </summary>
    private string TokenFor(string relative)
    {
        var key = relative.Replace('/', '\\').Trim(SEPARATORS);
        if (_tokens.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var token = string.Format(CultureInfo.InvariantCulture, TOKEN_FORMAT, TOKEN_PREFIX, _tokens.Count + FIRST_TOKEN_NUMBER);
        _tokens[key] = token;
        var lastSeparator = key.LastIndexOf('\\');
        _leafByToken[token] = lastSeparator >= 0 ? key[(lastSeparator + 1)..] : key;
        return token;
    }

    /// <summary>
    /// 프로필 자리표시자 아래 경로(구성 요소 하나 이상).
    /// </summary>
    [GeneratedRegex(
        @"%USERPROFILE%(?<rel>(?:[\\/][^\\/""|<>*?:;\r\n\t]*[^\\/""|<>*?:;\r\n\t .])+)[\\/]?",
        RegexOptions.CultureInvariant,
        REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex ProfilePathPattern();

    /// <summary>
    /// 이미 토큰화한 경로.
    /// </summary>
    [GeneratedRegex(@"%USERPROFILE%\\(?<token>folder-[0-9]+)(?![0-9])", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex TokenPattern();
}
