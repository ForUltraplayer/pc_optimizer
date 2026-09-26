/**
 * @file    : UserContext.cs
 * @author  : rudals252
 * @brief   : 검사를 실행한 사용자 컨텍스트(익명 사용자 ID, 관리자 권한 여부, 내보내기 제외 SID) 레코드
 */

// 기본 패키지
using System.Text.Json.Serialization;

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사·수집을 실행한 사용자 컨텍스트입니다. 값은 App/Probes가 채우며 Core는 OS에 묻지 않습니다.
/// </summary>
/// <param name="AnonymizedUserId">검사 단위 익명 사용자 ID.</param>
/// <param name="IsElevated">관리자 권한으로 실행 중인지 여부.</param>
public sealed record UserContext(string AnonymizedUserId, bool IsElevated)
{
    /// <summary>
    /// 사용자 SID(없으면 null). 다른 SID 컨텍스트 구분용이며 JSON 내보내기에 포함하지 않는다.
    /// </summary>
    [JsonIgnore]
    public string? Sid { get; init; }
}
