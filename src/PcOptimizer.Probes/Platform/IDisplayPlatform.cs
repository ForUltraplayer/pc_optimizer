/**
 * @file    : IDisplayPlatform.cs
 * @author  : rudals252
 * @brief   : 디스플레이 조회 계약(활성 경로·이름, GDI 장치 모드 열거, 어댑터 이름, 원격 세션)과 원시 읽기 결과 레코드. 테스트에서 fixture 값으로 바꿔 끼운다
 */

// 사용자 패키지
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 활성 디스플레이 경로 하나의 원시 읽기 결과입니다(QueryDisplayConfig + DisplayConfigGetDeviceInfo).
/// 이름을 읽지 못한 항목은 null이며, 해당 Win32 오류 코드를 함께 담습니다(0이면 성공).
/// </summary>
/// <param name="AdapterLuid">어댑터 LUID(같은 부팅 안에서 원본 공유 여부 판단용).</param>
/// <param name="SourceId">원본 ID.</param>
/// <param name="TargetId">대상 ID.</param>
/// <param name="OutputTechnology">출력 기술 원시 값.</param>
/// <param name="PathRefreshHz">경로 주사율(분모 0이면 null).</param>
/// <param name="SignalRefreshHz">대상 모드 신호 주사율(모드 정보가 없으면 null).</param>
/// <param name="MonitorDevicePath">모니터 장치 경로.</param>
/// <param name="MonitorFriendlyName">모니터 표시 이름.</param>
/// <param name="TargetNameError">대상 이름 조회 오류 코드.</param>
/// <param name="GdiDeviceName">GDI 원본 이름(예: "\\.\DISPLAY1").</param>
/// <param name="SourceNameError">원본 이름 조회 오류 코드.</param>
/// <param name="AdapterDevicePath">어댑터 장치 경로.</param>
/// <param name="AdapterNameError">어댑터 이름 조회 오류 코드.</param>
public sealed record DisplayPathReading(
    long AdapterLuid,
    uint SourceId,
    uint TargetId,
    int OutputTechnology,
    double? PathRefreshHz,
    double? SignalRefreshHz,
    string? MonitorDevicePath,
    string? MonitorFriendlyName,
    int TargetNameError,
    string? GdiDeviceName,
    int SourceNameError,
    string? AdapterDevicePath,
    int AdapterNameError);

/// <summary>
/// 활성 경로 조회 결과입니다. Error가 0이 아니면 경로 목록은 비어 있고 조회 실패입니다.
/// </summary>
/// <param name="Error">Win32 오류 코드(0 성공, <see cref="IDisplayPlatform.LAYOUT_MISMATCH_ERROR"/>는 구조체 배치 불일치).</param>
/// <param name="Paths">활성 경로 목록.</param>
public sealed record DisplayTopologyReading(int Error, IReadOnlyList<DisplayPathReading> Paths);

/// <summary>
/// GDI 장치 하나의 모드 열거 결과입니다.
/// </summary>
/// <param name="Current">현재 모드(읽지 못하면 null).</param>
/// <param name="Modes">드라이버가 보고한 모드 목록(현재 방향 기준).</param>
public sealed record DisplayModeListReading(DisplayMode? Current, IReadOnlyList<DisplayMode> Modes);

/// <summary>
/// 디스플레이 조회 계약입니다. 조회 전용이며 표시 설정을 바꾸지 않습니다. 예외 대신 오류 코드·null로 실패를 알립니다.
/// </summary>
public interface IDisplayPlatform
{
    /// <summary>구조체 배치가 기대와 달라 조회하지 않았음을 뜻하는 오류 코드.</summary>
    public const int LAYOUT_MISMATCH_ERROR = -1;

    /// <summary>
    /// 활성 경로와 각 경로의 모니터·원본·어댑터 이름을 읽습니다.
    /// </summary>
    /// <returns>조회 결과.</returns>
    DisplayTopologyReading QueryActivePaths();

    /// <summary>
    /// GDI 장치의 현재 모드와 보고된 모드 목록을 읽습니다.
    /// </summary>
    /// <param name="gdiDeviceName">GDI 장치 이름(빈 문자열 불가).</param>
    /// <returns>모드 열거 결과.</returns>
    DisplayModeListReading EnumerateModes(string gdiDeviceName);

    /// <summary>
    /// GDI 장치에 연결된 어댑터 이름(EnumDisplayDevices DeviceString)을 읽습니다. 찾지 못하면 null.
    /// </summary>
    /// <param name="gdiDeviceName">GDI 장치 이름.</param>
    /// <returns>어댑터 이름 또는 null.</returns>
    string? GetAdapterName(string gdiDeviceName);

    /// <summary>
    /// 원격 세션(원격 데스크톱 등)에서 실행 중인지 확인합니다.
    /// </summary>
    /// <returns>원격 세션이면 true.</returns>
    bool IsRemoteSession();
}
