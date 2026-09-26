/**
 * @file    : DisplayRefreshRuleTests.cs
 * @author  : rudals252
 * @brief   : 디스플레이 주사율 규칙의 후보/현재 모드 정보/모호(복제·원격·가상·인터레이스·DRR)/부분 데이터 분기, 노트북 조건 문구, 전원 상태 근거, 다중 모니터 ID 안정성, 금지 문구 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="DisplayRefreshRule"/>이 스펙 §5 디스플레이 행과 §9 디스플레이 테스트 항목을 따르는지 검증합니다.
/// 해상도·주사율·장치 경로는 모두 테스트용 예시이며 이 PC 값이 아닙니다.
/// </summary>
public sealed class DisplayRefreshRuleTests
{
    private const string PATH_A = @"\\?\DISPLAY#TST0001#5&aaaa&0&UID1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string PATH_B = @"\\?\DISPLAY#TST0002#5&aaaa&0&UID2#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string PATH_C = @"\\?\DISPLAY#TST0003#5&aaaa&0&UID3#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string LAPTOP = "10";
    private const string DESKTOP = "3";

    private static readonly DisplayRefreshRule RULE = new();

    /// <summary>어떤 경우에도 나오면 안 되는 표현(구동 가능·안정성 확정 금지).</summary>
    private static readonly string[] FORBIDDEN_PHRASES = ["구동 가능 확정", "확정", "검증 완료", "검증했", "보장", "안정적으로 동작해요"];

    /// <summary>
    /// 규칙의 모든 분기 입력(금지 문구 검사용).
    /// </summary>
    public static TheoryData<FakeDisplay, bool> AllBranches()
    {
        return new TheoryData<FakeDisplay, bool>
        {
            { new FakeDisplay(PATH_A, RefreshHz: 120, SignalHz: 120, SameModeRates: [60, 120, 130]), false },
            { new FakeDisplay(PATH_A, RefreshHz: 60, SignalHz: 59.94, SameModeRates: [60]), false },
            { new FakeDisplay(PATH_A, RefreshHz: 120, SameModeRates: [60, 144], Cloned: true), false },
            { new FakeDisplay(PATH_A, RefreshHz: 120, SameModeRates: [60, 144]), true },
            { new FakeDisplay(PATH_A, RefreshHz: 120, SignalHz: 60, SameModeRates: [60, 120, 144]), false },
            { new FakeDisplay(PATH_A, RefreshHz: null, SameModeRates: [60]), false },
        };
    }

    /// <summary>
    /// 디스플레이 결과(와 선택적 전원 결과)로 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(bool remote, ProbeResult? power, params FakeDisplay[] displays)
    {
        var results = new List<ProbeResult> { HardwareRuleTestData.DisplayResult(remote, displays) };
        if (power is not null)
        {
            results.Add(power);
        }

        return RULE.Evaluate(HardwareRuleTestData.Snapshot([.. results]));
    }

    /// <summary>같은 조건에서 1Hz 넘게 높은 모드가 있으면 조건부 Candidate를 내고 설정 열기를 제공한다.</summary>
    [Fact]
    public void 같은_조건의_더_높은_주사율은_후보다()
    {
        var display = new FakeDisplay(PATH_A, Name: "모니터 A", RefreshHz: 120, SignalHz: 120, SameModeRates: [60, 100, 120, 130]);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(FindingCategory.Display, finding.Category);
        Assert.Equal(DisplayRefreshRule.FINDING_ID_PREFIX + PATH_A, finding.Id);
        Assert.Contains("모니터 A", finding.Title, StringComparison.Ordinal);
        Assert.Contains("130", finding.Recommendation!.Text, StringComparison.Ordinal);
        Assert.Contains("130", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("120", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("HDR", finding.Recommendation.Condition, StringComparison.Ordinal);
        Assert.Contains("색", finding.Recommendation.Condition, StringComparison.Ordinal);
        Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: DisplayRefreshRule.DISPLAY_SETTINGS_URI });
        Assert.Contains(finding.Actions, a => a is ShowDetailsAction);
        Assert.NotNull(finding.Impact);
    }

    /// <summary>59.94/60, 59/60처럼 1Hz 이하 차이는 개선 후보가 아니라 현재 모드 Info다.</summary>
    [Theory]
    [InlineData(59, 59.94, new[] { 59, 60 })]
    [InlineData(60, 59.94, new[] { 59, 60 })]
    [InlineData(60, 60.0, new[] { 60, 61 })]
    [InlineData(144, 143.97, new[] { 60, 120, 144 })]
    public void 일Hz_이하_차이는_후보가_아니다(int currentHz, double signalHz, int[] rates)
    {
        var display = new FakeDisplay(PATH_A, RefreshHz: currentHz, SignalHz: signalHz, SameModeRates: rates);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Null(finding.Recommendation);
        Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: DisplayRefreshRule.DISPLAY_SETTINGS_URI });
    }

    /// <summary>현재 모드 Info는 현재 모드와 같은 조건 최고 주사율을 알린다.</summary>
    [Fact]
    public void 현재_모드_정보는_최고_주사율을_포함한다()
    {
        var display = new FakeDisplay(PATH_A, Width: 3440, Height: 1440, RefreshHz: 165, SignalHz: 164.9, SameModeRates: [60, 100, 165]);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("3440", finding.Title, StringComparison.Ordinal);
        Assert.Contains("165", finding.Title, StringComparison.Ordinal);
        Assert.Contains("165", finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>복제·원격·가상 어댑터·가상 출력 기술·인터레이스·DRR 불일치는 후보 대신 CannotVerify(Ambiguous)이며 사유를 Detail에 적는다.</summary>
    [Theory]
    [InlineData("cloned")]
    [InlineData("remote")]
    [InlineData("virtualName")]
    [InlineData("nonPci")]
    [InlineData("indirectVirtual")]
    [InlineData("interlaced")]
    [InlineData("drr")]
    public void 모호한_표시_상태는_확인_불가다(string condition)
    {
        var display = new FakeDisplay(PATH_A, RefreshHz: 120, SignalHz: 120, SameModeRates: [60, 120, 144]);
        var remote = false;
        string expectedDetail;
        switch (condition)
        {
            case "cloned":
                display = display with { Cloned = true };
                expectedDetail = CoreStrings.Display_Detail_Cloned;
                break;
            case "remote":
                remote = true;
                expectedDetail = CoreStrings.Display_Detail_Remote;
                break;
            case "virtualName":
                display = display with { AdapterName = "Parsec Virtual Display Adapter", AdapterPath = @"\\?\PCI#VEN_1234#x#{guid}" };
                expectedDetail = "Parsec Virtual Display Adapter";
                break;
            case "nonPci":
                display = display with { AdapterName = "테스트 가상 표시", AdapterPath = @"\\?\ROOT#DISPLAY#0000#{guid}" };
                expectedDetail = "테스트 가상 표시";
                break;
            case "indirectVirtual":
                display = display with { OutputTechnology = DisplayProbeContract.OUTPUT_TECHNOLOGY_INDIRECT_VIRTUAL };
                expectedDetail = "테스트 GPU";
                break;
            case "interlaced":
                display = display with { Interlaced = true };
                expectedDetail = CoreStrings.Display_Detail_Interlaced;
                break;
            default:
                display = display with { SignalHz = 60 };
                expectedDetail = "DRR";
                break;
        }

        var finding = Assert.Single(Evaluate(remote, null, display));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Ambiguous, finding.CannotVerifyReason);
        Assert.Contains(expectedDetail, finding.Detail, StringComparison.Ordinal);
        Assert.Null(finding.Recommendation);
    }

    /// <summary>가상 어댑터(예: 원격 데스크톱용 가상 디스플레이)는 더 높은 모드가 있어도 후보를 만들지 않는다.</summary>
    [Fact]
    public void 가상_어댑터는_거짓_후보를_만들지_않는다()
    {
        var display = new FakeDisplay(
            PATH_B,
            AdapterName: "Parsec Virtual Display Adapter",
            AdapterPath: @"\\?\ROOT#DISPLAY#0000#{guid}",
            RefreshHz: 60,
            SameModeRates: [60, 240]);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.NotEqual(Verdict.Candidate, finding.Verdict);
    }

    /// <summary>노트북형 섀시면 후보 조건에 '전원 연결 시' 확인을 붙이고, 데스크톱이면 붙이지 않는다.</summary>
    [Theory]
    [InlineData(LAPTOP, true)]
    [InlineData(DESKTOP, false)]
    public void 노트북이면_전원_연결_조건을_붙인다(string chassis, bool expectLaptopText)
    {
        var display = new FakeDisplay(PATH_A, RefreshHz: 60, SameModeRates: [60, 144]);
        var power = HardwareRuleTestData.PowerResult([chassis], PowerProbeContract.AC_LINE_OFFLINE);

        var finding = Assert.Single(Evaluate(false, power, display));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(expectLaptopText, finding.Recommendation!.Condition.Contains("전원 연결 시", StringComparison.Ordinal));
        Assert.Contains("HDR", finding.Recommendation.Condition, StringComparison.Ordinal);
    }

    /// <summary>전원 프로브의 AC/배터리 상태가 있으면 근거와 측정값에 함께 보여 준다.</summary>
    [Fact]
    public void AC_상태를_근거에_포함한다()
    {
        var display = new FakeDisplay(PATH_A, RefreshHz: 60, SameModeRates: [60, 144]);
        var power = HardwareRuleTestData.PowerResult([DESKTOP], PowerProbeContract.AC_LINE_ONLINE);

        var finding = Assert.Single(Evaluate(false, power, display));

        Assert.Contains(CoreStrings.Power_Supply_Ac, finding.Evidence, StringComparison.Ordinal);
        Assert.Contains(finding.Measured, m => m.Name == PowerProbeContract.AC_LINE_STATUS);
    }

    /// <summary>전원 상태를 모르면 AC로 추정하지 않고 '알 수 없음'으로 표시한다.</summary>
    [Fact]
    public void 전원_상태를_모르면_알_수_없음이다()
    {
        var display = new FakeDisplay(PATH_A, RefreshHz: 60, SameModeRates: [60, 144]);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.Contains(CoreStrings.Power_Supply_Unknown, finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(CoreStrings.Power_Supply_Ac, finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>다중 모니터 Finding ID는 모니터 장치 경로에서 오며 열거 순서·GDI 순번이 바뀌어도 같은 모니터는 같은 ID다.</summary>
    [Fact]
    public void 다중_모니터_ID는_순서와_무관하게_유지된다()
    {
        var a = new FakeDisplay(PATH_A, Name: "A", GdiName: @"\\.\DISPLAY1", RefreshHz: 60, SameModeRates: [60]);
        var b = new FakeDisplay(PATH_B, Name: "B", GdiName: @"\\.\DISPLAY2", RefreshHz: 120, SameModeRates: [60, 120, 130]);
        var c = new FakeDisplay(PATH_C, Name: "C", GdiName: @"\\.\DISPLAY3", RefreshHz: 144, SameModeRates: [144]);

        var first = Evaluate(false, null, a, b, c).ToDictionary(f => f.Id, f => f.Verdict);
        var reordered = Evaluate(
            false,
            null,
            c with { GdiName = @"\\.\DISPLAY1" },
            a with { GdiName = @"\\.\DISPLAY3" },
            b with { GdiName = @"\\.\DISPLAY2" }).ToDictionary(f => f.Id, f => f.Verdict);

        Assert.Equal(3, first.Count);
        Assert.Equal(first.OrderBy(p => p.Key, StringComparer.Ordinal), reordered.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal(Verdict.Candidate, first[DisplayRefreshRule.FINDING_ID_PREFIX + PATH_B]);
        Assert.All(first.Keys, id => Assert.DoesNotContain(@"\\.\DISPLAY", id, StringComparison.Ordinal));
    }

    /// <summary>모니터 장치 경로가 없으면 어댑터 경로와 대상 ID로 ID를 만들고 GDI 순번은 쓰지 않는다.</summary>
    [Fact]
    public void 장치_경로가_없으면_어댑터와_대상_ID로_식별한다()
    {
        var display = new FakeDisplay(null, GdiName: @"\\.\DISPLAY7", RefreshHz: 60, SameModeRates: [60], TargetId: 4353);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.StartsWith(DisplayRefreshRule.FINDING_ID_PREFIX, finding.Id, StringComparison.Ordinal);
        Assert.Contains("4353", finding.Id, StringComparison.Ordinal);
        Assert.DoesNotContain("DISPLAY7", finding.Id, StringComparison.Ordinal);
    }

    /// <summary>현재 모드나 같은 조건 모드 목록이 없으면 CannotVerify(PartialData)다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 모드_정보가_없으면_부분_데이터다(bool missingCurrent)
    {
        var display = missingCurrent
            ? new FakeDisplay(PATH_A, RefreshHz: null, SameModeRates: [60])
            : new FakeDisplay(PATH_A, RefreshHz: 60, SameModeRates: null);

        var finding = Assert.Single(Evaluate(false, null, display));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>프로브가 실패·건너뜀이면 규칙은 아무것도 만들지 않는다(상태 변환기가 CannotVerify로 드러냄).</summary>
    [Theory]
    [InlineData(ProbeStatus.Failed)]
    [InlineData(ProbeStatus.Skipped)]
    [InlineData(ProbeStatus.Cancelled)]
    public void 실패한_프로브는_판정하지_않는다(ProbeStatus status)
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(DisplayProbeContract.PROBE_ID, status));

        Assert.Empty(RULE.Evaluate(snapshot));
    }

    /// <summary>어떤 분기에서도 '구동 가능 확정'·안정성 검증 문구를 쓰지 않는다.</summary>
    [Theory]
    [MemberData(nameof(AllBranches))]
    public void 금지_문구가_없다(FakeDisplay display, bool remote)
    {
        var power = HardwareRuleTestData.PowerResult([LAPTOP], PowerProbeContract.AC_LINE_ONLINE);

        foreach (var finding in Evaluate(remote, power, display))
        {
            var text = RuleTestData.AllText(finding);
            Assert.All(FORBIDDEN_PHRASES, phrase => Assert.DoesNotContain(phrase, text, StringComparison.Ordinal));
        }
    }
}
