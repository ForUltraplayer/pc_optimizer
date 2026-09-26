/**
 * @file    : NativeMethods.DisplayStructs.cs
 * @author  : rudals252
 * @brief   : 디스플레이 조회 P/Invoke 구조체(DISPLAYCONFIG_* 경로·모드·장치 이름, DEVMODEW, DISPLAY_DEVICEW)의 네이티브 배치와 기대 크기 상수
 */

// 기본 패키지
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 디스플레이 구조체의 기대 크기(바이트)입니다. 실행 시 실제 크기와 비교해 배치가 어긋나면 조회하지 않습니다.
/// </summary>
internal static class DisplayStructSizes
{
    /// <summary>DISPLAYCONFIG_PATH_INFO.</summary>
    public const int PATH_INFO = 72;

    /// <summary>DISPLAYCONFIG_MODE_INFO.</summary>
    public const int MODE_INFO = 64;

    /// <summary>DISPLAYCONFIG_DEVICE_INFO_HEADER.</summary>
    public const int DEVICE_INFO_HEADER = 20;

    /// <summary>DISPLAYCONFIG_TARGET_DEVICE_NAME.</summary>
    public const int TARGET_DEVICE_NAME = 420;

    /// <summary>DISPLAYCONFIG_SOURCE_DEVICE_NAME.</summary>
    public const int SOURCE_DEVICE_NAME = 84;

    /// <summary>DISPLAYCONFIG_ADAPTER_NAME.</summary>
    public const int ADAPTER_NAME = 276;

    /// <summary>DEVMODEW(드라이버 추가 영역 제외).</summary>
    public const int DEVMODE = 220;

    /// <summary>DISPLAY_DEVICEW.</summary>
    public const int DISPLAY_DEVICE = 840;

    /// <summary>
    /// 모든 구조체의 실제 크기가 기대 크기와 같은지 확인합니다.
    /// </summary>
    /// <returns>같으면 true.</returns>
    public static bool AllMatch()
    {
        return Unsafe.SizeOf<DisplayConfigPathInfo>() == PATH_INFO
            && Unsafe.SizeOf<DisplayConfigModeInfo>() == MODE_INFO
            && Unsafe.SizeOf<DisplayConfigDeviceInfoHeader>() == DEVICE_INFO_HEADER
            && Unsafe.SizeOf<DisplayConfigTargetDeviceName>() == TARGET_DEVICE_NAME
            && Unsafe.SizeOf<DisplayConfigSourceDeviceName>() == SOURCE_DEVICE_NAME
            && Unsafe.SizeOf<DisplayConfigAdapterName>() == ADAPTER_NAME
            && Unsafe.SizeOf<DevModeW>() == DEVMODE
            && Unsafe.SizeOf<DisplayDeviceW>() == DISPLAY_DEVICE;
    }
}

/// <summary>32 UTF-16 문자 고정 버퍼.</summary>
[InlineArray(32)]
internal struct CharBuffer32
{
    private char _element;
}

/// <summary>64 UTF-16 문자 고정 버퍼.</summary>
[InlineArray(64)]
internal struct CharBuffer64
{
    private char _element;
}

/// <summary>128 UTF-16 문자 고정 버퍼.</summary>
[InlineArray(128)]
internal struct CharBuffer128
{
    private char _element;
}

/// <summary>LUID(어댑터 식별자, 부팅마다 바뀔 수 있음).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    /// <summary>하위 32비트.</summary>
    public uint LowPart;

    /// <summary>상위 32비트.</summary>
    public int HighPart;
}

/// <summary>DISPLAYCONFIG_RATIONAL.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigRational
{
    /// <summary>분자.</summary>
    public uint Numerator;

    /// <summary>분모.</summary>
    public uint Denominator;
}

/// <summary>DISPLAYCONFIG_2DREGION.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfig2DRegion
{
    /// <summary>가로.</summary>
    public uint Cx;

    /// <summary>세로.</summary>
    public uint Cy;
}

/// <summary>DISPLAYCONFIG_PATH_SOURCE_INFO(20바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathSourceInfo
{
    /// <summary>어댑터 LUID.</summary>
    public Luid AdapterId;

    /// <summary>원본 ID.</summary>
    public uint Id;

    /// <summary>모드 배열 인덱스.</summary>
    public uint ModeInfoIdx;

    /// <summary>상태 플래그.</summary>
    public uint StatusFlags;
}

/// <summary>DISPLAYCONFIG_PATH_TARGET_INFO(48바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathTargetInfo
{
    /// <summary>어댑터 LUID.</summary>
    public Luid AdapterId;

    /// <summary>대상 ID.</summary>
    public uint Id;

    /// <summary>모드 배열 인덱스.</summary>
    public uint ModeInfoIdx;

    /// <summary>출력 기술(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY).</summary>
    public int OutputTechnology;

    /// <summary>회전.</summary>
    public int Rotation;

    /// <summary>배율.</summary>
    public int Scaling;

    /// <summary>경로 주사율.</summary>
    public DisplayConfigRational RefreshRate;

    /// <summary>주사선 순서.</summary>
    public int ScanLineOrdering;

    /// <summary>대상 사용 가능 여부(BOOL).</summary>
    public int TargetAvailable;

    /// <summary>상태 플래그.</summary>
    public uint StatusFlags;
}

/// <summary>DISPLAYCONFIG_PATH_INFO(72바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathInfo
{
    /// <summary>원본 정보.</summary>
    public DisplayConfigPathSourceInfo SourceInfo;

    /// <summary>대상 정보.</summary>
    public DisplayConfigPathTargetInfo TargetInfo;

    /// <summary>경로 플래그.</summary>
    public uint Flags;
}

/// <summary>DISPLAYCONFIG_VIDEO_SIGNAL_INFO(48바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigVideoSignalInfo
{
    /// <summary>픽셀 속도.</summary>
    public ulong PixelRate;

    /// <summary>수평 동기 주파수.</summary>
    public DisplayConfigRational HSyncFreq;

    /// <summary>수직 동기 주파수(신호 주사율).</summary>
    public DisplayConfigRational VSyncFreq;

    /// <summary>활성 영역.</summary>
    public DisplayConfig2DRegion ActiveSize;

    /// <summary>전체 영역.</summary>
    public DisplayConfig2DRegion TotalSize;

    /// <summary>비디오 표준(공용체).</summary>
    public uint VideoStandard;

    /// <summary>주사선 순서.</summary>
    public int ScanLineOrdering;
}

/// <summary>DISPLAYCONFIG_MODE_INFO(64바이트). 공용체 중 대상 모드(신호 정보)만 씁니다.</summary>
[StructLayout(LayoutKind.Explicit, Size = DisplayStructSizes.MODE_INFO)]
internal struct DisplayConfigModeInfo
{
    /// <summary>모드 정보 형식.</summary>
    [FieldOffset(0)]
    public int InfoType;

    /// <summary>원본 또는 대상 ID.</summary>
    [FieldOffset(4)]
    public uint Id;

    /// <summary>어댑터 LUID.</summary>
    [FieldOffset(8)]
    public Luid AdapterId;

    /// <summary>대상 모드의 신호 정보(InfoType이 대상일 때만 유효).</summary>
    [FieldOffset(16)]
    public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
}

/// <summary>DISPLAYCONFIG_DEVICE_INFO_HEADER(20바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigDeviceInfoHeader
{
    /// <summary>요청 형식.</summary>
    public int Type;

    /// <summary>요청 구조체 전체 크기.</summary>
    public uint Size;

    /// <summary>어댑터 LUID.</summary>
    public Luid AdapterId;

    /// <summary>원본 또는 대상 ID.</summary>
    public uint Id;
}

/// <summary>DISPLAYCONFIG_TARGET_DEVICE_NAME(420바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigTargetDeviceName
{
    /// <summary>요청 헤더.</summary>
    public DisplayConfigDeviceInfoHeader Header;

    /// <summary>플래그(비트 필드 공용체).</summary>
    public uint Flags;

    /// <summary>출력 기술.</summary>
    public int OutputTechnology;

    /// <summary>EDID 제조사 ID.</summary>
    public ushort EdidManufactureId;

    /// <summary>EDID 제품 코드.</summary>
    public ushort EdidProductCodeId;

    /// <summary>커넥터 인스턴스.</summary>
    public uint ConnectorInstance;

    /// <summary>모니터 표시 이름.</summary>
    public CharBuffer64 MonitorFriendlyDeviceName;

    /// <summary>모니터 장치 경로.</summary>
    public CharBuffer128 MonitorDevicePath;
}

/// <summary>DISPLAYCONFIG_SOURCE_DEVICE_NAME(84바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSourceDeviceName
{
    /// <summary>요청 헤더.</summary>
    public DisplayConfigDeviceInfoHeader Header;

    /// <summary>GDI 장치 이름(예: "\\.\DISPLAY1").</summary>
    public CharBuffer32 ViewGdiDeviceName;
}

/// <summary>DISPLAYCONFIG_ADAPTER_NAME(276바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigAdapterName
{
    /// <summary>요청 헤더.</summary>
    public DisplayConfigDeviceInfoHeader Header;

    /// <summary>어댑터 장치 경로.</summary>
    public CharBuffer128 AdapterDevicePath;
}

/// <summary>DEVMODEW(220바이트, 디스플레이용 필드만 이름 붙임).</summary>
[StructLayout(LayoutKind.Explicit, Size = DisplayStructSizes.DEVMODE)]
internal struct DevModeW
{
    /// <summary>장치 이름.</summary>
    [FieldOffset(0)]
    public CharBuffer32 DeviceName;

    /// <summary>구조체 크기(dmSize). 호출 전에 반드시 채운다.</summary>
    [FieldOffset(68)]
    public ushort Size;

    /// <summary>드라이버 추가 영역 크기(dmDriverExtra).</summary>
    [FieldOffset(70)]
    public ushort DriverExtra;

    /// <summary>유효 필드(dmFields).</summary>
    [FieldOffset(72)]
    public uint Fields;

    /// <summary>방향(dmDisplayOrientation).</summary>
    [FieldOffset(84)]
    public uint DisplayOrientation;

    /// <summary>픽셀당 비트 수(dmBitsPerPel).</summary>
    [FieldOffset(168)]
    public uint BitsPerPel;

    /// <summary>가로(dmPelsWidth).</summary>
    [FieldOffset(172)]
    public uint PelsWidth;

    /// <summary>세로(dmPelsHeight).</summary>
    [FieldOffset(176)]
    public uint PelsHeight;

    /// <summary>표시 플래그(dmDisplayFlags).</summary>
    [FieldOffset(180)]
    public uint DisplayFlags;

    /// <summary>주사율(dmDisplayFrequency).</summary>
    [FieldOffset(184)]
    public uint DisplayFrequency;
}

/// <summary>DISPLAY_DEVICEW(840바이트).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayDeviceW
{
    /// <summary>구조체 크기(cb). 호출 전에 반드시 채운다.</summary>
    public uint Cb;

    /// <summary>장치 이름(예: "\\.\DISPLAY1").</summary>
    public CharBuffer32 DeviceName;

    /// <summary>장치 설명(어댑터 이름).</summary>
    public CharBuffer128 DeviceString;

    /// <summary>상태 플래그.</summary>
    public uint StateFlags;

    /// <summary>장치 ID.</summary>
    public CharBuffer128 DeviceId;

    /// <summary>레지스트리 키.</summary>
    public CharBuffer128 DeviceKey;
}
