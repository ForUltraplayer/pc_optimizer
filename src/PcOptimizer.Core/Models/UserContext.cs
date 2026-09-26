/**
 * @file    : UserContext.cs
 * @author  : rudals252
 * @brief   : 검사를 실행한 사용자 컨텍스트(익명 사용자 ID, 관리자 권한 여부) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사·수집을 실행한 사용자 컨텍스트입니다. 값은 App/Probes가 채우며 Core는 OS에 묻지 않습니다.
/// </summary>
/// <param name="AnonymizedUserId">검사 단위 익명 사용자 ID.</param>
/// <param name="IsElevated">관리자 권한으로 실행 중인지 여부.</param>
public sealed record UserContext(string AnonymizedUserId, bool IsElevated);
