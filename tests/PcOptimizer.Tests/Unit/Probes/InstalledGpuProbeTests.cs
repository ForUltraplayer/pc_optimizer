/**
 * @file    : InstalledGpuProbeTests.cs
 * @author  : rudals252
 * @brief   : 설치 GPU 프로브가 fixture(정상·하드웨어 ID 조회 실패·버전 누락·클래스 없음·빈 결과·접근 거부)를 측정값/상태/Issue로 바꾸는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="InstalledGpuProbe"/>의 수집 결과 변환을 비식별 fixture로 검증합니다.
/// </summary>
public sealed class InstalledGpuProbeTests
{
    private const string NVIDIA_PNP = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1\4&TEST&0&0009";
    private const string VIRTUAL_PNP = @"ROOT\DISPLAY\0000";

    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 비디오 컨트롤러 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> Controller(string name, string? version, string pnpId)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = name,
            ["DriverVersion"] = version,
            ["DriverDate"] = "20260102000000.000000-000",
            ["PNPDeviceID"] = pnpId,
            ["AdapterCompatibility"] = "테스트",
            ["VideoProcessor"] = null,
        };
    }

    /// <summary>
    /// PnP 장치 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> Entity(string pnpId, params string[] hardwareIds)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["PNPDeviceID"] = pnpId, ["HardwareID"] = hardwareIds };
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeWmiClient wmi)
    {
        return new InstalledGpuProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>어댑터별 이름·버전·날짜(yyyy-MM-dd)·PnP ID와 PnP 하드웨어 ID를 기록하고 Success다.</summary>
    [Fact]
    public async Task 어댑터별_설치_정보를_기록한다()
    {
        var wmi = new FakeWmiClient()
            .WithRows(InstalledGpuProbe.VIDEO_CONTROLLER_CLASS, Controller("가상", "0.45.0.0", VIRTUAL_PNP), Controller("NVIDIA", "32.0.16.1656", NVIDIA_PNP))
            .WithRows(InstalledGpuProbe.PNP_ENTITY_CLASS, Entity(NVIDIA_PNP, @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1", @"PCI\VEN_10DE&DEV_0001"), Entity("OTHER", "X"));

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new IntegerValue(2), Assert.Single(result.Measurements, m => m.Name == GpuProbeContract.ADAPTER_COUNT).Value);
        Assert.Equal(
            new TextValue("32.0.16.1656"),
            Assert.Single(result.Measurements, m => m.Name == GpuProbeContract.AdapterMeasurementName(1, GpuProbeContract.FIELD_DRIVER_VERSION)).Value);
        Assert.Equal(
            new TextValue("2026-01-02"),
            Assert.Single(result.Measurements, m => m.Name == GpuProbeContract.AdapterMeasurementName(1, GpuProbeContract.FIELD_DRIVER_DATE)).Value);
        var ids = Assert.Single(result.Measurements, m => m.Name == GpuProbeContract.AdapterMeasurementName(1, GpuProbeContract.FIELD_HARDWARE_IDS));
        Assert.Equal(2, Assert.IsType<TextListValue>(ids.Value).Values.Count);
        Assert.DoesNotContain(result.Measurements, m => m.Name == GpuProbeContract.AdapterMeasurementName(0, GpuProbeContract.FIELD_HARDWARE_IDS));
        Assert.DoesNotContain(result.Measurements, m => m.Name == GpuProbeContract.AdapterMeasurementName(1, GpuProbeContract.FIELD_VIDEO_PROCESSOR));
    }

    /// <summary>하드웨어 ID 보조 조회가 실패해도 설치 정보는 유지하고 Partial이다.</summary>
    [Fact]
    public async Task 하드웨어_ID_실패는_부분_수집이다()
    {
        var wmi = new FakeWmiClient()
            .WithRows(InstalledGpuProbe.VIDEO_CONTROLLER_CLASS, Controller("NVIDIA", "32.0.16.1656", NVIDIA_PNP))
            .WithFailure(InstalledGpuProbe.PNP_ENTITY_CLASS, WmiQueryStatus.Timeout);

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.PartialData, Assert.Single(result.Issues).Reason);
        Assert.Contains(result.Measurements, m => m.Name == GpuProbeContract.AdapterMeasurementName(0, GpuProbeContract.FIELD_DRIVER_VERSION));
    }

    /// <summary>드라이버 버전이 빈 어댑터가 있으면 Partial이다.</summary>
    [Fact]
    public async Task 버전_누락은_부분_수집이다()
    {
        var wmi = new FakeWmiClient()
            .WithRows(InstalledGpuProbe.VIDEO_CONTROLLER_CLASS, Controller("NVIDIA", null, NVIDIA_PNP))
            .WithRows(InstalledGpuProbe.PNP_ENTITY_CLASS);

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
    }

    /// <summary>클래스 없음·접근 거부·빈 결과는 측정값 없이 Failed이며 빈 성공으로 돌려주지 않는다.</summary>
    [Theory]
    [InlineData(WmiQueryStatus.ClassUnavailable, CannotVerifyReason.Unsupported)]
    [InlineData(WmiQueryStatus.AccessDenied, CannotVerifyReason.AccessDenied)]
    [InlineData(WmiQueryStatus.Success, CannotVerifyReason.Unsupported)]
    public async Task 조회_실패와_빈_결과는_실패다(WmiQueryStatus status, CannotVerifyReason reason)
    {
        var wmi = status == WmiQueryStatus.Success
            ? new FakeWmiClient().WithRows(InstalledGpuProbe.VIDEO_CONTROLLER_CLASS)
            : new FakeWmiClient().WithFailure(InstalledGpuProbe.VIDEO_CONTROLLER_CLASS, status);

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(reason, Assert.Single(result.Issues).Reason);
    }
}
