/**
 * @file    : GpuVendor.cs
 * @author  : rudals252
 * @brief   : PnP 장치 ID의 PCI 공급업체 ID로 구분한 GPU 제조사(NVIDIA/AMD/Intel/기타 PCI/가상·비PCI) 열거형과 분류 도우미
 */

// 기본 패키지
using System.Text.RegularExpressions;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// GPU 제조사 구분입니다.
/// </summary>
public enum GpuVendor
{
    /// <summary>PCI 장치이지만 알려진 GPU 공급업체가 아님.</summary>
    Other,

    /// <summary>NVIDIA(VEN_10DE).</summary>
    Nvidia,

    /// <summary>AMD(VEN_1002).</summary>
    Amd,

    /// <summary>Intel(VEN_8086).</summary>
    Intel,

    /// <summary>PCI 장치가 아님(ROOT·SWD 등 소프트웨어/가상 어댑터) 또는 PnP 장치 ID 없음.</summary>
    Virtual,
}

/// <summary>
/// PnP 장치 ID로 GPU 제조사를 구분하는 도우미입니다. 이름 문자열로 추정하지 않습니다.
/// </summary>
public static partial class GpuVendorClassifier
{
    /// <summary>NVIDIA PCI 공급업체 ID.</summary>
    public const string NVIDIA_VENDOR_ID = "10DE";

    /// <summary>AMD(ATI) PCI 공급업체 ID.</summary>
    public const string AMD_VENDOR_ID = "1002";

    /// <summary>Intel PCI 공급업체 ID.</summary>
    public const string INTEL_VENDOR_ID = "8086";

    private const string VENDOR_GROUP = "vendor";
    private const int REGEX_TIMEOUT_MILLISECONDS = 100;

    /// <summary>
    /// PnP 장치 ID로 제조사를 구분합니다. "PCI\VEN_xxxx"로 시작하지 않으면 <see cref="GpuVendor.Virtual"/>입니다.
    /// </summary>
    /// <param name="pnpDeviceId">PnP 장치 ID(없으면 null).</param>
    /// <returns>제조사 구분.</returns>
    public static GpuVendor Classify(string? pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId))
        {
            return GpuVendor.Virtual;
        }

        var match = PciVendorPattern().Match(pnpDeviceId.Trim());
        if (!match.Success)
        {
            return GpuVendor.Virtual;
        }

        return match.Groups[VENDOR_GROUP].Value.ToUpperInvariant() switch
        {
            NVIDIA_VENDOR_ID => GpuVendor.Nvidia,
            AMD_VENDOR_ID => GpuVendor.Amd,
            INTEL_VENDOR_ID => GpuVendor.Intel,
            _ => GpuVendor.Other,
        };
    }

    /// <summary>
    /// "PCI\VEN_xxxx" 접두사에서 공급업체 ID를 찾는 정규식.
    /// </summary>
    [GeneratedRegex(@"^PCI\\VEN_(?<vendor>[0-9A-Fa-f]{4})", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex PciVendorPattern();
}
