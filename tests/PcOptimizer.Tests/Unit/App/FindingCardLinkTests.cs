/**
 * @file    : FindingCardLinkTests.cs
 * @author  : rudals252
 * @brief   : 카드의 공식 링크 버튼(허용된 OpenLink만 이름표와 함께 표시·기본 이름표·숨긴 링크 안내, 만들 때 자동으로 열지 않음, 누르면 검증한 URL 열기, 실행 실패 안내) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Rules;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="FindingCardViewModel"/>의 링크 버튼을 검증합니다. 실제 브라우저는 열지 않습니다.
/// </summary>
public sealed class FindingCardLinkTests
{
    private const string DELL_URL = "https://www.dell.com/support/home/";
    private const string NVIDIA_DETAILS_URL = "https://www.nvidia.com/en-us/drivers/details/279803/";

    private readonly List<string> _launched = [];

    /// <summary>
    /// 링크 동작을 가진 Info Finding을 만든다.
    /// </summary>
    private static Finding WithLinks(params FindingAction[] actions)
    {
        return new Finding("fake:links", FindingCategory.Driver, "링크 카드", [], "근거", Verdict.Info, null, null, null, null, actions);
    }

    /// <summary>
    /// 실행 요청을 기록하는 정책으로 카드를 만든다.
    /// </summary>
    private FindingCardViewModel CreateCard(Finding finding, Action<string>? launcher = null)
    {
        var launch = launcher ?? _launched.Add;
        return new FindingCardViewModel(
            finding, new SettingsUriPolicy(NullAppLogger.Instance, _ => { }), new LinkPolicy(DriverRuleTestData.CATALOG, NullAppLogger.Instance, launch));
    }

    /// <summary>허용된 링크만 순서대로 버튼으로 보여 주고(이름표 없으면 기본 문구), 나머지는 숨긴 개수를 안내하며, 만들 때 아무것도 열지 않는다.</summary>
    [Fact]
    public void 허용된_링크만_버튼으로_보여_준다()
    {
        var card = CreateCard(WithLinks(
            new OpenLinkAction(DELL_URL, "Dell 지원"),
            new OpenLinkAction(NVIDIA_DETAILS_URL),
            new OpenLinkAction("https://evil.example.com/"),
            new ShowDetailsAction()));

        Assert.True(card.HasLinks);
        Assert.Equal(["Dell 지원", Strings.Button_OpenLinkDefault], card.Links.Select(link => link.Label));
        Assert.Equal([DELL_URL, NVIDIA_DETAILS_URL], card.Links.Select(link => link.Url));
        Assert.Equal(1, card.HiddenLinkCount);
        Assert.True(card.HasHiddenLinks);
        Assert.NotNull(card.HiddenLinksNote);
        Assert.Empty(_launched);
    }

    /// <summary>버튼을 누르면 정책을 다시 거쳐 검증한 URL을 연다.</summary>
    [Fact]
    public void 누르면_검증한_링크를_연다()
    {
        var card = CreateCard(WithLinks(new OpenLinkAction(DELL_URL, "Dell 지원")));

        card.Links[0].OpenCommand.Execute(null);

        Assert.Equal([DELL_URL], _launched);
        Assert.False(card.HasLinkStatus);
    }

    /// <summary>셸 실행이 실패하면 카드에 안내를 보여 준다(예외 없음).</summary>
    [Fact]
    public void 실행_실패는_안내한다()
    {
        var card = CreateCard(WithLinks(new OpenLinkAction(DELL_URL, "Dell 지원")), _ => throw new InvalidOperationException("no browser"));

        card.Links[0].OpenCommand.Execute(null);

        Assert.Equal(Strings.Link_OpenFailed, card.LinkStatusText);
        Assert.True(card.HasLinkStatus);
    }

    /// <summary>링크가 없으면 링크 영역과 안내가 없다.</summary>
    [Fact]
    public void 링크가_없으면_표시하지_않는다()
    {
        var card = CreateCard(WithLinks(new ShowDetailsAction()));

        Assert.False(card.HasLinks);
        Assert.False(card.HasHiddenLinks);
        Assert.Null(card.HiddenLinksNote);
    }
}
