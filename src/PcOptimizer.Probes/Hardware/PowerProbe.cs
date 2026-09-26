/**
 * @file    : PowerProbe.cs
 * @author  : rudals252
 * @brief   : 활성 전원 계획(GUID·이름), 섀시 종류 코드, AC/배터리 원시 상태를 수집하는 전원 프로브
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
/// 전원 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="PowerProbeContract"/>를 따릅니다.
/// 네 부분(활성 계획, 계획 이름, 섀시, 전원 상태) 중 일부만 읽으면 Partial, 하나도 못 읽으면 Failed입니다.
/// </summary>
public sealed class PowerProbe : IProbe
{
    /// <summary>섀시 정보 WMI 클래스.</summary>
    public const string SYSTEM_ENCLOSURE_CLASS = ChassisTypesReader.SYSTEM_ENCLOSURE_CLASS;

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(10);

    private const string GUID_FORMAT = "D";
    private const string PERCENT_UNIT = "%";
    private const string SOURCE_ACTIVE_SCHEME = "powrprof PowerGetActiveScheme";
    private const string SOURCE_FRIENDLY_NAME = "powrprof PowerReadFriendlyName";
    private const string SOURCE_POWER_STATUS = "kernel32 GetSystemPowerStatus";

    private readonly IPowerPlatform _power;
    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 Win32 API·WMI와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public PowerProbe()
        : this(Win32PowerPlatform.Instance, WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// 전원 API·WMI·시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="power">전원 API.</param>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public PowerProbe(IPowerPlatform power, IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(power);
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _power = power;
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => PowerProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Power;

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

        ReadActiveScheme(measurements, issues, observedAt);
        ct.ThrowIfCancellationRequested();
        ReadChassis(measurements, issues, observedAt, ct);
        ct.ThrowIfCancellationRequested();
        ReadPowerStatus(measurements, issues, observedAt);

        var status = measurements.Count == 0
            ? ProbeStatus.Failed
            : issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }

    /// <summary>
    /// 활성 전원 계획 GUID와 이름을 읽는다.
    /// </summary>
    private void ReadActiveScheme(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt)
    {
        var error = _power.GetActiveScheme(out var schemeGuid);
        if (error != NativeMethods.ERROR_SUCCESS)
        {
            issues.Add(new Issue(CannotVerifyReason.ProbeError, Format(ProbeStrings.Power_ActiveSchemeUnavailable, error)));
            return;
        }

        measurements.Add(new Measurement(
            PowerProbeContract.ACTIVE_SCHEME_GUID,
            new TextValue(schemeGuid.ToString(GUID_FORMAT, CultureInfo.InvariantCulture)),
            null,
            SOURCE_ACTIVE_SCHEME,
            observedAt,
            MeasurementQuality.Observed));

        error = _power.ReadFriendlyName(schemeGuid, out var name);
        if (error != NativeMethods.ERROR_SUCCESS || string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, Format(ProbeStrings.Power_FriendlyNameUnavailable, error)));
            return;
        }

        measurements.Add(new Measurement(
            PowerProbeContract.ACTIVE_SCHEME_NAME, new TextValue(name.Trim()), null, SOURCE_FRIENDLY_NAME, observedAt, MeasurementQuality.Observed));
    }

    /// <summary>
    /// 섀시 종류 코드를 읽는다. 여러 인클로저가 있으면 모든 코드를 모은다.
    /// </summary>
    private void ReadChassis(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt, CancellationToken ct)
    {
        if (!ChassisTypesReader.TryRead(_wmi, WMI_PROVIDER_TIMEOUT, ct, out var codes, out var issue))
        {
            issues.Add(issue);
            return;
        }

        measurements.Add(new Measurement(
            PowerProbeContract.CHASSIS_TYPES, new TextListValue(codes), null, ChassisTypesReader.SOURCE, observedAt, MeasurementQuality.Reported));
    }

    /// <summary>
    /// AC/배터리 원시 상태를 읽는다.
    /// </summary>
    private void ReadPowerStatus(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt)
    {
        var error = _power.GetPowerStatus(out var reading);
        if (error != NativeMethods.ERROR_SUCCESS || reading is null)
        {
            issues.Add(new Issue(CannotVerifyReason.ProbeError, Format(ProbeStrings.Power_StatusUnavailable, error)));
            return;
        }

        measurements.Add(new Measurement(
            PowerProbeContract.AC_LINE_STATUS, new IntegerValue(reading.AcLineStatus), null, SOURCE_POWER_STATUS, observedAt, MeasurementQuality.Observed));
        measurements.Add(new Measurement(
            PowerProbeContract.BATTERY_FLAG, new IntegerValue(reading.BatteryFlag), null, SOURCE_POWER_STATUS, observedAt, MeasurementQuality.Observed));
        measurements.Add(new Measurement(
            PowerProbeContract.BATTERY_LIFE_PERCENT, new IntegerValue(reading.BatteryLifePercent), PERCENT_UNIT, SOURCE_POWER_STATUS, observedAt, MeasurementQuality.Observed));
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채운다.
    /// </summary>
    private static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
