/**
 * @file    : UserScopeResolverTests.cs
 * @author  : rudals252
 * @brief   : 프로세스 토큰 사용자와 대화형 로그온 사용자 SID 비교로 검사 범위(전체/시스템만)를 정하는 판정과 SID 확인 불가 구분을 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>사용자 범위 판정을 검증합니다.</summary>
public sealed class UserScopeResolverTests
{
    /// <summary>같은 SID면 전체, 다르거나 모르면 시스템만.</summary>
    [Theory]
    [InlineData("S-1-5-21-1-2-3-1001", "S-1-5-21-1-2-3-1001", UserScopeMode.Full)]
    [InlineData("S-1-5-21-1-2-3-1001", "s-1-5-21-1-2-3-1001", UserScopeMode.Full)]
    [InlineData("S-1-5-21-1-2-3-500", "S-1-5-21-1-2-3-1001", UserScopeMode.SystemOnly)]
    [InlineData(null, "S-1-5-21-1-2-3-1001", UserScopeMode.SystemOnly)]
    [InlineData("S-1-5-21-1-2-3-1001", null, UserScopeMode.SystemOnly)]
    public void ResolvesScope(string? tokenSid, string? interactiveSid, UserScopeMode expected)
    {
        Assert.Equal(expected, UserScopeResolver.Resolve(tokenSid, interactiveSid));
    }

    /// <summary>어느 한쪽 SID라도 모르면 "확인 불가"(배너 문구 구분용)이고, 둘 다 알면 같든 다르든 확인된 판정이다.</summary>
    [Theory]
    [InlineData("S-1-5-21-1-2-3-1001", "S-1-5-21-1-2-3-1001", false)]
    [InlineData("S-1-5-21-1-2-3-500", "S-1-5-21-1-2-3-1001", false)]
    [InlineData(null, "S-1-5-21-1-2-3-1001", true)]
    [InlineData("S-1-5-21-1-2-3-1001", null, true)]
    [InlineData(null, null, true)]
    public void DetectsUnresolvedScope(string? tokenSid, string? interactiveSid, bool expected)
    {
        Assert.Equal(expected, UserScopeResolver.IsUnresolved(tokenSid, interactiveSid));
    }
}
