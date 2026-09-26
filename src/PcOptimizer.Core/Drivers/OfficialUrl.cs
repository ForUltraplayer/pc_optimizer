/**
 * @file    : OfficialUrl.cs
 * @author  : rudals252
 * @brief   : 공식 링크 URL 엄격 해석(절대 HTTPS·기본 포트·사용자 정보 없음·DNS 호스트·끝 점/공백/제어 문자/역슬래시 거부)과 ASCII(퓨니코드) 호스트·도메인 일치 도우미
 */

// 기본 패키지
using System.Diagnostics.CodeAnalysis;

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 외부 링크 URL을 엄격하게 해석합니다. 응답·표·규칙이 준 문자열을 열기 전에 반드시 이 검사를 거칩니다.
/// 호스트는 국제화 도메인을 ASCII(퓨니코드)로 바꾼 값으로 비교해 모양이 비슷한 글자로 속일 수 없게 합니다.
/// </summary>
public static class OfficialUrl
{
    /// <summary>받아들이는 URL 최대 길이.</summary>
    public const int MAX_URL_LENGTH = 2048;

    private const char BACKSLASH = '\\';
    private const char HOST_SEPARATOR = '.';

    /// <summary>
    /// 문자열을 공식 링크 후보 URL로 해석합니다.
    /// </summary>
    /// <param name="text">URL 문자열.</param>
    /// <param name="uri">해석한 URL.</param>
    /// <returns>절대 HTTPS URL이며 사용자 정보·비기본 포트·IP 호스트·끝 점·공백·제어 문자·역슬래시가 없으면 true.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrEmpty(text) || text.Length > MAX_URL_LENGTH || text.Any(IsForbiddenCharacter))
        {
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed)
            || !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || parsed.UserInfo.Length > 0
            || !parsed.IsDefaultPort
            || parsed.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var host = parsed.IdnHost;
        if (host.Length == 0 || host.EndsWith(HOST_SEPARATOR) || host.StartsWith(HOST_SEPARATOR))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>
    /// URL의 ASCII(퓨니코드) 소문자 호스트를 돌려줍니다.
    /// </summary>
    /// <param name="uri">URL.</param>
    /// <returns>호스트.</returns>
    public static string AsciiHost(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IdnHost.ToLowerInvariant();
    }

    /// <summary>
    /// 호스트가 도메인과 같거나 그 하위 도메인인지 확인합니다(문자열 끝 일치가 아니라 점 경계 기준).
    /// </summary>
    /// <param name="asciiHost">ASCII 소문자 호스트.</param>
    /// <param name="domain">ASCII 소문자 도메인(예: "dell.com").</param>
    /// <returns>같거나 하위 도메인이면 true.</returns>
    public static bool IsHostInDomain(string asciiHost, string domain)
    {
        ArgumentNullException.ThrowIfNull(asciiHost);
        ArgumentNullException.ThrowIfNull(domain);
        return string.Equals(asciiHost, domain, StringComparison.Ordinal)
            || asciiHost.EndsWith(HOST_SEPARATOR + domain, StringComparison.Ordinal);
    }

    /// <summary>
    /// URL에 허용하지 않는 문자(공백·제어 문자·역슬래시)인지 확인한다. 셸과 URL 해석기가 다르게 읽을 수 있는 문자를 미리 거부한다.
    /// </summary>
    private static bool IsForbiddenCharacter(char character)
    {
        return char.IsWhiteSpace(character) || char.IsControl(character) || character == BACKSLASH;
    }
}
