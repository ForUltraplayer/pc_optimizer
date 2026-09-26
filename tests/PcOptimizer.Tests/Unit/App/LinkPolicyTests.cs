/**
 * @file    : LinkPolicyTests.cs
 * @author  : rudals252
 * @brief   : 외부 링크 열기 정책의 허용/거부 표(공식 링크 표 호스트+경로 접두사, NVIDIA 허용 호스트, HTTP·javascript:·file:·미등록 호스트·사용자 정보·퓨니코드 유사 호스트·끝 점 거부, 대문자 호스트·HTTPS 대문자 허용)와 검증한 문자열로만 셸 실행·실패 흡수 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Tests.Unit.Rules;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="LinkPolicy"/>를 검증합니다. 실제 브라우저는 열지 않습니다.
/// </summary>
public sealed class LinkPolicyTests
{
    private readonly List<string> _launched = [];

    /// <summary>
    /// 실행 요청을 기록만 하는 정책을 만든다.
    /// </summary>
    private LinkPolicy CreatePolicy()
    {
        return new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, _launched.Add);
    }

    /// <summary>허용: 표의 호스트+경로 접두사, NVIDIA 응답의 상세·다운로드 호스트, 대문자 호스트·스킴.</summary>
    [Theory]
    [InlineData("https://www.dell.com/support/home/", "https://www.dell.com/support/home/")]
    [InlineData("https://www.amd.com/en/support/download/drivers.html", "https://www.amd.com/en/support/download/drivers.html")]
    [InlineData("https://www.nvidia.com/en-us/drivers/details/279803/", "https://www.nvidia.com/en-us/drivers/details/279803/")]
    [InlineData(
        "https://us.download.nvidia.com/Windows/617.14/617.14-desktop-win10-win11-64bit-international-dch-whql.exe",
        "https://us.download.nvidia.com/Windows/617.14/617.14-desktop-win10-win11-64bit-international-dch-whql.exe")]
    [InlineData("https://WWW.NVIDIA.COM/en-us/drivers/", "https://www.nvidia.com/en-us/drivers/")]
    [InlineData("HTTPS://www.dell.com/support/home/", "https://www.dell.com/support/home/")]
    public void 허용된_링크는_검증한_문자열로_연다(string url, string expectedLaunch)
    {
        var policy = CreatePolicy();

        Assert.True(policy.IsAllowed(url));
        Assert.Equal(LinkOpenResult.Opened, policy.TryOpen(url));
        Assert.Equal([expectedLaunch], _launched);
    }

    /// <summary>거부: HTTP, javascript:, file:, 미등록 호스트, 호스트 뒤에 붙인 도메인, 경로에 넣은 도메인, 사용자 정보, 끝 점, 유사 문자, 표 경로 밖.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://www.nvidia.com/en-us/drivers/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("https://evil.com/")]
    [InlineData("https://www.nvidia.com.evil.com/")]
    [InlineData("https://evil.com/www.nvidia.com")]
    [InlineData("https://user@www.nvidia.com/")]
    [InlineData("https://www.nvidia.com@evil.com/")]
    [InlineData("https://www.nvidia.com./")]
    [InlineData("https://www.nv\u0456dia.com/")]
    [InlineData("https://www.dell.com/other/")]
    [InlineData("https://www.dell.com:444/support/home/")]
    [InlineData("ms-settings:display")]
    [InlineData("https://www.nvidia.com/ a")]
    [InlineData("https://www.nvidia.com/other/")]
    [InlineData("https://us.download.nvidia.com/other/file.exe")]
    public void 허용되지_않은_링크는_거부한다(string? url)
    {
        var policy = CreatePolicy();

        Assert.False(policy.IsAllowed(url));
        Assert.Equal(LinkOpenResult.Refused, policy.TryOpen(url));
        Assert.Empty(_launched);
    }

    /// <summary>공식 링크 표가 없으면 NVIDIA 허용 호스트만 열고 표 항목(OEM 등)은 거부한다.</summary>
    [Fact]
    public void 표가_없으면_NVIDIA_호스트만_연다()
    {
        var policy = new LinkPolicy(null, NullAppLogger.Instance, _launched.Add);

        Assert.True(policy.IsAllowed("https://www.nvidia.com/en-us/drivers/details/279803/"));
        Assert.False(policy.IsAllowed("https://www.dell.com/support/home/"));
    }

    /// <summary>셸 실행이 실패해도 예외를 던지지 않고 Failed를 돌려준다.</summary>
    [Fact]
    public void 실행_실패는_Failed다()
    {
        var policy = new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, _ => throw new InvalidOperationException("launcher failed"));

        Assert.Equal(LinkOpenResult.Failed, policy.TryOpen("https://www.dell.com/support/home/"));
    }
}
