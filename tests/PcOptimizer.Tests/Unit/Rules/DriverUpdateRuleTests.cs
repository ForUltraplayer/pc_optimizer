/**
 * @file    : DriverUpdateRuleTests.cs
 * @author  : rudals252
 * @brief   : NVIDIA 같은 계열 비교 규칙의 후보/같음/더 높음, 계열 모호(양쪽·어느 쪽도 아님·Studio 없음), 제품 매핑 모호, 조회 실패·설치 버전 불명, 금지 문구·링크 동작 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="DriverUpdateRule"/>을 검증합니다. 버전·날짜·URL은 테스트 예시이며 이 PC 값이 아닙니다.
/// </summary>
public sealed class DriverUpdateRuleTests
{
    private const string NAME = "테스트 NVIDIA GPU";

    private static readonly DriverUpdateRule RULE = new(DriverRuleTestData.CATALOG);

    /// <summary>Game Ready만 있는 목록(설치 616.64가 Game Ready에만 있음).</summary>
    private static readonly string[] GR_ONLY = ["617.14", "616.92", "616.64"];

    /// <summary>Studio 목록(616.64 없음).</summary>
    private static readonly string[] STUDIO = ["616.92", "616.56"];

    /// <summary>
    /// 어댑터 하나를 평가한다.
    /// </summary>
    private static Finding EvaluateSingle(FakeNvidiaAdapter adapter, ProbeStatus status = ProbeStatus.Success)
    {
        return Assert.Single(RULE.Evaluate(HardwareRuleTestData.Snapshot(DriverRuleTestData.NvidiaResult(status, adapter))));
    }

    /// <summary>
    /// Game Ready 계열로 정해지는 어댑터를 만든다.
    /// </summary>
    private static FakeNvidiaAdapter GameReadyAdapter(string installed, string[] gameReady, FakeLatest latest)
    {
        return new FakeNvidiaAdapter(NAME, DriverRuleTestData.NVIDIA_PNP, installed, GameReadyVersions: gameReady, GameReadyLatest: latest, StudioVersions: STUDIO, StudioLatest: DriverRuleTestData.STUDIO_LATEST);
    }

    /// <summary>테스트용 공식 링크 표로 규칙을 만든다(<see cref="CandidateExplanationTests"/>가 쓰는 헬퍼).</summary>
    public static DriverUpdateRule CreateRule()
    {
        return new DriverUpdateRule(DriverRuleTestData.CATALOG);
    }

    /// <summary>Candidate를 내는 스냅샷(같은 계열 최신이 설치 버전보다 높은 어댑터 하나)을 만든다.</summary>
    public static ScanSnapshot CandidateSnapshot()
    {
        return HardwareRuleTestData.Snapshot(DriverRuleTestData.NvidiaResult(ProbeStatus.Success, GameReadyAdapter("616.64", GR_ONLY, DriverRuleTestData.GR_LATEST)));
    }

    /// <summary>같은 계열 최신이 설치 버전보다 높으면 조건부 후보이며 근거·권장 조건·영향·공식 링크 두 개·자세히 보기가 있다.</summary>
    [Fact]
    public void 같은_계열_최신이_높으면_후보다()
    {
        var finding = Assert.Single(RULE.Evaluate(CandidateSnapshot()));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(FindingCategory.Driver, finding.Category);
        Assert.Equal(DriverUpdateRule.FINDING_ID_PREFIX + DriverRuleTestData.NVIDIA_PNP, finding.Id);
        Assert.Contains("그래픽 드라이버 업데이트 후보가 있어요", finding.Title, StringComparison.Ordinal);
        Assert.Contains("설치 616.64(2026-08-20)", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("최신 617.14(2026-09-22)", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("Game Ready", finding.Evidence, StringComparison.Ordinal);
        Assert.Equal("공식 배포 설명을 읽고 본인 GPU·Windows 버전 확인 후 설치", finding.Recommendation!.Condition);
        Assert.Contains("재부팅", finding.Impact!.SideEffect, StringComparison.Ordinal);
        Assert.Contains("초기화", finding.Impact.SideEffect, StringComparison.Ordinal);

        var links = finding.Actions.OfType<OpenLinkAction>().ToList();
        Assert.Equal([DriverRuleTestData.GR_LATEST.DetailsUrl, DriverRuleTestData.GR_LATEST.DownloadUrl], links.Select(link => link.Url));
        Assert.Equal(["공식 배포 설명", "공식 다운로드"], links.Select(link => link.Label));
        Assert.Contains(finding.Actions, action => action is ShowDetailsAction);
        Assert.DoesNotContain(finding.Actions, action => action is ApplyAction);
    }

    /// <summary>후보 문구에는 '권장'·'호환성 검증 완료'·고장 표현이 없다.</summary>
    [Fact]
    public void 후보_문구에_금지_표현이_없다()
    {
        var finding = EvaluateSingle(GameReadyAdapter("616.64", GR_ONLY, DriverRuleTestData.GR_LATEST));
        var text = DriverRuleTestData.VisibleText(finding);

        foreach (var phrase in DriverRuleTestData.FORBIDDEN_DRIVER_PHRASES)
        {
            Assert.DoesNotContain(phrase, text, StringComparison.Ordinal);
        }
    }

    /// <summary>Studio 계열로 정해지면 Studio 최신과 비교한다.</summary>
    [Fact]
    public void 스튜디오_계열은_스튜디오_최신과_비교한다()
    {
        var adapter = new FakeNvidiaAdapter(
            NAME, DriverRuleTestData.NVIDIA_PNP, "616.56", GameReadyVersions: ["617.14", "616.92"], GameReadyLatest: DriverRuleTestData.GR_LATEST,
            StudioVersions: STUDIO, StudioLatest: DriverRuleTestData.STUDIO_LATEST);

        var finding = EvaluateSingle(adapter);

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Contains("Studio", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("최신 616.92(2026-09-09)", finding.Evidence, StringComparison.Ordinal);
        Assert.Equal(DriverRuleTestData.STUDIO_LATEST.DetailsUrl, finding.Actions.OfType<OpenLinkAction>().First().Url);
    }

    /// <summary>설치 버전이 같은 계열 최신과 같으면 정보다(후보 아님).</summary>
    [Fact]
    public void 최신과_같으면_정보다()
    {
        var finding = EvaluateSingle(GameReadyAdapter("617.14", GR_ONLY, DriverRuleTestData.GR_LATEST));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("같은 계열 최신 버전을 사용 중", finding.Title, StringComparison.Ordinal);
        Assert.Null(finding.Recommendation);
    }

    /// <summary>설치 버전이 목록 최신보다 높으면 정보다(후보 아님).</summary>
    [Fact]
    public void 설치가_더_높으면_정보다()
    {
        var finding = EvaluateSingle(GameReadyAdapter("617.20", ["617.20", "617.14"], DriverRuleTestData.GR_LATEST));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Null(finding.Recommendation);
    }

    /// <summary>설치 버전이 두 목록에 모두 있으면 계열 모호(Ambiguous)이며, 이유와 두 계열 공식 배포 설명 링크를 준다.</summary>
    [Fact]
    public void 두_목록에_모두_있으면_모호하다()
    {
        var adapter = new FakeNvidiaAdapter(
            NAME, DriverRuleTestData.NVIDIA_PNP, "616.92", GameReadyVersions: ["617.14", "616.92"], GameReadyLatest: DriverRuleTestData.GR_LATEST,
            StudioVersions: STUDIO, StudioLatest: DriverRuleTestData.STUDIO_LATEST);

        var finding = EvaluateSingle(adapter);

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Ambiguous, finding.CannotVerifyReason);
        Assert.Contains("모두", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("616.92", finding.Detail, StringComparison.Ordinal);
        Assert.Equal(
            [DriverRuleTestData.GR_LATEST.DetailsUrl, DriverRuleTestData.STUDIO_LATEST.DetailsUrl],
            finding.Actions.OfType<OpenLinkAction>().Select(link => link.Url));
        Assert.Null(finding.Recommendation);
    }

    /// <summary>설치 버전이 어느 목록에도 없으면 모호하다.</summary>
    [Fact]
    public void 어느_목록에도_없으면_모호하다()
    {
        var finding = EvaluateSingle(GameReadyAdapter("600.10", GR_ONLY, DriverRuleTestData.GR_LATEST));

        Assert.Equal(CannotVerifyReason.Ambiguous, finding.CannotVerifyReason);
        Assert.Contains("어디에도 없어", finding.Detail, StringComparison.Ordinal);
    }

    /// <summary>Studio 목록을 받지 못했으면 Game Ready 목록에 있어도 모호하며 Game Ready 링크만 준다.</summary>
    [Fact]
    public void 스튜디오_목록이_없으면_모호하다()
    {
        var adapter = new FakeNvidiaAdapter(NAME, DriverRuleTestData.NVIDIA_PNP, "616.64", GameReadyVersions: GR_ONLY, GameReadyLatest: DriverRuleTestData.GR_LATEST);

        var finding = EvaluateSingle(adapter, ProbeStatus.Partial);

        Assert.Equal(CannotVerifyReason.Ambiguous, finding.CannotVerifyReason);
        Assert.Contains("Studio 목록을 받지 못해", finding.Detail, StringComparison.Ordinal);
        Assert.Equal([DriverRuleTestData.GR_LATEST.DetailsUrl], finding.Actions.OfType<OpenLinkAction>().Select(link => link.Url));
    }

    /// <summary>제품 목록 매핑이 정확히 하나가 아니면 모호하며 NVIDIA 공식 드라이버 페이지 링크를 준다.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void 제품_매핑이_하나가_아니면_모호하다(long matchCount)
    {
        var adapter = new FakeNvidiaAdapter(NAME, DriverRuleTestData.NVIDIA_PNP, "616.56", State: NvidiaLookupProbeContract.STATE_PRODUCT_AMBIGUOUS, MatchCount: matchCount);

        var finding = EvaluateSingle(adapter);

        Assert.Equal(CannotVerifyReason.Ambiguous, finding.CannotVerifyReason);
        Assert.Contains(matchCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "개", finding.Evidence, StringComparison.Ordinal);
        Assert.Equal(["https://www.nvidia.com/en-us/drivers/"], finding.Actions.OfType<OpenLinkAction>().Select(link => link.Url));
    }

    /// <summary>어댑터별 조회 실패는 기록된 사유의 확인 불가다.</summary>
    [Theory]
    [InlineData(nameof(CannotVerifyReason.NetworkFailed), CannotVerifyReason.NetworkFailed)]
    [InlineData(nameof(CannotVerifyReason.ProbeError), CannotVerifyReason.ProbeError)]
    [InlineData("알 수 없는 값", CannotVerifyReason.ProbeError)]
    public void 조회_실패는_사유대로_확인_불가다(string recorded, CannotVerifyReason expected)
    {
        var adapter = new FakeNvidiaAdapter(NAME, DriverRuleTestData.NVIDIA_PNP, "616.56", State: NvidiaLookupProbeContract.STATE_FAILED, FailureReason: recorded);

        var finding = EvaluateSingle(adapter, ProbeStatus.Partial);

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(expected, finding.CannotVerifyReason);
    }

    /// <summary>설치 버전을 NVIDIA 표기로 바꾸지 못했으면 PartialData다.</summary>
    [Fact]
    public void 설치_버전을_모르면_부분_데이터다()
    {
        var adapter = new FakeNvidiaAdapter(NAME, DriverRuleTestData.NVIDIA_PNP, null, State: NvidiaLookupProbeContract.STATE_VERSION_UNKNOWN, MatchCount: 0);

        var finding = EvaluateSingle(adapter);

        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>같은 계열 최신 항목이 없으면(모두 베타 등) 확인 불가(PartialData)다.</summary>
    [Fact]
    public void 계열_최신_항목이_없으면_부분_데이터다()
    {
        var adapter = new FakeNvidiaAdapter(NAME, DriverRuleTestData.NVIDIA_PNP, "616.64", GameReadyVersions: GR_ONLY, GameReadyLatest: null, StudioVersions: STUDIO, StudioLatest: DriverRuleTestData.STUDIO_LATEST);

        var finding = EvaluateSingle(adapter);

        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
    }

    /// <summary>프로브가 건너뜀·실패면 규칙은 아무것도 만들지 않는다(엔진의 상태 카드와 설치 정보 카드가 남음).</summary>
    [Theory]
    [InlineData(ProbeStatus.Skipped)]
    [InlineData(ProbeStatus.Failed)]
    [InlineData(ProbeStatus.Cancelled)]
    public void 프로브가_성공하지_않으면_규칙은_빈_결과다(ProbeStatus status)
    {
        var snapshot = HardwareRuleTestData.Snapshot(DriverRuleTestData.NvidiaResult(status));

        Assert.Empty(RULE.Evaluate(snapshot));
    }

    /// <summary>NVIDIA 어댑터가 없으면(0개) 아무것도 만들지 않는다.</summary>
    [Fact]
    public void 어댑터가_없으면_빈_결과다()
    {
        Assert.Empty(RULE.Evaluate(HardwareRuleTestData.Snapshot(DriverRuleTestData.NvidiaResult(ProbeStatus.Success))));
    }
}
