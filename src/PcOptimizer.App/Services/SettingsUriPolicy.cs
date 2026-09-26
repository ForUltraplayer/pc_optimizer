/**
 * @file    : SettingsUriPolicy.cs
 * @author  : rudals252
 * @brief   : 코드에 정의한 ms-settings: URI 허용 목록과, 허용된 URI만 셸로 여는 설정 열기 정책(그 밖의 프로세스 실행 없음)
 */

// 기본 패키지
using System.Collections.Frozen;
using System.Diagnostics;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.App.Services;

/// <summary>
/// Windows 설정 URI 허용 정책입니다. 규칙·응답이 지정한 임의 실행 파일·명령·file URI는 실행하지 않고,
/// 코드에 정의한 설정 URI와 정확히 같은 문자열(대소문자 무시)만 허용 목록의 정식 표기로 엽니다.
/// </summary>
public sealed class SettingsUriPolicy
{
    /// <summary>디스플레이 설정 URI.</summary>
    public const string DISPLAY_SETTINGS_URI = "ms-settings:display";

    /// <summary>전원 및 절전 설정 URI.</summary>
    public const string POWER_SLEEP_SETTINGS_URI = "ms-settings:powersleep";

    /// <summary>그래픽 고급 설정(하드웨어 가속 GPU 예약) URI.</summary>
    public const string ADVANCED_GRAPHICS_SETTINGS_URI = "ms-settings:display-advancedgraphics";

    /// <summary>게임 모드 설정 URI.</summary>
    public const string GAME_MODE_SETTINGS_URI = "ms-settings:gaming-gamemode";

    /// <summary>저장소(저장 공간 센스) 설정 URI.</summary>
    public const string STORAGE_SENSE_SETTINGS_URI = "ms-settings:storagesense";

    /// <summary>시작 앱 설정 URI.</summary>
    public const string STARTUP_APPS_SETTINGS_URI = "ms-settings:startupapps";

    /// <summary>Windows 업데이트 설정 URI(드라이버 후보는 고급 옵션의 선택적 업데이트에서 확인).</summary>
    public const string WINDOWS_UPDATE_SETTINGS_URI = "ms-settings:windowsupdate-optionalupdates";

    private const string LOG_CATEGORY = nameof(SettingsUriPolicy);

    /// <summary>허용 목록(키: 비교용, 값: 실행할 정식 표기).</summary>
    private static readonly FrozenDictionary<string, string> ALLOWED_URIS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [DISPLAY_SETTINGS_URI] = DISPLAY_SETTINGS_URI,
        [POWER_SLEEP_SETTINGS_URI] = POWER_SLEEP_SETTINGS_URI,
        [ADVANCED_GRAPHICS_SETTINGS_URI] = ADVANCED_GRAPHICS_SETTINGS_URI,
        [GAME_MODE_SETTINGS_URI] = GAME_MODE_SETTINGS_URI,
        [STORAGE_SENSE_SETTINGS_URI] = STORAGE_SENSE_SETTINGS_URI,
        [STARTUP_APPS_SETTINGS_URI] = STARTUP_APPS_SETTINGS_URI,
        [WINDOWS_UPDATE_SETTINGS_URI] = WINDOWS_UPDATE_SETTINGS_URI,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly IAppLogger _logger;
    private readonly Action<string> _shellLauncher;

    /// <summary>
    /// 셸 실행(Process.Start + UseShellExecute)을 쓰는 정책을 만듭니다.
    /// </summary>
    /// <param name="logger">공용 로거.</param>
    public SettingsUriPolicy(IAppLogger logger)
        : this(logger, LaunchWithShell)
    {
    }

    /// <summary>
    /// 실행기를 지정해 정책을 만듭니다(테스트에서 실제 실행을 막기 위함).
    /// </summary>
    /// <param name="logger">공용 로거.</param>
    /// <param name="shellLauncher">허용된 URI를 여는 실행기.</param>
    public SettingsUriPolicy(IAppLogger logger, Action<string> shellLauncher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(shellLauncher);
        _logger = logger;
        _shellLauncher = shellLauncher;
    }

    /// <summary>
    /// URI가 허용 목록에 있는지 확인합니다.
    /// </summary>
    /// <param name="uri">확인할 URI.</param>
    /// <returns>허용되면 true.</returns>
    public static bool IsAllowed(string? uri)
    {
        return uri is not null && ALLOWED_URIS.ContainsKey(uri);
    }

    /// <summary>
    /// 허용된 URI이면 설정 화면을 엽니다. 허용되지 않았거나 실행에 실패하면 false이며 예외를 던지지 않습니다.
    /// </summary>
    /// <param name="uri">열 URI.</param>
    /// <returns>실행을 요청했으면 true.</returns>
    public bool TryOpen(string? uri)
    {
        if (uri is null || !ALLOWED_URIS.TryGetValue(uri, out var canonical))
        {
            _logger.Warn(LOG_CATEGORY, "SettingsUriRejected");
            return false;
        }

        try
        {
            _shellLauncher(canonical);
            _logger.Info(LOG_CATEGORY, $"SettingsUriOpened uri={canonical}");
            return true;
        }
        catch (Exception ex)
        {
            // 셸 실행 실패는 UI를 멈추지 않도록 흡수하고 예외 형식 이름만 남긴다.
            _logger.Warn(LOG_CATEGORY, $"SettingsUriOpenFailed uri={canonical} error={ex.GetType().Name}");
            return false;
        }
    }

    /// <summary>
    /// 셸로 URI를 연다(ms-settings: 프로토콜 처리기).
    /// </summary>
    private static void LaunchWithShell(string uri)
    {
        using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
