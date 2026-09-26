/**
 * @file    : FakeDisplayPlatform.cs
 * @author  : rudals252
 * @brief   : 활성 경로·GDI 장치별 모드·어댑터 이름·원격 세션을 고정 fixture 값으로 돌려주는 테스트용 디스플레이 API
 */

// 사용자 패키지
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 디스플레이 API입니다. 값은 테스트용 예시이며 실제 PC 값이 아닙니다.
/// </summary>
internal sealed class FakeDisplayPlatform : IDisplayPlatform
{
    /// <summary>활성 경로 조회 결과.</summary>
    public DisplayTopologyReading Topology { get; set; } = new(0, []);

    /// <summary>GDI 장치 이름별 모드 열거 결과(없으면 현재 모드 없음·빈 목록).</summary>
    public Dictionary<string, DisplayModeListReading> Modes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>GDI 장치 이름별 어댑터 이름.</summary>
    public Dictionary<string, string> AdapterNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>원격 세션 여부.</summary>
    public bool Remote { get; set; }

    /// <summary>모드 열거를 요청받은 GDI 장치 이름(빈 문자열 전달 금지 확인용).</summary>
    public List<string> EnumeratedDevices { get; } = [];

    /// <summary>
    /// 정상 경로 하나를 만든다.
    /// </summary>
    public static DisplayPathReading Path(
        uint targetId,
        string? devicePath,
        string? gdiName,
        uint sourceId = 0,
        long adapterLuid = 1,
        double? signalHz = 60,
        int targetNameError = 0,
        int sourceNameError = 0)
    {
        return new DisplayPathReading(
            adapterLuid,
            sourceId,
            targetId,
            10,
            signalHz,
            signalHz,
            devicePath,
            devicePath is null ? null : "테스트 모니터",
            targetNameError,
            gdiName,
            sourceNameError,
            @"\\?\PCI#VEN_10DE&DEV_0000#test#{guid}",
            0);
    }

    /// <summary>
    /// 모드 목록을 만든다(현재 모드 + 모드들).
    /// </summary>
    public static DisplayModeListReading ModeList(DisplayMode? current, params DisplayMode[] modes)
    {
        return new DisplayModeListReading(current, modes);
    }

    /// <inheritdoc />
    public DisplayTopologyReading QueryActivePaths()
    {
        return Topology;
    }

    /// <inheritdoc />
    public DisplayModeListReading EnumerateModes(string gdiDeviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gdiDeviceName);
        EnumeratedDevices.Add(gdiDeviceName);
        return Modes.TryGetValue(gdiDeviceName, out var modes) ? modes : new DisplayModeListReading(null, []);
    }

    /// <inheritdoc />
    public string? GetAdapterName(string gdiDeviceName)
    {
        return AdapterNames.TryGetValue(gdiDeviceName, out var name) ? name : null;
    }

    /// <inheritdoc />
    public bool IsRemoteSession()
    {
        return Remote;
    }
}
