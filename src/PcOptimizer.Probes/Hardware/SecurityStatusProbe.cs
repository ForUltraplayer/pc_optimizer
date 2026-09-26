/**
 * @file    : SecurityStatusProbe.cs
 * @author  : rudals252
 * @brief   : root\Microsoft\Windows\DeviceGuard의 Win32_DeviceGuard에서 VBS 상태·설정/실행 중 보안 서비스·사용 가능 보안 속성을 원시 값 그대로 수집하는 보안 상태 프로브
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 보안 상태를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="SecurityStatusProbeContract"/>를 따릅니다.
/// 클래스·네임스페이스 없음은 Failed(Unsupported), 접근 거부는 Failed(AccessDenied), 빈 결과는 Failed이며
/// 일부 값만 비어 있으면 Partial입니다. 값이 없다고 0·false로 채우지 않습니다.
/// </summary>
public sealed class SecurityStatusProbe : IProbe
{
    /// <summary>Device Guard WMI 클래스.</summary>
    public const string DEVICE_GUARD_CLASS = "Win32_DeviceGuard";

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(10);

    private const string PROPERTY_VBS_STATUS = "VirtualizationBasedSecurityStatus";
    private const string PROPERTY_SERVICES_CONFIGURED = "SecurityServicesConfigured";
    private const string PROPERTY_SERVICES_RUNNING = "SecurityServicesRunning";
    private const string PROPERTY_AVAILABLE_PROPERTIES = "AvailableSecurityProperties";
    private const string SOURCE_PREFIX = "WMI " + DEVICE_GUARD_CLASS + ".";

    private static readonly string[] PROPERTIES =
    [
        PROPERTY_VBS_STATUS,
        PROPERTY_SERVICES_CONFIGURED,
        PROPERTY_SERVICES_RUNNING,
        PROPERTY_AVAILABLE_PROPERTIES,
    ];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WMI와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public SecurityStatusProbe()
        : this(WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// WMI 클라이언트와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public SecurityStatusProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => SecurityStatusProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Security;

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
        var result = _wmi.Query(WmiNamespaces.DEVICE_GUARD, DEVICE_GUARD_CLASS, PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            return Task.FromResult(CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(DEVICE_GUARD_CLASS, result)], observedAt));
        }

        if (result.Rows.Count == 0)
        {
            return Task.FromResult(CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.EmptyIssue(DEVICE_GUARD_CLASS)], observedAt));
        }

        var row = result.Rows[0];
        var measurements = new List<Measurement>();
        if (WmiResultInterpreter.GetInt64(row, PROPERTY_VBS_STATUS) is { } vbs)
        {
            measurements.Add(new Measurement(
                SecurityStatusProbeContract.VBS_STATUS, new IntegerValue(vbs), null, SOURCE_PREFIX + PROPERTY_VBS_STATUS, observedAt, MeasurementQuality.Reported));
        }

        AddList(measurements, SecurityStatusProbeContract.SERVICES_CONFIGURED, row, PROPERTY_SERVICES_CONFIGURED, observedAt);
        AddList(measurements, SecurityStatusProbeContract.SERVICES_RUNNING, row, PROPERTY_SERVICES_RUNNING, observedAt);
        AddList(measurements, SecurityStatusProbeContract.AVAILABLE_PROPERTIES, row, PROPERTY_AVAILABLE_PROPERTIES, observedAt);

        if (measurements.Count == 0)
        {
            return Task.FromResult(CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.EmptyIssue(DEVICE_GUARD_CLASS)], observedAt));
        }

        IReadOnlyList<Issue> issues = measurements.Count == PROPERTIES.Length
            ? []
            : [new Issue(CannotVerifyReason.PartialData, ProbeStrings.Security_ValuesMissing)];
        var status = issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(CreateResult(context, status, measurements, issues, observedAt));
    }

    /// <summary>
    /// 정수 배열 속성이 있으면 숫자 문자열 목록 측정값으로 추가한다(빈 배열은 빈 목록으로 기록).
    /// </summary>
    private static void AddList(List<Measurement> measurements, string name, IReadOnlyDictionary<string, object?> row, string property, DateTimeOffset observedAt)
    {
        if (WmiResultInterpreter.GetUInt32ArrayAsText(row, property) is { } values)
        {
            measurements.Add(new Measurement(name, new TextListValue(values), null, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
        }
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
