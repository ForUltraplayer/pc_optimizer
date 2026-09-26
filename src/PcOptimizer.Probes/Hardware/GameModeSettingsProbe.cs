/**
 * @file    : GameModeSettingsProbe.cs
 * @author  : rudals252
 * @brief   : 게임 모드(HKCU GameBar\AutoGameModeEnabled) 레지스트리 값의 존재·형식·값을 구분해 읽기 전용으로 수집하는 사용자 범위 그래픽 설정 프로브
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 게임 모드 설정값을 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="GraphicsSettingsProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// 실행 사용자의 HKCU를 읽으므로 사용자 범위입니다. 다른 계정으로 승격된 재검사에서는 실행하지 않습니다.
/// 키·값이 없으면 존재 여부 false를 기록하고 성공으로 봅니다. 읽기 실패는 측정값 없이 Issue와 함께 Failed입니다. 레지스트리에 쓰지 않습니다.
/// </remarks>
public sealed class GameModeSettingsProbe : IProbe
{
    /// <summary>게임 모드 레지스트리 하위 키(HKCU).</summary>
    public const string GAME_MODE_SUB_KEY = @"Software\Microsoft\GameBar";

    /// <summary>게임 모드 값 이름.</summary>
    public const string GAME_MODE_VALUE_NAME = "AutoGameModeEnabled";

    private const string SOURCE_GAME_MODE = @"Registry HKCU\" + GAME_MODE_SUB_KEY + @"\" + GAME_MODE_VALUE_NAME;

    private static readonly RegistrySetting GAME_MODE_SETTING = new(
        RegistryRoot.CurrentUser, GAME_MODE_SUB_KEY, GAME_MODE_VALUE_NAME, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, SOURCE_GAME_MODE);

    private readonly IRegistryReader _registry;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 레지스트리와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public GameModeSettingsProbe()
        : this(Win32RegistryReader.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// 레지스트리 읽기와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="clock">UTC 시계.</param>
    public GameModeSettingsProbe(IRegistryReader registry, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(clock);
        _registry = registry;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Graphics;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.User;

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
        var read = RegistrySettingReader.Read(_registry, GAME_MODE_SETTING, measurements, issues, observedAt);

        var status = read ? ProbeStatus.Success : ProbeStatus.Failed;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }
}
