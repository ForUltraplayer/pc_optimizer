/**
 * @file    : NvidiaLookupClientTests.cs
 * @author  : rudals252
 * @brief   : NVIDIA 조회 어댑터를 저장한 실제 HTTP 응답 fixture로 검증(제품 정확 일치·0/2개 일치, Game Ready/Studio 목록 필드 검증, Studio 없음 응답, 요청 URL, 깨진 JSON·HTML·필드 누락·HTTP·타 호스트·리디렉션·상태 코드·연결 실패·매핑 불일치는 예외 없이 NetworkFailed/ProbeError)
 */

// 기본 패키지
using System.Text.Json.Nodes;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="NvidiaLookupClient"/>를 검증합니다. 실제 네트워크 요청은 없습니다.
/// </summary>
public sealed class NvidiaLookupClientTests
{
    private static readonly NvidiaProduct PRODUCT = new(NvidiaFixtures.GPU_NAME, 127, 1041);

    /// <summary>
    /// 본문 하나만 돌려주는 전송으로 드라이버 목록을 조회한다.
    /// </summary>
    private static async Task<NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>>> GetDriversFromBody(
        string body, NvidiaDriverListKind kind = NvidiaDriverListKind.GameReady, int statusCode = 200)
    {
        var client = new NvidiaLookupClient(new FixtureHttpTransport().On(_ => true, body, statusCode));
        return await client.GetDriversAsync(PRODUCT, kind, CancellationToken.None);
    }

    /// <summary>
    /// Game Ready fixture의 첫 항목 downloadInfo를 고친 본문을 만든다.
    /// </summary>
    private static string MutateFirstEntry(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(NvidiaFixtures.Read(NvidiaFixtures.GAME_READY_2))!.AsObject();
        mutate(root["IDS"]![0]!["downloadInfo"]!.AsObject());
        return root.ToJsonString();
    }

    /// <summary>
    /// 최상위 JSON을 고친 본문을 만든다.
    /// </summary>
    private static string MutateRoot(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(NvidiaFixtures.Read(NvidiaFixtures.GAME_READY_2))!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    /// <summary>제품 목록에서 어댑터 이름과 정확히 같은 항목(대소문자·공백 차이 무시)을 찾아 psid/pfid를 얻는다.</summary>
    [Theory]
    [InlineData("NVIDIA GeForce RTX 4080 SUPER")]
    [InlineData("  nvidia   geforce rtx 4080 super ")]
    public async Task 제품을_정확히_찾는다(string adapterName)
    {
        var client = new NvidiaLookupClient(NvidiaFixtures.CompleteTransport());

        var products = await client.GetProductsAsync(CancellationToken.None);

        Assert.True(products.IsSuccess);
        var match = Assert.Single(NvidiaLookupClient.FindExactMatches(products.Value!, adapterName));
        Assert.Equal(127, match.Psid);
        Assert.Equal(1041, match.Pfid);
    }

    /// <summary>일치 항목이 없거나(노트북 이름 접두사 차이) 여러 개면 추측하지 않는다(접두사로 고르지 않음).</summary>
    [Theory]
    [InlineData("NVIDIA GeForce RTX 4080 Laptop GPU", 0)]
    [InlineData("GeForce GTX 1060", 2)]
    [InlineData("NVIDIA GeForce RTX 4080 SUPER Ti", 0)]
    public async Task 없거나_여러_개면_그대로_돌려준다(string adapterName, int expected)
    {
        var client = new NvidiaLookupClient(NvidiaFixtures.CompleteTransport());
        var products = await client.GetProductsAsync(CancellationToken.None);

        Assert.Equal(expected, NvidiaLookupClient.FindExactMatches(products.Value!, adapterName).Count);
    }

    /// <summary>'RTX 4080'은 'RTX 4080 SUPER'가 아니라 정확히 같은 이름의 항목(pfid 999)과만 일치한다.</summary>
    [Fact]
    public async Task 접두사가_같은_다른_제품과_섞지_않는다()
    {
        var client = new NvidiaLookupClient(NvidiaFixtures.CompleteTransport());
        var products = await client.GetProductsAsync(CancellationToken.None);

        Assert.Equal(999, Assert.Single(NvidiaLookupClient.FindExactMatches(products.Value!, "NVIDIA GeForce RTX 4080")).Pfid);
    }

    /// <summary>Game Ready 목록의 버전·UTC 날짜·디코딩한 이름·검증된 URL을 읽는다.</summary>
    [Fact]
    public async Task 게임_레디_목록을_읽는다()
    {
        var result = await GetDriversFromBody(NvidiaFixtures.Read(NvidiaFixtures.GAME_READY_2));

        Assert.True(result.IsSuccess);
        Assert.Equal(["617.14", "616.92"], result.Value!.Select(entry => entry.Version));
        var first = result.Value![0];
        Assert.Equal(new DateOnly(2026, 9, 22), first.ReleaseDate);
        Assert.Equal("GeForce Game Ready Driver", first.Name);
        Assert.Equal("https://www.nvidia.com/en-us/drivers/details/279803/", first.DetailsUrl);
        Assert.Equal("https://us.download.nvidia.com/Windows/617.14/617.14-desktop-win10-win11-64bit-international-dch-whql.exe", first.DownloadUrl);
        Assert.False(first.IsBeta);
    }

    /// <summary>Studio 목록(upCRD=1)은 모든 항목이 Studio 배포이며 이름은 'NVIDIA Studio Driver'다.</summary>
    [Fact]
    public async Task 스튜디오_목록을_읽는다()
    {
        var result = await GetDriversFromBody(NvidiaFixtures.Read(NvidiaFixtures.STUDIO_10), NvidiaDriverListKind.Studio);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.Count);
        Assert.All(result.Value!, entry => Assert.Equal("NVIDIA Studio Driver", entry.Name));
    }

    /// <summary>'드라이버 없음' 응답(Success 0, DriverDownloadIDNotFound)은 빈 목록 성공이다.</summary>
    [Fact]
    public async Task 없음_응답은_빈_목록이다()
    {
        var result = await GetDriversFromBody(NvidiaFixtures.Read(NvidiaFixtures.STUDIO_NOT_FOUND), NvidiaDriverListKind.Studio);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    /// <summary>계열 표시(IsCRD)가 요청한 목록과 다르면(Studio 응답을 Game Ready로 받음) 매핑 불일치 ProbeError다.</summary>
    [Fact]
    public async Task 계열_표시가_다르면_수집_오류다()
    {
        var result = await GetDriversFromBody(NvidiaFixtures.Read(NvidiaFixtures.STUDIO_10), NvidiaDriverListKind.GameReady);

        Assert.False(result.IsSuccess);
        Assert.Equal(CannotVerifyReason.ProbeError, result.Failure!.Reason);
    }

    /// <summary>요청 URL은 고정 매개변수(osID=57, languageCode=1033, dch=1, 10개)를 쓰고, Studio는 upCRD=1과 isWHQL=0을 쓴다.</summary>
    [Fact]
    public void 요청_URL을_고정_매개변수로_만든다()
    {
        Assert.Equal(
            "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=127&pfid=1041&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=10",
            NvidiaLookupClient.DriverListUri(PRODUCT, NvidiaDriverListKind.GameReady).AbsoluteUri);
        Assert.Equal(
            "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=127&pfid=1041&osID=57&languageCode=1033&isWHQL=0&dch=1&upCRD=1&sort1=0&numberOfResults=10",
            NvidiaLookupClient.DriverListUri(PRODUCT, NvidiaDriverListKind.Studio).AbsoluteUri);
        Assert.Equal("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3", NvidiaLookupClient.PRODUCT_LIST_URI.AbsoluteUri);
    }

    /// <summary>JSON이 아니거나(HTML 오류 페이지·잘린 JSON) 상태 코드가 2xx가 아니거나 리디렉션이면 예외 없이 NetworkFailed다.</summary>
    [Theory]
    [InlineData("<!DOCTYPE html><html><body>Service Unavailable</body></html>", 200)]
    [InlineData("{ \"Success\" : \"2\", \"IDS\" : [", 200)]
    [InlineData("", 200)]
    [InlineData("", 302)]
    [InlineData("", 301)]
    [InlineData("", 500)]
    [InlineData("", 403)]
    public async Task 형식_오류와_HTTP_오류는_네트워크_실패다(string body, int statusCode)
    {
        var result = await GetDriversFromBody(body, statusCode: statusCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(CannotVerifyReason.NetworkFailed, result.Failure!.Reason);
        Assert.False(string.IsNullOrWhiteSpace(result.Failure.Code));
    }

    /// <summary>연결 실패(HttpRequestException)는 NetworkFailed이며 예외 형식 이름만 코드로 남긴다.</summary>
    [Fact]
    public async Task 연결_실패는_네트워크_실패다()
    {
        var client = new NvidiaLookupClient(new FixtureHttpTransport().Throw(_ => true, NvidiaFixtures.ConnectionFailure()));

        var result = await client.GetProductsAsync(CancellationToken.None);

        Assert.Equal(CannotVerifyReason.NetworkFailed, result.Failure!.Reason);
        Assert.DoesNotContain("fake connection failure", result.Failure.Code, StringComparison.Ordinal);
    }

    /// <summary>필수 항목 누락·값 오류·허용되지 않은 URL·요청과 다른 응답은 ProbeError다(빈 성공으로 바꾸지 않음).</summary>
    [Theory]
    [InlineData("missingDownloadUrl")]
    [InlineData("httpDownloadUrl")]
    [InlineData("otherHostDetailsUrl")]
    [InlineData("userInfoDetailsUrl")]
    [InlineData("badVersion")]
    [InlineData("badDate")]
    [InlineData("wrongWeekday")]
    [InlineData("emptyName")]
    [InlineData("missingIsCrd")]
    [InlineData("productNotInSeries")]
    [InlineData("noWindows11")]
    [InlineData("successNotNumber")]
    [InlineData("requestPfidMismatch")]
    [InlineData("missingRequest")]
    [InlineData("successWithoutIds")]
    public async Task 스키마_오류는_수집_오류다(string mutation)
    {
        var body = mutation switch
        {
            "missingDownloadUrl" => MutateFirstEntry(info => info.Remove("DownloadURL")),
            "httpDownloadUrl" => MutateFirstEntry(info => info["DownloadURL"] = "http://us.download.nvidia.com/Windows/617.14/x.exe"),
            "otherHostDetailsUrl" => MutateFirstEntry(info => info["DetailsURL"] = "https://www.nvidia.com.evil.com/en-us/drivers/details/1/"),
            "userInfoDetailsUrl" => MutateFirstEntry(info => info["DetailsURL"] = "https://user@www.nvidia.com/en-us/drivers/details/1/"),
            "badVersion" => MutateFirstEntry(info => info["Version"] = "617.1"),
            "badDate" => MutateFirstEntry(info => info["ReleaseDateTime"] = "2026-09-22"),
            "wrongWeekday" => MutateFirstEntry(info => info["ReleaseDateTime"] = "Mon Sep 22, 2026"),
            "emptyName" => MutateFirstEntry(info => info["Name"] = "%20"),
            "missingIsCrd" => MutateFirstEntry(info => info.Remove("IsCRD")),
            "productNotInSeries" => MutateFirstEntry(info => info["series"] = new JsonArray()),
            "noWindows11" => MutateFirstEntry(info => info["OSList"] = new JsonArray(new JsonObject { ["OSName"] = "Windows%207", ["OsCode"] = "6.1" })),
            "successNotNumber" => MutateRoot(root => root["Success"] = "yes"),
            "requestPfidMismatch" => MutateRoot(root => root["Request"]![0]!["pfid"] = "999"),
            "missingRequest" => MutateRoot(root => root.Remove("Request")),
            "successWithoutIds" => MutateRoot(root => root["IDS"] = new JsonArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        var result = await GetDriversFromBody(body);

        Assert.False(result.IsSuccess);
        Assert.Equal(CannotVerifyReason.ProbeError, result.Failure!.Reason);
    }

    /// <summary>제품 목록이 XML이 아니면 NetworkFailed, 구조가 다르거나 값이 비면 ProbeError다.</summary>
    [Theory]
    [InlineData("<!DOCTYPE html><html><body>error</body></html>", CannotVerifyReason.NetworkFailed)]
    [InlineData("not xml", CannotVerifyReason.NetworkFailed)]
    [InlineData("<?xml version=\"1.0\"?><Other><LookupValues /></Other>", CannotVerifyReason.ProbeError)]
    [InlineData("<?xml version=\"1.0\"?><LookupValueSearch><LookupValues></LookupValues></LookupValueSearch>", CannotVerifyReason.ProbeError)]
    [InlineData("<?xml version=\"1.0\"?><LookupValueSearch><LookupValues><LookupValue ParentID=\"127\"><Name>X</Name></LookupValue></LookupValues></LookupValueSearch>", CannotVerifyReason.ProbeError)]
    [InlineData("<?xml version=\"1.0\"?><LookupValueSearch><LookupValues><LookupValue ParentID=\"a\"><Name>X</Name><Value>1</Value></LookupValue></LookupValues></LookupValueSearch>", CannotVerifyReason.ProbeError)]
    public async Task 제품_목록_형식_오류를_구분한다(string body, CannotVerifyReason expected)
    {
        var client = new NvidiaLookupClient(new FixtureHttpTransport().On(_ => true, body));

        var result = await client.GetProductsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Failure!.Reason);
    }

    /// <summary>취소는 결과로 바꾸지 않고 그대로 전파한다(실행기가 Cancelled/Timeout으로 기록).</summary>
    [Fact]
    public async Task 취소는_전파한다()
    {
        var client = new NvidiaLookupClient(NvidiaFixtures.CompleteTransport());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetProductsAsync(cts.Token));
    }
}
