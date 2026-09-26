/**
 * @file    : NvidiaUrlAllowlist.cs
 * @author  : rudals252
 * @brief   : NVIDIA 조회 응답의 상세·다운로드 URL과 링크 열기에 쓰는 NVIDIA 허용 호스트(www.nvidia.com, *.download.nvidia.com) 검사
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// NVIDIA 허용 호스트 목록입니다. 조회 응답 검증(Probes)과 링크 열기 정책(App)이 같은 목록을 씁니다.
/// </summary>
public static class NvidiaUrlAllowlist
{
    /// <summary>화면에서 여는 NVIDIA 링크는 알려진 드라이버 경로만 허용합니다.</summary>
    public static bool IsAllowedLink(Uri uri)
    {
        if (!OfficialUrl.TryParse(uri.AbsoluteUri, out _) || !IsAllowedHost(uri)) { return false; }
        var path = uri.AbsolutePath;
        // 인코딩된 구분자·우회 경로를 받지 않는다.
        if (path.Contains('%', StringComparison.Ordinal)) { return false; }
        return OfficialUrl.AsciiHost(uri) == WWW_HOST
            ? path.StartsWith("/en-us/drivers/", StringComparison.Ordinal)
                || path.StartsWith("/Download/driverResults.aspx/", StringComparison.Ordinal)
            : path.StartsWith("/Windows/", StringComparison.Ordinal) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>상세 페이지 호스트.</summary>
    public const string WWW_HOST = "www.nvidia.com";

    /// <summary>다운로드 호스트 도메인(하위 도메인만 허용, 예: us.download.nvidia.com).</summary>
    public const string DOWNLOAD_DOMAIN = "download.nvidia.com";

    private const string SUBDOMAIN_SEPARATOR = ".";

    /// <summary>
    /// URL 문자열이 엄격한 HTTPS URL이며 NVIDIA 허용 호스트인지 확인합니다.
    /// </summary>
    /// <param name="url">URL 문자열.</param>
    /// <returns>허용되면 true.</returns>
    public static bool IsAllowed(string? url)
    {
        return OfficialUrl.TryParse(url, out var uri) && IsAllowedHost(uri);
    }

    /// <summary>
    /// 해석한 URL의 호스트가 NVIDIA 허용 호스트인지 확인합니다.
    /// </summary>
    /// <param name="uri">해석한 URL(<see cref="OfficialUrl.TryParse"/> 통과).</param>
    /// <returns>허용되면 true.</returns>
    public static bool IsAllowedHost(Uri uri)
    {
        var host = OfficialUrl.AsciiHost(uri);
        return string.Equals(host, WWW_HOST, StringComparison.Ordinal)
            || host.EndsWith(SUBDOMAIN_SEPARATOR + DOWNLOAD_DOMAIN, StringComparison.Ordinal);
    }
}
