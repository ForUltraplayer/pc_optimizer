/**
 * @file    : InstalledGpuProbe.cs
 * @author  : rudals252
 * @brief   : Win32_VideoController 어댑터별 이름·드라이버 버전·날짜·PnP ID·제공자·프로세서와 Win32_PnPEntity 하드웨어 ID를 수집하는 설치 GPU 드라이버 프로브(네트워크 없음)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// 설치된 GPU 드라이버 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="GpuProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// Win32_VideoController 조회 실패·빈 결과는 Failed입니다. 하드웨어 ID는 Win32_PnPEntity를 PNPDeviceID로 연결해 읽으며,
/// 이 보조 조회가 실패해도 설치 정보는 유지하고 Partial로 표시합니다. 최신 버전 조회(네트워크)는 하지 않습니다.
/// </remarks>
public sealed class InstalledGpuProbe : IProbe
{
    /// <summary>비디오 컨트롤러 WMI 클래스.</summary>
    public const string VIDEO_CONTROLLER_CLASS = "Win32_VideoController";

    /// <summary>PnP 장치 WMI 클래스.</summary>
    public const string PNP_ENTITY_CLASS = "Win32_PnPEntity";

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(6);

    private const string PROPERTY_NAME = "Name";
    private const string PROPERTY_DRIVER_VERSION = "DriverVersion";
    private const string PROPERTY_DRIVER_DATE = "DriverDate";
    private const string PROPERTY_PNP_DEVICE_ID = "PNPDeviceID";
    private const string PROPERTY_ADAPTER_COMPATIBILITY = "AdapterCompatibility";
    private const string PROPERTY_VIDEO_PROCESSOR = "VideoProcessor";
    private const string PROPERTY_HARDWARE_ID = "HardwareID";
    private const string SOURCE_PREFIX = "WMI " + VIDEO_CONTROLLER_CLASS + ".";
    private const string SOURCE_HARDWARE_ID = "WMI " + PNP_ENTITY_CLASS + "." + PROPERTY_HARDWARE_ID;
    private const string SOURCE_COUNT = SOURCE_PREFIX + "Count";

    private static readonly string[] CONTROLLER_PROPERTIES =
    [
        PROPERTY_NAME,
        PROPERTY_DRIVER_VERSION,
        PROPERTY_DRIVER_DATE,
        PROPERTY_PNP_DEVICE_ID,
        PROPERTY_ADAPTER_COMPATIBILITY,
        PROPERTY_VIDEO_PROCESSOR,
    ];

    private static readonly string[] PNP_PROPERTIES = [PROPERTY_PNP_DEVICE_ID, PROPERTY_HARDWARE_ID];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WMI와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public InstalledGpuProbe()
        : this(WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// WMI 클라이언트와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public InstalledGpuProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => GpuProbeContract.PROBE_ID;

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
        return Task.FromResult(Collect(context, ct));
    }

    /// <summary>
    /// 어댑터 정보를 조회해 측정값과 Issue를 만든다.
    /// </summary>
    private ProbeResult Collect(ScanContext context, CancellationToken ct)
    {
        var observedAt = _clock.UtcNow;
        var controllers = _wmi.Query(VIDEO_CONTROLLER_CLASS, CONTROLLER_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (controllers.Status != WmiQueryStatus.Success)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(VIDEO_CONTROLLER_CLASS, controllers)], observedAt);
        }

        if (controllers.Rows.Count == 0)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.EmptyIssue(VIDEO_CONTROLLER_CLASS)], observedAt);
        }

        var issues = new List<Issue>();
        var hardwareIds = ReadHardwareIds(issues, ct);
        var measurements = new List<Measurement>
        {
            new(GpuProbeContract.ADAPTER_COUNT, new IntegerValue(controllers.Rows.Count), null, SOURCE_COUNT, observedAt, MeasurementQuality.Observed),
        };

        var missingVersion = false;
        for (var index = 0; index < controllers.Rows.Count; index++)
        {
            var row = controllers.Rows[index];
            AddText(measurements, index, GpuProbeContract.FIELD_NAME, WmiResultInterpreter.GetText(row, PROPERTY_NAME), PROPERTY_NAME, observedAt);
            var version = WmiResultInterpreter.GetText(row, PROPERTY_DRIVER_VERSION);
            missingVersion |= version is null;
            AddText(measurements, index, GpuProbeContract.FIELD_DRIVER_VERSION, version, PROPERTY_DRIVER_VERSION, observedAt);
            AddText(measurements, index, GpuProbeContract.FIELD_DRIVER_DATE, WmiResultInterpreter.GetDmtfDate(row, PROPERTY_DRIVER_DATE), PROPERTY_DRIVER_DATE, observedAt);
            var pnpId = WmiResultInterpreter.GetText(row, PROPERTY_PNP_DEVICE_ID);
            AddText(measurements, index, GpuProbeContract.FIELD_PNP_DEVICE_ID, pnpId, PROPERTY_PNP_DEVICE_ID, observedAt);
            AddText(
                measurements, index, GpuProbeContract.FIELD_ADAPTER_COMPATIBILITY, WmiResultInterpreter.GetText(row, PROPERTY_ADAPTER_COMPATIBILITY), PROPERTY_ADAPTER_COMPATIBILITY, observedAt);
            AddText(measurements, index, GpuProbeContract.FIELD_VIDEO_PROCESSOR, WmiResultInterpreter.GetText(row, PROPERTY_VIDEO_PROCESSOR), PROPERTY_VIDEO_PROCESSOR, observedAt);

            if (pnpId is not null && hardwareIds is not null && hardwareIds.TryGetValue(pnpId, out var ids))
            {
                measurements.Add(new Measurement(
                    GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_HARDWARE_IDS),
                    new TextListValue(ids),
                    null,
                    SOURCE_HARDWARE_ID,
                    observedAt,
                    MeasurementQuality.Reported));
            }
        }

        if (missingVersion)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Gpu_MissingDriverVersion));
        }

        var status = issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return CreateResult(context, status, measurements, issues, observedAt);
    }

    /// <summary>
    /// PnP 장치의 하드웨어 ID를 PNPDeviceID 기준으로 읽는다. 조회에 실패하면 Issue를 남기고 null.
    /// </summary>
    private Dictionary<string, IReadOnlyList<string>>? ReadHardwareIds(List<Issue> issues, CancellationToken ct)
    {
        var entities = _wmi.Query(PNP_ENTITY_CLASS, PNP_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (entities.Status != WmiQueryStatus.Success)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Gpu_HardwareIdsUnavailable));
            return null;
        }

        var byDevice = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in entities.Rows)
        {
            if (WmiResultInterpreter.GetText(row, PROPERTY_PNP_DEVICE_ID) is { } pnpId
                && WmiResultInterpreter.GetStringArray(row, PROPERTY_HARDWARE_ID) is { Count: > 0 } ids)
            {
                byDevice.TryAdd(pnpId, ids);
            }
        }

        return byDevice;
    }

    /// <summary>
    /// 값이 있으면 어댑터 문자열 측정값을 추가한다.
    /// </summary>
    private static void AddText(List<Measurement> measurements, int index, string field, string? value, string property, DateTimeOffset observedAt)
    {
        if (value is not null)
        {
            measurements.Add(new Measurement(
                GpuProbeContract.AdapterMeasurementName(index, field), new TextValue(value), null, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
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
