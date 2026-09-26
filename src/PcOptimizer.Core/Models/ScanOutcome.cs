/**
 * @file    : ScanOutcome.cs
 * @author  : rudals252
 * @brief   : 검사 단위 결과(완료/부분 검사/취소) 열거형
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 전체의 결과입니다.
/// </summary>
public enum ScanOutcome
{
    /// <summary>정책상 건너뛴 항목을 빼고 모든 프로브가 끝까지 수집했습니다.</summary>
    Completed,

    /// <summary>실패·타임아웃·부분 수집이 있어 일부만 검사했습니다.</summary>
    Partial,

    /// <summary>사용자가 취소했습니다. 이미 얻은 결과는 남아 있습니다.</summary>
    Cancelled,
}
