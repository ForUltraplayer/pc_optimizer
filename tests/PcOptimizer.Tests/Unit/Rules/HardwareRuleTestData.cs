/**
 * @file    : HardwareRuleTestData.cs
 * @author  : rudals252
 * @brief   : 디스플레이·GPU·시스템 정보·그래픽 설정·보안·저장소 규칙 단위 테스트용 가짜 측정값/스냅샷 생성 도우미
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 활성 디스플레이 대상 하나입니다. null은 "측정값 없음"입니다. 숫자는 테스트용 예시이며 실제 PC 값이 아닙니다.
/// </summary>
public sealed record FakeDisplay(
    string? DevicePath,
    string? Name = "테스트 모니터",
    string? GdiName = @"\\.\DISPLAY1",
    string? AdapterName = "테스트 GPU",
    string? AdapterPath = @"\\?\PCI#VEN_10DE&DEV_0000#test#{guid}",
    long? Width = 2560,
    long? Height = 1440,
    long? RefreshHz = 60,
    long Orientation = 0,
    long BitsPerPixel = 32,
    bool Interlaced = false,
    double? SignalHz = null,
    int[]? SameModeRates = null,
    bool Cloned = false,
    long? OutputTechnology = 10,
    long TargetId = 1);

/// <summary>
/// 가짜 GPU 어댑터 하나입니다.
/// </summary>
public sealed record FakeAdapter(
    string? Name,
    string? DriverVersion,
    string? PnpDeviceId,
    string? DriverDate = "2026-01-02",
    string[]? HardwareIds = null);

/// <summary>
/// 가짜 볼륨 하나입니다.
/// </summary>
public sealed record FakeVolume(string? DriveLetter, long? Size, long? SizeRemaining, long DriveType = VolumeProbeContract.DRIVE_TYPE_FIXED, long HealthStatus = 0);

/// <summary>
/// 가짜 물리 디스크 하나입니다.
/// </summary>
public sealed record FakeDisk(
    string? ProviderDiskId,
    string? FriendlyName,
    long? HealthStatus,
    string DeviceId = "0",
    long MediaType = PhysicalDiskProbeContract.MEDIA_TYPE_SSD,
    long BusType = 17,
    long Size = 1_000_000_000_000);

/// <summary>
/// 하드웨어·저장소 규칙 테스트 데이터 도우미입니다.
/// </summary>
internal static class HardwareRuleTestData
{
    /// <summary>1 GiB(바이트).</summary>
    public const long GIB = 1024L * 1024 * 1024;

    /// <summary>
    /// 실수 측정값을 만든다.
    /// </summary>
    public static Measurement Decimal(string name, double value, string? unit = null)
    {
        return new Measurement(name, new DecimalValue(value), unit, "fake", RuleTestData.OBSERVED_AT, MeasurementQuality.Observed);
    }

    /// <summary>
    /// 불리언 측정값을 만든다.
    /// </summary>
    public static Measurement Boolean(string name, bool value)
    {
        return new Measurement(name, new BooleanValue(value), null, "fake", RuleTestData.OBSERVED_AT, MeasurementQuality.Observed);
    }

    /// <summary>
    /// 디스플레이 프로브 결과를 만든다.
    /// </summary>
    public static ProbeResult DisplayResult(bool? remote, params FakeDisplay[] displays)
    {
        var measurements = new List<Measurement> { RuleTestData.Integer(DisplayProbeContract.TARGET_COUNT, displays.Length) };
        if (remote is { } isRemote)
        {
            measurements.Add(Boolean(DisplayProbeContract.REMOTE_SESSION, isRemote));
        }

        for (var index = 0; index < displays.Length; index++)
        {
            var display = displays[index];
            string Name(string field) => DisplayProbeContract.TargetMeasurementName(index, field);

            AddText(measurements, Name(DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH), display.DevicePath);
            AddText(measurements, Name(DisplayProbeContract.FIELD_MONITOR_NAME), display.Name);
            AddText(measurements, Name(DisplayProbeContract.FIELD_GDI_DEVICE_NAME), display.GdiName);
            AddText(measurements, Name(DisplayProbeContract.FIELD_ADAPTER_NAME), display.AdapterName);
            AddText(measurements, Name(DisplayProbeContract.FIELD_ADAPTER_DEVICE_PATH), display.AdapterPath);
            measurements.Add(RuleTestData.Integer(Name(DisplayProbeContract.FIELD_TARGET_ID), display.TargetId));
            AddInteger(measurements, Name(DisplayProbeContract.FIELD_OUTPUT_TECHNOLOGY), display.OutputTechnology);
            AddInteger(measurements, Name(DisplayProbeContract.FIELD_WIDTH), display.Width, DisplayProbeContract.UNIT_PIXELS);
            AddInteger(measurements, Name(DisplayProbeContract.FIELD_HEIGHT), display.Height, DisplayProbeContract.UNIT_PIXELS);
            AddInteger(measurements, Name(DisplayProbeContract.FIELD_REFRESH_HZ), display.RefreshHz, DisplayProbeContract.UNIT_HZ);
            measurements.Add(RuleTestData.Integer(Name(DisplayProbeContract.FIELD_ORIENTATION), display.Orientation));
            measurements.Add(RuleTestData.Integer(Name(DisplayProbeContract.FIELD_BITS_PER_PIXEL), display.BitsPerPixel, DisplayProbeContract.UNIT_BITS_PER_PIXEL));
            measurements.Add(Boolean(Name(DisplayProbeContract.FIELD_INTERLACED), display.Interlaced));
            measurements.Add(Boolean(Name(DisplayProbeContract.FIELD_CLONED), display.Cloned));
            if (display.SignalHz is { } signal)
            {
                measurements.Add(Decimal(Name(DisplayProbeContract.FIELD_SIGNAL_REFRESH_HZ), signal, DisplayProbeContract.UNIT_HZ));
            }

            if (display.SameModeRates is { } rates)
            {
                measurements.Add(RuleTestData.TextList(
                    Name(DisplayProbeContract.FIELD_SAME_MODE_REFRESH_RATES),
                    [.. rates.Select(rate => rate.ToString(System.Globalization.CultureInfo.InvariantCulture))]));
                if (rates.Length > 0)
                {
                    measurements.Add(RuleTestData.Integer(Name(DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ), rates.Max(), DisplayProbeContract.UNIT_HZ));
                }
            }
        }

        return EngineTestData.CreateResult(DisplayProbeContract.PROBE_ID, ProbeStatus.Success, measurements);
    }

    /// <summary>
    /// 전원 프로브 결과(섀시·AC 상태만)를 만든다.
    /// </summary>
    public static ProbeResult PowerResult(string[]? chassisTypes, long? acLineStatus)
    {
        var measurements = new List<Measurement>();
        if (chassisTypes is not null)
        {
            measurements.Add(RuleTestData.TextList(PowerProbeContract.CHASSIS_TYPES, chassisTypes));
        }

        if (acLineStatus is { } ac)
        {
            measurements.Add(RuleTestData.Integer(PowerProbeContract.AC_LINE_STATUS, ac));
        }

        return EngineTestData.CreateResult(PowerProbeContract.PROBE_ID, ProbeStatus.Success, measurements);
    }

    /// <summary>
    /// GPU 프로브 결과를 만든다.
    /// </summary>
    public static ProbeResult GpuResult(params FakeAdapter[] adapters)
    {
        var measurements = new List<Measurement> { RuleTestData.Integer(GpuProbeContract.ADAPTER_COUNT, adapters.Length) };
        for (var index = 0; index < adapters.Length; index++)
        {
            var adapter = adapters[index];
            AddText(measurements, GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_NAME), adapter.Name);
            AddText(measurements, GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_DRIVER_VERSION), adapter.DriverVersion);
            AddText(measurements, GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_PNP_DEVICE_ID), adapter.PnpDeviceId);
            AddText(measurements, GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_DRIVER_DATE), adapter.DriverDate);
            if (adapter.HardwareIds is { } ids)
            {
                measurements.Add(RuleTestData.TextList(GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_HARDWARE_IDS), ids));
            }
        }

        return EngineTestData.CreateResult(GpuProbeContract.PROBE_ID, ProbeStatus.Success, measurements);
    }

    /// <summary>
    /// 볼륨 프로브 결과를 만든다.
    /// </summary>
    public static ProbeResult VolumeResult(bool? windowsOldExists, params FakeVolume[] volumes)
    {
        var measurements = new List<Measurement> { RuleTestData.Integer(VolumeProbeContract.VOLUME_COUNT, volumes.Length) };
        if (windowsOldExists is { } exists)
        {
            measurements.Add(RuleTestData.Text(VolumeProbeContract.SYSTEM_DRIVE, "C:"));
            measurements.Add(Boolean(VolumeProbeContract.WINDOWS_OLD_EXISTS, exists));
        }

        for (var index = 0; index < volumes.Length; index++)
        {
            var volume = volumes[index];
            AddText(measurements, VolumeProbeContract.VolumeMeasurementName(index, VolumeProbeContract.FIELD_DRIVE_LETTER), volume.DriveLetter);
            measurements.Add(RuleTestData.Integer(VolumeProbeContract.VolumeMeasurementName(index, VolumeProbeContract.FIELD_DRIVE_TYPE), volume.DriveType));
            measurements.Add(RuleTestData.Integer(VolumeProbeContract.VolumeMeasurementName(index, VolumeProbeContract.FIELD_HEALTH_STATUS), volume.HealthStatus));
            AddInteger(measurements, VolumeProbeContract.VolumeMeasurementName(index, VolumeProbeContract.FIELD_SIZE), volume.Size, VolumeProbeContract.UNIT_BYTES);
            AddInteger(
                measurements, VolumeProbeContract.VolumeMeasurementName(index, VolumeProbeContract.FIELD_SIZE_REMAINING), volume.SizeRemaining, VolumeProbeContract.UNIT_BYTES);
        }

        return EngineTestData.CreateResult(VolumeProbeContract.PROBE_ID, ProbeStatus.Success, measurements);
    }

    /// <summary>
    /// 물리 디스크 프로브 결과를 만든다.
    /// </summary>
    public static ProbeResult DiskResult(params FakeDisk[] disks)
    {
        var measurements = new List<Measurement> { RuleTestData.Integer(PhysicalDiskProbeContract.DISK_COUNT, disks.Length) };
        for (var index = 0; index < disks.Length; index++)
        {
            var disk = disks[index];
            string Name(string field) => PhysicalDiskProbeContract.DiskMeasurementName(index, field);

            AddText(measurements, Name(PhysicalDiskProbeContract.FIELD_PROVIDER_DISK_ID), disk.ProviderDiskId);
            AddText(measurements, Name(PhysicalDiskProbeContract.FIELD_FRIENDLY_NAME), disk.FriendlyName);
            AddText(measurements, Name(PhysicalDiskProbeContract.FIELD_DEVICE_ID), disk.DeviceId);
            AddInteger(measurements, Name(PhysicalDiskProbeContract.FIELD_HEALTH_STATUS), disk.HealthStatus);
            measurements.Add(RuleTestData.Integer(Name(PhysicalDiskProbeContract.FIELD_MEDIA_TYPE), disk.MediaType));
            measurements.Add(RuleTestData.Integer(Name(PhysicalDiskProbeContract.FIELD_BUS_TYPE), disk.BusType));
            measurements.Add(RuleTestData.Integer(Name(PhysicalDiskProbeContract.FIELD_SIZE), disk.Size, PhysicalDiskProbeContract.UNIT_BYTES));
        }

        return EngineTestData.CreateResult(PhysicalDiskProbeContract.PROBE_ID, ProbeStatus.Success, measurements);
    }

    /// <summary>
    /// 결과 목록으로 스냅샷을 만든다.
    /// </summary>
    public static ScanSnapshot Snapshot(params ProbeResult[] results)
    {
        return EngineTestData.CreateSnapshot(results);
    }

    /// <summary>
    /// 값이 있으면 문자열 측정값을 추가한다.
    /// </summary>
    private static void AddText(List<Measurement> measurements, string name, string? value)
    {
        if (value is not null)
        {
            measurements.Add(RuleTestData.Text(name, value));
        }
    }

    /// <summary>
    /// 값이 있으면 정수 측정값을 추가한다.
    /// </summary>
    private static void AddInteger(List<Measurement> measurements, string name, long? value, string? unit = null)
    {
        if (value is { } number)
        {
            measurements.Add(RuleTestData.Integer(name, number, unit));
        }
    }
}
