/**
 * @file    : IRule.cs
 * @author  : rudals252
 * @brief   : 판정 규칙 계약. 불변 스냅샷만 입력받아 Finding 목록을 내는 순수 함수
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 판정 규칙 계약입니다. I/O 없이 스냅샷(측정값 + 프로브 상태/Issues)만 보고 Finding을 만듭니다.
/// </summary>
public interface IRule
{
    /// <summary>규칙 ID.</summary>
    string Id { get; }

    /// <summary>
    /// 스냅샷을 평가해 Finding 목록을 만듭니다. 같은 입력에는 항상 같은 결과를 내야 합니다.
    /// </summary>
    /// <param name="snapshot">검사 스냅샷.</param>
    /// <returns>판정 결과(해당 없음이면 빈 목록).</returns>
    IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot);
}
