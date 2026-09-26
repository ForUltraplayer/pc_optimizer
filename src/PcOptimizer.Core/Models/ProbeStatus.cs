/**
 * @file    : ProbeStatus.cs
 * @author  : rudals252
 * @brief   : 프로브 수집 상태(성공/부분/실패/건너뜀/취소) 열거형
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 프로브 한 번의 수집 상태입니다. 미실행·실패·빈 성공을 구분하기 위해 규칙에 함께 전달됩니다.
/// </summary>
public enum ProbeStatus
{
    /// <summary>모든 측정을 얻었습니다(빈 목록일 수 있으며, 이는 "조회 성공, 항목 없음"입니다).</summary>
    Success,

    /// <summary>일부 측정만 얻었습니다. Issues에 빠진 부분의 사유가 있습니다.</summary>
    Partial,

    /// <summary>수집에 실패했습니다(예외·타임아웃 등).</summary>
    Failed,

    /// <summary>정책(권한·온라인 미요청·이전 실행 종료 중 등) 때문에 실행하지 않았습니다.</summary>
    Skipped,

    /// <summary>사용자 취소로 끝났거나 시작하지 않았습니다.</summary>
    Cancelled,
}
