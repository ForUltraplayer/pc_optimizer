/**
 * @file    : VendorLinkCatalogTests.cs
 * @author  : rudals252
 * @brief   : 공식 링크 표(vendor-links.json) 해석·검증(HTTPS·공식 도메인·중복·종류·스키마)과 제조사 조회·호스트+경로 접두사 허용 판정 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.Tests.Unit.Drivers;

/// <summary>
/// <see cref="VendorLinkCatalogParser"/>와 <see cref="VendorLinkCatalog"/>를 검증합니다. 표의 URL은 테스트용 예시입니다.
/// </summary>
public sealed class VendorLinkCatalogTests
{
    private const string VALID_JSON = """
        {
          "schemaVersion": 1,
          "entries": [
            { "id": "nvidia-drivers", "kind": "gpuVendor", "key": "nvidia", "label": "NVIDIA 드라이버", "url": "https://www.nvidia.com/en-us/drivers/", "officialDomains": ["nvidia.com"] },
            { "id": "amd-drivers", "kind": "gpuVendor", "key": "amd", "label": "AMD 드라이버", "url": "https://www.amd.com/en/support/download/drivers.html", "officialDomains": ["amd.com"] },
            { "id": "amd-auto-detect", "kind": "tool", "key": "amd", "label": "AMD 자동 감지 도구", "url": "https://www.amd.com/en/resources/support-articles/faqs/GPU-131.html", "officialDomains": ["amd.com"] },
            { "id": "msi-support", "kind": "oem", "key": "msi", "manufacturers": ["Micro-Star International Co., Ltd.", "MSI"], "label": "MSI 지원", "url": "https://www.msi.com/support/download", "officialDomains": ["msi.com"] },
            { "id": "dell-support", "kind": "oem", "key": "dell", "manufacturers": ["Dell Inc."], "label": "Dell 지원", "url": "https://www.dell.com/support/home/", "officialDomains": ["dell.com"] }
          ]
        }
        """;

    /// <summary>
    /// 올바른 표를 해석한다.
    /// </summary>
    private static VendorLinkCatalog ParseValid()
    {
        var result = VendorLinkCatalogParser.Parse(VALID_JSON);
        Assert.Empty(result.Errors);
        return Assert.IsType<VendorLinkCatalog>(result.Catalog);
    }

    /// <summary>
    /// 첫 항목의 속성 하나를 바꾼 표를 만든다.
    /// </summary>
    private static string WithEntry(string entry)
    {
        return "{ \"schemaVersion\": 1, \"entries\": [ " + entry + " ] }";
    }

    /// <summary>올바른 표는 모든 항목을 읽는다.</summary>
    [Fact]
    public void 올바른_표를_읽는다()
    {
        var catalog = ParseValid();

        Assert.Equal(5, catalog.Entries.Count);
        Assert.Equal(VendorLinkKind.Tool, catalog.Entries.Single(e => e.Id == "amd-auto-detect").Kind);
    }

    /// <summary>제조사 이름은 정규화해 찾는다(MSI 정식 회사명, Dell Inc.).</summary>
    [Theory]
    [InlineData("Micro-Star International Co., Ltd.", "msi-support")]
    [InlineData("MSI", "msi-support")]
    [InlineData("Dell Inc.", "dell-support")]
    [InlineData("DELL", "dell-support")]
    public void 제조사_이름을_정규화해_찾는다(string manufacturer, string expectedId)
    {
        Assert.Equal(expectedId, ParseValid().FindOem(manufacturer)?.Id);
    }

    /// <summary>표에 없는 제조사나 GPU 공급업체 이름은 OEM으로 찾지 않는다.</summary>
    [Theory]
    [InlineData("Unknown Maker Ltd.")]
    [InlineData("NVIDIA")]
    [InlineData("")]
    [InlineData(null)]
    public void 표에_없는_제조사는_찾지_않는다(string? manufacturer)
    {
        Assert.Null(ParseValid().FindOem(manufacturer));
    }

    /// <summary>GPU 공급업체 키로 공식 페이지와 도구 링크를 함께 찾는다.</summary>
    [Fact]
    public void 공급업체_키로_페이지와_도구를_찾는다()
    {
        var entries = ParseValid().ForVendor("amd");

        Assert.Equal(["amd-drivers", "amd-auto-detect"], entries.Select(e => e.Id));
    }

    /// <summary>링크는 표 항목과 같은 호스트이고 경로가 그 항목 경로로 시작할 때만 허용한다.</summary>
    [Theory]
    [InlineData("https://www.dell.com/support/home/", true)]
    [InlineData("https://www.dell.com/support/home/ko-kr", true)]
    [InlineData("https://WWW.DELL.COM/support/home/", true)]
    [InlineData("https://www.amd.com/en/support/download/drivers.html", true)]
    [InlineData("https://www.amd.com/en/support/download/drivers.html?x=1", true)]
    [InlineData("https://www.amd.com/en/support/download/drivers.html.evil", false)]
    [InlineData("https://www.dell.com/other/", false)]
    [InlineData("https://dell.com/support/home/", false)]
    [InlineData("https://www.dell.com.evil.com/support/home/", false)]
    [InlineData("http://www.dell.com/support/home/", false)]
    [InlineData("https://user@www.dell.com/support/home/", false)]
    public void 호스트와_경로_접두사로_허용한다(string url, bool expected)
    {
        Assert.Equal(expected, ParseValid().IsAllowedLink(url));
    }

    /// <summary>항목 오류는 표 전체를 거부한다(HTTP, 공식 도메인 밖, 사용자 정보, 알 수 없는 종류, 빈 이름표·키).</summary>
    [Theory]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "http://www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "https://www.a.com.evil.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "https://user@www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "driverTool", "key": "a", "label": "A", "url": "https://www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": " ", "url": "https://www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "A Corp", "label": "A", "url": "https://www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "https://www.a.com/", "officialDomains": [] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "https://www.a.com/", "officialDomains": [".a.com"] }""")]
    [InlineData("""{ "id": "", "kind": "oem", "key": "a", "label": "A", "url": "https://www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "officialDomains": ["a.com"] }""")]
    public void 항목_오류는_표_전체를_거부한다(string entry)
    {
        var result = VendorLinkCatalogParser.Parse(WithEntry(entry));

        Assert.Null(result.Catalog);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>중복 ID·중복 URL·두 OEM 항목에 겹치는 제조사 이름은 거부한다.</summary>
    [Theory]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "https://www.a.com/", "officialDomains": ["a.com"] }, { "id": "A", "kind": "oem", "key": "b", "label": "B", "url": "https://www.b.com/", "officialDomains": ["b.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "label": "A", "url": "https://www.a.com/", "officialDomains": ["a.com"] }, { "id": "b", "kind": "tool", "key": "b", "label": "B", "url": "https://www.a.com/", "officialDomains": ["a.com"] }""")]
    [InlineData("""{ "id": "a", "kind": "oem", "key": "a", "manufacturers": ["Same Co., Ltd."], "label": "A", "url": "https://www.a.com/", "officialDomains": ["a.com"] }, { "id": "b", "kind": "oem", "key": "b", "manufacturers": ["SAME"], "label": "B", "url": "https://www.b.com/", "officialDomains": ["b.com"] }""")]
    public void 중복은_거부한다(string entries)
    {
        var result = VendorLinkCatalogParser.Parse(WithEntry(entries));

        Assert.Null(result.Catalog);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>JSON이 아니거나 스키마 버전·항목 목록이 맞지 않으면 거부한다.</summary>
    [Theory]
    [InlineData("<html>error</html>")]
    [InlineData("")]
    [InlineData("{ \"schemaVersion\": 2, \"entries\": [] }")]
    [InlineData("{ \"schemaVersion\": 1 }")]
    [InlineData("{ \"schemaVersion\": 1, \"entries\": [] }")]
    [InlineData("[1, 2]")]
    public void 형식이_맞지_않으면_거부한다(string json)
    {
        var result = VendorLinkCatalogParser.Parse(json);

        Assert.Null(result.Catalog);
        Assert.NotEmpty(result.Errors);
    }
}
