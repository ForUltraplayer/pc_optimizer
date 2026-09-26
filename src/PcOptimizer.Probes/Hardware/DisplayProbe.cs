/**
 * @file    : DisplayProbe.cs
 * @author  : rudals252
 * @brief   : 활성 디스플레이 경로별 모니터 장치 경로·이름·어댑터·현재 모드·신호/경로 주사율·같은 조건 모드 주사율·복제 여부와 원격 세션 여부를 수집하는 디스플레이 프로브
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
/// 디스플레이 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="DisplayProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)로 활성 대상을 식별하고, 원본의 GDI 이름으로 EnumDisplaySettingsExW 모드를 열거해 연결합니다.
/// 같은 조건(해상도·방향·색 깊이·순차 주사) 모드의 선택은 Core의 <see cref="DisplayModeMatcher"/>를 씁니다.
/// 경로 조회 실패·활성 경로 없음은 Failed(빈 성공으로 돌려주지 않음), 일부 이름·모드를 읽지 못하면 Partial입니다.
/// </remarks>
public sealed class DisplayProbe : IProbe
{
    private const string SOURCE_QUERY_DISPLAY_CONFIG = "user32 QueryDisplayConfig";
    private const string SOURCE_DEVICE_INFO = "user32 DisplayConfigGetDeviceInfo";
    private const string SOURCE_ENUM_DISPLAY_DEVICES = "user32 EnumDisplayDevicesW";
    private const string SOURCE_CURRENT_SETTINGS = "user32 EnumDisplaySettingsExW(ENUM_CURRENT_SETTINGS)";
    private const string SOURCE_MODE_LIST = "user32 EnumDisplaySettingsExW";
    private const string SOURCE_REMOTE_SESSION = "user32 GetSystemMetrics(SM_REMOTESESSION)";
    private const int CLONE_GROUP_MIN_SIZE = 2;
    private const int WIN32_SUCCESS = 0;

    private readonly IDisplayPlatform _display;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 Win32 API와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public DisplayProbe()
        : this(Win32DisplayPlatform.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// 디스플레이 API와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="display">디스플레이 API.</param>
    /// <param name="clock">UTC 시계.</param>
    public DisplayProbe(IDisplayPlatform display, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(clock);
        _display = display;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => DisplayProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Display;

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
    /// 활성 경로와 모드를 읽어 측정값과 Issue를 만든다.
    /// </summary>
    private ProbeResult Collect(ScanContext context, CancellationToken ct)
    {
        var observedAt = _clock.UtcNow;
        var topology = _display.QueryActivePaths();
        if (topology.Error != WIN32_SUCCESS)
        {
            var summary = topology.Error == IDisplayPlatform.LAYOUT_MISMATCH_ERROR
                ? ProbeStrings.Display_LayoutMismatch
                : ProbeText.Format(ProbeStrings.Display_TopologyFailed, topology.Error);
            return CreateResult(context, ProbeStatus.Failed, [], [new Issue(CannotVerifyReason.ProbeError, summary)], observedAt);
        }

        if (topology.Paths.Count == 0)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [new Issue(CannotVerifyReason.Unsupported, ProbeStrings.Display_NoActivePaths)], observedAt);
        }

        var measurements = new List<Measurement>
        {
            new(DisplayProbeContract.TARGET_COUNT, new IntegerValue(topology.Paths.Count), null, SOURCE_QUERY_DISPLAY_CONFIG, observedAt, MeasurementQuality.Observed),
            new(DisplayProbeContract.REMOTE_SESSION, new BooleanValue(_display.IsRemoteSession()), null, SOURCE_REMOTE_SESSION, observedAt, MeasurementQuality.Observed),
        };
        var issues = new List<Issue>();
        var sourceUseCounts = topology.Paths
            .GroupBy(path => (path.AdapterLuid, path.SourceId))
            .ToDictionary(group => group.Key, group => group.Count());

        for (var index = 0; index < topology.Paths.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var path = topology.Paths[index];
            var cloned = sourceUseCounts[(path.AdapterLuid, path.SourceId)] >= CLONE_GROUP_MIN_SIZE;
            new TargetCollector(this, index, path, observedAt, measurements, issues).Collect(cloned);
        }

        var status = issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return CreateResult(context, status, measurements, issues, observedAt);
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

    /// <summary>
    /// 경로(대상) 하나의 측정값을 모으는 도우미입니다.
    /// </summary>
    private sealed class TargetCollector(
        DisplayProbe owner,
        int index,
        DisplayPathReading path,
        DateTimeOffset observedAt,
        List<Measurement> measurements,
        List<Issue> issues)
    {
        /// <summary>
        /// 경로 정보·이름·모드를 기록한다.
        /// </summary>
        public void Collect(bool cloned)
        {
            var target = path.TargetId.ToString(CultureInfo.InvariantCulture);
            AddText(DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH, path.MonitorDevicePath, SOURCE_DEVICE_INFO);
            AddText(DisplayProbeContract.FIELD_MONITOR_NAME, path.MonitorFriendlyName, SOURCE_DEVICE_INFO);
            AddText(DisplayProbeContract.FIELD_GDI_DEVICE_NAME, path.GdiDeviceName, SOURCE_DEVICE_INFO);
            AddText(DisplayProbeContract.FIELD_ADAPTER_DEVICE_PATH, path.AdapterDevicePath, SOURCE_DEVICE_INFO);
            Add(DisplayProbeContract.FIELD_TARGET_ID, new IntegerValue(path.TargetId), null, SOURCE_QUERY_DISPLAY_CONFIG, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_OUTPUT_TECHNOLOGY, new IntegerValue(path.OutputTechnology), null, SOURCE_QUERY_DISPLAY_CONFIG, MeasurementQuality.Reported);
            Add(DisplayProbeContract.FIELD_CLONED, new BooleanValue(cloned), null, SOURCE_QUERY_DISPLAY_CONFIG, MeasurementQuality.Observed);
            AddHz(DisplayProbeContract.FIELD_PATH_REFRESH_HZ, path.PathRefreshHz);
            AddHz(DisplayProbeContract.FIELD_SIGNAL_REFRESH_HZ, path.SignalRefreshHz);

            if (path.TargetNameError != WIN32_SUCCESS)
            {
                issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Display_TargetNameFailed, target, path.TargetNameError)));
            }

            if (path.AdapterNameError != WIN32_SUCCESS)
            {
                issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Display_AdapterNameFailed, target, path.AdapterNameError)));
            }

            if (path.GdiDeviceName is null)
            {
                issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Display_SourceNameFailed, target, path.SourceNameError)));
                return;
            }

            AddText(DisplayProbeContract.FIELD_ADAPTER_NAME, owner._display.GetAdapterName(path.GdiDeviceName), SOURCE_ENUM_DISPLAY_DEVICES);
            CollectModes(path.GdiDeviceName, target);
        }

        /// <summary>
        /// 현재 모드와 같은 조건 모드 주사율을 기록한다.
        /// </summary>
        private void CollectModes(string gdiDeviceName, string target)
        {
            var modes = owner._display.EnumerateModes(gdiDeviceName);
            if (modes.Current is not { } current)
            {
                issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Display_CurrentModeFailed, target)));
                return;
            }

            Add(DisplayProbeContract.FIELD_WIDTH, new IntegerValue(current.Width), DisplayProbeContract.UNIT_PIXELS, SOURCE_CURRENT_SETTINGS, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_HEIGHT, new IntegerValue(current.Height), DisplayProbeContract.UNIT_PIXELS, SOURCE_CURRENT_SETTINGS, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_REFRESH_HZ, new IntegerValue(current.RefreshHz), DisplayProbeContract.UNIT_HZ, SOURCE_CURRENT_SETTINGS, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_ORIENTATION, new IntegerValue(current.Orientation), null, SOURCE_CURRENT_SETTINGS, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_BITS_PER_PIXEL, new IntegerValue(current.BitsPerPixel), DisplayProbeContract.UNIT_BITS_PER_PIXEL, SOURCE_CURRENT_SETTINGS, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_INTERLACED, new BooleanValue(current.Interlaced), null, SOURCE_CURRENT_SETTINGS, MeasurementQuality.Observed);
            Add(DisplayProbeContract.FIELD_ENUMERATED_MODE_COUNT, new IntegerValue(modes.Modes.Count), null, SOURCE_MODE_LIST, MeasurementQuality.Reported);

            if (modes.Modes.Count == 0)
            {
                issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Display_ModeListEmpty, target)));
                return;
            }

            var rates = DisplayModeMatcher.SameShapeRefreshRates(current, modes.Modes);
            Add(
                DisplayProbeContract.FIELD_SAME_MODE_REFRESH_RATES,
                new TextListValue([.. rates.Select(rate => rate.ToString(CultureInfo.InvariantCulture))]),
                DisplayProbeContract.UNIT_HZ,
                SOURCE_MODE_LIST,
                MeasurementQuality.Reported);
            if (rates.Count > 0)
            {
                Add(DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ, new IntegerValue(rates.Max()), DisplayProbeContract.UNIT_HZ, SOURCE_MODE_LIST, MeasurementQuality.Reported);
            }
        }

        /// <summary>
        /// 값이 있으면 문자열 측정값을 추가한다.
        /// </summary>
        private void AddText(string field, string? value, string source)
        {
            if (value is not null)
            {
                Add(field, new TextValue(value), null, source, MeasurementQuality.Reported);
            }
        }

        /// <summary>
        /// 값이 있으면 실수 Hz 측정값을 추가한다.
        /// </summary>
        private void AddHz(string field, double? value)
        {
            if (value is { } hz)
            {
                Add(field, new DecimalValue(hz), DisplayProbeContract.UNIT_HZ, SOURCE_QUERY_DISPLAY_CONFIG, MeasurementQuality.Observed);
            }
        }

        /// <summary>
        /// 대상 필드 측정값을 추가한다.
        /// </summary>
        private void Add(string field, MeasurementValue value, string? unit, string source, MeasurementQuality quality)
        {
            measurements.Add(new Measurement(DisplayProbeContract.TargetMeasurementName(index, field), value, unit, source, observedAt, quality));
        }
    }
}
