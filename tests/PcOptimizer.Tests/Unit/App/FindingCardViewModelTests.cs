/**
 * @file    : FindingCardViewModelTests.cs
 * @author  : rudals252
 * @brief   : 카드 모델의 판정 배지 텍스트·자세히 보기 토글·허용 URI 설정 열기/수동 경로 안내·비활성 적용 설명 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="FindingCardViewModel"/>의 표시·동작을 가짜 Finding으로 검증합니다.
/// </summary>
public sealed class FindingCardViewModelTests
{
    private readonly List<string> _launched = [];

    /// <summary>
    /// 권고가 있는 Candidate를 지정한 동작으로 만든다.
    /// </summary>
    private static Finding Candidate(params FindingAction[] actions)
    {
        return new Finding(
            id: "fake:candidate",
            category: FindingCategory.Power,
            title: "fake title",
            measured: [FakeProbe.CreateMeasurement("fake.value", 42)],
            evidence: "fake evidence",
            verdict: Verdict.Candidate,
            cannotVerifyReason: null,
            detail: null,
            recommendation: new Recommendation("fake text", "fake condition"),
            impact: new Impact("fake benefit", "fake side effect"),
            actions: actions);
    }

    /// <summary>
    /// 기록만 하는 정책으로 카드 모델을 만든다.
    /// </summary>
    private FindingCardViewModel CreateCard(Finding finding)
    {
        return new FindingCardViewModel(finding, new SettingsUriPolicy(NullAppLogger.Instance, _launched.Add));
    }

    /// <summary>판정은 배지 텍스트로도 읽을 수 있다.</summary>
    [Theory]
    [InlineData(Verdict.Candidate)]
    [InlineData(Verdict.Info)]
    public void 판정_배지_텍스트가_있다(Verdict verdict)
    {
        var finding = verdict == Verdict.Candidate
            ? Candidate()
            : new Finding("fake:info", FindingCategory.Memory, "t", [], "e", Verdict.Info, null, null, null, null, []);

        var card = CreateCard(finding);

        Assert.Equal(DisplayText.Verdict(verdict), card.VerdictText);
        Assert.False(string.IsNullOrWhiteSpace(card.VerdictText));
        Assert.False(card.HasReason);
    }

    /// <summary>자세히 보기는 측정값 상세(출처 포함) 패널을 토글한다.</summary>
    [Fact]
    public void 자세히_보기를_토글한다()
    {
        var card = CreateCard(Candidate(new ShowDetailsAction()));

        Assert.True(card.CanShowDetails);
        Assert.False(card.IsDetailsVisible);
        Assert.Equal(Strings.Button_ShowDetails, card.DetailsButtonText);

        card.ToggleDetailsCommand.Execute(null);
        Assert.True(card.IsDetailsVisible);
        Assert.Equal(Strings.Button_HideDetails, card.DetailsButtonText);
        Assert.Contains("fake", Assert.Single(card.Measurements).DetailText, StringComparison.Ordinal);

        card.ToggleDetailsCommand.Execute(null);
        Assert.False(card.IsDetailsVisible);
    }

    /// <summary>허용된 설정 URI가 있으면 [설정 열기]를 제공하고 실행한다.</summary>
    [Fact]
    public void 허용된_설정_URI만_연다()
    {
        var card = CreateCard(Candidate(new OpenSettingsAction(SettingsUriPolicy.POWER_SLEEP_SETTINGS_URI)));

        Assert.True(card.CanOpenSettings);
        Assert.False(card.ShowManualPathHint);
        card.OpenSettingsCommand.Execute(null);
        Assert.Equal([SettingsUriPolicy.POWER_SLEEP_SETTINGS_URI], _launched);
    }

    /// <summary>허용되지 않은 URI만 있으면 [설정 열기] 대신 수동 경로 안내를 보여 주고 실행하지 않는다.</summary>
    [Theory]
    [InlineData("ms-settings:privacy")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    public void 허용되지_않은_URI는_수동_안내로_대체한다(string uri)
    {
        var card = CreateCard(Candidate(new OpenSettingsAction(uri)));

        Assert.False(card.CanOpenSettings);
        Assert.False(card.OpenSettingsCommand.CanExecute(null));
        Assert.True(card.ShowManualPathHint);
        Assert.Empty(_launched);
    }

    /// <summary>[적용]은 항상 비활성이며 '자동 조치는 다음 버전 예정' 설명을 제공한다.</summary>
    [Fact]
    public void 적용은_비활성이고_설명이_있다()
    {
        var card = CreateCard(Candidate(new ApplyAction()));

        Assert.True(card.HasApplyAction);
        Assert.False(card.IsApplyEnabled);
        Assert.Equal(Strings.Tooltip_Apply, card.ApplyTooltip);
    }

    /// <summary>확인 불가 카드는 사유 문자열을 보여 준다.</summary>
    [Fact]
    public void 확인_불가는_사유를_보여_준다()
    {
        var finding = new Finding("fake:cv", FindingCategory.Memory, "t", [], "e", Verdict.CannotVerify, CannotVerifyReason.Timeout, null, null, null, []);

        var card = CreateCard(finding);

        Assert.True(card.HasReason);
        Assert.Equal(DisplayText.Reason(CannotVerifyReason.Timeout), card.ReasonText);
    }
}
