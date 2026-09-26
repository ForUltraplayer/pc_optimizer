/**
 * @file    : GraphicsSettingsProbe.cs
 * @author  : ForUltraplayer
 * @brief   : HAGS(HKLM GraphicsDrivers\HwSchMode) 레지스트리 값의 존재·형식·값을 구분해 읽기 전용으로 수집하는 시스템 범위 그래픽 설정 프로브
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 하드웨어 가속 GPU 예약(HAGS) 설정값을 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="GraphicsSettingsProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// HKLM 값만 읽으므로 시스템 범위입니다(사용자별 게임 모드는 <see cref="GameModeSettingsProbe"/>로 분리).
/// 키·값이 없으면 존재 여부 false를 기록하고(값 측정 없음) 성공으로 봅니다. 읽기 실패(접근 거부 등)는 측정값 없이 Issue와 함께 Failed입니다.
/// 레지스트리에 쓰지 않습니다.
/// </remarks>
public sealed class GraphicsSettingsProbe : IProbe
{
    /// <summary>HAGS 레지스트리 하위 키(HKLM).</summary>
    public const string HAGS_SUB_KEY = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    /// <summary>HAGS 값 이름.</summary>
    public const string HAGS_VALUE_NAME = "HwSchMode";

    private const string SOURCE_HAGS = @"Registry HKLM\" + HAGS_SUB_KEY + @"\" + HAGS_VALUE_NAME;

    private static readonly RegistrySetting HAGS_SETTING = new(
        RegistryRoot.LocalMachine, HAGS_SUB_KEY, HAGS_VALUE_NAME, GraphicsSettingsProbeContract.HAGS_PREFIX, SOURCE_HAGS);

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
        var read = RegistrySettingReader.Read(_registry, HAGS_SETTING, measurements, issues, observedAt);

        var status = read ? ProbeStatus.Success : ProbeStatus.Failed;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }
}
