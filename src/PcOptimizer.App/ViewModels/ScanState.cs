/**
 * @file    : ScanState.cs
 * @author  : rudals252
 * @brief   : 화면에 표시하는 검사 상태(대기/검사 중/완료/부분 완료/취소됨) 열거형
 */

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 화면의 검사 상태입니다.
/// </summary>
public enum ScanState
{
    /// <summary>아직 검사하지 않았습니다(대기).</summary>
    Idle,

    /// <summary>검사 중입니다.</summary>
    Scanning,

    /// <summary>모든 항목을 끝까지 검사했습니다(완료).</summary>
    Completed,

    /// <summary>실패·타임아웃·부분 수집이 있습니다(부분 완료).</summary>
    Partial,

    /// <summary>사용자가 취소했습니다(취소됨). 이미 얻은 결과는 남아 있습니다.</summary>
    Cancelled,
}
