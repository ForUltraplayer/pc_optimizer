/**
 * @file    : OfficialUrlTests.cs
 * @author  : rudals252
 * @brief   : 공식 링크 URL 엄격 해석(HTTPS·기본 포트·사용자 정보 없음·DNS 호스트·끝 점/공백/역슬래시 거부·ASCII 호스트 비교)과 NVIDIA 허용 호스트 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.Tests.Unit.Drivers;

/// <summary>
/// <see cref="OfficialUrl"/>과 <see cref="NvidiaUrlAllowlist"/>를 검증합니다.
/// </summary>
public sealed class OfficialUrlTests
{
    /// <summary>키릴 문자 'і'(U+0456)가 섞인 모양이 비슷한 호스트.</summary>
    private const string HOMOGRAPH_URL = "https://www.nv\u0456dia.com/";

    /// <summary>HTTPS 절대 URL이며 사용자 정보·비기본 포트·IP·끝 점·공백이 없어야 한다.</summary>
    [Theory]
    [InlineData("https://www.nvidia.com/en-us/drivers/", true)]
    [InlineData("HTTPS://WWW.NVIDIA.COM/en-us/drivers/", true)]
    [InlineData("http://www.nvidia.com/", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("https://user@www.nvidia.com/", false)]
    [InlineData("https://www.nvidia.com:8443/", false)]
    [InlineData("https://www.nvidia.com./", false)]
    [InlineData("https://203.0.113.10/", false)]
    [InlineData("https://www.nvidia.com/ drivers", false)]
    [InlineData("https://evil.com\\@www.nvidia.com/", false)]
    [InlineData("/relative/path", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void 엄격한_HTTPS_URL만_받는다(string? text, bool expected)
    {
        Assert.Equal(expected, OfficialUrl.TryParse(text, out _));
    }

    /// <summary>국제화 도메인은 ASCII(퓨니코드) 호스트로 비교해 모양이 비슷한 글자로 속일 수 없다.</summary>
    [Fact]
    public void 국제화_호스트는_퓨니코드로_비교한다()
    {
        Assert.True(OfficialUrl.TryParse(HOMOGRAPH_URL, out var uri));
        Assert.StartsWith("www.xn--", OfficialUrl.AsciiHost(uri), StringComparison.Ordinal);
        Assert.False(NvidiaUrlAllowlist.IsAllowed(HOMOGRAPH_URL));
    }

    /// <summary>NVIDIA 허용 호스트는 www.nvidia.com과 *.download.nvidia.com뿐이다.</summary>
    [Theory]
    [InlineData("https://www.nvidia.com/en-us/drivers/details/279803/", true)]
    [InlineData("https://us.download.nvidia.com/Windows/617.14/617.14-desktop-win10-win11-64bit-international-dch-whql.exe", true)]
    [InlineData("https://WWW.NVIDIA.COM/", true)]
    [InlineData("https://www.nvidia.com.evil.com/", false)]
    [InlineData("https://evil.com/www.nvidia.com", false)]
    [InlineData("https://user@www.nvidia.com/", false)]
    [InlineData("https://www.nvidia.com./", false)]
    [InlineData("https://nvidia.com/", false)]
    [InlineData("https://download.nvidia.com.evil.com/x", false)]
    [InlineData("https://evildownload.nvidia.com/x", false)]
    [InlineData("http://us.download.nvidia.com/x", false)]
    public void NVIDIA_허용_호스트만_받는다(string text, bool expected)
    {
        Assert.Equal(expected, NvidiaUrlAllowlist.IsAllowed(text));
    }
}
