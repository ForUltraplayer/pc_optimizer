/**
 * @file    : MemoryProbeTests.cs
 * @author  : rudals252
 * @brief   : 메모리 프로브가 비식별 WMI fixture(정상·빈 결과·클래스 없음·값 누락·SMBIOS 버전 누락)를 측정값/상태/Issue로 바꾸는지 검증
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
/// <see cref="MemoryProbe"/>의 수집 결과 변환을 fixture로 검증합니다. 실제 WMI를 호출하지 않습니다.
/// </summary>
public sealed class MemoryProbeTests
{
    private const uint FIXTURE_SPEED = 3200;
    private const ulong FIXTURE_CAPACITY = 8_589_934_592;

    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 비식별 모듈 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> ModuleRow(uint? speed, uint? configured)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DeviceLocator"] = "DIMM X",
            ["BankLabel"] = "BANK X",
            ["Manufacturer"] = "Vendor",
            ["PartNumber"] = "PART-0000  ",
            ["Capacity"] = FIXTURE_CAPACITY,
            ["Speed"] = speed,
            ["ConfiguredClockSpeed"] = configured,
        };
    }

    /// <summary>
    /// SMBIOS 버전 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> BiosRow(ushort major, ushort minor)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SMBIOSMajorVersion"] = major,
            ["SMBIOSMinorVersion"] = minor,
        };
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeWmiClient wmi)
    {
        return new MemoryProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>정상 fixture는 Success와 모듈별 원시 값·단위·Reported 품질을 남긴다.</summary>
    [Fact]
    public async Task 정상_fixture는_원시_값을_보존한다()
    {
        var wmi = new FakeWmiClient()
            .WithRows(MemoryProbe.PHYSICAL_MEMORY_CLASS, ModuleRow(FIXTURE_SPEED, FIXTURE_SPEED), ModuleRow(FIXTURE_SPEED, FIXTURE_SPEED))
            .WithRows(MemoryProbe.BIOS_CLASS, BiosRow(3, 4));

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Empty(result.Issues);
        var speed = Assert.Single(result.Measurements, m => m.Name == MemoryProbeContract.ModuleMeasurementName(1, MemoryProbeContract.FIELD_SPEED));
        Assert.Equal(new IntegerValue(FIXTURE_SPEED), speed.Value);
        Assert.Equal(MemoryProbeContract.UNIT_MEGATRANSFERS, speed.Unit);
        Assert.Equal(MeasurementQuality.Reported, speed.Quality);
        var partNumber = Assert.Single(result.Measurements, m => m.Name == MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_PART_NUMBER));
        Assert.Equal(new TextValue("PART-0000"), partNumber.Value);
        Assert.Contains(result.Measurements, m => m.Name == MemoryProbeContract.MODULE_COUNT && m.Value == new IntegerValue(2));
    }

    /// <summary>SMBIOS 3 이전이면 단위는 MHz다.</summary>
    [Fact]
    public async Task 이전_SMBIOS는_MHz_단위다()
    {
        var wmi = new FakeWmiClient()
            .WithRows(MemoryProbe.PHYSICAL_MEMORY_CLASS, ModuleRow(FIXTURE_SPEED, FIXTURE_SPEED))
            .WithRows(MemoryProbe.BIOS_CLASS, BiosRow(2, 8));

        var result = await RunAsync(wmi);

        var speed = Assert.Single(result.Measurements, m => m.Name == MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_SPEED));
        Assert.Equal(MemoryProbeContract.UNIT_MEGAHERTZ, speed.Unit);
    }

    /// <summary>모듈 클래스를 쓸 수 없으면 Failed + Unsupported이며 빈 성공이 아니다.</summary>
    [Fact]
    public async Task 클래스가_없으면_실패다()
    {
        var result = await RunAsync(new FakeWmiClient());

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(CannotVerifyReason.Unsupported, Assert.Single(result.Issues).Reason);
    }

    /// <summary>조회 권한이 없거나 타임아웃이면 그 사유로 Failed.</summary>
    [Theory]
    [InlineData(WmiQueryStatus.AccessDenied, CannotVerifyReason.AccessDenied)]
    [InlineData(WmiQueryStatus.Timeout, CannotVerifyReason.Timeout)]
    [InlineData(WmiQueryStatus.Error, CannotVerifyReason.ProbeError)]
    public async Task 조회_실패는_사유와_함께_실패다(WmiQueryStatus status, CannotVerifyReason reason)
    {
        var result = await RunAsync(new FakeWmiClient().WithFailure(MemoryProbe.PHYSICAL_MEMORY_CLASS, status));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(reason, Assert.Single(result.Issues).Reason);
    }

    /// <summary>모듈이 하나도 보고되지 않으면 빈 성공이 아니라 Failed.</summary>
    [Fact]
    public async Task 빈_결과는_실패다()
    {
        var wmi = new FakeWmiClient().WithRows(MemoryProbe.PHYSICAL_MEMORY_CLASS).WithRows(MemoryProbe.BIOS_CLASS, BiosRow(3, 4));

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.NotEmpty(result.Issues);
    }

    /// <summary>속도 값이 비어 있으면 Partial + PartialData이고, 0은 원시 값 그대로 남긴다.</summary>
    [Fact]
    public async Task 속도_값_누락은_부분_수집이다()
    {
        var wmi = new FakeWmiClient()
            .WithRows(MemoryProbe.PHYSICAL_MEMORY_CLASS, ModuleRow(FIXTURE_SPEED, null), ModuleRow(0, 0))
            .WithRows(MemoryProbe.BIOS_CLASS, BiosRow(3, 4));

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.PartialData, Assert.Single(result.Issues).Reason);
        Assert.DoesNotContain(result.Measurements, m => m.Name == MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED));
        Assert.Contains(
            result.Measurements,
            m => m.Name == MemoryProbeContract.ModuleMeasurementName(1, MemoryProbeContract.FIELD_SPEED) && m.Value == new IntegerValue(0));
    }

    /// <summary>SMBIOS 버전을 못 읽으면 단위 없이(null) 기록하고 Partial.</summary>
    [Fact]
    public async Task SMBIOS_버전_누락은_단위_없음과_부분_수집이다()
    {
        var wmi = new FakeWmiClient().WithRows(MemoryProbe.PHYSICAL_MEMORY_CLASS, ModuleRow(FIXTURE_SPEED, FIXTURE_SPEED));

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        var speed = Assert.Single(result.Measurements, m => m.Name == MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_SPEED));
        Assert.Null(speed.Unit);
    }
}
