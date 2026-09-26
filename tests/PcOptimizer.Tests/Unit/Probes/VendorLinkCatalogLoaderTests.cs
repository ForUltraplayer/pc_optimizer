/**
 * @file    : VendorLinkCatalogLoaderTests.cs
 * @author  : rudals252
 * @brief   : 포함된 공식 링크 표(rules/vendor-links.json)가 검증을 통과하고 요구된 GPU 공급업체·도구·OEM 항목을 모두 가지며, 제조사 정규화 조회(MSI·Dell·GIGABYTE 정식 회사명)가 동작하는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;
using PcOptimizer.Probes.Drivers;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="VendorLinkCatalogLoader"/>와 포함 표 내용을 검증합니다.
/// </summary>
public sealed class VendorLinkCatalogLoaderTests
{
    /// <summary>표에 있어야 하는 OEM 키(스펙 P6 결정 목록).</summary>
    private static readonly string[] REQUIRED_OEM_KEYS =
        ["dell", "hp", "lenovo", "asus", "acer", "msi", "samsung", "lg", "gigabyte", "asrock", "microsoft", "razer", "huawei", "xiaomi"];

    /// <summary>
    /// 포함 표를 읽는다.
    /// </summary>
    private static VendorLinkCatalog Load()
    {
        var result = VendorLinkCatalogLoader.LoadEmbedded();
        Assert.Empty(result.Errors);
        return Assert.IsType<VendorLinkCatalog>(result.Catalog);
    }

    /// <summary>포함 표는 검증을 통과하고 모든 URL이 HTTPS이며 항목의 공식 도메인 안에 있다.</summary>
    [Fact]
    public void 포함_표는_검증을_통과한다()
    {
        var catalog = Load();

        Assert.All(catalog.Entries, entry =>
        {
            Assert.True(OfficialUrl.TryParse(entry.Url, out var uri), entry.Id);
            Assert.Contains(entry.OfficialDomains, domain => OfficialUrl.IsHostInDomain(OfficialUrl.AsciiHost(uri!), domain));
            Assert.True(catalog.IsAllowedLink(entry.Url), entry.Id);
        });
    }

    /// <summary>NVIDIA 드라이버 페이지, AMD 드라이버 페이지와 자동 감지 도구, Intel 지원 도우미와 Arc 드라이버 페이지가 있다.</summary>
    [Fact]
    public void GPU_공급업체_링크가_있다()
    {
        var catalog = Load();

        Assert.Contains(catalog.ForVendor("nvidia"), entry => entry.Kind == VendorLinkKind.GpuVendor);
        Assert.Contains(catalog.ForVendor("amd"), entry => entry.Kind == VendorLinkKind.GpuVendor);
        Assert.Contains(catalog.ForVendor("amd"), entry => entry.Kind == VendorLinkKind.Tool);
        Assert.Contains(catalog.ForVendor("intel"), entry => entry.Kind == VendorLinkKind.GpuVendor);
        Assert.Contains(catalog.ForVendor("intel"), entry => entry.Kind == VendorLinkKind.Tool);
    }

    /// <summary>요구된 OEM 제조사가 모두 있다.</summary>
    [Fact]
    public void 요구된_OEM이_모두_있다()
    {
        var keys = Load().Entries.Where(entry => entry.Kind == VendorLinkKind.Oem).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

        Assert.All(REQUIRED_OEM_KEYS, key => Assert.Contains(key, keys));
    }

    /// <summary>WMI가 보고하는 정식 회사명을 정규화해 찾고, 표에 없는 값은 찾지 않는다.</summary>
    [Theory]
    [InlineData("Micro-Star International Co., Ltd.", "msi")]
    [InlineData("Dell Inc.", "dell")]
    [InlineData("Gigabyte Technology Co., Ltd.", "gigabyte")]
    [InlineData("ASUSTeK COMPUTER INC.", "asus")]
    [InlineData("HP", "hp")]
    [InlineData("LENOVO", "lenovo")]
    [InlineData("To Be Filled By O.E.M.", null)]
    [InlineData("System manufacturer", null)]
    public void 정식_회사명으로_찾는다(string manufacturer, string? expectedKey)
    {
        Assert.Equal(expectedKey, Load().FindOem(manufacturer)?.Key);
    }
}
