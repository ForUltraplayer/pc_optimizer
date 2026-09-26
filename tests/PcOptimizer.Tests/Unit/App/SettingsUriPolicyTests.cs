/**
 * @file    : SettingsUriPolicyTests.cs
 * @author  : rudals252
 * @brief   : 설정 URI 허용 목록 정책(허용 URI만 실행, 규칙이 내는 설정 URI는 모두 허용, file:/http:/미등록 ms-settings·windowsdefender 등 거부, P6 Windows 업데이트 설정 허용) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="SettingsUriPolicy"/>가 코드에 정의한 설정 URI만 실행하는지 검증합니다. 실제 프로세스는 띄우지 않습니다.
/// </summary>
public sealed class SettingsUriPolicyTests
{
    private readonly List<string> _launched = [];

    /// <summary>
    /// 실행 요청을 기록만 하는 정책을 만든다.
    /// </summary>
    private SettingsUriPolicy CreatePolicy()
    {
        return new SettingsUriPolicy(NullAppLogger.Instance, _launched.Add);
    }

    /// <summary>허용 목록의 URI는 통과하고 실행된다.</summary>
    [Theory]
    [InlineData("ms-settings:display")]
    [InlineData("ms-settings:powersleep")]
    [InlineData("ms-settings:display-advancedgraphics")]
    [InlineData("ms-settings:gaming-gamemode")]
    [InlineData("ms-settings:storagesense")]
    [InlineData("ms-settings:startupapps")]
    [InlineData("ms-settings:windowsupdate-optionalupdates")]
    public void 허용된_URI는_실행한다(string uri)
    {
        var policy = CreatePolicy();

        Assert.True(SettingsUriPolicy.IsAllowed(uri));
        Assert.True(policy.TryOpen(uri));
        Assert.Equal([uri], _launched);
    }

    /// <summary>대소문자만 다른 허용 URI는 허용 목록의 정식 문자열로 실행한다.</summary>
    [Fact]
    public void 대소문자가_달라도_정식_URI로_실행한다()
    {
        var policy = CreatePolicy();

        Assert.True(policy.TryOpen("MS-SETTINGS:DISPLAY"));
        Assert.Equal(["ms-settings:display"], _launched);
    }

    /// <summary>허용 목록 밖의 모든 URI·경로는 거부하고 실행하지 않는다.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("file:ms-settings:display")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/ms-settings:display")]
    [InlineData("ms-settings:privacy")]
    [InlineData("ms-settings:windowsupdate-action")]
    [InlineData("ms-settings:display?extra=1")]
    [InlineData("ms-settings:display ")]
    [InlineData(" ms-settings:display")]
    [InlineData("ms-settings:display\0")]
    [InlineData("ms-settings:")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("cmd.exe /c calc")]
    [InlineData("windowsdefender://coreisolation")]
    [InlineData("ms-settings:display-advancedgraphics-top")]
    [InlineData("ms-settings:storagesense;calc")]
    public void 허용되지_않은_URI는_거부한다(string? uri)
    {
        var policy = CreatePolicy();

        Assert.False(SettingsUriPolicy.IsAllowed(uri));
        Assert.False(policy.TryOpen(uri));
        Assert.Empty(_launched);
    }

    /// <summary>실행기가 실패해도 예외를 던지지 않고 false를 돌려준다.</summary>
    [Fact]
    public void 실행_실패는_false다()
    {
        var policy = new SettingsUriPolicy(NullAppLogger.Instance, _ => throw new InvalidOperationException("launcher failed"));

        Assert.False(policy.TryOpen("ms-settings:display"));
    }

    /// <summary>규칙이 Finding에 붙이는 설정 URI는 모두 허용 목록에 있다(없으면 설정 열기 대신 수동 안내만 보이게 됨).</summary>
    [Theory]
    [InlineData(PowerPlanRule.POWER_SETTINGS_URI)]
    [InlineData(DisplayRefreshRule.DISPLAY_SETTINGS_URI)]
    [InlineData(GraphicsSettingsRule.HAGS_SETTINGS_URI)]
    [InlineData(GraphicsSettingsRule.GAME_MODE_SETTINGS_URI)]
    [InlineData(StorageSpaceRule.STORAGE_SETTINGS_URI)]
    [InlineData(StartupItemsRule.STARTUP_SETTINGS_URI)]
    [InlineData(WindowsUpdateDriverRule.WINDOWS_UPDATE_SETTINGS_URI)]
    public void 규칙이_쓰는_설정_URI는_허용_목록에_있다(string uri)
    {
        Assert.True(SettingsUriPolicy.IsAllowed(uri));
    }
}
