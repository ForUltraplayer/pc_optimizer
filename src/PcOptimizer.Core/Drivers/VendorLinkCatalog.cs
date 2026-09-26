/**
 * @file    : VendorLinkCatalog.cs
 * @author  : rudals252
 * @brief   : 검증한 공식 링크 표(불변)에서 제조사 이름으로 OEM 지원 페이지 찾기, 공급업체 키로 드라이버 페이지·도구 찾기, 링크의 호스트+경로 접두사 허용 판정
 */

// 기본 패키지
using System.Collections.Frozen;

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 공식 링크 표입니다(스펙 §2 vendor-links.json, §7 OpenLink 허용 목록). <see cref="VendorLinkCatalogParser"/>만 만들며 생성 후 바뀌지 않습니다.
/// </summary>
public sealed class VendorLinkCatalog
{
    private const char PATH_SEPARATOR = '/';

    private readonly FrozenDictionary<string, VendorLinkEntry> _oemByManufacturer;

    /// <summary>
    /// 검증한 항목으로 표를 만듭니다(파서 전용).
    /// </summary>
    /// <param name="entries">검증한 항목(ID·URL·OEM 제조사 이름 중복 없음).</param>
    internal VendorLinkCatalog(IReadOnlyList<VendorLinkEntry> entries)
    {
        Entries = [.. entries];
        var byManufacturer = new Dictionary<string, VendorLinkEntry>(StringComparer.Ordinal);
        foreach (var entry in Entries.Where(entry => entry.Kind == VendorLinkKind.Oem))
        {
            foreach (var name in entry.ManufacturerNames)
            {
                byManufacturer[name] = entry;
            }
        }

        _oemByManufacturer = byManufacturer.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>표 항목(파일 순서).</summary>
    public IReadOnlyList<VendorLinkEntry> Entries { get; }

    /// <summary>
    /// 제조사 이름(WMI 보고 값)으로 OEM 지원 페이지 항목을 찾습니다. 정규화한 이름 전체가 같을 때만 찾습니다.
    /// </summary>
    /// <param name="manufacturer">제조사 이름.</param>
    /// <returns>항목 또는 null(표에 없음).</returns>
    public VendorLinkEntry? FindOem(string? manufacturer)
    {
        var normalized = ManufacturerNameNormalizer.Normalize(manufacturer);
        return normalized.Length > 0 && _oemByManufacturer.TryGetValue(normalized, out var entry) ? entry : null;
    }

    /// <summary>
    /// 공급업체 키(예: "amd", "intel")의 GPU 드라이버 페이지와 도구 항목을 파일 순서로 찾습니다.
    /// </summary>
    /// <param name="vendorKey">공급업체 키.</param>
    /// <returns>항목 목록(없으면 빈 목록).</returns>
    public IReadOnlyList<VendorLinkEntry> ForVendor(string vendorKey)
    {
        ArgumentNullException.ThrowIfNull(vendorKey);
        return [.. Entries.Where(entry => entry.Kind != VendorLinkKind.Oem && string.Equals(entry.Key, vendorKey, StringComparison.Ordinal))];
    }

    /// <summary>
    /// 링크가 표 항목과 같은 호스트이고 경로가 그 항목의 경로로 시작하는지 확인합니다(경로 경계 기준, 대소문자 구분).
    /// </summary>
    /// <param name="url">확인할 URL 문자열.</param>
    /// <returns>허용되면 true.</returns>
    public bool IsAllowedLink(string? url)
    {
        return OfficialUrl.TryParse(url, out var uri) && IsAllowedLink(uri);
    }

    /// <summary>
    /// 해석한 URL이 표 항목의 호스트+경로 접두사에 들어가는지 확인합니다.
    /// </summary>
    /// <param name="uri">해석한 URL(<see cref="OfficialUrl.TryParse"/> 통과).</param>
    /// <returns>허용되면 true.</returns>
    public bool IsAllowedLink(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var host = OfficialUrl.AsciiHost(uri);
        var path = uri.AbsolutePath;
        foreach (var entry in Entries)
        {
            if (!OfficialUrl.TryParse(entry.Url, out var allowed) || !string.Equals(OfficialUrl.AsciiHost(allowed), host, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsPathWithin(path, allowed.AbsolutePath))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 경로가 허용 경로와 같거나, 허용 경로가 '/'로 끝나며 그 아래이거나, 허용 경로 바로 뒤가 '/'인지 확인한다.
    /// </summary>
    private static bool IsPathWithin(string path, string allowedPath)
    {
        if (string.Equals(path, allowedPath, StringComparison.Ordinal))
        {
            return true;
        }

        if (!path.StartsWith(allowedPath, StringComparison.Ordinal))
        {
            return false;
        }

        return allowedPath.EndsWith(PATH_SEPARATOR) || path[allowedPath.Length] == PATH_SEPARATOR;
    }
}
