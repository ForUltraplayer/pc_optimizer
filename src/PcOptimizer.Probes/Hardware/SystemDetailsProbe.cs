/**
 * @file    : SystemDetailsProbe.cs
 * @author  : rudals252
 * @brief   : Win32_OperatingSystem·Win32_Processor·Win32_BIOS(ReleaseDate)·Win32_BaseBoard·Win32_NetworkAdapter(PhysicalAdapter)·Win32_PnPSignedDriver(NET)로 내 PC 사양 섹션의 상세 값을 수집. 일련번호·MAC·UUID는 수집하지 않음
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
/// 내 PC 사양 섹션용 시스템 상세 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="SystemDetailsProbeContract"/>를 따릅니다.
/// OS·CPU·BIOS 배포일·메인보드·물리 네트워크 어댑터 다섯 구간을 각각 조회하며, 일부만 성공하면 Partial, 하나도 얻지 못하면 Failed입니다.
/// 장치 일련번호·MAC 주소·UUID는 수집하지 않습니다.
/// </summary>
public sealed class SystemDetailsProbe : IProbe
{
    /// <summary>OS 클래스.</summary>
    public const string OS_CLASS = "Win32_OperatingSystem";

    /// <summary>CPU 클래스.</summary>
    public const string CPU_CLASS = "Win32_Processor";

    /// <summary>BIOS 클래스.</summary>
    public const string BIOS_CLASS = "Win32_BIOS";

    /// <summary>메인보드 클래스.</summary>
    public const string BOARD_CLASS = "Win32_BaseBoard";

    /// <summary>네트워크 어댑터 클래스.</summary>
    public const string NIC_CLASS = "Win32_NetworkAdapter";

    /// <summary>서명 드라이버 클래스(NET 클래스 드라이버 버전).</summary>
    public const string DRIVER_CLASS = "Win32_PnPSignedDriver";

    /// <summary>WMI 제공자 타임아웃.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(4);

    private const string DRIVER_CLASS_NET = "NET";
    private const string WMI_DATETIME_FORMAT = "yyyyMMddHHmmss.ffffff";
    private const int WMI_DATETIME_LENGTH = 25;
    private const string ISO_OFFSET_FORMAT = "yyyy-MM-ddTHH:mm:sszzz";
    private const string SOURCE_PREFIX = "WMI ";

    private const string PROPERTY_NAME = "Name";
    private const string PROPERTY_CAPTION = "Caption";
    private const string PROPERTY_VERSION = "Version";
    private const string PROPERTY_BUILD_NUMBER = "BuildNumber";
    private const string PROPERTY_INSTALL_DATE = "InstallDate";
    private const string PROPERTY_NUMBER_OF_CORES = "NumberOfCores";
    private const string PROPERTY_NUMBER_OF_LOGICAL_PROCESSORS = "NumberOfLogicalProcessors";
    private const string PROPERTY_MAX_CLOCK_SPEED = "MaxClockSpeed";
    private const string PROPERTY_RELEASE_DATE = "ReleaseDate";
    private const string PROPERTY_MANUFACTURER = "Manufacturer";
    private const string PROPERTY_PRODUCT = "Product";
    private const string PROPERTY_ADAPTER_TYPE_ID = "AdapterTypeId";
    private const string PROPERTY_NET_ENABLED = "NetEnabled";
    private const string PROPERTY_PHYSICAL_ADAPTER = "PhysicalAdapter";
    private const string PROPERTY_PNP_DEVICE_ID = "PNPDeviceID";
    private const string PROPERTY_DEVICE_ID = "DeviceID";
    private const string PROPERTY_DRIVER_VERSION = "DriverVersion";
    private const string PROPERTY_DEVICE_CLASS = "DeviceClass";

    private static readonly string[] OS_PROPERTIES = [PROPERTY_CAPTION, PROPERTY_VERSION, PROPERTY_BUILD_NUMBER, PROPERTY_INSTALL_DATE];
    private static readonly string[] CPU_PROPERTIES = [PROPERTY_NAME, PROPERTY_NUMBER_OF_CORES, PROPERTY_NUMBER_OF_LOGICAL_PROCESSORS, PROPERTY_MAX_CLOCK_SPEED];
    private static readonly string[] BIOS_PROPERTIES = [PROPERTY_RELEASE_DATE];
    private static readonly string[] BOARD_PROPERTIES = [PROPERTY_MANUFACTURER, PROPERTY_PRODUCT, PROPERTY_VERSION];
    private static readonly string[] NIC_PROPERTIES =
        [PROPERTY_NAME, PROPERTY_ADAPTER_TYPE_ID, PROPERTY_MANUFACTURER, PROPERTY_NET_ENABLED, PROPERTY_PHYSICAL_ADAPTER, PROPERTY_PNP_DEVICE_ID];
    private static readonly string[] DRIVER_PROPERTIES = [PROPERTY_DEVICE_ID, PROPERTY_DRIVER_VERSION, PROPERTY_DEVICE_CLASS];

    private static readonly string[] PLACEHOLDER_BOARD_VALUES = ["To be filled by O.E.M.", "Default string", "System Product Name"];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>실제 WMI와 시스템 시계를 쓰는 프로브를 만듭니다.</summary>
    public SystemDetailsProbe() : this(WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>WMI 클라이언트와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).</summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public SystemDetailsProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => SystemDetailsProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;

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

        var observedAt = _clock.UtcNow;
        var measurements = new List<Measurement>();
        var issues = new List<Issue>();
        var sections = 0;

        sections += ReadOs(measurements, issues, observedAt, ct) ? 1 : 0;
        ct.ThrowIfCancellationRequested();
        sections += ReadCpu(measurements, issues, observedAt, ct) ? 1 : 0;
        ct.ThrowIfCancellationRequested();
        sections += ReadBiosDate(measurements, issues, observedAt, ct) ? 1 : 0;
        ct.ThrowIfCancellationRequested();
        sections += ReadBoard(measurements, issues, observedAt, ct) ? 1 : 0;
        ct.ThrowIfCancellationRequested();
        sections += ReadAdapters(measurements, issues, observedAt, ct) ? 1 : 0;

        var status = sections == 0 ? ProbeStatus.Failed : issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }

    /// <summary>
    /// OS 이름·버전·빌드·설치일을 읽는다. 설치일은 오프셋 포함 ISO 8601 문자열로 바꾼다. 값 변환에 실패하면 그 측정만 생략한다.
    /// </summary>
    private bool ReadOs(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var result = _wmi.Query(OS_CLASS, OS_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(OS_CLASS, result));
            return false;
        }

        if (result.Rows.Count == 0)
        {
            issues.Add(WmiResultInterpreter.EmptyIssue(OS_CLASS));
            return false;
        }

        var row = result.Rows[0];
        var captionOk = AddText(measurements, SystemDetailsProbeContract.OS_CAPTION, row, PROPERTY_CAPTION, OS_CLASS, observedAt);
        var versionOk = AddText(measurements, SystemDetailsProbeContract.OS_VERSION, row, PROPERTY_VERSION, OS_CLASS, observedAt);
        AddText(measurements, SystemDetailsProbeContract.OS_BUILD, row, PROPERTY_BUILD_NUMBER, OS_CLASS, observedAt);

        var installText = WmiResultInterpreter.GetText(row, PROPERTY_INSTALL_DATE);
        if (installText is not null)
        {
            if (TryParseWmiDateTime(installText, out var installDate))
            {
                measurements.Add(new Measurement(
                    SystemDetailsProbeContract.OS_INSTALL_DATE,
                    new TextValue(installDate.ToString(ISO_OFFSET_FORMAT, CultureInfo.InvariantCulture)),
                    null,
                    Source(OS_CLASS, PROPERTY_INSTALL_DATE),
                    observedAt,
                    MeasurementQuality.Reported));
            }
            else
            {
                issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.SystemDetails_InstallDateInvalid));
            }
        }

        if (!captionOk || !versionOk)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.SystemDetails_OsMissing));
        }

        return true;
    }

    /// <summary>
    /// CPU 모델명·물리 코어 수·논리 프로세서 수·최대 클럭을 읽는다.
    /// </summary>
    private bool ReadCpu(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var result = _wmi.Query(CPU_CLASS, CPU_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(CPU_CLASS, result));
            return false;
        }

        if (result.Rows.Count == 0)
        {
            issues.Add(WmiResultInterpreter.EmptyIssue(CPU_CLASS));
            return false;
        }

        var row = result.Rows[0];
        var nameOk = AddText(measurements, SystemDetailsProbeContract.CPU_NAME, row, PROPERTY_NAME, CPU_CLASS, observedAt);
        var coresOk = AddInteger(measurements, SystemDetailsProbeContract.CPU_CORES, row, PROPERTY_NUMBER_OF_CORES, CPU_CLASS, observedAt, null);
        var threadsOk = AddInteger(measurements, SystemDetailsProbeContract.CPU_THREADS, row, PROPERTY_NUMBER_OF_LOGICAL_PROCESSORS, CPU_CLASS, observedAt, null);
        var clockOk = AddInteger(
            measurements, SystemDetailsProbeContract.CPU_MAX_CLOCK_MHZ, row, PROPERTY_MAX_CLOCK_SPEED, CPU_CLASS, observedAt, SystemDetailsProbeContract.UNIT_MEGAHERTZ);

        if (!nameOk || !coresOk || !threadsOk || !clockOk)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.SystemDetails_CpuMissing));
        }

        return true;
    }

    /// <summary>
    /// BIOS 배포일을 "yyyy-MM-dd"로 읽는다.
    /// </summary>
    private bool ReadBiosDate(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var result = _wmi.Query(BIOS_CLASS, BIOS_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(BIOS_CLASS, result));
            return false;
        }

        if (result.Rows.Count == 0)
        {
            issues.Add(WmiResultInterpreter.EmptyIssue(BIOS_CLASS));
            return false;
        }

        var date = WmiResultInterpreter.GetDmtfDate(result.Rows[0], PROPERTY_RELEASE_DATE);
        if (date is null)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.SystemDetails_BiosDateInvalid));
            return true;
        }

        measurements.Add(new Measurement(
            SystemDetailsProbeContract.BIOS_RELEASE_DATE, new TextValue(date), null, Source(BIOS_CLASS, PROPERTY_RELEASE_DATE), observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>
    /// 메인보드 제조사·제품명·버전을 읽는다. 플레이스홀더 값("To be filled by O.E.M." 등)이나 공백은 생략한다.
    /// </summary>
    private bool ReadBoard(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var result = _wmi.Query(BOARD_CLASS, BOARD_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(BOARD_CLASS, result));
            return false;
        }

        if (result.Rows.Count == 0)
        {
            issues.Add(WmiResultInterpreter.EmptyIssue(BOARD_CLASS));
            return false;
        }

        var row = result.Rows[0];
        var manufacturerOk = AddBoardText(measurements, SystemDetailsProbeContract.BOARD_MANUFACTURER, row, PROPERTY_MANUFACTURER, observedAt);
        var productOk = AddBoardText(measurements, SystemDetailsProbeContract.BOARD_PRODUCT, row, PROPERTY_PRODUCT, observedAt);
        var versionOk = AddBoardText(measurements, SystemDetailsProbeContract.BOARD_VERSION, row, PROPERTY_VERSION, observedAt);

        if (!manufacturerOk || !productOk || !versionOk)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.SystemDetails_BoardMissing));
        }

        return true;
    }

    /// <summary>
    /// 물리 네트워크 어댑터(PhysicalAdapter=true)만 골라 이름·종류·제조사·사용 여부·NET 클래스 드라이버 버전을 읽는다.
    /// 드라이버 버전은 Win32_PnPSignedDriver의 DeviceClass="NET" 행을 DeviceID로 조인해 찾는다. 일련번호·MAC·UUID는 읽지 않는다.
    /// </summary>
    private bool ReadAdapters(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var nicResult = _wmi.Query(NIC_CLASS, NIC_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (nicResult.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(NIC_CLASS, nicResult));
            return false;
        }

        var physicalRows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var row in nicResult.Rows)
        {
            if (IsPhysicalAdapter(row))
            {
                physicalRows.Add(row);
            }
        }

        var driverResult = _wmi.Query(DRIVER_CLASS, DRIVER_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        var driverVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (driverResult.Status == WmiQueryStatus.Success)
        {
            foreach (var row in driverResult.Rows)
            {
                var deviceClass = WmiResultInterpreter.GetText(row, PROPERTY_DEVICE_CLASS);
                if (!string.Equals(deviceClass, DRIVER_CLASS_NET, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var deviceId = WmiResultInterpreter.GetText(row, PROPERTY_DEVICE_ID);
                var driverVersion = WmiResultInterpreter.GetText(row, PROPERTY_DRIVER_VERSION);
                if (deviceId is not null && driverVersion is not null)
                {
                    driverVersions[deviceId] = driverVersion;
                }
            }
        }
        else
        {
            issues.Add(WmiResultInterpreter.ToIssue(DRIVER_CLASS, driverResult));
        }

        measurements.Add(new Measurement(
            SystemDetailsProbeContract.NIC_COUNT, new IntegerValue(physicalRows.Count), null, Source(NIC_CLASS, PROPERTY_PHYSICAL_ADAPTER), observedAt, MeasurementQuality.Reported));

        for (var index = 0; index < physicalRows.Count; index++)
        {
            var row = physicalRows[index];
            AddText(measurements, SystemDetailsProbeContract.AdapterMeasurementName(index, SystemDetailsProbeContract.FIELD_NAME), row, PROPERTY_NAME, NIC_CLASS, observedAt);
            AddInteger(
                measurements, SystemDetailsProbeContract.AdapterMeasurementName(index, SystemDetailsProbeContract.FIELD_ADAPTER_TYPE), row, PROPERTY_ADAPTER_TYPE_ID, NIC_CLASS, observedAt, null);
            AddText(
                measurements, SystemDetailsProbeContract.AdapterMeasurementName(index, SystemDetailsProbeContract.FIELD_MANUFACTURER), row, PROPERTY_MANUFACTURER, NIC_CLASS, observedAt);
            AddBoolean(
                measurements, SystemDetailsProbeContract.AdapterMeasurementName(index, SystemDetailsProbeContract.FIELD_NET_ENABLED), row, PROPERTY_NET_ENABLED, NIC_CLASS, observedAt);

            var pnpId = WmiResultInterpreter.GetText(row, PROPERTY_PNP_DEVICE_ID);
            if (pnpId is not null && driverVersions.TryGetValue(pnpId, out var driverVersion))
            {
                measurements.Add(new Measurement(
                    SystemDetailsProbeContract.AdapterMeasurementName(index, SystemDetailsProbeContract.FIELD_DRIVER_VERSION),
                    new TextValue(driverVersion),
                    null,
                    Source(DRIVER_CLASS, PROPERTY_DRIVER_VERSION),
                    observedAt,
                    MeasurementQuality.Reported));
            }
        }

        return true;
    }

    /// <summary>행이 물리 네트워크 어댑터인지 판정한다(PhysicalAdapter 속성이 true).</summary>
    private static bool IsPhysicalAdapter(IReadOnlyDictionary<string, object?> row)
    {
        return row.TryGetValue(PROPERTY_PHYSICAL_ADAPTER, out var raw) && raw is bool value && value;
    }

    /// <summary>메인보드 플레이스홀더 값인지 판정한다(대소문자 무시).</summary>
    private static bool IsPlaceholderBoardValue(string text)
    {
        foreach (var placeholder in PLACEHOLDER_BOARD_VALUES)
        {
            if (string.Equals(text, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>문자열 속성이 있으면 측정값으로 추가한다. 추가했으면 true.</summary>
    private static bool AddText(
        List<Measurement> measurements, string name, IReadOnlyDictionary<string, object?> row, string property, string className, DateTimeOffset observedAt)
    {
        var text = WmiResultInterpreter.GetText(row, property);
        if (text is null)
        {
            return false;
        }

        measurements.Add(new Measurement(name, new TextValue(text), null, Source(className, property), observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>메인보드 문자열 속성이 있고 플레이스홀더가 아니면 측정값으로 추가한다. 추가했으면 true.</summary>
    private static bool AddBoardText(List<Measurement> measurements, string name, IReadOnlyDictionary<string, object?> row, string property, DateTimeOffset observedAt)
    {
        var text = WmiResultInterpreter.GetText(row, property);
        if (text is null || IsPlaceholderBoardValue(text))
        {
            return false;
        }

        measurements.Add(new Measurement(name, new TextValue(text), null, Source(BOARD_CLASS, property), observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>
    /// 정수 속성이 있으면 측정값으로 추가한다. WMI는 부호 없는 정수(uint·ushort·ulong 등)를 박싱해 돌려주므로 Convert로 변환한다.
    /// 값이 없으면 생략하고 false를 돌려준다(0으로 바꾸지 않음).
    /// </summary>
    private static bool AddInteger(
        List<Measurement> measurements, string name, IReadOnlyDictionary<string, object?> row, string property, string className, DateTimeOffset observedAt, string? unit)
    {
        if (!row.TryGetValue(property, out var raw) || raw is null)
        {
            return false;
        }

        var value = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
        measurements.Add(new Measurement(name, new IntegerValue(value), unit, Source(className, property), observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>불리언 속성이 있으면 측정값으로 추가한다. 추가했으면 true.</summary>
    private static bool AddBoolean(
        List<Measurement> measurements, string name, IReadOnlyDictionary<string, object?> row, string property, string className, DateTimeOffset observedAt)
    {
        if (!row.TryGetValue(property, out var raw) || raw is not bool value)
        {
            return false;
        }

        measurements.Add(new Measurement(name, new BooleanValue(value), null, Source(className, property), observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>측정값 출처 문자열("WMI 클래스.속성")을 만든다.</summary>
    private static string Source(string className, string property) => SOURCE_PREFIX + className + "." + property;

    /// <summary>
    /// WMI CIM_DATETIME(yyyyMMddHHmmss.ffffff±UUU, UUU는 UTC 오프셋 분)을 DateTimeOffset으로 변환합니다.
    /// </summary>
    /// <param name="text">DMTF 날짜/시간 문자열.</param>
    /// <param name="value">변환된 값.</param>
    /// <returns>변환에 성공했으면 true.</returns>
    internal static bool TryParseWmiDateTime(string? text, out DateTimeOffset value)
    {
        value = default;
        if (text is null || text.Length != WMI_DATETIME_LENGTH)
        {
            return false;
        }

        var body = text[..WMI_DATETIME_FORMAT.Length];
        var offsetText = text[WMI_DATETIME_FORMAT.Length..];
        if (!DateTime.TryParseExact(body, WMI_DATETIME_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return false;
        }

        if (!int.TryParse(offsetText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offsetMinutes))
        {
            return false;
        }

        value = new DateTimeOffset(local, TimeSpan.FromMinutes(offsetMinutes));
        return true;
    }
}
