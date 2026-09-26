/**
 * @file    : PowerProbeContract.cs
 * @author  : rudals252
 * @brief   : 전원 프로브와 전원 계획 규칙이 공유하는 프로브 ID·측정 이름·원시 값 의미(전원 계획 GUID, AC/배터리 플래그) 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 전원 프로브(Probes)와 <see cref="PowerPlanRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 원시 값은 Windows가 보고한 그대로 기록하며 해석은 규칙이 합니다.
/// </summary>
public static class PowerProbeContract
{
    /// <summary>전원 프로브 ID.</summary>
    public const string PROBE_ID = "hardware.power";

    /// <summary>활성 전원 계획 GUID(문자열, 소문자 "D" 형식).</summary>
    public const string ACTIVE_SCHEME_GUID = "power.activeScheme.guid";

    /// <summary>활성 전원 계획 표시 이름(문자열).</summary>
    public const string ACTIVE_SCHEME_NAME = "power.activeScheme.name";

    /// <summary>섀시 종류 코드 목록(Win32_SystemEnclosure.ChassisTypes, 숫자 문자열 목록).</summary>
    public const string CHASSIS_TYPES = "power.chassisTypes";

    /// <summary>AC 전원 상태 원시 값(SYSTEM_POWER_STATUS.ACLineStatus).</summary>
    public const string AC_LINE_STATUS = "power.acLineStatus";

    /// <summary>배터리 플래그 원시 값(SYSTEM_POWER_STATUS.BatteryFlag).</summary>
    public const string BATTERY_FLAG = "power.batteryFlag";

    /// <summary>배터리 잔량 원시 값(SYSTEM_POWER_STATUS.BatteryLifePercent, 255는 알 수 없음).</summary>
    public const string BATTERY_LIFE_PERCENT = "power.batteryLifePercent";

    /// <summary>Windows 기본 "고성능" 계획 GUID.</summary>
    public const string HIGH_PERFORMANCE_SCHEME_GUID = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    /// <summary>Windows "최고의 성능(Ultimate Performance)" 계획 GUID.</summary>
    public const string ULTIMATE_PERFORMANCE_SCHEME_GUID = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    /// <summary>ACLineStatus: AC 전원 끊김(배터리 동작).</summary>
    public const long AC_LINE_OFFLINE = 0;

    /// <summary>ACLineStatus: AC 전원 연결.</summary>
    public const long AC_LINE_ONLINE = 1;

    /// <summary>ACLineStatus: 알 수 없음.</summary>
    public const long AC_LINE_UNKNOWN = 255;

    /// <summary>BatteryFlag: 시스템 배터리 없음.</summary>
    public const long BATTERY_FLAG_NO_SYSTEM_BATTERY = 128;

    /// <summary>BatteryFlag: 배터리 상태를 읽을 수 없음.</summary>
    public const long BATTERY_FLAG_UNKNOWN = 255;
}
