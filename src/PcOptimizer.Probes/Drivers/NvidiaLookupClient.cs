/**
 * @file    : NvidiaLookupClient.cs
 * @author  : rudals252
 * @brief   : NVIDIA 공개 조회(제품 목록 XML, Game Ready/Studio 드라이버 목록 JSON)를 전송 계약 뒤에서 한 번씩만 요청하고 응답을 검증하는 어댑터(재시도·리디렉션 추종·다운로드 없음)
 */

// 기본 패키지
using System.Globalization;
using System.Net.Http;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// NVIDIA 공개 조회 어댑터입니다(스펙 §5: 보장된 SDK가 아닌 변경 가능한 어댑터로 취급).
/// 요청마다 한 번만 보내며(재시도 없음), 3xx는 따라가지 않고 NetworkFailed로 처리합니다. 응답은 두 가지 작은 목록뿐이며 드라이버 파일은 내려받지 않습니다.
/// 네트워크 오류·HTTP 오류·형식 오류는 결과(<see cref="NvidiaLookupFailure"/>)로 돌려주고, 취소만 예외로 전파합니다.
/// </summary>
public sealed class NvidiaLookupClient
{
    /// <summary>드라이버 목록 요청 OS ID(Windows 10/11 64비트 공용 배포, 응답의 OSList로 Windows 11 대상 확인).</summary>
    public const int OS_ID = 57;

    /// <summary>요청 언어 코드(en-US).</summary>
    public const int LANGUAGE_CODE = 1033;

    /// <summary>계열별로 받는 최근 항목 수.</summary>
    public const int RESULT_COUNT = 10;

    /// <summary>드라이버 목록 조회 기본 주소.</summary>
    public const string DRIVER_LOOKUP_BASE = "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php";

    /// <summary>Game Ready 목록 매개변수(WHQL, DCH).</summary>
    public const string GAME_READY_PARAMETERS = "isWHQL=1&dch=1";

    /// <summary>Studio 목록 매개변수(2026-09-26 실제 응답으로 확인: upCRD=1은 isWHQL=0일 때만 Studio 목록을 돌려줌).</summary>
    public const string STUDIO_PARAMETERS = "isWHQL=0&dch=1&upCRD=1";

    /// <summary>제품 목록(TypeID=3) 주소.</summary>
    public static readonly Uri PRODUCT_LIST_URI = new("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3");

    private const int HTTP_OK_MIN = 200;
    private const int HTTP_OK_MAX = 299;
    private const int HTTP_REDIRECT_MIN = 300;
    private const int HTTP_REDIRECT_MAX = 399;

    private readonly IHttpTransport _transport;

    /// <summary>
    /// 조회 어댑터를 만듭니다.
    /// </summary>
    /// <param name="transport">HTTP 전송(테스트에서는 저장한 응답 fixture).</param>
    public NvidiaLookupClient(IHttpTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
    }

    /// <summary>
    /// 드라이버 목록 요청 주소를 만듭니다.
    /// </summary>
    /// <param name="product">제품.</param>
    /// <param name="kind">목록 종류.</param>
    /// <returns>주소.</returns>
    public static Uri DriverListUri(NvidiaProduct product, NvidiaDriverListKind kind)
    {
        ArgumentNullException.ThrowIfNull(product);
        var parameters = kind == NvidiaDriverListKind.Studio ? STUDIO_PARAMETERS : GAME_READY_PARAMETERS;
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{DRIVER_LOOKUP_BASE}?func=DriverManualLookup&psid={product.Psid}&pfid={product.Pfid}&osID={OS_ID}&languageCode={LANGUAGE_CODE}&{parameters}&sort1=0&numberOfResults={RESULT_COUNT}"));
    }

    /// <summary>
    /// 어댑터 이름과 정확히 같은 제품을 찾습니다(<see cref="NvidiaProductListParser.FindExactMatches"/>).
    /// </summary>
    /// <param name="products">제품 목록.</param>
    /// <param name="adapterName">어댑터 이름.</param>
    /// <returns>일치 항목.</returns>
    public static IReadOnlyList<NvidiaProduct> FindExactMatches(IReadOnlyList<NvidiaProduct> products, string adapterName)
    {
        return NvidiaProductListParser.FindExactMatches(products, adapterName);
    }

    /// <summary>
    /// 제품 목록을 한 번 조회합니다.
    /// </summary>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>제품 목록 또는 실패.</returns>
    public async Task<NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>> GetProductsAsync(CancellationToken ct)
    {
        var body = await FetchAsync(PRODUCT_LIST_URI, ct).ConfigureAwait(false);
        return body.IsSuccess
            ? NvidiaProductListParser.Parse(body.Value!)
            : NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>.From(body.Failure!);
    }

    /// <summary>
    /// 제품의 계열별 드라이버 목록을 한 번 조회합니다.
    /// </summary>
    /// <param name="product">제품.</param>
    /// <param name="kind">목록 종류.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>검증한 목록 또는 실패.</returns>
    public async Task<NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>>> GetDriversAsync(NvidiaProduct product, NvidiaDriverListKind kind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(product);
        var body = await FetchAsync(DriverListUri(product, kind), ct).ConfigureAwait(false);
        return body.IsSuccess
            ? NvidiaDriverListParser.Parse(body.Value!, product, kind, OS_ID)
            : NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>>.From(body.Failure!);
    }

    /// <summary>
    /// 요청을 한 번 보내 2xx 본문을 받는다. 연결 오류·3xx·그 밖의 상태 코드는 NetworkFailed(요약 코드만, 예외 원문 없음).
    /// </summary>
    private async Task<NvidiaLookupResult<string>> FetchAsync(Uri uri, CancellationToken ct)
    {
        HttpTransportResponse response;
        try
        {
            response = await _transport.GetAsync(uri, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return NvidiaLookupResult<string>.Fail(CannotVerifyReason.NetworkFailed, "http." + ex.GetType().Name);
        }

        if (response.StatusCode is >= HTTP_REDIRECT_MIN and <= HTTP_REDIRECT_MAX)
        {
            return NvidiaLookupResult<string>.Fail(CannotVerifyReason.NetworkFailed, string.Create(CultureInfo.InvariantCulture, $"http.redirect{response.StatusCode}"));
        }

        if (response.StatusCode is < HTTP_OK_MIN or > HTTP_OK_MAX)
        {
            return NvidiaLookupResult<string>.Fail(CannotVerifyReason.NetworkFailed, string.Create(CultureInfo.InvariantCulture, $"http.status{response.StatusCode}"));
        }

        return NvidiaLookupResult<string>.Success(response.Body ?? string.Empty);
    }
}
