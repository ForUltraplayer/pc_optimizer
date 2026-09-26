/**
 * @file    : ManualTimeProvider.cs
 * @author  : rudals252
 * @brief   : 테스트가 직접 앞으로 돌리는 단조 시간(타임스탬프) 공급자. 시간 예산 테스트를 실제 대기 없이 결정적으로 만든다
 */

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 수동으로 진행하는 시간 공급자입니다. 타임스탬프 단위는 TimeSpan 틱입니다.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        return Interlocked.Read(ref _ticks);
    }

    /// <summary>
    /// 시간을 앞으로 돌린다.
    /// </summary>
    public void Advance(TimeSpan amount)
    {
        Interlocked.Add(ref _ticks, amount.Ticks);
    }
}
