/**
 * @file    : PowerSupplyKind.cs
 * @author  : rudals252
 * @brief   : ACLineStatus 원시 값을 분류한 전원 공급 상태(알 수 없음/AC 연결/배터리 동작) 열거형
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 전원 공급 상태입니다. 값이 없거나 알 수 없음이면 <see cref="Unknown"/>이며 AC로 추정하지 않습니다.
/// </summary>
public enum PowerSupplyKind
{
    /// <summary>알 수 없음(값 없음 또는 255 등).</summary>
    Unknown,

    /// <summary>AC 전원 연결.</summary>
    Ac,

    /// <summary>배터리로 동작.</summary>
    Battery,
}
