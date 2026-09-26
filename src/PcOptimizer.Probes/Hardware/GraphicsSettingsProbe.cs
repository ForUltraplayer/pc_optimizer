/**
 * @file    : GraphicsSettingsProbe.cs
 * @author  : rudals252
 * @brief   : HAGS(HKLM GraphicsDrivers\HwSchMode)·게임 모드(HKCU GameBar\AutoGameModeEnabled) 레지스트리 값의 존재·형식·값을 구분해 읽기 전용으로 수집하는 그래픽 설정 프로브
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 그래픽 설정값을 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="GraphicsSettingsProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// 키·값이 없으면 존재 여부 false를 기록하고(값 측정 없음) 성공으로 봅니다. 읽기 실패(접근 거부 등)는 측정값을 남기지 않고 Issue로 알립니다.
/// 두 값 중 하나만 읽으면 Partial, 둘 다 실패하면 Failed입니다. 레지스트리에 쓰지 않습니다.
/// </remarks>
public sealed class GraphicsSettingsProbe : IProbe
{
    /// <summary>HAGS 레지스트리 하위 키(HKLM).</summary>
    public const string HAGS_SUB_KEY = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    /// <summary>HAGS 값 이름.</summary>
    public const string HAGS_VALUE_NAME = "HwSchMode";

    /// <summary>게임 모드 레지스트리 하위 키(HKCU).</summary>
    public const string GAME_MODE_SUB_KEY = @"Software\Microsoft\GameBar";

    /// <summary>게임 모드 값 이름.</summary>
    public const string GAME_MODE_VALUE_NAME = "AutoGameModeEnabled";

    private const string SOURCE_HAGS = @"Registry HKLM\" + HAGS_SUB_KEY + @"\" + HAGS_VALUE_NAME;
    private const string SOURCE_GAME_MODE = @"Registry HKCU\" + GAME_MODE_SUB_KEY + @"\" + GAME_MODE_VALUE_NAME;

    private readonly IRegistryReader _registry;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 레지스트리와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public GraphicsSettingsProbe()
        : this(Win32RegistryReader.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// 레지스트리 읽기와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="clock">UTC 시계.</param>
    public GraphicsSettingsProbe(IRegistryReader registry, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(clock);
        _registry = registry;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => GraphicsSettingsProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Graphics;

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
        var readCount = 0;

        readCount += Read(RegistryRoot.LocalMachine, HAGS_SUB_KEY, HAGS_VALUE_NAME, GraphicsSettingsProbeContract.HAGS_PREFIX, SOURCE_HAGS, measurements, issues, observedAt);
        ct.ThrowIfCancellationRequested();
        readCount += Read(
            RegistryRoot.CurrentUser, GAME_MODE_SUB_KEY, GAME_MODE_VALUE_NAME, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, SOURCE_GAME_MODE, measurements, issues, observedAt);

        var status = readCount == 0
            ? ProbeStatus.Failed
            : issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }

    /// <summary>
    /// 값 하나를 읽어 존재·형식·값 측정값을 추가한다. 읽기에 성공(값 없음 포함)하면 1, 실패하면 0.
    /// </summary>
    private int Read(
        RegistryRoot root,
        string subKey,
        string valueName,
        string prefix,
        string source,
        List<Measurement> measurements,
        List<Issue> issues,
        DateTimeOffset observedAt)
    {
        var reading = _registry.ReadValue(root, subKey, valueName);
        switch (reading.Status)
        {
            case RegistryReadStatus.AccessDenied:
                issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.Registry_AccessDenied, valueName)));
                return 0;
            case RegistryReadStatus.Error:
                issues.Add(new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Registry_Error, valueName, reading.ErrorCode ?? string.Empty)));
                return 0;
            case RegistryReadStatus.KeyMissing:
            case RegistryReadStatus.ValueMissing:
                Add(measurements, prefix, GraphicsSettingsProbeContract.FIELD_EXISTS, new BooleanValue(false), source, observedAt);
                return 1;
            case RegistryReadStatus.Found:
            default:
                Add(measurements, prefix, GraphicsSettingsProbeContract.FIELD_EXISTS, new BooleanValue(true), source, observedAt);
                if (reading.Kind is { } kind)
                {
                    Add(measurements, prefix, GraphicsSettingsProbeContract.FIELD_KIND, new TextValue(kind), source, observedAt);
                }

                if (reading.DwordValue is { } value)
                {
                    Add(measurements, prefix, GraphicsSettingsProbeContract.FIELD_VALUE, new IntegerValue(value), source, observedAt);
                }

                return 1;
        }
    }

    /// <summary>
    /// 설정 필드 측정값을 추가한다.
    /// </summary>
    private static void Add(List<Measurement> measurements, string prefix, string field, MeasurementValue value, string source, DateTimeOffset observedAt)
    {
        measurements.Add(new Measurement(
            GraphicsSettingsProbeContract.MeasurementName(prefix, field), value, null, source, observedAt, MeasurementQuality.Observed));
    }
}
