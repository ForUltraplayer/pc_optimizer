/**
 * @file    : GraphicsSettingsRule.cs
 * @author  : rudals252
 * @brief   : HAGS(HwSchMode)·게임 모드(AutoGameModeEnabled) 레지스트리 설정값을 정보로 알리고, 값 부재·알 수 없는 값은 지원 불가, 읽기 실패는 부분 데이터로 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 그래픽 설정 규칙입니다(스펙 §5 그래픽 설정 행).
/// <list type="bullet">
/// <item>알려진 DWORD 값: Info '설정값: 켜짐/꺼짐'. 설정값만으로 지원·실행 중·재부팅 적용 완료를 단정하지 않습니다.</item>
/// <item>값 부재: 꺼짐이 아니라 CannotVerify(Unsupported).</item>
/// <item>알 수 없는 값·DWORD가 아닌 형식: CannotVerify(Unsupported).</item>
/// <item>읽기 실패(존재 여부 측정값 없음): CannotVerify(PartialData).</item>
/// </list>
/// HAGS(시스템 범위)와 게임 모드(사용자 범위)는 서로 다른 프로브에서 오므로 각각 독립적으로 판정합니다.
/// 한쪽 프로브가 건너뜀·실패여도 다른 쪽 판정은 유지합니다.
/// </summary>
public sealed class GraphicsSettingsRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "graphics.settings";

    /// <summary>HAGS Finding ID.</summary>
    public const string HAGS_FINDING_ID = "graphics-setting:hags";

    /// <summary>게임 모드 Finding ID.</summary>
    public const string GAME_MODE_FINDING_ID = "graphics-setting:game-mode";

    /// <summary>그래픽 고급 설정(HAGS) URI.</summary>
    public const string HAGS_SETTINGS_URI = "ms-settings:display-advancedgraphics";

    /// <summary>게임 모드 설정 URI.</summary>
    public const string GAME_MODE_SETTINGS_URI = "ms-settings:gaming-gamemode";

    private const string HAGS_VALUE_NAME = "HwSchMode";
    private const string GAME_MODE_VALUE_NAME = "AutoGameModeEnabled";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var findings = new List<Finding>();
        if (TryGetUsable(snapshot, GraphicsSettingsProbeContract.PROBE_ID, out var hagsResult))
        {
            findings.Add(EvaluateSetting(
                snapshot,
                hagsResult,
                new Setting(
                    GraphicsSettingsProbeContract.PROBE_ID,
                    HAGS_FINDING_ID,
                    GraphicsSettingsProbeContract.HAGS_PREFIX,
                    HAGS_VALUE_NAME,
                    CoreStrings.Graphics_Name_Hags,
                    HAGS_SETTINGS_URI,
                    GraphicsSettingsProbeContract.HAGS_ENABLED,
                    GraphicsSettingsProbeContract.HAGS_DISABLED)));
        }

        if (TryGetUsable(snapshot, GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID, out var gameModeResult))
        {
            findings.Add(EvaluateSetting(
                snapshot,
                gameModeResult,
                new Setting(
                    GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID,
                    GAME_MODE_FINDING_ID,
                    GraphicsSettingsProbeContract.GAME_MODE_PREFIX,
                    GAME_MODE_VALUE_NAME,
                    CoreStrings.Graphics_Name_GameMode,
                    GAME_MODE_SETTINGS_URI,
                    GraphicsSettingsProbeContract.GAME_MODE_ENABLED,
                    GraphicsSettingsProbeContract.GAME_MODE_DISABLED)));
        }

        return findings;
    }

    /// <summary>
    /// 판정에 쓸 수 있는(성공·부분) 프로브 결과를 찾는다. 건너뜀·실패·취소는 엔진이 상태 Finding으로 알리므로 판정하지 않는다.
    /// </summary>
    private static bool TryGetUsable(ScanSnapshot snapshot, string probeId, [MaybeNullWhen(false)] out ProbeResult result)
    {
        return snapshot.TryGetProbe(probeId, out result)
            && (result.Status == ProbeStatus.Success || result.Status == ProbeStatus.Partial);
    }

    /// <summary>
    /// 설정 하나를 판정한다.
    /// </summary>
    private static Finding EvaluateSetting(ScanSnapshot snapshot, ProbeResult result, Setting setting)
    {
        string Name(string field) => GraphicsSettingsProbeContract.MeasurementName(setting.Prefix, field);

        Measurement[] measured = SnapshotValues.WithPrefix(result, setting.Prefix + ".");
        var exists = SnapshotValues.Boolean(snapshot, setting.ProbeId, Name(GraphicsSettingsProbeContract.FIELD_EXISTS));
        if (exists is null)
        {
            return Create(
                setting,
                SnapshotValues.Format(CoreStrings.Graphics_Title_NotRead, setting.DisplayName),
                measured,
                CoreStrings.Graphics_Evidence_NotRead,
                Verdict.CannotVerify,
                CannotVerifyReason.PartialData);
        }

        if (exists == false)
        {
            return Create(
                setting,
                SnapshotValues.Format(CoreStrings.Graphics_Title_Absent, setting.DisplayName),
                measured,
                SnapshotValues.Format(CoreStrings.Graphics_Evidence_Absent, setting.ValueName),
                Verdict.CannotVerify,
                CannotVerifyReason.Unsupported);
        }

        var kind = SnapshotValues.Text(snapshot, setting.ProbeId, Name(GraphicsSettingsProbeContract.FIELD_KIND));
        var value = SnapshotValues.Integer(snapshot, setting.ProbeId, Name(GraphicsSettingsProbeContract.FIELD_VALUE));
        var state = string.Equals(kind, GraphicsSettingsProbeContract.KIND_DWORD, StringComparison.Ordinal) && value is { } known
            ? StateOf(setting, known)
            : null;

        if (state is null)
        {
            return Create(
                setting,
                SnapshotValues.Format(CoreStrings.Graphics_Title_UnknownValue, setting.DisplayName),
                measured,
                SnapshotValues.Format(
                    CoreStrings.Graphics_Evidence_UnknownValue,
                    setting.ValueName,
                    kind ?? CoreStrings.Graphics_ValueUnknown,
                    value?.ToString(CultureInfo.InvariantCulture) ?? CoreStrings.Graphics_ValueUnknown),
                Verdict.CannotVerify,
                CannotVerifyReason.Unsupported);
        }

        return Create(
            setting,
            SnapshotValues.Format(CoreStrings.Graphics_Title_Value, setting.DisplayName, state),
            measured,
            SnapshotValues.Format(CoreStrings.Graphics_Evidence_Value, setting.ValueName, value!.Value.ToString(CultureInfo.InvariantCulture)),
            Verdict.Info,
            cannotVerifyReason: null);
    }

    /// <summary>
    /// 알려진 원시 값의 표시 문자열(켜짐/꺼짐). 알 수 없는 값이면 null.
    /// </summary>
    private static string? StateOf(Setting setting, long value)
    {
        if (value == setting.EnabledValue)
        {
            return CoreStrings.Graphics_State_On;
        }

        return value == setting.DisabledValue ? CoreStrings.Graphics_State_Off : null;
    }

    /// <summary>
    /// 그래픽 설정 Finding을 만든다(상세 보기 + 해당 설정 화면 열기).
    /// </summary>
    private static Finding Create(
        Setting setting,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason)
    {
        return new Finding(
            id: setting.FindingId,
            category: FindingCategory.Graphics,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction(), new OpenSettingsAction(setting.SettingsUri)]);
    }

    /// <summary>
    /// 판정할 레지스트리 설정 하나의 정의입니다.
    /// </summary>
    private sealed record Setting(
        string ProbeId,
        string FindingId,
        string Prefix,
        string ValueName,
        string DisplayName,
        string SettingsUri,
        long EnabledValue,
        long DisabledValue);
}
