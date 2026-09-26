/**
 * @file    : IClock.cs
 * @author  : rudals252
 * @brief   : 현재 UTC 시각 제공 계약
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 현재 UTC 시각을 제공하는 계약입니다. 테스트에서는 고정 시계로 바꿉니다.
/// </summary>
public interface IClock
{
    /// <summary>현재 UTC 시각.</summary>
    DateTimeOffset UtcNow { get; }
}
