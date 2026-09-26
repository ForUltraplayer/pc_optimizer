/**
 * @file    : SystemClock.cs
 * @author  : rudals252
 * @brief   : 시스템 시각을 돌려주는 IClock 구현
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 시스템 UTC 시각을 돌려주는 시계입니다.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// 외부에서 인스턴스를 만들지 않도록 막습니다.
    /// </summary>
    private SystemClock()
    {
    }

    /// <summary>공유 인스턴스.</summary>
    public static SystemClock Instance { get; } = new();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
