/**
 * @file    : PcSpecTests.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 스냅샷(섹션 순서·확인 불가 표시·장치별 개별 나열·빈 값 비표시)과 프로브 예외·타임아웃 흡수를 가짜 프로브 결과로 검증
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="PcSpecService"/>의 스냅샷 구성을 검증합니다. 실제 OS 조회는 하지 않습니다.
/// </summary>
public sealed class PcSpecTests
{
    private const string SOURCE = "test";
    private const string SIZE_FORMAT = "0.0";
    private const long BYTES_PER_GIB = 1024L * 1024 * 1024;
    private const long TIB_IN_GIB = 1024;
    private const long NVME_BUS = 17;
    private const long SATA_BUS = 11;
    private const int SECTION_CPU = 1;
    private const int SECTION_MEMORY = 2;
    private const int SECTION_GPU = 3;
    private const int SECTION_DISPLAY = 4;
    private const int SECTION_STORAGE = 5;
    private const int SECTION_BOARD = 6;
    private const int SECTION_NETWORK = 7;

    private static readonly DateTimeOffset AT = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    /// <summary>프로브 결과에서 9개 섹션을 순서대로 만들고, 없는 프로브는 확인 불가 섹션으로 표시한다.</summary>
    [Fact]
    public void BuildsSectionsInOrderAndMarksUnavailable()
    {
        var at = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        var results = new Dictionary<string, ProbeResult>
        {
            [SystemDetailsProbeContract.PROBE_ID] = Result(SystemDetailsProbeContract.PROBE_ID,
                (SystemDetailsProbeContract.OS_CAPTION, new TextValue("Windows 11 Pro")),
                (SystemDetailsProbeContract.CPU_NAME, new TextValue("Ryzen 7")),
                (SystemDetailsProbeContract.CPU_CORES, new IntegerValue(8)),
                (SystemDetailsProbeContract.CPU_THREADS, new IntegerValue(16))),
            [MemoryProbeContract.PROBE_ID] = Result(MemoryProbeContract.PROBE_ID,
                (MemoryProbeContract.MODULE_COUNT, new IntegerValue(1)),
                (MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_CAPACITY), new IntegerValue(25_769_803_776)),
                (MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), new IntegerValue(6000))),
        };

        var snapshot = PcSpecService.BuildSnapshot(results, at);

        Assert.Equal(["운영체제", "CPU", "메모리", "그래픽", "모니터", "저장장치", "메인보드·BIOS", "네트워크", "전원"], snapshot.Sections.Select(s => s.Title));
        Assert.Contains(snapshot.Sections[1].Items, i => i.Label == Strings.Spec_CpuCores && i.Value == "8코어 / 16스레드");
        Assert.Contains(snapshot.Sections[2].Items, i => i.Value == "24 GB · 6000 MT/s");
        Assert.Contains("그래픽", snapshot.UnavailableSections);
        Assert.All(snapshot.Sections[3].Items, i => Assert.Null(i.Value));
    }

    /// <summary>물리 디스크는 매체 종류로 SSD/HDD 라벨을 달고, 값에 모델명·용량·인터페이스·상태를 담아 각각 한 줄로 나열한다.</summary>
    [Fact]
    public void DisksAreLabeledSsdOrHddByMediaTypeWithModelName()
    {
        string Disk(int index, string field) => PhysicalDiskProbeContract.DiskMeasurementName(index, field);
        var results = new Dictionary<string, ProbeResult>
        {
            [PhysicalDiskProbeContract.PROBE_ID] = Result(PhysicalDiskProbeContract.PROBE_ID,
                (PhysicalDiskProbeContract.DISK_COUNT, new IntegerValue(2)),
                (Disk(0, PhysicalDiskProbeContract.FIELD_FRIENDLY_NAME), new TextValue("Samsung SSD 990 PRO 2TB")),
                (Disk(0, PhysicalDiskProbeContract.FIELD_MEDIA_TYPE), new IntegerValue(PhysicalDiskProbeContract.MEDIA_TYPE_SSD)),
                (Disk(0, PhysicalDiskProbeContract.FIELD_BUS_TYPE), new IntegerValue(NVME_BUS)),
                (Disk(0, PhysicalDiskProbeContract.FIELD_SIZE), new IntegerValue(TIB_IN_GIB * BYTES_PER_GIB)),
                (Disk(0, PhysicalDiskProbeContract.FIELD_HEALTH_STATUS), new IntegerValue(PhysicalDiskProbeContract.HEALTH_HEALTHY)),
                (Disk(1, PhysicalDiskProbeContract.FIELD_FRIENDLY_NAME), new TextValue("WDC WD40EZAZ")),
                (Disk(1, PhysicalDiskProbeContract.FIELD_MEDIA_TYPE), new IntegerValue(PhysicalDiskProbeContract.MEDIA_TYPE_HDD)),
                (Disk(1, PhysicalDiskProbeContract.FIELD_BUS_TYPE), new IntegerValue(SATA_BUS))),
        };

        var snapshot = PcSpecService.BuildSnapshot(results, AT);
        var storage = snapshot.Sections[SECTION_STORAGE];

        var ssd = Assert.Single(storage.Items, i => i.Label == Strings.Spec_LabelSsd);
        var size = ((double)TIB_IN_GIB).ToString(SIZE_FORMAT, CultureInfo.CurrentCulture);
        Assert.Equal($"Samsung SSD 990 PRO 2TB · {size} GB · NVMe · {Strings.Spec_DiskHealthy}", ssd.Value);
        var hdd = Assert.Single(storage.Items, i => i.Label == Strings.Spec_LabelHdd);
        Assert.Equal("WDC WD40EZAZ · SATA", hdd.Value);
        Assert.Equal("SSD", Strings.Spec_LabelSsd);
        Assert.Equal("HDD", Strings.Spec_LabelHdd);
        Assert.DoesNotContain(Strings.Spec_Section_Storage, snapshot.UnavailableSections);
    }

    /// <summary>모니터 두 대는 요약하지 않고 이름·해상도·주사율을 각각 한 줄로 나열한다.</summary>
    [Fact]
    public void TwoMonitorsAreListedSeparately()
    {
        string Target(int index, string field) => DisplayProbeContract.TargetMeasurementName(index, field);
        var results = new Dictionary<string, ProbeResult>
        {
            [DisplayProbeContract.PROBE_ID] = Result(DisplayProbeContract.PROBE_ID,
                (DisplayProbeContract.TARGET_COUNT, new IntegerValue(2)),
                (Target(0, DisplayProbeContract.FIELD_MONITOR_NAME), new TextValue("DELL U2723QE")),
                (Target(0, DisplayProbeContract.FIELD_WIDTH), new IntegerValue(3840)),
                (Target(0, DisplayProbeContract.FIELD_HEIGHT), new IntegerValue(2160)),
                (Target(0, DisplayProbeContract.FIELD_REFRESH_HZ), new IntegerValue(60)),
                (Target(1, DisplayProbeContract.FIELD_MONITOR_NAME), new TextValue("LG ULTRAGEAR")),
                (Target(1, DisplayProbeContract.FIELD_WIDTH), new IntegerValue(2560)),
                (Target(1, DisplayProbeContract.FIELD_HEIGHT), new IntegerValue(1440)),
                (Target(1, DisplayProbeContract.FIELD_REFRESH_HZ), new IntegerValue(144))),
        };

        var display = PcSpecService.BuildSnapshot(results, AT).Sections[SECTION_DISPLAY];

        Assert.Equal(2, display.Items.Count);
        Assert.Equal(Fmt(Strings.Spec_Display, 1), display.Items[0].Label);
        Assert.Equal("DELL U2723QE · 3840x2160 @ 60Hz", display.Items[0].Value);
        Assert.Equal(Fmt(Strings.Spec_Display, 2), display.Items[1].Label);
        Assert.Equal("LG ULTRAGEAR · 2560x1440 @ 144Hz", display.Items[1].Value);
    }

    /// <summary>메인보드 제품명이 없으면 시스템 모델로 대체하고 대체 표기를 붙이며, 있으면 제품명을 그대로 쓴다.</summary>
    [Fact]
    public void BoardFallsBackToSystemModelWithMarker()
    {
        var systemInfo = Result(SystemInfoProbeContract.PROBE_ID,
            (SystemInfoProbeContract.MANUFACTURER, new TextValue("SAMSUNG ELECTRONICS")),
            (SystemInfoProbeContract.MODEL, new TextValue("Galaxy Book4")),
            (SystemInfoProbeContract.BIOS_VERSION, new TextValue("P08RGF")));
        var withoutBoard = new Dictionary<string, ProbeResult>
        {
            [SystemInfoProbeContract.PROBE_ID] = systemInfo,
            [SystemDetailsProbeContract.PROBE_ID] = Result(SystemDetailsProbeContract.PROBE_ID,
                (SystemDetailsProbeContract.BIOS_RELEASE_DATE, new TextValue("2024-03-15"))),
        };

        var board = PcSpecService.BuildSnapshot(withoutBoard, AT).Sections[SECTION_BOARD];

        var model = Assert.Single(board.Items, i => i.Label == Strings.Spec_BoardModel);
        Assert.Equal($"Galaxy Book4 {Strings.Spec_BoardFromSystem}", model.Value);
        Assert.Equal("(시스템 모델로 대체)", Strings.Spec_BoardFromSystem);
        Assert.Contains(board.Items, i => i.Label == Strings.Spec_BiosVersion && i.Value == "P08RGF (2024-03-15)");

        var withBoard = new Dictionary<string, ProbeResult>
        {
            [SystemInfoProbeContract.PROBE_ID] = systemInfo,
            [SystemDetailsProbeContract.PROBE_ID] = Result(SystemDetailsProbeContract.PROBE_ID,
                (SystemDetailsProbeContract.BOARD_MANUFACTURER, new TextValue("ASUSTeK COMPUTER INC.")),
                (SystemDetailsProbeContract.BOARD_PRODUCT, new TextValue("ROG STRIX B650E-F GAMING WIFI"))),
        };

        var direct = PcSpecService.BuildSnapshot(withBoard, AT).Sections[SECTION_BOARD];

        Assert.Contains(direct.Items, i => i.Label == Strings.Spec_BoardModel && i.Value == "ROG STRIX B650E-F GAMING WIFI");
        Assert.Contains(direct.Items, i => i.Label == Strings.Spec_BoardMaker && i.Value == "ASUSTeK COMPUTER INC.");
    }

    /// <summary>속도가 없는 메모리 모듈은 용량·제조사·파트 번호만 표시하고 속도 자리에 0이나 빈 단위를 넣지 않는다.</summary>
    [Fact]
    public void MemoryModuleWithoutSpeedShowsCapacityButNoSpeedText()
    {
        string Module(string field) => MemoryProbeContract.ModuleMeasurementName(0, field);
        var results = new Dictionary<string, ProbeResult>
        {
            [MemoryProbeContract.PROBE_ID] = Result(MemoryProbeContract.PROBE_ID,
                (MemoryProbeContract.MODULE_COUNT, new IntegerValue(1)),
                (Module(MemoryProbeContract.FIELD_DEVICE_LOCATOR), new TextValue("DIMM A1")),
                (Module(MemoryProbeContract.FIELD_CAPACITY), new IntegerValue(16 * BYTES_PER_GIB)),
                (Module(MemoryProbeContract.FIELD_MANUFACTURER), new TextValue("Samsung")),
                (Module(MemoryProbeContract.FIELD_PART_NUMBER), new TextValue("M425R2GA3BB0-CWMOD"))),
        };

        var memory = PcSpecService.BuildSnapshot(results, AT).Sections[SECTION_MEMORY];

        var module = Assert.Single(memory.Items, i => i.Label == Fmt(Strings.Spec_MemorySlot, "DIMM A1"));
        Assert.Equal("16 GB · Samsung · M425R2GA3BB0-CWMOD", module.Value);
        Assert.Contains(memory.Items, i => i.Label == Strings.Spec_MemoryTotal && i.Value == "16 GB");
        Assert.All(memory.Items, i => Assert.DoesNotContain("MT/s", i.Value!, StringComparison.Ordinal));
        Assert.All(memory.Items, i => Assert.DoesNotContain(" 0 ", i.Value!, StringComparison.Ordinal));
    }

    /// <summary>보고된 속도 단위(MHz)를 그대로 쓰고, 모듈이 여러 개면 모듈마다 한 줄씩 나열한다(총량 속도는 최솟값).</summary>
    [Fact]
    public void MemoryModulesAreListedIndividuallyWithReportedUnit()
    {
        string Module(int index, string field) => MemoryProbeContract.ModuleMeasurementName(index, field);
        var results = new Dictionary<string, ProbeResult>
        {
            [MemoryProbeContract.PROBE_ID] = Result(MemoryProbeContract.PROBE_ID, ProbeStatus.Success,
                M(MemoryProbeContract.MODULE_COUNT, new IntegerValue(2)),
                M(Module(0, MemoryProbeContract.FIELD_DEVICE_LOCATOR), new TextValue("ChannelA-DIMM0")),
                M(Module(0, MemoryProbeContract.FIELD_CAPACITY), new IntegerValue(8 * BYTES_PER_GIB)),
                M(Module(0, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), new IntegerValue(2400), MemoryProbeContract.UNIT_MEGAHERTZ),
                M(Module(1, MemoryProbeContract.FIELD_DEVICE_LOCATOR), new TextValue("ChannelB-DIMM0")),
                M(Module(1, MemoryProbeContract.FIELD_CAPACITY), new IntegerValue(8 * BYTES_PER_GIB)),
                M(Module(1, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), new IntegerValue(2133), MemoryProbeContract.UNIT_MEGAHERTZ)),
        };

        var memory = PcSpecService.BuildSnapshot(results, AT).Sections[SECTION_MEMORY];

        Assert.Equal(3, memory.Items.Count);
        Assert.Equal(Strings.Spec_MemoryTotal, memory.Items[0].Label);
        Assert.Equal("16 GB · 2133 MHz", memory.Items[0].Value);
        Assert.Equal(Fmt(Strings.Spec_MemorySlot, "ChannelA-DIMM0"), memory.Items[1].Label);
        Assert.Equal("8 GB · 2400 MHz", memory.Items[1].Value);
        Assert.Equal("8 GB · 2133 MHz", memory.Items[2].Value);
    }

    /// <summary>GPU 어댑터와 물리 네트워크 어댑터를 각각 이름으로 한 줄씩 나열한다.</summary>
    [Fact]
    public void GpuAndNetworkAdaptersAreListedByName()
    {
        string Gpu(int index, string field) => GpuProbeContract.AdapterMeasurementName(index, field);
        string Nic(int index, string field) => SystemDetailsProbeContract.AdapterMeasurementName(index, field);
        var results = new Dictionary<string, ProbeResult>
        {
            [GpuProbeContract.PROBE_ID] = Result(GpuProbeContract.PROBE_ID,
                (GpuProbeContract.ADAPTER_COUNT, new IntegerValue(2)),
                (Gpu(0, GpuProbeContract.FIELD_NAME), new TextValue("NVIDIA GeForce RTX 4070")),
                (Gpu(0, GpuProbeContract.FIELD_DRIVER_VERSION), new TextValue("32.0.15.6094")),
                (Gpu(0, GpuProbeContract.FIELD_DRIVER_DATE), new TextValue("2024-08-14")),
                (Gpu(1, GpuProbeContract.FIELD_NAME), new TextValue("AMD Radeon(TM) Graphics"))),
            [SystemDetailsProbeContract.PROBE_ID] = Result(SystemDetailsProbeContract.PROBE_ID,
                (SystemDetailsProbeContract.NIC_COUNT, new IntegerValue(2)),
                (Nic(0, SystemDetailsProbeContract.FIELD_NAME), new TextValue("Intel(R) Ethernet Controller I226-V")),
                (Nic(0, SystemDetailsProbeContract.FIELD_DRIVER_VERSION), new TextValue("2.1.4.3")),
                (Nic(1, SystemDetailsProbeContract.FIELD_NAME), new TextValue("Realtek Wi-Fi"))),
        };

        var snapshot = PcSpecService.BuildSnapshot(results, AT);
        var gpu = snapshot.Sections[SECTION_GPU];
        var network = snapshot.Sections[SECTION_NETWORK];

        Assert.Equal(2, gpu.Items.Count);
        Assert.Equal(Fmt(Strings.Spec_GpuAdapter, 1), gpu.Items[0].Label);
        Assert.Equal($"NVIDIA GeForce RTX 4070 · {Fmt(Strings.Spec_DriverWithDate, "32.0.15.6094", "2024-08-14")}", gpu.Items[0].Value);
        Assert.Equal(Fmt(Strings.Spec_GpuAdapter, 2), gpu.Items[1].Label);
        Assert.Equal("AMD Radeon(TM) Graphics", gpu.Items[1].Value);
        Assert.Equal(2, network.Items.Count);
        Assert.Equal($"Intel(R) Ethernet Controller I226-V · {Fmt(Strings.Spec_Driver, "2.1.4.3")}", network.Items[0].Value);
        Assert.Equal($"Realtek Wi-Fi · {Fmt(Strings.Spec_Driver, Strings.Spec_ValueUnknown)}", network.Items[1].Value);
    }

    /// <summary>WMI가 "알 수 없음"으로 보고한 0(메모리 용량·속도, 디스크 크기, 모니터 가로 해상도)은 "0"으로 표시하지 않고 확인 불가(null)로 둔다.</summary>
    [Fact]
    public void LiteralZeroMeasurementsRenderAsUnknownNotZero()
    {
        string Module(string field) => MemoryProbeContract.ModuleMeasurementName(0, field);
        var results = new Dictionary<string, ProbeResult>
        {
            [MemoryProbeContract.PROBE_ID] = Result(MemoryProbeContract.PROBE_ID,
                (MemoryProbeContract.MODULE_COUNT, new IntegerValue(1)),
                (Module(MemoryProbeContract.FIELD_CAPACITY), new IntegerValue(0)),
                (Module(MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), new IntegerValue(0)),
                (Module(MemoryProbeContract.FIELD_SPEED), new IntegerValue(0))),
            [PhysicalDiskProbeContract.PROBE_ID] = Result(PhysicalDiskProbeContract.PROBE_ID,
                (PhysicalDiskProbeContract.DISK_COUNT, new IntegerValue(1)),
                (PhysicalDiskProbeContract.DiskMeasurementName(0, PhysicalDiskProbeContract.FIELD_SIZE), new IntegerValue(0))),
            [DisplayProbeContract.PROBE_ID] = Result(DisplayProbeContract.PROBE_ID,
                (DisplayProbeContract.TARGET_COUNT, new IntegerValue(1)),
                (DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_WIDTH), new IntegerValue(0)),
                (DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_HEIGHT), new IntegerValue(1080))),
        };

        var snapshot = PcSpecService.BuildSnapshot(results, AT);

        Assert.All(snapshot.Sections[SECTION_MEMORY].Items, i => Assert.Null(i.Value));
        Assert.Equal(2, snapshot.Sections[SECTION_MEMORY].Items.Count);
        Assert.Null(Assert.Single(snapshot.Sections[SECTION_STORAGE].Items, i => i.Label == Strings.Spec_Disk).Value);
        Assert.Null(Assert.Single(snapshot.Sections[SECTION_DISPLAY].Items).Value);
        Assert.Contains(Strings.Spec_Section_Memory, snapshot.UnavailableSections);
        Assert.Contains(Strings.Spec_Section_Display, snapshot.UnavailableSections);
    }

    /// <summary>실패한 프로브 결과는 측정값이 있어도 섹션 전체를 확인 불가로 둔다.</summary>
    [Fact]
    public void FailedProbeMarksSectionUnavailable()
    {
        var results = new Dictionary<string, ProbeResult>
        {
            [SystemDetailsProbeContract.PROBE_ID] = Result(SystemDetailsProbeContract.PROBE_ID, ProbeStatus.Failed,
                M(SystemDetailsProbeContract.CPU_NAME, new TextValue("Ryzen 7"))),
        };

        var snapshot = PcSpecService.BuildSnapshot(results, AT);

        Assert.All(snapshot.Sections[SECTION_CPU].Items, i => Assert.Null(i.Value));
        Assert.Contains(Strings.Spec_Section_Cpu, snapshot.UnavailableSections);
        Assert.Equal(AT, snapshot.CapturedAtUtc);
    }

    /// <summary>CaptureAsync는 예외를 던지거나 멈춘 프로브를 형식 이름만 기록하고 해당 섹션을 확인 불가로 둔다.</summary>
    [Fact]
    public async Task CaptureAbsorbsThrowingAndHangingProbes()
    {
        var logger = new RecordingLogger();
        var context = new ScanContext(Guid.NewGuid(), EngineTestData.USER, false, FakeClock.DEFAULT_NOW);
        var service = new PcSpecService(
            [new ThrowingProbe(SystemDetailsProbeContract.PROBE_ID), new HangingProbe(MemoryProbeContract.PROBE_ID)],
            new FakeClock(),
            logger,
            () => context);

        var snapshot = await service.CaptureAsync(CancellationToken.None);

        Assert.Equal(FakeClock.DEFAULT_NOW, snapshot.CapturedAtUtc);
        Assert.Contains(Strings.Spec_Section_Os, snapshot.UnavailableSections);
        Assert.Contains(Strings.Spec_Section_Cpu, snapshot.UnavailableSections);
        Assert.Contains(Strings.Spec_Section_Memory, snapshot.UnavailableSections);
        Assert.Contains(logger.Entries, e => e.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Message.Contains(MemoryProbeContract.PROBE_ID, StringComparison.Ordinal));
        Assert.All(logger.Entries, e => Assert.Null(e.Exception));
        Assert.All(logger.Entries, e => Assert.DoesNotContain(ThrowingProbe.ERROR_MESSAGE, e.Message, StringComparison.Ordinal));
    }

    /// <summary>
    /// 측정값 튜플로 성공 프로브 결과를 만든다(출처 "test", 품질 Reported, 단위 없음).
    /// </summary>
    private static ProbeResult Result(string id, params (string Name, MeasurementValue Value)[] values)
    {
        return Result(id, ProbeStatus.Success, [.. values.Select(v => M(v.Name, v.Value))]);
    }

    /// <summary>
    /// 측정값 목록으로 지정 상태의 프로브 결과를 만든다.
    /// </summary>
    private static ProbeResult Result(string id, ProbeStatus status, params Measurement[] measurements)
    {
        return new ProbeResult(id, status, measurements, [], AT, TimeSpan.Zero, EngineTestData.USER);
    }

    /// <summary>
    /// 측정값 하나를 만든다.
    /// </summary>
    private static Measurement M(string name, MeasurementValue value, string? unit = null)
    {
        return new Measurement(name, value, unit, SOURCE, AT, MeasurementQuality.Reported);
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식을 채운다.
    /// </summary>
    private static string Fmt(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
