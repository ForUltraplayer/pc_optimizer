/**
 * @file    : GraphicsSettingsProbeContract.cs
 * @author  : rudals252
 * @brief   : 그래픽 설정 프로브와 규칙이 공유하는 프로브 ID·레지스트리 값별(HAGS, 게임 모드) 존재·형식·값 측정 이름과 알려진 원시 값 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 그래픽 설정 프로브(Probes)와 <see cref="GraphicsSettingsRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 레지스트리 값마다 존재 여부(불리언), 값 형식(문자열), DWORD 값(정수)을 따로 기록합니다.
/// 값을 읽지 못한 경우(접근 거부 등)에는 존재 여부 측정값 자체가 없습니다.
/// </summary>
public static class GraphicsSettingsProbeContract
{
    /// <summary>그래픽 설정 프로브 ID.</summary>
    public const string PROBE_ID = "hardware.graphicsSettings";

    /// <summary>HAGS 설정 측정 이름 접두사(HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers 의 HwSchMode).</summary>
    public const string HAGS_PREFIX = "graphics.hwSchMode";

    /// <summary>게임 모드 설정 측정 이름 접두사(HKCU\Software\Microsoft\GameBar 의 AutoGameModeEnabled).</summary>
    public const string GAME_MODE_PREFIX = "graphics.autoGameModeEnabled";

    /// <summary>필드: 값 존재 여부(불리언).</summary>
    public const string FIELD_EXISTS = "exists";

    /// <summary>필드: 레지스트리 값 형식(RegistryValueKind 이름, 문자열).</summary>
    public const string FIELD_KIND = "kind";

    /// <summary>필드: DWORD 값(정수).</summary>
    public const string FIELD_VALUE = "value";

    /// <summary>DWORD 값 형식 이름.</summary>
    public const string KIND_DWORD = "DWord";

    /// <summary>HwSchMode: 켜짐.</summary>
    public const long HAGS_ENABLED = 2;

    /// <summary>HwSchMode: 꺼짐.</summary>
    public const long HAGS_DISABLED = 1;

    /// <summary>AutoGameModeEnabled: 켜짐.</summary>
    public const long GAME_MODE_ENABLED = 1;

    /// <summary>AutoGameModeEnabled: 꺼짐.</summary>
    public const long GAME_MODE_DISABLED = 0;

    /// <summary>
    /// 설정 필드의 측정 이름을 만듭니다(예: "graphics.hwSchMode.exists").
    /// </summary>
    /// <param name="prefix">설정 접두사 상수.</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string MeasurementName(string prefix, string field)
    {
        return prefix + "." + field;
    }
}
