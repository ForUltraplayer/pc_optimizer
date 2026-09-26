/**
 * @file    : MemoryProbe.cs
 * @author  : rudals252
 * @brief   : Win32_PhysicalMemory 모듈별 위치·제조사·부품 번호·용량·보고 속도·설정 속도를 원시 값 그대로 수집하는 메모리 프로브
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 메모리 모듈 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="MemoryProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// 속도 값은 SMBIOS Type 17에서 온 원시 값이며 Quality는 Reported입니다.
/// WMI 스키마의 단위 한정자는 Speed를 "나노초"로, ConfiguredClockSpeed는 단위 없이 표기해 신뢰할 수 없으므로,
/// SMBIOS 버전(Win32_BIOS)으로 단위를 정합니다: 3.x 이상은 MT/s, 그 이전은 MHz, 버전을 모르면 단위 없음(null).
/// 조회 실패·클래스 없음·빈 결과는 Failed, 일부 값 누락은 Partial이며 빈 성공으로 돌려주지 않습니다.
/// </remarks>
public sealed class MemoryProbe : IProbe
{
    /// <summary>모듈 정보 WMI 클래스.</summary>
    public const string PHYSICAL_MEMORY_CLASS = "Win32_PhysicalMemory";

    /// <summary>SMBIOS 버전 WMI 클래스.</summary>
    public const string BIOS_CLASS = "Win32_BIOS";

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(10);

    private const string PROPERTY_DEVICE_LOCATOR = "DeviceLocator";
    private const string PROPERTY_BANK_LABEL = "BankLabel";
    private const string PROPERTY_MANUFACTURER = "Manufacturer";
    private const string PROPERTY_PART_NUMBER = "PartNumber";
    private const string PROPERTY_CAPACITY = "Capacity";
    private const string PROPERTY_SPEED = "Speed";
    private const string PROPERTY_CONFIGURED_CLOCK_SPEED = "ConfiguredClockSpeed";
    private const string PROPERTY_SMBIOS_MAJOR = "SMBIOSMajorVersion";
    private const string PROPERTY_SMBIOS_MINOR = "SMBIOSMinorVersion";
    private const long SMBIOS_MEGATRANSFERS_MAJOR_VERSION = 3;
    private const string SOURCE_PREFIX = "WMI " + PHYSICAL_MEMORY_CLASS + ".";
    private const string SMBIOS_VERSION_SOURCE = "WMI " + BIOS_CLASS + ".SMBIOSMajorVersion/SMBIOSMinorVersion";

    private static readonly string[] MODULE_PROPERTIES =
    [
        PROPERTY_DEVICE_LOCATOR,
        PROPERTY_BANK_LABEL,
        PROPERTY_MANUFACTURER,
        PROPERTY_PART_NUMBER,
        PROPERTY_CAPACITY,
        PROPERTY_SPEED,
        PROPERTY_CONFIGURED_CLOCK_SPEED,
    ];

    private static readonly string[] BIOS_PROPERTIES = [PROPERTY_SMBIOS_MAJOR, PROPERTY_SMBIOS_MINOR];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WMI와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public MemoryProbe()
        : this(WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// WMI 클라이언트와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public MemoryProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => MemoryProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Memory;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_LOCAL_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Collect(context, ct));
    }

    /// <summary>
    /// 모듈 정보를 조회해 측정값과 Issue를 만든다.
    /// </summary>
    private ProbeResult Collect(ScanContext context, CancellationToken ct)
    {
        var observedAt = _clock.UtcNow;
        var modules = _wmi.Query(PHYSICAL_MEMORY_CLASS, MODULE_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (modules.Status != WmiQueryStatus.Success)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(PHYSICAL_MEMORY_CLASS, modules)], observedAt);
        }

        if (modules.Rows.Count == 0)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.EmptyIssue(PHYSICAL_MEMORY_CLASS)], observedAt);
        }

        var measurements = new List<Measurement>();
        var issues = new List<Issue>();
        var unit = ReadSpeedUnit(measurements, issues, observedAt, ct);

        measurements.Add(new Measurement(
            MemoryProbeContract.MODULE_COUNT, new IntegerValue(modules.Rows.Count), null, SOURCE_PREFIX + "Count", observedAt, MeasurementQuality.Observed));

        var missingSpeed = false;
        for (var index = 0; index < modules.Rows.Count; index++)
        {
            var row = modules.Rows[index];
            AddText(measurements, index, MemoryProbeContract.FIELD_DEVICE_LOCATOR, row, PROPERTY_DEVICE_LOCATOR, observedAt);
            AddText(measurements, index, MemoryProbeContract.FIELD_BANK_LABEL, row, PROPERTY_BANK_LABEL, observedAt);
            AddText(measurements, index, MemoryProbeContract.FIELD_MANUFACTURER, row, PROPERTY_MANUFACTURER, observedAt);
            AddText(measurements, index, MemoryProbeContract.FIELD_PART_NUMBER, row, PROPERTY_PART_NUMBER, observedAt);
            AddInteger(measurements, index, MemoryProbeContract.FIELD_CAPACITY, row, PROPERTY_CAPACITY, MemoryProbeContract.UNIT_BYTES, observedAt);
            missingSpeed |= !AddInteger(measurements, index, MemoryProbeContract.FIELD_SPEED, row, PROPERTY_SPEED, unit, observedAt);
            missingSpeed |= !AddInteger(
                measurements, index, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED, row, PROPERTY_CONFIGURED_CLOCK_SPEED, unit, observedAt);
        }

        if (missingSpeed)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Memory_MissingSpeedValues));
        }

        var status = issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return CreateResult(context, status, measurements, issues, observedAt);
    }

    /// <summary>
    /// SMBIOS 버전을 읽어 기록하고 속도 단위를 정한다. 읽지 못하면 Issue를 남기고 null(단위 불명)을 돌려준다.
    /// </summary>
    private string? ReadSpeedUnit(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var bios = _wmi.Query(BIOS_CLASS, BIOS_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        var row = bios.Status == WmiQueryStatus.Success && bios.Rows.Count > 0 ? bios.Rows[0] : null;
        var major = row is null ? null : WmiResultInterpreter.GetInt64(row, PROPERTY_SMBIOS_MAJOR);
        var minor = row is null ? null : WmiResultInterpreter.GetInt64(row, PROPERTY_SMBIOS_MINOR);

        if (major is not { } majorVersion || majorVersion <= 0)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Memory_SmbiosVersionUnavailable));
            return null;
        }

        var version = string.Create(CultureInfo.InvariantCulture, $"{majorVersion}.{minor ?? 0}");
        measurements.Add(new Measurement(
            MemoryProbeContract.SMBIOS_VERSION, new TextValue(version), null, SMBIOS_VERSION_SOURCE, observedAt, MeasurementQuality.Reported));

        return majorVersion >= SMBIOS_MEGATRANSFERS_MAJOR_VERSION
            ? MemoryProbeContract.UNIT_MEGATRANSFERS
            : MemoryProbeContract.UNIT_MEGAHERTZ;
    }

    /// <summary>
    /// 문자열 속성이 있으면 모듈 측정값으로 추가한다.
    /// </summary>
    private static void AddText(
        List<Measurement> measurements,
        int index,
        string field,
        IReadOnlyDictionary<string, object?> row,
        string property,
        DateTimeOffset observedAt)
    {
        var text = WmiResultInterpreter.GetText(row, property);
        if (text is not null)
        {
            measurements.Add(new Measurement(
                MemoryProbeContract.ModuleMeasurementName(index, field), new TextValue(text), null, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
        }
    }

    /// <summary>
    /// 정수 속성이 있으면 모듈 측정값으로 추가한다(0도 원시 값 그대로 기록). 값이 없으면 false.
    /// </summary>
    private static bool AddInteger(
        List<Measurement> measurements,
        int index,
        string field,
        IReadOnlyDictionary<string, object?> row,
        string property,
        string? unit,
        DateTimeOffset observedAt)
    {
        var value = WmiResultInterpreter.GetInt64(row, property);
        if (value is null)
        {
            return false;
        }

        measurements.Add(new Measurement(
            MemoryProbeContract.ModuleMeasurementName(index, field), new IntegerValue(value.Value), unit, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>
    /// 이 프로브의 결과를 만든다. 시작 시각·소요 시간은 실행 조율기가 덮어쓴다.
    /// </summary>
    private ProbeResult CreateResult(
        ScanContext context,
        ProbeStatus status,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<Issue> issues,
        DateTimeOffset startedAt)
    {
        return new ProbeResult(Id, status, measurements, issues, startedAt, TimeSpan.Zero, context.UserContext);
    }
}
