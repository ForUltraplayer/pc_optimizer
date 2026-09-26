/**
 * @file    : SystemDetailsProbeTests.cs
 * @author  : rudals252
 * @brief   : OS·CPU·BIOS 날짜·메인보드·물리 네트워크 어댑터를 가짜 WMI 행으로 수집하고, 일부 실패 시 Partial, 전부 실패 시 Failed인지, 플레이스홀더 메인보드 값과 개인정보(일련번호·MAC·UUID) 미수집을 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Hardware;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="SystemDetailsProbe"/>의 수집 결과 변환을 비식별 fixture로 검증합니다.
/// </summary>
public sealed class SystemDetailsProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>프로브를 실행한다.</summary>
    private static Task<ProbeResult> RunAsync(FakeWmiClient wmi) =>
        new SystemDetailsProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);

    /// <summary>다섯 클래스가 모두 있으면 Success이고 측정 이름이 계약대로다.</summary>
    [Fact]
    public async Task CollectsAllSections()
    {
        var wmi = new FakeWmiClient()
            .WithRows(SystemDetailsProbe.OS_CLASS, new Dictionary<string, object?> { ["Caption"] = "Microsoft Windows 11 Pro", ["Version"] = "10.0.26200", ["BuildNumber"] = "26200", ["InstallDate"] = "20260101120000.000000+540" })
            .WithRows(SystemDetailsProbe.CPU_CLASS, new Dictionary<string, object?> { ["Name"] = "AMD Ryzen 7 9800X3D", ["NumberOfCores"] = 8u, ["NumberOfLogicalProcessors"] = 16u, ["MaxClockSpeed"] = 4700u })
            .WithRows(SystemDetailsProbe.BIOS_CLASS, new Dictionary<string, object?> { ["ReleaseDate"] = "20260815000000.000000+000" })
            .WithRows(SystemDetailsProbe.BOARD_CLASS, new Dictionary<string, object?> { ["Manufacturer"] = "ASUSTeK COMPUTER INC.", ["Product"] = "ROG STRIX B650E-F GAMING WIFI", ["Version"] = "Rev 1.xx" })
            .WithRows(SystemDetailsProbe.NIC_CLASS,
                new Dictionary<string, object?> { ["Name"] = "Intel(R) Ethernet Controller I226-V", ["AdapterTypeId"] = 0u, ["Manufacturer"] = "Intel", ["NetEnabled"] = true, ["PhysicalAdapter"] = true, ["PNPDeviceID"] = "PCI\\VEN_8086&DEV_125C\\1" },
                new Dictionary<string, object?> { ["Name"] = "WAN Miniport (IP)", ["PhysicalAdapter"] = false })
            .WithRows(SystemDetailsProbe.DRIVER_CLASS, new Dictionary<string, object?> { ["DeviceID"] = "PCI\\VEN_8086&DEV_125C\\1", ["DriverVersion"] = "2.1.4.3", ["DeviceClass"] = "NET" });

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal("Microsoft Windows 11 Pro", Text(result, SystemDetailsProbeContract.OS_CAPTION));
        Assert.Equal("2026-01-01T12:00:00+09:00", Text(result, SystemDetailsProbeContract.OS_INSTALL_DATE));
        Assert.Equal(8, Integer(result, SystemDetailsProbeContract.CPU_CORES));
        Assert.Equal("2026-08-15", Text(result, SystemDetailsProbeContract.BIOS_RELEASE_DATE));
        Assert.Equal("ROG STRIX B650E-F GAMING WIFI", Text(result, SystemDetailsProbeContract.BOARD_PRODUCT));
        Assert.Equal(1, Integer(result, SystemDetailsProbeContract.NIC_COUNT));
        Assert.Equal("2.1.4.3", Text(result, SystemDetailsProbeContract.AdapterMeasurementName(0, SystemDetailsProbeContract.FIELD_DRIVER_VERSION)));
    }

    /// <summary>CPU 조회만 실패하면 Partial이고 사유가 남는다. 모두 실패하면 Failed.</summary>
    [Fact]
    public async Task PartialAndFailed()
    {
        var partial = new FakeWmiClient()
            .WithRows(SystemDetailsProbe.OS_CLASS, new Dictionary<string, object?> { ["Caption"] = "W", ["Version"] = "10.0", ["BuildNumber"] = "1" })
            .WithFailure(SystemDetailsProbe.CPU_CLASS, WmiQueryStatus.ClassUnavailable)
            .WithRows(SystemDetailsProbe.BIOS_CLASS, new Dictionary<string, object?> { ["ReleaseDate"] = "20260815000000.000000+000" })
            .WithRows(SystemDetailsProbe.BOARD_CLASS)
            .WithRows(SystemDetailsProbe.NIC_CLASS)
            .WithRows(SystemDetailsProbe.DRIVER_CLASS);
        var result = await RunAsync(partial);
        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Contains(result.Issues, i => i.Reason == CannotVerifyReason.ProbeError || i.Reason == CannotVerifyReason.Unsupported);

        var failed = new FakeWmiClient()
            .WithFailure(SystemDetailsProbe.OS_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.CPU_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.BIOS_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.BOARD_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.NIC_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.DRIVER_CLASS, WmiQueryStatus.AccessDenied);
        var failedResult = await RunAsync(failed);
        Assert.Equal(ProbeStatus.Failed, failedResult.Status);
    }

    /// <summary>플레이스홀더·공백 메인보드 값은 측정에서 생략되고 PartialData Issue가 남는다.</summary>
    [Fact]
    public async Task PlaceholderBoardValuesAreOmitted()
    {
        var wmi = new FakeWmiClient()
            .WithRows(SystemDetailsProbe.OS_CLASS, new Dictionary<string, object?> { ["Caption"] = "W", ["Version"] = "10.0", ["BuildNumber"] = "1" })
            .WithRows(SystemDetailsProbe.CPU_CLASS, new Dictionary<string, object?> { ["Name"] = "CPU", ["NumberOfCores"] = 4u, ["NumberOfLogicalProcessors"] = 8u, ["MaxClockSpeed"] = 3000u })
            .WithRows(SystemDetailsProbe.BIOS_CLASS, new Dictionary<string, object?> { ["ReleaseDate"] = "20260815000000.000000+000" })
            .WithRows(SystemDetailsProbe.BOARD_CLASS, new Dictionary<string, object?> { ["Manufacturer"] = "To be filled by O.E.M.", ["Product"] = "Default string", ["Version"] = "   " })
            .WithRows(SystemDetailsProbe.NIC_CLASS)
            .WithRows(SystemDetailsProbe.DRIVER_CLASS);

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == SystemDetailsProbeContract.BOARD_MANUFACTURER);
        Assert.DoesNotContain(result.Measurements, m => m.Name == SystemDetailsProbeContract.BOARD_PRODUCT);
        Assert.DoesNotContain(result.Measurements, m => m.Name == SystemDetailsProbeContract.BOARD_VERSION);
        Assert.Contains(result.Issues, i => i.Reason == CannotVerifyReason.PartialData);
    }

    /// <summary>측정 이름 중 일련번호·MAC 주소·UUID를 나타내는 이름이 없다(개인정보 미수집).</summary>
    [Fact]
    public async Task DoesNotCollectPrivacySensitiveFields()
    {
        var wmi = new FakeWmiClient()
            .WithRows(SystemDetailsProbe.OS_CLASS, new Dictionary<string, object?> { ["Caption"] = "W", ["Version"] = "10.0", ["BuildNumber"] = "1" })
            .WithRows(SystemDetailsProbe.CPU_CLASS, new Dictionary<string, object?> { ["Name"] = "CPU", ["NumberOfCores"] = 4u, ["NumberOfLogicalProcessors"] = 8u, ["MaxClockSpeed"] = 3000u })
            .WithRows(SystemDetailsProbe.BIOS_CLASS, new Dictionary<string, object?> { ["ReleaseDate"] = "20260815000000.000000+000" })
            .WithRows(SystemDetailsProbe.BOARD_CLASS, new Dictionary<string, object?> { ["Manufacturer"] = "ASUSTeK COMPUTER INC.", ["Product"] = "ROG STRIX B650E-F GAMING WIFI", ["Version"] = "Rev 1.xx" })
            .WithRows(SystemDetailsProbe.NIC_CLASS,
                new Dictionary<string, object?> { ["Name"] = "Intel(R) Ethernet Controller I226-V", ["AdapterTypeId"] = 0u, ["Manufacturer"] = "Intel", ["NetEnabled"] = true, ["PhysicalAdapter"] = true, ["PNPDeviceID"] = "PCI\\VEN_8086&DEV_125C\\1" })
            .WithRows(SystemDetailsProbe.DRIVER_CLASS, new Dictionary<string, object?> { ["DeviceID"] = "PCI\\VEN_8086&DEV_125C\\1", ["DriverVersion"] = "2.1.4.3", ["DeviceClass"] = "NET" });

        var result = await RunAsync(wmi);

        Assert.NotEmpty(result.Measurements);
        Assert.All(result.Measurements, m =>
        {
            Assert.DoesNotContain("serial", m.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("mac", m.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uuid", m.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string? Text(ProbeResult result, string name) =>
        (result.Measurements.FirstOrDefault(m => m.Name == name)?.Value as TextValue)?.Value;

    private static long? Integer(ProbeResult result, string name) =>
        (result.Measurements.FirstOrDefault(m => m.Name == name)?.Value as IntegerValue)?.Value;
}
