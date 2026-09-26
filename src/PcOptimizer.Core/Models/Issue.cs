/**
 * @file    : Issue.cs
 * @author  : rudals252
 * @brief   : 프로브 수집 중 발생한 문제(사유 코드와 요약) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 프로브 수집 중 발생한 문제 하나입니다.
/// </summary>
/// <param name="Reason">사유 코드.</param>
/// <param name="Summary">요약. 스택 추적을 담지 않는다.</param>
public sealed record Issue(CannotVerifyReason Reason, string Summary);
