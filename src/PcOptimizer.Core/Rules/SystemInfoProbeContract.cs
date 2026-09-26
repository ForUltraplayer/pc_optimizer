/**
 * @file    : SystemInfoProbeContract.cs
 * @author  : rudals252
 * @brief   : 시스템 정보 프로브와 시스템 정보 규칙이 공유하는 프로브 ID·측정 이름(제조사·모델·제품군·섀시·BIOS 버전) 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 시스템 정보 프로브(Probes)와 <see cref="SystemInfoRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 값은 제조사가 SMBIOS/WMI로 보고한 그대로이며 해석하지 않습니다.
/// </summary>
public static class SystemInfoProbeContract
{
    /// <summary>시스템 정보 프로브 ID.</summary>
    public const string PROBE_ID = "hardware.systemInfo";

    /// <summary>제조사(Win32_ComputerSystem.Manufacturer).</summary>
    public const string MANUFACTURER = "system.manufacturer";

    /// <summary>모델(Win32_ComputerSystem.Model).</summary>
    public const string MODEL = "system.model";

    /// <summary>제품군(Win32_ComputerSystem.SystemFamily).</summary>
    public const string SYSTEM_FAMILY = "system.systemFamily";

    /// <summary>섀시 종류 코드 목록(Win32_SystemEnclosure.ChassisTypes, 숫자 문자열 목록).</summary>
    public const string CHASSIS_TYPES = "system.chassisTypes";

    /// <summary>BIOS 버전(Win32_BIOS.SMBIOSBIOSVersion).</summary>
    public const string BIOS_VERSION = "system.biosVersion";
}
