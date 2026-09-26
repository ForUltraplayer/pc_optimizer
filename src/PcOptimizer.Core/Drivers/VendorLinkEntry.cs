/**
 * @file    : VendorLinkEntry.cs
 * @author  : rudals252
 * @brief   : 검증을 마친 공식 링크 표 항목 하나(ID·종류·공급업체 키·정규화한 제조사 이름·이름표·HTTPS URL·공식 도메인) 레코드
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 공식 링크 표의 항목 하나입니다. <see cref="VendorLinkCatalogParser"/>가 검증한 뒤에만 만들어집니다.
/// </summary>
/// <param name="Id">항목 ID(표 안에서 고유).</param>
/// <param name="Kind">종류.</param>
/// <param name="Key">공급업체 키(정규화한 소문자, 예: "nvidia", "msi").</param>
/// <param name="ManufacturerNames">OEM 조회에 쓰는 정규화한 제조사 이름(키 포함).</param>
/// <param name="Label">사용자에게 보여 줄 이름표.</param>
/// <param name="Url">공식 페이지 URL(HTTPS, 공식 도메인 안).</param>
/// <param name="OfficialDomains">이 공급업체의 공식 도메인(ASCII 소문자).</param>
public sealed record VendorLinkEntry(
    string Id,
    VendorLinkKind Kind,
    string Key,
    IReadOnlyList<string> ManufacturerNames,
    string Label,
    string Url,
    IReadOnlyList<string> OfficialDomains);
