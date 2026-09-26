/**
 * @file    : NativeMethods.Display.cs
 * @author  : rudals252
 * @brief   : 조회 전용 디스플레이 Win32 P/Invoke 선언(user32 QueryDisplayConfig·DisplayConfigGetDeviceInfo·EnumDisplaySettingsExW·EnumDisplayDevicesW·GetSystemMetrics)과 상수
 */

// 기본 패키지
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 디스플레이 구성 조회 함수 선언입니다. 표시 설정을 바꾸는 함수(SetDisplayConfig, ChangeDisplaySettingsEx 등)는 선언하지 않습니다.
/// 구조체 배치는 <see cref="NativeMethods"/>의 디스플레이 구조체 파일(NativeMethods.DisplayStructs.cs)에 있습니다.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>QueryDisplayConfig 플래그: 활성 경로만.</summary>
    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

    /// <summary>버퍼가 작음(토폴로지가 바뀜) 오류.</summary>
    internal const int ERROR_INSUFFICIENT_BUFFER = 122;

    /// <summary>경로의 모드 인덱스가 없음을 뜻하는 값.</summary>
    internal const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;

    /// <summary>모드 정보 형식: 대상 모드.</summary>
    internal const int DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2;

    /// <summary>장치 정보 요청: 원본(GDI) 이름.</summary>
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;

    /// <summary>장치 정보 요청: 대상(모니터) 이름·장치 경로.</summary>
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

    /// <summary>장치 정보 요청: 어댑터 장치 경로.</summary>
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4;

    /// <summary>EnumDisplaySettingsExW 모드 번호: 현재 설정.</summary>
    internal const uint ENUM_CURRENT_SETTINGS = 0xFFFFFFFF;

    /// <summary>DEVMODE.dmFields: 방향 필드 유효.</summary>
    internal const uint DM_DISPLAYORIENTATION = 0x00000080;

    /// <summary>DEVMODE.dmDisplayFlags: 인터레이스.</summary>
    internal const uint DM_INTERLACED = 0x00000002;

    /// <summary>GetSystemMetrics: 원격 세션 여부.</summary>
    internal const int SM_REMOTESESSION = 0x1000;

    private const string USER32 = "user32.dll";

    /// <summary>
    /// QueryDisplayConfig에 필요한 경로·모드 배열 크기를 읽습니다.
    /// </summary>
    [LibraryImport(USER32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    /// <summary>
    /// 현재 디스플레이 경로·모드 구성을 읽습니다(조회 전용).
    /// </summary>
    [LibraryImport(USER32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static unsafe partial int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        DisplayConfigPathInfo* pathArray,
        ref uint numModeInfoArrayElements,
        DisplayConfigModeInfo* modeInfoArray,
        IntPtr currentTopologyId);

    /// <summary>
    /// 원본·대상·어댑터 이름을 읽습니다. requestPacket은 헤더로 시작하는 요청 구조체를 가리킵니다.
    /// </summary>
    [LibraryImport(USER32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static unsafe partial int DisplayConfigGetDeviceInfo(DisplayConfigDeviceInfoHeader* requestPacket);

    /// <summary>
    /// GDI 장치의 모드(번호 또는 ENUM_CURRENT_SETTINGS)를 읽습니다. 장치 이름은 빈 문자열이 아닌 "\\.\DISPLAYn"이어야 합니다.
    /// </summary>
    [LibraryImport(USER32, EntryPoint = "EnumDisplaySettingsExW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool EnumDisplaySettingsEx(string deviceName, uint modeNum, DevModeW* devMode, uint flags);

    /// <summary>
    /// 디스플레이 어댑터(deviceName이 null)를 순서대로 읽습니다.
    /// </summary>
    [LibraryImport(USER32, EntryPoint = "EnumDisplayDevicesW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool EnumDisplayDevices(string? deviceName, uint deviceIndex, DisplayDeviceW* displayDevice, uint flags);

    /// <summary>
    /// 시스템 지표를 읽습니다.
    /// </summary>
    [LibraryImport(USER32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int GetSystemMetrics(int index);
}
