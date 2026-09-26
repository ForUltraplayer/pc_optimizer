/**
 * @file    : FakeClock.cs
 * @author  : rudals252
 * @brief   : 고정된 UTC 시각을 돌려주는 테스트용 시계
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Tests.Unit.Engine.Fakes;

/// <summary>
/// 지정한 UTC 시각을 돌려주는 테스트용 시계입니다.
/// </summary>
internal sealed class FakeClock : IClock
{
    /// <summary>테스트 기본 시각.</summary>
    public static readonly DateTimeOffset DEFAULT_NOW = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = DEFAULT_NOW;
}
