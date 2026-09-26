/**
 * @file    : IUnknownFolderAdvisor.cs
 * @author  : rudals252
 * @brief   : [예약] 미분류 폴더 조언자 타입 계약(요약을 받아 소유 앱 추정과 조사 방향 반환). 1차 미구현·미호출
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// [예약·1차 미구현·미호출] 미분류 폴더 요약을 받아 소유 앱 추정과 조사 방향을 반환하는 조언자 계약입니다.
/// 1차에서는 구현·등록·호출하지 않으며, 수집→판정의 필수 경유 단계가 아닙니다.
/// </summary>
public interface IUnknownFolderAdvisor
{
    /// <summary>
    /// [1차 미구현·미호출] 미분류 폴더 요약에 대한 조언을 만듭니다.
    /// </summary>
    /// <param name="summary">미분류 폴더 요약.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>조언.</returns>
    Task<UnknownFolderAdvice> AdviseAsync(UnknownFolderSummary summary, CancellationToken ct);
}

/// <summary>
/// [예약·1차 미구현·미호출] 미분류 폴더 요약입니다. 전체 개인 경로를 담지 않습니다.
/// </summary>
/// <param name="FolderLabel">폴더 표시 이름(개인 경로 제외).</param>
/// <param name="TotalBytes">총 크기(바이트).</param>
/// <param name="FileCount">파일 수.</param>
public sealed record UnknownFolderSummary(string FolderLabel, long TotalBytes, long FileCount);

/// <summary>
/// [예약·1차 미구현·미호출] 미분류 폴더에 대한 조언입니다.
/// </summary>
/// <param name="EstimatedOwner">추정 소유 앱(모르면 null).</param>
/// <param name="InvestigationHint">조사 방향.</param>
public sealed record UnknownFolderAdvice(string? EstimatedOwner, string InvestigationHint);
