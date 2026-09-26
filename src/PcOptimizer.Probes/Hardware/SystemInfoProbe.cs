/**
 * @file    : SystemInfoProbe.cs
 * @author  : rudals252
 * @brief   : Win32_ComputerSystem 제조사·모델·제품군, Win32_SystemEnclosure 섀시 코드, Win32_BIOS BIOS 버전을 원시 값 그대로 수집하는 시스템 정보 프로브
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 시스템 제조사·모델 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="SystemInfoProbeContract"/>를 따릅니다.
/// 섀시 코드는 전원 프로브와 같은 <see cref="ChassisTypesReader"/>로 읽고, 장치 일련번호는 수집하지 않습니다.
/// 세 조회 중 일부만 성공하면 Partial, 하나도 값을 얻지 못하면 Failed입니다.
/// </summary>
public sealed class SystemInfoProbe : IProbe
{
    /// <summary>컴퓨터 시스템 WMI 클래스.</summary>
    public const string COMPUTER_SYSTEM_CLASS = "Win32_ComputerSystem";

    /// <summary>BIOS WMI 클래스.</summary>
    public const string BIOS_CLASS = "Win32_BIOS";

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(4);

    private const string PROPERTY_MANUFACTURER = "Manufacturer";
    private const string PROPERTY_MODEL = "Model";
    private const string PROPERTY_SYSTEM_FAMILY = "SystemFamily";
    private const string PROPERTY_BIOS_VERSION = "SMBIOSBIOSVersion";
    private const string SOURCE_COMPUTER_SYSTEM = "WMI " + COMPUTER_SYSTEM_CLASS + ".";
    private const string SOURCE_BIOS = "WMI " + BIOS_CLASS + "." + PROPERTY_BIOS_VERSION;

    private static readonly string[] COMPUTER_SYSTEM_PROPERTIES = [PROPERTY_MANUFACTURER, PROPERTY_MODEL, PROPERTY_SYSTEM_FAMILY];
    private static readonly string[] BIOS_PROPERTIES = [PROPERTY_BIOS_VERSION];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WMI와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public SystemInfoProbe()
        : this(WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// WMI 클라이언트와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public SystemInfoProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => SystemInfoProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

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

        ReadComputerSystem(measurements, issues, observedAt, ct);
        ct.ThrowIfCancellationRequested();
        if (ChassisTypesReader.TryRead(_wmi, WMI_PROVIDER_TIMEOUT, ct, out var codes, out var chassisIssue))
        {
            measurements.Add(new Measurement(
                SystemInfoProbeContract.CHASSIS_TYPES, new TextListValue(codes), null, ChassisTypesReader.SOURCE, observedAt, MeasurementQuality.Reported));
        }
        else
        {
            issues.Add(chassisIssue);
        }

        ct.ThrowIfCancellationRequested();
        ReadBios(measurements, issues, observedAt, ct);

        var status = measurements.Count == 0
            ? ProbeStatus.Failed
            : issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }

    /// <summary>
    /// 제조사·모델·제품군을 읽는다.
    /// </summary>
    private void ReadComputerSystem(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var result = _wmi.Query(COMPUTER_SYSTEM_CLASS, COMPUTER_SYSTEM_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(COMPUTER_SYSTEM_CLASS, result));
            return;
        }

        if (result.Rows.Count == 0)
        {
            issues.Add(WmiResultInterpreter.EmptyIssue(COMPUTER_SYSTEM_CLASS));
            return;
        }

        var row = result.Rows[0];
        var manufacturer = AddText(measurements, SystemInfoProbeContract.MANUFACTURER, row, PROPERTY_MANUFACTURER, observedAt);
        var model = AddText(measurements, SystemInfoProbeContract.MODEL, row, PROPERTY_MODEL, observedAt);
        AddText(measurements, SystemInfoProbeContract.SYSTEM_FAMILY, row, PROPERTY_SYSTEM_FAMILY, observedAt);
        if (!manufacturer || !model)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.System_ValuesMissing));
        }
    }

    /// <summary>
    /// BIOS 버전을 읽는다.
    /// </summary>
    private void ReadBios(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        var result = _wmi.Query(BIOS_CLASS, BIOS_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            issues.Add(WmiResultInterpreter.ToIssue(BIOS_CLASS, result));
            return;
        }

        var version = result.Rows.Count > 0 ? WmiResultInterpreter.GetText(result.Rows[0], PROPERTY_BIOS_VERSION) : null;
        if (version is null)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.System_BiosVersionMissing));
            return;
        }

        measurements.Add(new Measurement(SystemInfoProbeContract.BIOS_VERSION, new TextValue(version), null, SOURCE_BIOS, observedAt, MeasurementQuality.Reported));
    }

    /// <summary>
    /// 문자열 속성이 있으면 측정값으로 추가한다. 추가했으면 true.
    /// </summary>
    private static bool AddText(List<Measurement> measurements, string name, IReadOnlyDictionary<string, object?> row, string property, DateTimeOffset observedAt)
    {
        var text = WmiResultInterpreter.GetText(row, property);
        if (text is null)
        {
            return false;
        }

        measurements.Add(new Measurement(name, new TextValue(text), null, SOURCE_COMPUTER_SYSTEM + property, observedAt, MeasurementQuality.Reported));
        return true;
    }
}
