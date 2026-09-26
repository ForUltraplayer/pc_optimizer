/**
 * @file    : VendorLinkKind.cs
 * @author  : rudals252
 * @brief   : 공식 링크 표 항목 종류(GPU 공급업체 드라이버 페이지/PC 제조사 지원 페이지/공식 감지 도구) 열거형
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 공식 링크 표(vendor-links.json) 항목의 종류입니다. JSON에서는 "gpuVendor", "oem", "tool"로 씁니다.
/// </summary>
public enum VendorLinkKind
{
    /// <summary>GPU 공급업체의 공식 드라이버 페이지.</summary>
    GpuVendor,

    /// <summary>PC·메인보드 제조사의 공식 지원(드라이버) 첫 화면.</summary>
    Oem,

    /// <summary>공급업체의 공식 자동 감지·지원 도구 안내 페이지.</summary>
    Tool,
}
