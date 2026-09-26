/**
 * @file    : FixtureHttpTransport.cs
 * @author  : rudals252
 * @brief   : 요청 URL 조건별로 저장한 HTTP 응답(fixture 본문·상태 코드) 또는 예외를 돌려주고 요청 URL을 기록하는 테스트용 전송(실제 네트워크 없음)과 NVIDIA fixture 읽기 도우미
 */

// 기본 패키지
using System.IO;
using System.Net.Http;
using System.Text;

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 저장한 HTTP 응답을 돌려주는 가짜 전송입니다. 등록하지 않은 URL은 404입니다.
/// </summary>
internal sealed class FixtureHttpTransport : IHttpTransport
{
    private readonly List<(Func<Uri, bool> Match, Func<HttpTransportResponse> Respond)> _routes = [];

    /// <summary>요청한 URL(호출 순서).</summary>
    public List<Uri> Requests { get; } = [];

    /// <summary>
    /// URL 조건에 본문 200 응답을 등록한다.
    /// </summary>
    public FixtureHttpTransport On(Func<Uri, bool> match, string body, int statusCode = 200)
    {
        _routes.Add((match, () => new HttpTransportResponse(statusCode, body)));
        return this;
    }

    /// <summary>
    /// URL 조건에 예외를 등록한다.
    /// </summary>
    public FixtureHttpTransport Throw(Func<Uri, bool> match, Exception exception)
    {
        _routes.Add((match, () => throw exception));
        return this;
    }

    /// <inheritdoc />
    public Task<HttpTransportResponse> GetAsync(Uri uri, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Requests.Add(uri);
        foreach (var (match, respond) in _routes)
        {
            if (match(uri))
            {
                return Task.FromResult(respond());
            }
        }

        return Task.FromResult(new HttpTransportResponse(404, string.Empty));
    }
}

/// <summary>
/// NVIDIA 응답 fixture(Fixtures/Nvidia, 2026-09-26 실제 응답 저장본) 읽기 도우미입니다.
/// </summary>
internal static class NvidiaFixtures
{
    /// <summary>제품 목록(lookupValueSearch TypeID=3) 일부.</summary>
    public const string PRODUCT_LIST = "product-list.xml";

    /// <summary>Game Ready 목록(psid=127, pfid=1041, numberOfResults=2).</summary>
    public const string GAME_READY_2 = "game-ready-2.json";

    /// <summary>Game Ready 목록(numberOfResults=10).</summary>
    public const string GAME_READY_10 = "game-ready-10.json";

    /// <summary>Studio 목록(upCRD=1, isWHQL=0, numberOfResults=10).</summary>
    public const string STUDIO_10 = "studio-10.json";

    /// <summary>Studio 목록 없음 응답(upCRD=1, isWHQL=1).</summary>
    public const string STUDIO_NOT_FOUND = "studio-not-found.json";

    /// <summary>fixture의 GPU 이름.</summary>
    public const string GPU_NAME = "NVIDIA GeForce RTX 4080 SUPER";

    /// <summary>
    /// fixture 본문을 읽는다.
    /// </summary>
    public static string Read(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Nvidia", fileName), Encoding.UTF8);
    }

    /// <summary>제품 목록 요청인지.</summary>
    public static bool IsProductList(Uri uri) => uri.AbsoluteUri.Contains("lookupValueSearch", StringComparison.Ordinal);

    /// <summary>Game Ready 목록 요청인지.</summary>
    public static bool IsGameReady(Uri uri) => uri.Query.Contains("DriverManualLookup", StringComparison.Ordinal) && !uri.Query.Contains("upCRD=1", StringComparison.Ordinal);

    /// <summary>Studio 목록 요청인지.</summary>
    public static bool IsStudio(Uri uri) => uri.Query.Contains("upCRD=1", StringComparison.Ordinal);

    /// <summary>
    /// 제품 목록·Game Ready·Studio fixture를 모두 등록한 전송을 만든다.
    /// </summary>
    public static FixtureHttpTransport CompleteTransport(string gameReady = GAME_READY_2, string studio = STUDIO_10)
    {
        return new FixtureHttpTransport()
            .On(IsProductList, Read(PRODUCT_LIST))
            .On(IsGameReady, Read(gameReady))
            .On(IsStudio, Read(studio));
    }

    /// <summary>
    /// 네트워크 연결 실패를 흉내 내는 예외를 만든다.
    /// </summary>
    public static HttpRequestException ConnectionFailure() => new("fake connection failure");
}
