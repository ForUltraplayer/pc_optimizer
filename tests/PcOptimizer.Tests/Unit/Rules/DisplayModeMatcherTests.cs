/**
 * @file    : DisplayModeMatcherTests.cs
 * @author  : rudals252
 * @brief   : 같은 조건 모드 선택(해상도 변경 필요·인터레이스·색 깊이·방향·기본값 제외)과 1Hz 초과 차이 판정(59/60, 59.94/60 억제) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="DisplayModeMatcher"/>의 비교 조건을 검증합니다. 모든 해상도·주사율은 테스트용 예시입니다.
/// </summary>
public sealed class DisplayModeMatcherTests
{
    private static readonly DisplayMode CURRENT = new(2560, 1440, 0, 32, false, 120);

    /// <summary>같은 해상도·방향·색 깊이·순차 주사 모드의 주사율만 중복 없이 오름차순으로 고른다.</summary>
    [Fact]
    public void 같은_조건_모드만_고른다()
    {
        DisplayMode[] modes =
        [
            new(2560, 1440, 0, 32, false, 144),
            new(2560, 1440, 0, 32, false, 60),
            new(2560, 1440, 0, 32, false, 120),
            new(2560, 1440, 0, 32, false, 144),
            new(1920, 1080, 0, 32, false, 240),
            new(3840, 2160, 0, 32, false, 160),
            new(2560, 1440, 0, 32, true, 165),
            new(2560, 1440, 0, 16, false, 170),
            new(2560, 1440, 1, 32, false, 175),
            new(2560, 1440, 0, 32, false, 1),
            new(2560, 1440, 0, 32, false, 0),
        ];

        var rates = DisplayModeMatcher.SameShapeRefreshRates(CURRENT, modes);

        Assert.Equal([60, 120, 144], rates);
    }

    /// <summary>해상도를 바꿔야 하는 모드만 높은 주사율을 가지면 후보 목록에 들어가지 않는다.</summary>
    [Fact]
    public void 해상도_변경이_필요한_모드는_제외한다()
    {
        DisplayMode[] modes = [new(2560, 1440, 0, 32, false, 120), new(1920, 1080, 0, 32, false, 240), new(2560, 1080, 0, 32, false, 200)];

        var rates = DisplayModeMatcher.SameShapeRefreshRates(CURRENT, modes);

        Assert.Equal([120], rates);
    }

    /// <summary>인터레이스 모드는 주사율이 높아도 제외한다.</summary>
    [Fact]
    public void 인터레이스_모드는_제외한다()
    {
        DisplayMode[] modes = [new(2560, 1440, 0, 32, true, 240)];

        Assert.Empty(DisplayModeMatcher.SameShapeRefreshRates(CURRENT, modes));
    }

    /// <summary>1Hz 이하 차이(59/60, 59.94/60, 120/121)는 의미 있게 높지 않다.</summary>
    [Theory]
    [InlineData(60, 59, false)]
    [InlineData(60, 59.94, false)]
    [InlineData(60, 60, false)]
    [InlineData(121, 120, false)]
    [InlineData(59, 60, false)]
    [InlineData(122, 120, true)]
    [InlineData(130, 120, true)]
    [InlineData(144, 59.94, true)]
    public void 일Hz_초과_차이만_의미_있다(double candidate, double current, bool expected)
    {
        Assert.Equal(expected, DisplayModeMatcher.IsMeaningfullyHigher(candidate, current));
    }
}
