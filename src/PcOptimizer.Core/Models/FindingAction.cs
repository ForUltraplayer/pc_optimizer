/**
 * @file    : FindingAction.cs
 * @author  : rudals252
 * @brief   : Finding에서 사용자가 고를 수 있는 동작(상세 보기/설정 열기/유지/적용/링크 열기)의 닫힌 레코드 계층
 */

// 기본 패키지
using System.Text.Json.Serialization;

namespace PcOptimizer.Core.Models;

/// <summary>
/// Finding에 붙는 사용자 동작입니다. 외부 어셈블리에서 파생할 수 없는 닫힌 계층입니다.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ShowDetailsAction), "showDetails")]
[JsonDerivedType(typeof(OpenSettingsAction), "openSettings")]
[JsonDerivedType(typeof(KeepAction), "keep")]
[JsonDerivedType(typeof(ApplyAction), "apply")]
[JsonDerivedType(typeof(OpenLinkAction), "openLink")]
public abstract record FindingAction
{
    /// <summary>
    /// 이 어셈블리 안의 봉인된 파생 형식만 만들 수 있도록 생성자를 제한합니다.
    /// </summary>
    private protected FindingAction()
    {
    }
}

/// <summary>
/// 상세 정보를 보여 주는 동작입니다.
/// </summary>
public sealed record ShowDetailsAction : FindingAction;

/// <summary>
/// Windows 설정 화면을 여는 동작입니다.
/// </summary>
/// <param name="Uri">설정 URI. 예: "ms-settings:display".</param>
public sealed record OpenSettingsAction(string Uri) : FindingAction;

/// <summary>
/// 현재 상태를 유지하는 동작입니다.
/// </summary>
public sealed record KeepAction : FindingAction;

/// <summary>
/// 권고를 적용하는 동작입니다. 2차 기능이며 1차 UI에서는 비활성으로 표시합니다.
/// </summary>
public sealed record ApplyAction : FindingAction;

/// <summary>
/// 허용된 외부 링크를 여는 동작입니다. 화면은 허용 목록(공식 링크 표·NVIDIA 허용 호스트) 검증을 통과한 URL만 버튼으로 보여 주며,
/// 사용자가 버튼을 누를 때만 엽니다(자동으로 열거나 내려받지 않음).
/// </summary>
/// <param name="Url">열 링크 URL.</param>
/// <param name="Label">버튼 이름표(없으면 화면의 기본 문구).</param>
public sealed record OpenLinkAction(string Url, string? Label = null) : FindingAction;
