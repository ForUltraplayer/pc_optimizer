/**
 * @file    : Win32PowerPlatform.cs
 * @author  : rudals252
 * @brief   : powrprof/kernel32 P/Invoke로 활성 전원 계획·이름·AC/배터리 상태를 읽는 조회 전용 구현
 */

// 기본 패키지
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// Win32 API로 전원 정보를 읽는 구현입니다. 전원 설정을 바꾸는 API는 호출하지 않습니다.
/// </summary>
public sealed class Win32PowerPlatform : IPowerPlatform
{
    private const int UTF16_CHAR_BYTES = sizeof(char);
    private const char NULL_CHAR = '\0';

    /// <summary>공유 인스턴스.</summary>
    public static Win32PowerPlatform Instance { get; } = new();

    /// <inheritdoc />
    public uint GetActiveScheme(out Guid schemeGuid)
    {
        schemeGuid = Guid.Empty;
        var error = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var pointer);
        if (error != NativeMethods.ERROR_SUCCESS)
        {
            return error;
        }

        try
        {
            schemeGuid = Marshal.PtrToStructure<Guid>(pointer);
            return NativeMethods.ERROR_SUCCESS;
        }
        finally
        {
            NativeMethods.LocalFree(pointer);
        }
    }

    /// <inheritdoc />
    public uint ReadFriendlyName(Guid schemeGuid, out string? friendlyName)
    {
        friendlyName = null;
        uint size = 0;
        var error = NativeMethods.PowerReadFriendlyName(IntPtr.Zero, in schemeGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size);
        if (error != NativeMethods.ERROR_SUCCESS)
        {
            return error;
        }

        if (size < UTF16_CHAR_BYTES)
        {
            friendlyName = string.Empty;
            return NativeMethods.ERROR_SUCCESS;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            error = NativeMethods.PowerReadFriendlyName(IntPtr.Zero, in schemeGuid, IntPtr.Zero, IntPtr.Zero, buffer, ref size);
            if (error != NativeMethods.ERROR_SUCCESS)
            {
                return error;
            }

            // 크기 안에서만 읽고 끝의 null 문자를 잘라 종료 문자가 없는 버퍼도 넘어 읽지 않게 한다.
            friendlyName = Marshal.PtrToStringUni(buffer, (int)size / UTF16_CHAR_BYTES).TrimEnd(NULL_CHAR);
            return NativeMethods.ERROR_SUCCESS;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <inheritdoc />
    public uint GetPowerStatus(out PowerStatusReading? reading)
    {
        reading = null;
        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            return (uint)Marshal.GetLastPInvokeError();
        }

        reading = new PowerStatusReading(status.ACLineStatus, status.BatteryFlag, status.BatteryLifePercent);
        return NativeMethods.ERROR_SUCCESS;
    }
}
