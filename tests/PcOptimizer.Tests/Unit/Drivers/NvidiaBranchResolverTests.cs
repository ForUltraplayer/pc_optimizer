/**
 * @file    : NvidiaBranchResolverTests.cs
 * @author  : rudals252
 * @brief   : 설치 버전의 Game Ready/Studio 목록 포함 여부로 계열을 정하는 순수 판정(한쪽만 → 그 계열, 양쪽·어느 쪽도 아님·Studio 목록 없음 → 모호) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.Tests.Unit.Drivers;

/// <summary>
/// <see cref="NvidiaBranchResolver"/>를 검증합니다. 버전은 테스트 예시입니다.
/// </summary>
public sealed class NvidiaBranchResolverTests
{
    private static readonly string[] GAME_READY = ["617.14", "616.92", "616.64"];
    private static readonly string[] STUDIO = ["616.92", "616.56", "610.88"];

    /// <summary>Game Ready 목록에만 있으면 Game Ready 계열이다.</summary>
    [Fact]
    public void 게임_레디에만_있으면_게임_레디다()
    {
        var resolution = NvidiaBranchResolver.Resolve("616.64", GAME_READY, STUDIO);
        Assert.Equal(NvidiaDriverBranch.GameReady, resolution.Branch);
        Assert.Equal(NvidiaBranchAmbiguity.None, resolution.Ambiguity);
    }

    /// <summary>Studio 목록에만 있으면 Studio 계열이다.</summary>
    [Fact]
    public void 스튜디오에만_있으면_스튜디오다()
    {
        var resolution = NvidiaBranchResolver.Resolve("616.56", GAME_READY, STUDIO);
        Assert.Equal(NvidiaDriverBranch.Studio, resolution.Branch);
        Assert.Equal(NvidiaBranchAmbiguity.None, resolution.Ambiguity);
    }

    /// <summary>두 목록에 같은 버전이 있으면 모호하다.</summary>
    [Fact]
    public void 양쪽에_있으면_모호하다()
    {
        var resolution = NvidiaBranchResolver.Resolve("616.92", GAME_READY, STUDIO);
        Assert.Equal(NvidiaDriverBranch.Ambiguous, resolution.Branch);
        Assert.Equal(NvidiaBranchAmbiguity.InBoth, resolution.Ambiguity);
    }

    /// <summary>어느 목록에도 없으면 모호하다.</summary>
    [Fact]
    public void 어느_쪽에도_없으면_모호하다()
    {
        var resolution = NvidiaBranchResolver.Resolve("600.10", GAME_READY, STUDIO);
        Assert.Equal(NvidiaDriverBranch.Ambiguous, resolution.Branch);
        Assert.Equal(NvidiaBranchAmbiguity.InNeither, resolution.Ambiguity);
    }

    /// <summary>Studio 목록을 받지 못했으면 Game Ready 목록에 있어도 계열을 정하지 않는다.</summary>
    [Theory]
    [InlineData("617.14")]
    [InlineData("600.10")]
    public void 스튜디오_목록이_없으면_모호하다(string installed)
    {
        var resolution = NvidiaBranchResolver.Resolve(installed, GAME_READY, studioVersions: null);
        Assert.Equal(NvidiaDriverBranch.Ambiguous, resolution.Branch);
        Assert.Equal(NvidiaBranchAmbiguity.StudioUnavailable, resolution.Ambiguity);
    }

    /// <summary>Studio 목록이 빈 목록(해당 제품에 Studio 배포 없음)으로 확인됐으면 Game Ready 목록 포함만으로 정한다.</summary>
    [Fact]
    public void 스튜디오_목록이_비었으면_게임_레디로_정한다()
    {
        var resolution = NvidiaBranchResolver.Resolve("617.14", GAME_READY, []);
        Assert.Equal(NvidiaDriverBranch.GameReady, resolution.Branch);
    }

    /// <summary>설치 버전을 해석할 수 없으면 모호하다.</summary>
    [Fact]
    public void 설치_버전을_해석할_수_없으면_모호하다()
    {
        var resolution = NvidiaBranchResolver.Resolve("알 수 없음", GAME_READY, STUDIO);
        Assert.Equal(NvidiaDriverBranch.Ambiguous, resolution.Branch);
        Assert.Equal(NvidiaBranchAmbiguity.InstalledUnparseable, resolution.Ambiguity);
    }
}
