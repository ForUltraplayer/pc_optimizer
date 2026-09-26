/**
 * @file    : CannotVerifyReason.cs
 * @author  : rudals252
 * @brief   : 확인 불가(CannotVerify) 판정과 프로브 Issue의 사유 코드 열거형
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 확인하지 못한 사유 코드입니다. CannotVerify 판정과 프로브 Issue가 함께 사용합니다.
/// </summary>
public enum CannotVerifyReason
{
    /// <summary>관리자 권한이 필요합니다.</summary>
    ElevationRequired,

    /// <summary>네트워크 요청이 실패했습니다.</summary>
    NetworkFailed,

    /// <summary>판정할 규칙이 없습니다.</summary>
    NoRule,

    /// <summary>이 환경에서 지원하지 않습니다.</summary>
    Unsupported,

    /// <summary>수집 중 오류가 발생했습니다.</summary>
    ProbeError,

    /// <summary>제한 시간 안에 끝나지 않았습니다.</summary>
    Timeout,

    /// <summary>사용자가 취소했습니다.</summary>
    Cancelled,

    /// <summary>접근이 거부되었습니다.</summary>
    AccessDenied,

    /// <summary>일부 데이터만 수집되었습니다.</summary>
    PartialData,

    /// <summary>결과가 모호해 판단할 수 없습니다.</summary>
    Ambiguous,

    /// <summary>요청하지 않은 검사입니다(예: 온라인 확인 미요청).</summary>
    NotRequested,
}
