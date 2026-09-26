/**
 * @file    : IHttpTransport.cs
 * @author  : rudals252
 * @brief   : 작은 HTTPS GET 응답(상태 코드·본문)을 한 번만 받는 전송 계약과 응답 레코드(리디렉션을 따르지 않음). 테스트에서 저장한 HTTP 응답 fixture로 바꿔 끼운다
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// HTTP 응답 하나입니다. 리디렉션(3xx)은 따라가지 않고 상태 코드 그대로 돌려줍니다.
/// </summary>
/// <param name="StatusCode">HTTP 상태 코드.</param>
/// <param name="Body">응답 본문(2xx가 아니면 비어 있을 수 있음).</param>
public sealed record HttpTransportResponse(int StatusCode, string Body);

/// <summary>
/// HTTPS GET 전송 계약입니다. 구현은 재시도·리디렉션 추종을 하지 않으며, 네트워크 오류는 <see cref="HttpRequestException"/>,
/// 취소는 <see cref="OperationCanceledException"/>으로 알립니다.
/// </summary>
public interface IHttpTransport
{
    /// <summary>
    /// GET 요청을 한 번 보냅니다.
    /// </summary>
    /// <param name="uri">요청 URL.</param>
    /// <param name="ct">취소 토큰(검사 취소·프로브 타임아웃).</param>
    /// <returns>응답.</returns>
    Task<HttpTransportResponse> GetAsync(Uri uri, CancellationToken ct);
}
