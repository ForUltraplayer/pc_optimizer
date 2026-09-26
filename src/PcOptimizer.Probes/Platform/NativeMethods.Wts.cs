/**
 * @file    : NativeMethods.Wts.cs
 * @author  : rudals252
 * @brief   : 대화형 세션 사용자 조회용 wtsapi32 P/Invoke(현재 세션의 사용자·도메인 이름 읽기, 버퍼 해제)
 */

// 기본 패키지
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 원격 데스크톱 서비스(WTS) 세션 정보 조회 함수 선언입니다. 세션 상태를 바꾸는 함수는 선언하지 않습니다.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>로컬 서버 핸들(WTS_CURRENT_SERVER_HANDLE).</summary>
    internal static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    /// <summary>호출 프로세스가 속한 세션(WTS_CURRENT_SESSION = (DWORD)-1).</summary>
    internal const uint WTS_CURRENT_SESSION = uint.MaxValue;

    /// <summary>WTS_INFO_CLASS.WTSUserName.</summary>
    internal const int WTS_USER_NAME = 5;

    /// <summary>WTS_INFO_CLASS.WTSDomainName.</summary>
    internal const int WTS_DOMAIN_NAME = 7;

    private const string WTSAPI32 = "wtsapi32.dll";

    /// <summary>
    /// 세션 정보를 읽습니다(UTF-16 문자열 버퍼). 돌려받은 버퍼는 <see cref="WTSFreeMemory"/>로 해제해야 합니다.
    /// </summary>
    [LibraryImport(WTSAPI32, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSQuerySessionInformationW(IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out uint bytesReturned);

    /// <summary>
    /// WTS 함수가 할당한 메모리를 해제합니다.
    /// </summary>
    [LibraryImport(WTSAPI32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial void WTSFreeMemory(IntPtr memory);
}
