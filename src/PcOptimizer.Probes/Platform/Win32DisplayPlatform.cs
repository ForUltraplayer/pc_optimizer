/**
 * @file    : Win32DisplayPlatform.cs
 * @author  : rudals252
 * @brief   : user32 P/Invoke로 활성 디스플레이 경로(QueryDisplayConfig)·이름(DisplayConfigGetDeviceInfo)·모드(EnumDisplaySettingsExW)·어댑터 이름(EnumDisplayDevicesW)·원격 세션을 읽는 조회 전용 구현
 */

// 기본 패키지
using System.Runtime.CompilerServices;

// 사용자 패키지
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// Win32 API로 디스플레이 구성을 읽는 구현입니다. 표시 설정을 바꾸는 API는 호출하지 않습니다.
/// 구조체 배치가 기대 크기와 다르면 잘못된 값을 읽지 않도록 조회하지 않고 <see cref="IDisplayPlatform.LAYOUT_MISMATCH_ERROR"/>를 돌려줍니다.
/// </summary>
public sealed class Win32DisplayPlatform : IDisplayPlatform
{
    private const int SUCCESS = 0;
    private const int MAX_QUERY_ATTEMPTS = 3;
    private const uint MAX_ENUMERATED_MODES = 4096;
    private const uint MAX_ENUMERATED_ADAPTERS = 64;
    private const int LUID_HIGH_SHIFT = 32;
    private const char NULL_CHAR = '\0';

    private static readonly bool LAYOUT_IS_VALID = DisplayStructSizes.AllMatch();

    /// <summary>공유 인스턴스.</summary>
    public static Win32DisplayPlatform Instance { get; } = new();

    /// <inheritdoc />
    public DisplayTopologyReading QueryActivePaths()
    {
        if (!LAYOUT_IS_VALID)
        {
            return new DisplayTopologyReading(IDisplayPlatform.LAYOUT_MISMATCH_ERROR, []);
        }

        var error = NativeMethods.ERROR_INSUFFICIENT_BUFFER;
        for (var attempt = 0; attempt < MAX_QUERY_ATTEMPTS && error == NativeMethods.ERROR_INSUFFICIENT_BUFFER; attempt++)
        {
            error = NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
            if (error != SUCCESS)
            {
                return new DisplayTopologyReading(error, []);
            }

            if (pathCount == 0)
            {
                return new DisplayTopologyReading(SUCCESS, []);
            }

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[Math.Max(modeCount, 1)];
            error = Query(paths, modes, ref pathCount, ref modeCount);
            if (error == SUCCESS)
            {
                return new DisplayTopologyReading(SUCCESS, [.. paths.Take((int)pathCount).Select(path => ReadPath(path, modes, modeCount))]);
            }
        }

        return new DisplayTopologyReading(error, []);
    }

    /// <inheritdoc />
    public DisplayModeListReading EnumerateModes(string gdiDeviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gdiDeviceName);
        if (!LAYOUT_IS_VALID)
        {
            return new DisplayModeListReading(null, []);
        }

        var current = ReadMode(gdiDeviceName, NativeMethods.ENUM_CURRENT_SETTINGS);
        var modes = new List<DisplayMode>();
        for (uint modeNumber = 0; modeNumber < MAX_ENUMERATED_MODES; modeNumber++)
        {
            if (ReadMode(gdiDeviceName, modeNumber) is not { } mode)
            {
                break;
            }

            modes.Add(mode);
        }

        return new DisplayModeListReading(current, modes);
    }

    /// <inheritdoc />
    public unsafe string? GetAdapterName(string gdiDeviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gdiDeviceName);
        if (!LAYOUT_IS_VALID)
        {
            return null;
        }

        for (uint index = 0; index < MAX_ENUMERATED_ADAPTERS; index++)
        {
            var device = new DisplayDeviceW { Cb = (uint)Unsafe.SizeOf<DisplayDeviceW>() };
            if (!NativeMethods.EnumDisplayDevices(null, index, &device, 0))
            {
                return null;
            }

            if (string.Equals(ToText(device.DeviceName), gdiDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                return NullIfBlank(ToText(device.DeviceString));
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsRemoteSession()
    {
        return NativeMethods.GetSystemMetrics(NativeMethods.SM_REMOTESESSION) != 0;
    }

    /// <summary>
    /// 고정 배열을 붙잡아 QueryDisplayConfig를 호출한다.
    /// </summary>
    private static unsafe int Query(DisplayConfigPathInfo[] paths, DisplayConfigModeInfo[] modes, ref uint pathCount, ref uint modeCount)
    {
        fixed (DisplayConfigPathInfo* pathPointer = paths)
        fixed (DisplayConfigModeInfo* modePointer = modes)
        {
            return NativeMethods.QueryDisplayConfig(NativeMethods.QDC_ONLY_ACTIVE_PATHS, ref pathCount, pathPointer, ref modeCount, modePointer, IntPtr.Zero);
        }
    }

    /// <summary>
    /// 경로 하나의 주사율과 이름들을 읽는다.
    /// </summary>
    private static DisplayPathReading ReadPath(DisplayConfigPathInfo path, DisplayConfigModeInfo[] modes, uint modeCount)
    {
        var target = path.TargetInfo;
        var targetError = ReadTargetName(target.AdapterId, target.Id, out var monitorPath, out var monitorName);
        var sourceError = ReadSourceName(path.SourceInfo.AdapterId, path.SourceInfo.Id, out var gdiName);
        var adapterError = ReadAdapterPath(target.AdapterId, out var adapterPath);

        return new DisplayPathReading(
            ToInt64(target.AdapterId),
            path.SourceInfo.Id,
            target.Id,
            target.OutputTechnology,
            ToHz(target.RefreshRate),
            SignalHz(target.ModeInfoIdx, modes, modeCount),
            monitorPath,
            monitorName,
            targetError,
            gdiName,
            sourceError,
            adapterPath,
            adapterError);
    }

    /// <summary>
    /// 대상 모드의 신호 주사율(vSyncFreq)을 읽는다. 모드 인덱스가 없거나 대상 모드가 아니면 null.
    /// </summary>
    private static double? SignalHz(uint modeIndex, DisplayConfigModeInfo[] modes, uint modeCount)
    {
        if (modeIndex == NativeMethods.DISPLAYCONFIG_PATH_MODE_IDX_INVALID || modeIndex >= modeCount || modeIndex >= modes.Length)
        {
            return null;
        }

        var mode = modes[modeIndex];
        return mode.InfoType == NativeMethods.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET ? ToHz(mode.TargetVideoSignalInfo.VSyncFreq) : null;
    }

    /// <summary>
    /// 모니터 장치 경로와 표시 이름을 읽는다.
    /// </summary>
    private static unsafe int ReadTargetName(Luid adapterId, uint targetId, out string? devicePath, out string? friendlyName)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Header = Header(NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, Unsafe.SizeOf<DisplayConfigTargetDeviceName>(), adapterId, targetId),
        };

        var error = NativeMethods.DisplayConfigGetDeviceInfo(&request.Header);
        devicePath = error == SUCCESS ? NullIfBlank(ToText(request.MonitorDevicePath)) : null;
        friendlyName = error == SUCCESS ? NullIfBlank(ToText(request.MonitorFriendlyDeviceName)) : null;
        return error;
    }

    /// <summary>
    /// 원본의 GDI 장치 이름을 읽는다.
    /// </summary>
    private static unsafe int ReadSourceName(Luid adapterId, uint sourceId, out string? gdiName)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = Header(NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, Unsafe.SizeOf<DisplayConfigSourceDeviceName>(), adapterId, sourceId),
        };

        var error = NativeMethods.DisplayConfigGetDeviceInfo(&request.Header);
        gdiName = error == SUCCESS ? NullIfBlank(ToText(request.ViewGdiDeviceName)) : null;
        return error;
    }

    /// <summary>
    /// 어댑터 장치 경로를 읽는다.
    /// </summary>
    private static unsafe int ReadAdapterPath(Luid adapterId, out string? adapterPath)
    {
        var request = new DisplayConfigAdapterName
        {
            Header = Header(NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME, Unsafe.SizeOf<DisplayConfigAdapterName>(), adapterId, 0),
        };

        var error = NativeMethods.DisplayConfigGetDeviceInfo(&request.Header);
        adapterPath = error == SUCCESS ? NullIfBlank(ToText(request.AdapterDevicePath)) : null;
        return error;
    }

    /// <summary>
    /// 모드 하나를 읽는다(dmSize를 채워 호출). 실패하면 null.
    /// </summary>
    private static unsafe DisplayMode? ReadMode(string gdiDeviceName, uint modeNumber)
    {
        var devMode = new DevModeW { Size = (ushort)Unsafe.SizeOf<DevModeW>(), DriverExtra = 0 };
        if (!NativeMethods.EnumDisplaySettingsEx(gdiDeviceName, modeNumber, &devMode, 0))
        {
            return null;
        }

        var orientation = (devMode.Fields & NativeMethods.DM_DISPLAYORIENTATION) != 0 ? (int)devMode.DisplayOrientation : 0;
        return new DisplayMode(
            (int)devMode.PelsWidth,
            (int)devMode.PelsHeight,
            orientation,
            (int)devMode.BitsPerPel,
            (devMode.DisplayFlags & NativeMethods.DM_INTERLACED) != 0,
            (int)devMode.DisplayFrequency);
    }

    /// <summary>
    /// 장치 정보 요청 헤더를 만든다.
    /// </summary>
    private static DisplayConfigDeviceInfoHeader Header(int type, int size, Luid adapterId, uint id)
    {
        return new DisplayConfigDeviceInfoHeader { Type = type, Size = (uint)size, AdapterId = adapterId, Id = id };
    }

    /// <summary>
    /// 유리수 주사율을 Hz로 바꾼다. 분모가 0이면 null.
    /// </summary>
    private static double? ToHz(DisplayConfigRational rational)
    {
        return rational.Denominator == 0 ? null : (double)rational.Numerator / rational.Denominator;
    }

    /// <summary>
    /// LUID를 64비트 정수로 바꾼다.
    /// </summary>
    private static long ToInt64(Luid luid)
    {
        return ((long)luid.HighPart << LUID_HIGH_SHIFT) | luid.LowPart;
    }

    /// <summary>
    /// 고정 문자 버퍼를 첫 NUL 앞까지 문자열로 읽는다.
    /// </summary>
    private static string ToText(ReadOnlySpan<char> buffer)
    {
        var end = buffer.IndexOf(NULL_CHAR);
        return new string(end < 0 ? buffer : buffer[..end]);
    }

    /// <summary>
    /// 공백 문자열을 null로 바꾼다.
    /// </summary>
    private static string? NullIfBlank(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
