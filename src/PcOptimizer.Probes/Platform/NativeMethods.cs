/**
 * @file    : NativeMethods.cs
 * @author  : rudals252
 * @brief   : 조회 전용 Win32 P/Invoke 선언(powrprof 활성 전원 계획·이름, kernel32 전원 상태·메모리 해제)
 */

// 기본 패키지
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 조회 전용 Win32 함수 선언입니다. 설정을 바꾸는 함수는 선언하지 않습니다.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>Win32 성공 코드.</summary>
    internal const uint ERROR_SUCCESS = 0;

    private const string POWRPROF = "powrprof.dll";
    private const string KERNEL32 = "kernel32.dll";

    /// <summary>
    /// 활성 전원 계획 GUID를 읽습니다. 돌려받은 포인터는 <see cref="LocalFree"/>로 해제해야 합니다.
    /// </summary>
    [LibraryImport(POWRPROF)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    /// <summary>
    /// 전원 계획의 표시 이름(UTF-16, null 종료)을 읽습니다. buffer가 0이면 필요한 크기만 돌려줍니다.
    /// </summary>
    [LibraryImport(POWRPROF)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PowerReadFriendlyName(
        IntPtr rootPowerKey,
        in Guid schemeGuid,
        IntPtr subGroupOfPowerSettingsGuid,
        IntPtr powerSettingGuid,
        IntPtr buffer,
        ref uint bufferSize);

    /// <summary>
    /// AC/배터리 전원 상태를 읽습니다.
    /// </summary>
    [LibraryImport(KERNEL32, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    /// <summary>
    /// LocalAlloc으로 할당된 메모리를 해제합니다.
    /// </summary>
    [LibraryImport(KERNEL32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial IntPtr LocalFree(IntPtr memory);

    /// <summary>
    /// SYSTEM_POWER_STATUS 구조체입니다.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemPowerStatus
    {
        /// <summary>AC 전원 상태(0 끊김, 1 연결, 255 알 수 없음).</summary>
        public byte ACLineStatus;

        /// <summary>배터리 플래그(128 시스템 배터리 없음, 255 알 수 없음).</summary>
        public byte BatteryFlag;

        /// <summary>배터리 잔량 백분율(255 알 수 없음).</summary>
        public byte BatteryLifePercent;

        /// <summary>시스템 상태 플래그(절전 모드 등).</summary>
        public byte SystemStatusFlag;

        /// <summary>남은 배터리 시간(초, -1 알 수 없음).</summary>
        public int BatteryLifeTime;

        /// <summary>완충 시 배터리 시간(초, -1 알 수 없음).</summary>
        public int BatteryFullLifeTime;
    }
}
