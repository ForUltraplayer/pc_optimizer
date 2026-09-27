/**
 * @file    : LinkPolicyTests.cs
 * @author  : rudals252
 * @brief   : 외부 링크 열기 정책의 허용/거부 표(공식 링크 표 호스트+경로 접두사, NVIDIA 허용 호스트, HTTP·javascript:·file:·미등록 호스트·사용자 정보·퓨니코드 유사 호스트·끝 점 거부, 대문자 호스트·HTTPS 대문자 허용)와 검증한 문자열로만 실행·실패 흡수, 관리자 권한 브라우저를 막는 비승격 셸(explorer.exe) 시작 정보 단위 테스트
 */

// 기본 패키지
using System.IO;

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

    /// <summary>비승격 셸에만 정규 URL을 전달하고 연결을 해제합니다.</summary>
    [Fact]
    public void ExistingDesktopReceivesCanonicalUrl()
    {
        var shell = new Shell();
        var policy = new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, new UnelevatedShellLauncher(() => shell));
        Assert.Equal(LinkOpenResult.Opened, policy.TryOpen("HTTPS://WWW.NVIDIA.COM/en-us/drivers/"));
        Assert.Equal("https://www.nvidia.com/en-us/drivers/", shell.Url);
        Assert.True(shell.Disposed);
    }

    /// <summary>셸 없음·다른 세션의 셸·확인 실패·서버 종료 시 관리자 실행으로 폴백하지 않습니다.</summary>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MissingOrUntrustedShellFailsClosed(int scenario)
    {
        var shell = new Shell { SessionShell = scenario != 1, Fail = scenario == 3 };
        var launcher = new UnelevatedShellLauncher(() => scenario switch
        {
            0 => null, 2 => throw new InvalidOperationException("query failed"), _ => shell,
        });
        var policy = new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, launcher);
        Assert.Equal(LinkOpenResult.Failed, policy.TryOpen("https://www.dell.com/support/home/"));
        Assert.Null(shell.Url);
        if (scenario is 1 or 3) { Assert.True(shell.Disposed); }
    }

    /// <summary>UAC를 끈 PC처럼 셸 자체가 승격돼 있어도 그 셸에 위임하며(앱이 브라우저를 직접 시작하지 않음), 승격 사실은 기록용으로 남깁니다.</summary>
    [Fact]
    public void ElevatedSessionShellStillReceivesUrl()
    {
        var shell = new Shell { Unelevated = false };
        var launcher = new UnelevatedShellLauncher(() => shell);
        var policy = new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, launcher);
        Assert.Equal(LinkOpenResult.Opened, policy.TryOpen("https://www.dell.com/support/home/"));
        Assert.Equal("https://www.dell.com/support/home/", shell.Url); Assert.True(launcher.ShellWasElevated);
    }

    /// <summary>모호한 주소는 COM 연결 전 거절해 복사 안내로 처리합니다.</summary>
    [Theory]
    [InlineData("https://www.dell.com/support/home/a,b")]
    [InlineData("https://www.dell.com/support/home/a%2Cb")]
    [InlineData("https://www.dell.com/support/home/?x=%2cb")]
    public void CommaNeverReachesShell(string url)
    {
        var calls = 0;
        var launcher = new UnelevatedShellLauncher(() => { calls++; return new Shell(); });
        var policy = new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, launcher);
        Assert.Equal(LinkOpenResult.Failed, policy.TryOpen(url));
        Assert.Equal(0, calls);
    }

    /// <summary>허용 목록 밖 주소는 셸 연결도 만들지 않습니다.</summary>
    [Fact]
    public void RefusedUrlNeverConnects()
    {
        var policy = new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance,
            new UnelevatedShellLauncher(() => throw new Xunit.Sdk.XunitException("must not connect")));
        Assert.Equal(LinkOpenResult.Refused, policy.TryOpen("https://evil.com/"));
    }

    private sealed class Shell : IDesktopShell
    {
        public bool Unelevated { get; init; } = true;
        public bool SessionShell { get; init; } = true;
        public bool Fail { get; init; }
        public bool IsSessionShell => SessionShell;
        public bool IsUnelevated => SessionShell && Unelevated;
        public string? Url { get; private set; }
        public bool Disposed { get; private set; }
        public void Open(string url) { if (Fail) { throw new InvalidOperationException("closed"); } Url = url; }
        public void Dispose() => Disposed = true;
    }
}
