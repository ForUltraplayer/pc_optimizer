/**
 * @file    : HttpClientTransport.cs
 * @author  : rudals252
 * @brief   : HttpClient 기반 HTTPS GET 전송(리디렉션 미추종·쿠키 미사용·응답 크기 제한·클라이언트 자체 타임아웃 없음, 취소 토큰으로만 중단). 재시도하지 않는다
 */

// 기본 패키지
using System.Net;
using System.Net.Http;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 실제 네트워크로 GET 요청을 보내는 전송입니다. 요청 한 번에 응답 한 번이며 재시도·리디렉션 추종이 없습니다.
/// 시간 제한은 프로브 실행기의 타임아웃(취소 토큰)이 맡습니다.
/// </summary>
public sealed class HttpClientTransport : IHttpTransport
{
    /// <summary>받아들이는 응답 본문 최대 크기(바이트). 넘으면 네트워크 오류로 처리합니다.</summary>
    public const long MAX_RESPONSE_BYTES = 4 * 1024 * 1024;

    /// <summary>요청 User-Agent 제품 이름.</summary>
    public const string USER_AGENT_PRODUCT = "PcOptimizer";

    /// <summary>요청 User-Agent 제품 버전.</summary>
    public const string USER_AGENT_VERSION = "1.0";

    /// <summary>연결 재사용 최대 기간.</summary>
    private static readonly TimeSpan POOLED_CONNECTION_LIFETIME = TimeSpan.FromMinutes(2);

    private static readonly Lazy<HttpClientTransport> SHARED = new(() => new HttpClientTransport());

    private readonly HttpClient _client;

    /// <summary>
    /// 전송을 만듭니다(리디렉션 미추종, 쿠키 미사용, 자동 압축 해제).
    /// </summary>
    private HttpClientTransport()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = POOLED_CONNECTION_LIFETIME,
        };

        _client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = MAX_RESPONSE_BYTES,
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue(USER_AGENT_PRODUCT, USER_AGENT_VERSION));
    }

    /// <summary>앱 전체에서 공유하는 전송(HttpClient 하나를 재사용).</summary>
    public static HttpClientTransport Shared => SHARED.Value;

    /// <inheritdoc />
    public async Task<HttpTransportResponse> GetAsync(Uri uri, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
            : string.Empty;
        return new HttpTransportResponse((int)response.StatusCode, body);
    }
}
