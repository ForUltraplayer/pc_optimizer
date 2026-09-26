/**
 * @file    : ElevationRelaunchResult.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 재검사 창 시작 요청의 결과(시작됨·UAC 취소·실패와 실패 코드) 열거형과 레코드
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 관리자 권한 재검사 창 시작 요청의 결과 종류입니다.
/// </summary>
public enum ElevationRelaunchOutcome
{
    /// <summary>승격된 새 창을 시작했습니다.</summary>
    Started,

    /// <summary>사용자가 UAC 요청을 취소했습니다.</summary>
    Cancelled,

    /// <summary>그 밖의 이유로 시작하지 못했습니다.</summary>
    Failed,
}

/// <summary>
/// 관리자 권한 재검사 창 시작 요청의 결과입니다.
/// </summary>
/// <param name="Outcome">결과 종류.</param>
/// <param name="ErrorCode">실패 코드(예외 형식 이름 또는 사전 점검 코드, 메시지 원문 아님). 실패가 아니면 null.</param>
public sealed record ElevationRelaunchResult(ElevationRelaunchOutcome Outcome, string? ErrorCode);
