/**
 * @file    : PcSpecService.Sections.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 스냅샷의 9개 섹션 빌더(운영체제·CPU·메모리·그래픽·모니터·저장장치·메인보드·BIOS·네트워크·전원). 실행·조립·값 해석·형식 도우미는 PcSpecService.cs
 */

// 사용자 패키지
using PcOptimizer.App.Models;
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.Services;

/// <summary>
/// <see cref="PcSpecService"/>의 섹션 빌더 부분입니다. 장치마다 한 줄(라벨: 값)로 나열하며, 값을 알 수 없는 부분은 빼고 전부 없으면 null로 둡니다.
/// </summary>
public sealed partial class PcSpecService
{
    /// <summary>
    /// 운영체제 섹션: 이름, 버전(빌드), 설치일.
    /// </summary>
    private static PcSpecItem[] Os(ProbeReader details)
    {
        var version = details.Text(SystemDetailsProbeContract.OS_VERSION);
        var build = details.Text(SystemDetailsProbeContract.OS_BUILD);
        var versionText = (version, build) switch
        {
            ({ } v, { } b) => Format(Strings.Spec_OsVersionValue, v, b),
            ({ } v, null) => v,
            (null, { } b) => Format(Strings.Spec_OsBuildOnly, b),
            _ => null,
        };

        return
        [
            new(Strings.Spec_OsName, details.Text(SystemDetailsProbeContract.OS_CAPTION)),
            new(Strings.Spec_OsVersion, versionText),
            new(Strings.Spec_OsInstallDate, DateText(details.Text(SystemDetailsProbeContract.OS_INSTALL_DATE))),
        ];
    }

    /// <summary>
    /// CPU 섹션: 모델, 코어/스레드, 최대 클럭.
    /// </summary>
    private static PcSpecItem[] Cpu(ProbeReader details)
    {
        var cores = Positive(details.Integer(SystemDetailsProbeContract.CPU_CORES));
        var threads = Positive(details.Integer(SystemDetailsProbeContract.CPU_THREADS));
        var coreText = (cores, threads) switch
        {
            ({ } c, { } t) => Format(Strings.Spec_CpuCoresValue, c, t),
            ({ } c, null) => Format(Strings.Spec_CpuCoresOnly, c),
            (null, { } t) => Format(Strings.Spec_CpuThreadsOnly, t),
            _ => null,
        };
        var clock = Positive(details.Integer(SystemDetailsProbeContract.CPU_MAX_CLOCK_MHZ));

        return
        [
            new(Strings.Spec_CpuName, details.Text(SystemDetailsProbeContract.CPU_NAME)),
            new(Strings.Spec_CpuCores, coreText),
            new(Strings.Spec_CpuClock, clock is { } mhz ? Format(Strings.Spec_CpuClockValue, mhz) : null),
        ];
    }

    /// <summary>
    /// 메모리 섹션: 총량 줄(합계 용량·최저 설정 속도) + 모듈마다 한 줄(위치 라벨, 용량·속도·제조사·파트 번호).
    /// </summary>
    private static PcSpecItem[] Memory(ProbeReader memory)
    {
        var count = Positive(memory.Integer(MemoryProbeContract.MODULE_COUNT));
        if (count is not { } moduleCount)
        {
            return [new(Strings.Spec_MemoryTotal, null)];
        }

        var modules = new List<PcSpecItem>();
        var capacities = new List<long?>();
        var speeds = new List<(long Value, string Unit)>();
        for (var index = 0; index < moduleCount; index++)
        {
            string Name(string field) => MemoryProbeContract.ModuleMeasurementName(index, field);
            var locator = memory.Text(Name(MemoryProbeContract.FIELD_DEVICE_LOCATOR));
            var capacity = PositiveBytes(memory.Integer(Name(MemoryProbeContract.FIELD_CAPACITY)));
            var speed = MemorySpeed(memory, Name(MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED))
                ?? MemorySpeed(memory, Name(MemoryProbeContract.FIELD_SPEED));
            capacities.Add(capacity);
            if (speed is { } reported)
            {
                speeds.Add(reported);
            }

            var label = locator is null
                ? Format(Strings.Spec_MemoryModule, index + FIRST_ORDINAL)
                : Format(Strings.Spec_MemorySlot, locator);
            modules.Add(new(
                label,
                Join(
                    capacity is { } bytes ? MemorySize(bytes) : null,
                    speed is { } s ? Format(Strings.Spec_Speed, s.Value, s.Unit) : null,
                    memory.Text(Name(MemoryProbeContract.FIELD_MANUFACTURER)),
                    memory.Text(Name(MemoryProbeContract.FIELD_PART_NUMBER)))));
        }

        var total = capacities.TrueForAll(c => c is not null) ? capacities.Sum(c => c!.Value) : (long?)null;
        var totalSpeed = speeds.Count > 0 && speeds.TrueForAll(s => s.Unit == speeds[0].Unit)
            ? Format(Strings.Spec_Speed, speeds.Min(s => s.Value), speeds[0].Unit)
            : null;
        return [new(Strings.Spec_MemoryTotal, Join(total is { } sum ? MemorySize(sum) : null, totalSpeed)), .. modules];
    }

    /// <summary>
    /// 그래픽 섹션: 어댑터마다 한 줄(이름·드라이버 버전·날짜). 가상 어댑터도 제외하지 않는다.
    /// </summary>
    private static PcSpecItem[] Gpu(ProbeReader gpu)
    {
        var count = NonNegative(gpu.Integer(GpuProbeContract.ADAPTER_COUNT));
        if (count is not { } adapterCount)
        {
            return [new(Strings.Spec_GpuGeneric, null)];
        }

        if (adapterCount == 0)
        {
            return [new(Strings.Spec_GpuGeneric, Strings.Spec_ValueNone)];
        }

        var items = new PcSpecItem[adapterCount];
        for (var index = 0; index < adapterCount; index++)
        {
            string Name(string field) => GpuProbeContract.AdapterMeasurementName(index, field);
            var version = gpu.Text(Name(GpuProbeContract.FIELD_DRIVER_VERSION));
            var date = DateText(gpu.Text(Name(GpuProbeContract.FIELD_DRIVER_DATE)));
            var driver = (version, date) switch
            {
                ({ } v, { } d) => Format(Strings.Spec_DriverWithDate, v, d),
                ({ } v, null) => Format(Strings.Spec_Driver, v),
                _ => null,
            };
            items[index] = new(Format(Strings.Spec_GpuAdapter, index + FIRST_ORDINAL), Join(gpu.Text(Name(GpuProbeContract.FIELD_NAME)), driver));
        }

        return items;
    }

    /// <summary>
    /// 모니터 섹션: 활성 대상마다 한 줄(모니터 이름·해상도·주사율). 이름이 없으면 라벨("모니터 n")이 이미 구분하므로 이름 자리를 비운다.
    /// </summary>
    private static PcSpecItem[] Displays(ProbeReader display)
    {
        var count = NonNegative(display.Integer(DisplayProbeContract.TARGET_COUNT));
        if (count is not { } targetCount)
        {
            return [new(Strings.Spec_DisplayGeneric, null)];
        }

        if (targetCount == 0)
        {
            return [new(Strings.Spec_DisplayGeneric, Strings.Spec_ValueNone)];
        }

        var items = new PcSpecItem[targetCount];
        for (var index = 0; index < targetCount; index++)
        {
            string Name(string field) => DisplayProbeContract.TargetMeasurementName(index, field);
            var width = Positive(display.Integer(Name(DisplayProbeContract.FIELD_WIDTH)));
            var height = Positive(display.Integer(Name(DisplayProbeContract.FIELD_HEIGHT)));
            var refresh = Positive(display.Integer(Name(DisplayProbeContract.FIELD_REFRESH_HZ)));
            var mode = (width, height, refresh) switch
            {
                ({ } w, { } h, { } hz) => Format(Strings.Spec_DisplayMode, w, h, hz),
                ({ } w, { } h, null) => Format(Strings.Spec_DisplayResolution, w, h),
                (_, _, { } hz) => Format(Strings.Spec_DisplayRefresh, hz),
                _ => null,
            };
            items[index] = new(Format(Strings.Spec_Display, index + FIRST_ORDINAL), Join(display.Text(Name(DisplayProbeContract.FIELD_MONITOR_NAME)), mode));
        }

        return items;
    }

    /// <summary>
    /// 저장장치 섹션의 물리 디스크 부분: 디스크마다 한 줄(라벨 SSD/HDD/…, 값 모델명·용량·인터페이스·상태).
    /// </summary>
    private static PcSpecItem[] Disks(ProbeReader disks)
    {
        var count = NonNegative(disks.Integer(PhysicalDiskProbeContract.DISK_COUNT));
        if (count is not { } diskCount)
        {
            return [new(Strings.Spec_Disk, null)];
        }

        if (diskCount == 0)
        {
            return [new(Strings.Spec_Disk, Strings.Spec_ValueNone)];
        }

        var items = new PcSpecItem[diskCount];
        for (var index = 0; index < diskCount; index++)
        {
            string Name(string field) => PhysicalDiskProbeContract.DiskMeasurementName(index, field);
            var size = PositiveBytes(disks.Integer(Name(PhysicalDiskProbeContract.FIELD_SIZE)));
            items[index] = new(
                DiskLabel(disks.Integer(Name(PhysicalDiskProbeContract.FIELD_MEDIA_TYPE))),
                Join(
                    disks.Text(Name(PhysicalDiskProbeContract.FIELD_FRIENDLY_NAME)),
                    size is { } bytes ? StorageSize(bytes) : null,
                    BusName(disks.Integer(Name(PhysicalDiskProbeContract.FIELD_BUS_TYPE))),
                    HealthName(disks.Integer(Name(PhysicalDiskProbeContract.FIELD_HEALTH_STATUS)))));
        }

        return items;
    }

    /// <summary>
    /// 저장장치 섹션의 볼륨 부분: 볼륨마다 한 줄(라벨 드라이브 문자, 값 볼륨 이름·전체/남은 용량·파일 시스템).
    /// </summary>
    private static PcSpecItem[] Volumes(ProbeReader volumes)
    {
        var count = NonNegative(volumes.Integer(VolumeProbeContract.VOLUME_COUNT));
        if (count is not { } volumeCount)
        {
            return [new(Strings.Spec_VolumeGeneric, null)];
        }

        if (volumeCount == 0)
        {
            return [new(Strings.Spec_VolumeGeneric, Strings.Spec_ValueNone)];
        }

        var items = new PcSpecItem[volumeCount];
        var unlettered = 0;
        for (var index = 0; index < volumeCount; index++)
        {
            string Name(string field) => VolumeProbeContract.VolumeMeasurementName(index, field);
            var letter = volumes.Text(Name(VolumeProbeContract.FIELD_DRIVE_LETTER));
            var label = letter is null
                ? Format(Strings.Spec_VolumeNoLetter, ++unlettered)
                : Format(Strings.Spec_VolumeLabel, letter);
            var size = PositiveBytes(volumes.Integer(Name(VolumeProbeContract.FIELD_SIZE)));
            var remaining = NonNegativeBytes(volumes.Integer(Name(VolumeProbeContract.FIELD_SIZE_REMAINING)));
            var capacity = (size, remaining) switch
            {
                ({ } total, { } free) => Format(Strings.Spec_Volume, StorageNumber(total), StorageNumber(free)),
                ({ } total, null) => StorageSize(total),
                (null, { } free) => Format(Strings.Spec_VolumeRemainingOnly, StorageNumber(free)),
                _ => null,
            };
            items[index] = new(
                label,
                Join(capacity, volumes.Text(Name(VolumeProbeContract.FIELD_FILE_SYSTEM))),
                IdentifyingValue: Join(
                    volumes.Text(Name(VolumeProbeContract.FIELD_LABEL)),
                    capacity,
                    volumes.Text(Name(VolumeProbeContract.FIELD_FILE_SYSTEM))));
        }

        return items;
    }

    /// <summary>
    /// 메인보드·BIOS 섹션: 제조사, 모델(없으면 시스템 모델로 대체 표기), 리비전, BIOS 버전(배포일).
    /// </summary>
    private static PcSpecItem[] Board(ProbeReader details, ProbeReader systemInfo)
    {
        var maker = details.Text(SystemDetailsProbeContract.BOARD_MANUFACTURER)
            ?? WithMarker(systemInfo.Text(SystemInfoProbeContract.MANUFACTURER), Strings.Spec_BoardMakerFromSystem);
        var model = details.Text(SystemDetailsProbeContract.BOARD_PRODUCT)
            ?? WithMarker(systemInfo.Text(SystemInfoProbeContract.MODEL), Strings.Spec_BoardFromSystem);
        var biosVersion = systemInfo.Text(SystemInfoProbeContract.BIOS_VERSION);
        var biosDate = DateText(details.Text(SystemDetailsProbeContract.BIOS_RELEASE_DATE));
        var bios = (biosVersion, biosDate) switch
        {
            ({ } v, { } d) => Format(Strings.Spec_BiosVersionValue, v, d),
            ({ } v, null) => v,
            (null, { } d) => d,
            _ => null,
        };

        return
        [
            new(Strings.Spec_BoardMaker, maker),
            new(Strings.Spec_BoardModel, model),
            new(Strings.Spec_BoardVersion, details.Text(SystemDetailsProbeContract.BOARD_VERSION)),
            new(Strings.Spec_BiosVersion, bios),
        ];
    }

    /// <summary>
    /// 네트워크 섹션: 물리 어댑터마다 한 줄(이름·드라이버 버전, 버전이 없으면 "확인 불가").
    /// </summary>
    private static PcSpecItem[] Network(ProbeReader details)
    {
        var count = NonNegative(details.Integer(SystemDetailsProbeContract.NIC_COUNT));
        if (count is not { } adapterCount)
        {
            return [new(Strings.Spec_NicGeneric, null)];
        }

        if (adapterCount == 0)
        {
            return [new(Strings.Spec_NicGeneric, Strings.Spec_ValueNone)];
        }

        var items = new PcSpecItem[adapterCount];
        for (var index = 0; index < adapterCount; index++)
        {
            string Name(string field) => SystemDetailsProbeContract.AdapterMeasurementName(index, field);
            var name = details.Text(Name(SystemDetailsProbeContract.FIELD_NAME));
            var version = details.Text(Name(SystemDetailsProbeContract.FIELD_DRIVER_VERSION));
            var value = name is null && version is null
                ? null
                : Format(Strings.Spec_Nic, name ?? Strings.Spec_ValueUnknown, version ?? Strings.Spec_ValueUnknown);
            items[index] = new(Format(Strings.Spec_NicLabel, index + FIRST_ORDINAL), value);
        }

        return items;
    }

    /// <summary>
    /// 전원 섹션: 형태(노트북형/데스크톱형, 전원 프로브 섀시 코드가 없으면 시스템 정보 섀시 코드), 전원 공급 상태, 활성 전원 계획.
    /// </summary>
    private static PcSpecItem[] Power(ProbeReader power, ProbeReader systemInfo)
    {
        var chassis = ChassisClassifier.Classify(power.Measurement(PowerProbeContract.CHASSIS_TYPES));
        if (chassis == ChassisKind.Unknown)
        {
            chassis = ChassisClassifier.Classify(systemInfo.Measurement(SystemInfoProbeContract.CHASSIS_TYPES));
        }

        var supply = PowerSupplyClassifier.Classify(power.Integer(PowerProbeContract.AC_LINE_STATUS));
        return
        [
            new(Strings.Spec_PowerKind, chassis == ChassisKind.Unknown ? null : ChassisClassifier.DisplayName(chassis)),
            new(Strings.Spec_PowerSource, supply == PowerSupplyKind.Unknown ? null : PowerSupplyClassifier.DisplayName(supply)),
            new(Strings.Spec_PowerPlan, power.Text(PowerProbeContract.ACTIVE_SCHEME_NAME)),
        ];
    }
}
