/**
 * @file    : StartupApprovedState.cs
 * @author  : rudals252
 * @brief   : 시작 항목의 StartupApproved 기준 활성 상태(활성/비활성/알 수 없음) 열거형
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// StartupApproved에 기록된 시작 항목의 활성 상태입니다. 알려진 값과 정확히 연결된 항목만 활성/비활성으로 해석합니다.
/// </summary>
public enum StartupApprovedState
{
    /// <summary>알 수 없음(값 없음·형식 다름·알려지지 않은 값·관리 대상 아님·읽기 실패).</summary>
    Unknown,

    /// <summary>활성(첫 바이트 0x02).</summary>
    Enabled,

    /// <summary>비활성(첫 바이트 0x03).</summary>
    Disabled,
}
