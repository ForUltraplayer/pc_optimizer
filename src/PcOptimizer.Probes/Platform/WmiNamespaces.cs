/**
 * @file    : WmiNamespaces.cs
 * @author  : rudals252
 * @brief   : 프로브가 조회하는 WMI 네임스페이스 이름 상수(cimv2, DeviceGuard, Storage)
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 프로브가 조회하는 WMI 네임스페이스입니다. 조회 전용이며 메서드 호출은 하지 않습니다.
/// </summary>
public static class WmiNamespaces
{
    /// <summary>기본 CIM 네임스페이스(Win32_* 클래스).</summary>
    public const string CIMV2 = @"root\cimv2";

    /// <summary>Device Guard 네임스페이스(Win32_DeviceGuard).</summary>
    public const string DEVICE_GUARD = @"root\Microsoft\Windows\DeviceGuard";

    /// <summary>Windows 저장소 관리 네임스페이스(MSFT_Volume, MSFT_PhysicalDisk).</summary>
    public const string STORAGE = @"root\Microsoft\Windows\Storage";
}
