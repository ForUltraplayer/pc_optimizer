/**
 * @file    : CannotVerifyTexts.cs
 * @author  : rudals252
 * @brief   : 수집 실패·미실행을 CannotVerify Finding으로 바꿀 때 쓰는 한국어 제목·근거 템플릿 상수
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 수집 실패·미실행을 CannotVerify Finding으로 바꿀 때 쓰는 한국어 템플릿입니다.
/// UI 리소스 분리(현지화)는 P2에서 이 값을 리소스 키로 옮길 때 처리합니다.
/// </summary>
public static class CannotVerifyTexts
{
    /// <summary>건너뛴 프로브의 제목.</summary>
    public const string TITLE_SKIPPED = "이번 검사에서 실행하지 않은 항목이에요";

    /// <summary>실패한 프로브의 제목.</summary>
    public const string TITLE_FAILED = "이 항목을 확인하지 못했어요";

    /// <summary>취소된 프로브의 제목.</summary>
    public const string TITLE_CANCELLED = "검사를 취소해서 이 항목을 확인하지 못했어요";

    /// <summary>일부만 수집한 프로브의 제목.</summary>
    public const string TITLE_PARTIAL = "이 항목은 일부만 확인했어요";

    /// <summary>판정 규칙 실행 실패의 제목.</summary>
    public const string TITLE_RULE_FAILED = "판정 규칙을 실행하지 못했어요";

    /// <summary>ElevationRequired 근거.</summary>
    public const string EVIDENCE_ELEVATION_REQUIRED = "관리자 권한이 있어야 확인할 수 있어요";

    /// <summary>NetworkFailed 근거.</summary>
    public const string EVIDENCE_NETWORK_FAILED = "네트워크 요청이 실패했어요";

    /// <summary>NoRule 근거.</summary>
    public const string EVIDENCE_NO_RULE = "판정할 규칙이 없어요";

    /// <summary>Unsupported 근거.</summary>
    public const string EVIDENCE_UNSUPPORTED = "이 PC에서는 지원하지 않는 항목이에요";

    /// <summary>ProbeError 근거.</summary>
    public const string EVIDENCE_PROBE_ERROR = "정보를 읽는 중 오류가 발생했어요";

    /// <summary>Timeout 근거.</summary>
    public const string EVIDENCE_TIMEOUT = "제한 시간 안에 끝나지 않았어요";

    /// <summary>Cancelled 근거.</summary>
    public const string EVIDENCE_CANCELLED = "사용자가 검사를 취소했어요";

    /// <summary>AccessDenied 근거.</summary>
    public const string EVIDENCE_ACCESS_DENIED = "접근이 거부되었어요";

    /// <summary>PartialData 근거.</summary>
    public const string EVIDENCE_PARTIAL_DATA = "일부 정보만 읽을 수 있었어요";

    /// <summary>Ambiguous 근거.</summary>
    public const string EVIDENCE_AMBIGUOUS = "결과가 모호해서 판단하지 못했어요";

    /// <summary>NotRequested 근거.</summary>
    public const string EVIDENCE_NOT_REQUESTED = "온라인 확인을 요청하지 않아 실행하지 않았어요";

    /// <summary>판정 규칙 실행 실패 근거.</summary>
    public const string EVIDENCE_RULE_FAILED = "판정 규칙 실행 중 오류가 발생했어요";

    /// <summary>
    /// 사유 코드에 맞는 근거 문장을 돌려줍니다.
    /// </summary>
    /// <param name="reason">사유 코드.</param>
    /// <returns>근거 문장.</returns>
    public static string EvidenceFor(CannotVerifyReason reason)
    {
        return reason switch
        {
            CannotVerifyReason.ElevationRequired => EVIDENCE_ELEVATION_REQUIRED,
            CannotVerifyReason.NetworkFailed => EVIDENCE_NETWORK_FAILED,
            CannotVerifyReason.NoRule => EVIDENCE_NO_RULE,
            CannotVerifyReason.Unsupported => EVIDENCE_UNSUPPORTED,
            CannotVerifyReason.ProbeError => EVIDENCE_PROBE_ERROR,
            CannotVerifyReason.Timeout => EVIDENCE_TIMEOUT,
            CannotVerifyReason.Cancelled => EVIDENCE_CANCELLED,
            CannotVerifyReason.AccessDenied => EVIDENCE_ACCESS_DENIED,
            CannotVerifyReason.PartialData => EVIDENCE_PARTIAL_DATA,
            CannotVerifyReason.Ambiguous => EVIDENCE_AMBIGUOUS,
            CannotVerifyReason.NotRequested => EVIDENCE_NOT_REQUESTED,
            _ => EVIDENCE_PROBE_ERROR,
        };
    }
}
