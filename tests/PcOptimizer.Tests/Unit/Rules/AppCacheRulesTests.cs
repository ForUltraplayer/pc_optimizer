/**
 * @file    : AppCacheRulesTests.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 앱 카드(검토 규칙만 캐시 문구·설정 열기, 커뮤니티 규칙은 중립 문구·상세 보기, 같은 앱 라벨 합치기, 파일 없음·합친 앱만 요약에 접기, 관측 실패 앱은 사유별 확인 불가 카드, 개인정보 관련 규칙 제외, 부분·관리자 검사 범위), Adobe·UNC 설정 확인 불가, Squirrel 버전 폴더 정보, 규칙 목록 요약(수·사유·접은 앱 수·확인 불가 앱 수·프로필 목록 없음·무결성 실패·보호 정책 무효)과 금지 문구 부재를 가짜 스냅샷으로 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="AppCacheRule"/>, <see cref="SquirrelVersionFoldersRule"/>, <see cref="RuleCatalogSummaryRule"/>를 검증합니다.
/// </summary>
public sealed class AppCacheRulesTests
{
    private const string COMMUNITY_ID = "winapp2:Discord";
    private const string NPM_ID = "supplement:npm cache";

    /// <summary>
    /// 규칙 하나만 있는 스냅샷으로 앱 캐시 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(params FakeAppRule[] rules)
    {
        return new AppCacheRule().Evaluate(AppCacheTestData.Snapshot(ProbeStatus.Success, AppCacheTestData.Catalog(), AppCacheTestData.Rules(rules)));
    }

    /// <summary>
    /// 금지 문구가 없는지 확인한다.
    /// </summary>
    private static void AssertNoForbiddenPhrases(IEnumerable<Finding> findings)
    {
        foreach (var finding in findings)
        {
            var text = AppCacheTestData.Text(finding);
            foreach (var phrase in AppCacheTestData.FORBIDDEN_PHRASES)
            {
                Assert.DoesNotContain(phrase, text, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// 규칙 목록 요약을 평가한다.
    /// </summary>
    private static Finding Summary(params FakeAppRule[] rules)
    {
        return Assert.Single(new RuleCatalogSummaryRule().Evaluate(AppCacheTestData.Snapshot(ProbeStatus.Success, AppCacheTestData.Catalog(), AppCacheTestData.Rules(rules))));
    }

    /// <summary>검토하지 않은 커뮤니티 규칙 카드: 앱 이름·관측 크기, "캐시" 대신 중립 문구, 영향 미확인, 상세 보기만(저장소 설정 열기 없음). 경로는 문장에 없고 측정값에만 있다.</summary>
    [Fact]
    public void 커뮤니티_규칙_카드는_중립_문구와_상세_보기만_제공한다()
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(COMMUNITY_ID, "Discord", AppCacheProbeContract.RULE_STATE_OBSERVED, 1_500_000, 42, SharedWith: ["winapp2:Discord Cache"])));

        Assert.Equal(AppCacheRule.FINDING_ID_PREFIX + "Discord", finding.Id);
        Assert.Equal(FindingCategory.AppCache, finding.Category);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal("Discord: 규칙 위치의 파일 관측 1.5 MB", finding.Title);
        Assert.DoesNotContain("캐시", finding.Title, StringComparison.Ordinal);
        Assert.Contains("42", finding.Evidence, StringComparison.Ordinal);
        Assert.Equal("영향 미확인", finding.Impact!.Benefit);
        Assert.Contains("영향 미확인", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains(COMMUNITY_ID + ": 1.5 MB", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Discord Cache", finding.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\", AppCacheTestData.Text(finding), StringComparison.Ordinal);
        Assert.Contains(finding.Measured, m => m.Name.EndsWith(AppCacheProbeContract.FIELD_PATHS, StringComparison.Ordinal));
        Assert.Equal([typeof(ShowDetailsAction)], finding.Actions.Select(a => a.GetType()));
        Assert.Null(finding.Recommendation);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>검토한 보충 규칙 카드만 캐시 문구와 저장소 설정 열기를 쓰고, 메타데이터 영향과 사용자 설정 경로/기본 위치만 확인 범위를 밝힌다.</summary>
    [Theory]
    [InlineData(AppCacheProbeContract.CONFIG_SOURCE_USER, "사용자 설정 경로")]
    [InlineData(AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY, "기본 위치만 확인")]
    public void 보충_규칙_카드는_캐시_문구와_검토_영향과_설정_범위를_표시한다(string configSource, string expected)
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(
            NPM_ID, "npm cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 2_000_000_000, 10, Origin: AppCacheProbeContract.ORIGIN_SUPPLEMENT,
            ConfigSource: configSource, ConfigApp: "npm", Impact: ("다음 설치 때 받을 패키지 사본", "오프라인 설치가 안 될 수 있음", "npm install 때 다시 받음"), App: "npm")));

        Assert.Equal(AppCacheRule.FINDING_ID_PREFIX + "npm", finding.Id);
        Assert.Equal("npm: 캐시·임시 파일 후보 관측 2.0 GB", finding.Title);
        Assert.Equal("다음 설치 때 받을 패키지 사본", finding.Impact!.Benefit);
        Assert.Equal("오프라인 설치가 안 될 수 있음", finding.Impact.SideEffect);
        Assert.Contains(expected, finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("테스트 버전 메모", finding.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("영향 미확인", AppCacheTestData.Text(finding), StringComparison.Ordinal);
        Assert.Equal([typeof(ShowDetailsAction), typeof(OpenSettingsAction)], finding.Actions.Select(a => a.GetType()));
        Assert.Equal(AppCacheRule.STORAGE_SETTINGS_URI, ((OpenSettingsAction)finding.Actions[1]).Uri);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>
    /// 같은 앱 라벨(검토한 보충 규칙의 appLabel과 같은 Section 값의 커뮤니티 규칙)은 카드 하나로 합치고 규칙별 크기를 상세에 나열한다.
    /// 검토 규칙과 커뮤니티 규칙이 섞여 파일을 셌으면 중립 문구·상세 보기만이다. 커뮤니티 Section 묶음은 실제 파싱 항목으로
    /// <c>AppCacheProbeTests.같은_Section의_커뮤니티_규칙은_앱_카드_하나로_합친다</c>에서 검증한다.
    /// </summary>
    [Fact]
    public void 같은_앱_라벨의_규칙은_카드_하나로_합친다()
    {
        var findings = Evaluate(
            new FakeAppRule("supplement:Mixed", "Mixed", AppCacheProbeContract.RULE_STATE_OBSERVED, 10, 1, Origin: AppCacheProbeContract.ORIGIN_SUPPLEMENT, Impact: ("a", "b", "c"), App: "Mixed"),
            new FakeAppRule("winapp2:Mixed Logs", "Mixed Logs", AppCacheProbeContract.RULE_STATE_OBSERVED, 20, 2, App: "Mixed"),
            new FakeAppRule("winapp2:Mixed Extra", "Mixed Extra", AppCacheProbeContract.RULE_STATE_ABSENT, App: "Mixed"));

        var mixed = Assert.Single(findings);
        Assert.Equal(AppCacheRule.FINDING_ID_PREFIX + "Mixed", mixed.Id);
        Assert.Equal("Mixed: 규칙 위치의 파일 관측 30 B", mixed.Title);
        Assert.Contains("supplement:Mixed: 10 B", mixed.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Mixed Logs: 20 B", mixed.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Mixed Extra: 이 규칙 위치에서는 관측한 파일이 없어요", mixed.Detail!, StringComparison.Ordinal);
        Assert.Equal([typeof(ShowDetailsAction)], mixed.Actions.Select(a => a.GetType()));
    }

    /// <summary>
    /// 관측한 파일이 없는 앱 중 파일 없음(위치 없음·0바이트·규칙 제외)과 겹쳐 합친 앱만 카드가 아니고 요약에 개수로 접힌다.
    /// 관측이 실패한 앱(보호·접근 거부·시간 초과)은 접지 않고 요약에는 앱별 확인 불가 카드 수만 밝힌다.
    /// </summary>
    [Fact]
    public void 파일_없음과_합친_앱만_요약에_접힌다()
    {
        FakeAppRule[] rules =
        [
            new(COMMUNITY_ID, "Discord", AppCacheProbeContract.RULE_STATE_OBSERVED, 100, 1),
            new("winapp2:Absent", "Absent App", AppCacheProbeContract.RULE_STATE_ABSENT),
            new("winapp2:Empty", "Empty App", AppCacheProbeContract.RULE_STATE_OBSERVED, 0, 0),
            new("winapp2:Excluded", "Excluded App", AppCacheProbeContract.RULE_STATE_EXCLUDED),
            new("winapp2:Merged", "Merged App", AppCacheProbeContract.RULE_STATE_MERGED, MergedTargets: 1),
            new("winapp2:Protected", "Protected App", AppCacheProbeContract.RULE_STATE_PROTECTED, ProtectedTargets: 2),
            new("winapp2:Denied", "Denied App", AppCacheProbeContract.RULE_STATE_ACCESS_DENIED),
            new("winapp2:Slow", "Slow App", AppCacheProbeContract.RULE_STATE_TIMED_OUT),
        ];

        var findings = Evaluate(rules);
        var summary = Summary(rules);

        Assert.Equal(
            ["Denied App", "Discord", "Protected App", "Slow App"],
            findings.Select(f => f.Id[AppCacheRule.FINDING_ID_PREFIX.Length..]).Order(StringComparer.Ordinal));
        Assert.Contains("관측한 파일이 있는 앱 1개", summary.Detail!, StringComparison.Ordinal);
        Assert.Contains("파일이 관측되지 않은 앱 3개", summary.Detail!, StringComparison.Ordinal);
        Assert.Contains("같은 위치라 합친 앱 1개", summary.Detail!, StringComparison.Ordinal);
        Assert.Contains("확인하지 못한 앱 3개", summary.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("재지 않은 앱", summary.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 관측이 실패한 앱은 사유와 함께 앱 단위 확인 불가 카드다: 접근 거부 → AccessDenied, 시간 초과 → Timeout,
    /// 검사하지 못함·파일 없는 부분 관측 → PartialData, 보호 폴더(다른 사용자 위치 포함) → Unsupported(보호 문구). 크기를 말하지 않고 상세 보기만 준다.
    /// </summary>
    [Theory]
    [InlineData(AppCacheProbeContract.RULE_STATE_ACCESS_DENIED, CannotVerifyReason.AccessDenied, "접근이 거부", "접근 거부")]
    [InlineData(AppCacheProbeContract.RULE_STATE_TIMED_OUT, CannotVerifyReason.Timeout, "제한 시간", "시간 초과")]
    [InlineData(AppCacheProbeContract.RULE_STATE_NOT_OBSERVED, CannotVerifyReason.PartialData, "확인하지 못했어요", "검사하지 못함")]
    [InlineData(AppCacheProbeContract.RULE_STATE_PARTIAL, CannotVerifyReason.PartialData, "확인하지 못했어요", "검사하지 못함")]
    [InlineData(AppCacheProbeContract.RULE_STATE_PROTECTED, CannotVerifyReason.Unsupported, "보호 폴더", "보호 폴더 안")]
    public void 관측이_실패한_앱은_사유별_확인_불가_카드다(string state, CannotVerifyReason reason, string evidence, string ruleLabel)
    {
        var finding = Assert.Single(Evaluate(
            new FakeAppRule("winapp2:Vendor Cache", "Vendor Cache", state, App: "Vendor Suite", ProtectedTargets: state == AppCacheProbeContract.RULE_STATE_PROTECTED ? 1 : 0),
            new FakeAppRule("winapp2:Vendor Logs", "Vendor Logs", AppCacheProbeContract.RULE_STATE_ABSENT, App: "Vendor Suite")));

        Assert.Equal(AppCacheRule.FINDING_ID_PREFIX + "Vendor Suite", finding.Id);
        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(reason, finding.CannotVerifyReason);
        Assert.Equal("Vendor Suite: 규칙 위치를 확인하지 못했어요", finding.Title);
        Assert.Contains(evidence, finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("winapp2:Vendor Cache: 확인하지 못했어요(" + ruleLabel + ")", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Vendor Logs: 이 규칙 위치에서는 관측한 파일이 없어요", finding.Detail!, StringComparison.Ordinal);
        Assert.Equal([typeof(ShowDetailsAction)], finding.Actions.Select(a => a.GetType()));
        Assert.DoesNotContain(" B", finding.Title, StringComparison.Ordinal);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>한 앱에 실패 사유가 여럿이면 접근 거부 > 시간 초과 > 검사하지 못함 > 보호 순으로 대표 사유를 정한다.</summary>
    [Fact]
    public void 실패_사유가_여럿이면_접근_거부가_먼저다()
    {
        var finding = Assert.Single(Evaluate(
            new FakeAppRule("winapp2:A", "A", AppCacheProbeContract.RULE_STATE_PROTECTED, App: "Suite"),
            new FakeAppRule("winapp2:B", "B", AppCacheProbeContract.RULE_STATE_TIMED_OUT, App: "Suite"),
            new FakeAppRule("winapp2:C", "C", AppCacheProbeContract.RULE_STATE_ACCESS_DENIED, App: "Suite")));

        Assert.Equal(CannotVerifyReason.AccessDenied, finding.CannotVerifyReason);
    }

    /// <summary>파일을 관측한 앱 카드에서도 관측이 실패한 규칙은 "파일 없음"이 아니라 확인하지 못했다고 상세에 쓴다.</summary>
    [Fact]
    public void 관측한_앱_카드의_실패_규칙은_확인하지_못했다고_쓴다()
    {
        var finding = Assert.Single(Evaluate(
            new FakeAppRule("winapp2:Suite Cache", "Suite Cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 500, 5, App: "Suite"),
            new FakeAppRule("winapp2:Suite Logs", "Suite Logs", AppCacheProbeContract.RULE_STATE_ACCESS_DENIED, App: "Suite")));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("winapp2:Suite Logs: 확인하지 못했어요(접근 거부)", finding.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("winapp2:Suite Logs: 이 규칙 위치에서는 관측한 파일이 없어요", finding.Detail!, StringComparison.Ordinal);
    }

    /// <summary>원본 Warning이 있거나 이름에 비밀번호·쿠키·세션·기록·자격 증명 낱말이 든 규칙은 크기가 있어도 카드에서 빠지고 요약에 개수만 남는다.</summary>
    [Theory]
    [InlineData("Google Chrome Saved Usernames & Passwords", false)]
    [InlineData("Microsoft Edge Web Browsing Cookies", false)]
    [InlineData("Firefox Session Restore", false)]
    [InlineData("Google Chrome Web Browsing History", false)]
    [InlineData("Windows Credential Manager Cache", false)]
    [InlineData("Steam Installers", true)]
    public void 개인정보_관련_규칙은_카드에서_빼고_센다(string name, bool hasWarning)
    {
        var rule = new FakeAppRule("winapp2:" + name, name, AppCacheProbeContract.RULE_STATE_OBSERVED, 2_100_000, 3, HasWarning: hasWarning);

        Assert.Empty(Evaluate(rule));
        Assert.Contains("개인정보 관련 규칙 1개 제외", Summary(rule).Detail!, StringComparison.Ordinal);
    }

    /// <summary>관리자 권한 검사에서는 보충 규칙 카드에 사용자 설정 경로를 적용하지 않았다고 밝힌다.</summary>
    [Fact]
    public void 관리자_권한_검사는_기본_위치만_확인했다고_밝힌다()
    {
        var snapshot = AppCacheTestData.Snapshot(
            ProbeStatus.Success,
            AppCacheTestData.Catalog(elevated: true),
            AppCacheTestData.Rules(new FakeAppRule(NPM_ID, "npm cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 10, 1, Origin: AppCacheProbeContract.ORIGIN_SUPPLEMENT,
                ConfigSource: AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY, ConfigApp: "npm", Impact: ("a", "b", "c"))));

        var finding = Assert.Single(new AppCacheRule().Evaluate(snapshot));

        Assert.Contains("사용자 설정 경로는 보안상 적용하지 않고 기본 위치만", finding.Detail!, StringComparison.Ordinal);
    }

    /// <summary>부분 관측 카드는 부분 문구와 보호 위치 수·건너뛴 항목을 상세에 남긴다.</summary>
    [Fact]
    public void 부분_관측은_건너뛴_항목과_보호_위치를_밝힌다()
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(
            COMMUNITY_ID, "Discord", AppCacheProbeContract.RULE_STATE_PARTIAL, 800, 4, Partial: true, ProtectedTargets: 1, SkipAccessDenied: 2, SkipTimeout: 1)));

        Assert.Contains("일부 관측", finding.Title, StringComparison.Ordinal);
        Assert.Contains("부분 합계", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("일부만 관측", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("보호 폴더 안이라 관측하지 않은 위치 1곳", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("접근 거부 2", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("시간 초과 1", finding.Detail!, StringComparison.Ordinal);
    }

    /// <summary>Adobe 설정은 형식을 검증할 수 없어 확인 불가(Unsupported)이며 기본 위치 범위를 밝힌다. UNC·보호·오프라인·해석 불가·읽기 실패도 사유와 함께 확인 불가다. 적용·설정 없음은 따로 Finding을 만들지 않는다.</summary>
    [Fact]
    public void 앱_설정_위치를_확인하지_못하면_사유를_표시한다()
    {
        var snapshot = AppCacheTestData.Snapshot(
            ProbeStatus.Success,
            AppCacheTestData.Catalog(),
            AppCacheTestData.Rules(),
            AppCacheTestData.Configs(
                ("adobe", AppCacheProbeContract.CONFIG_STATE_CANNOT_VERIFY),
                ("nuget", AppCacheProbeContract.CONFIG_STATE_UNC),
                ("pip", AppCacheProbeContract.CONFIG_STATE_UNREADABLE),
                ("npm", AppCacheProbeContract.CONFIG_STATE_APPLIED),
                ("steam", AppCacheProbeContract.CONFIG_STATE_NOT_CONFIGURED)));

        var findings = new AppCacheRule().Evaluate(snapshot);

        Assert.Equal(3, findings.Count);
        var adobe = Assert.Single(findings, f => f.Id == AppCacheRule.CONFIG_FINDING_ID_PREFIX + "adobe");
        Assert.Equal(CannotVerifyReason.Unsupported, adobe.CannotVerifyReason);
        Assert.Contains("기본 위치", adobe.Evidence, StringComparison.Ordinal);
        Assert.Contains("확인 범위 밖", adobe.Detail!, StringComparison.Ordinal);
        var nuget = Assert.Single(findings, f => f.Id == AppCacheRule.CONFIG_FINDING_ID_PREFIX + "nuget");
        Assert.Equal(CannotVerifyReason.Unsupported, nuget.CannotVerifyReason);
        Assert.Contains("네트워크", nuget.Evidence, StringComparison.Ordinal);
        Assert.Contains("NuGet", nuget.Title, StringComparison.Ordinal);
        Assert.Equal(CannotVerifyReason.AccessDenied, Assert.Single(findings, f => f.Id == AppCacheRule.CONFIG_FINDING_ID_PREFIX + "pip").CannotVerifyReason);
        AssertNoForbiddenPhrases(findings);
    }

    /// <summary>프로브가 실패하거나 없으면 규칙별 Finding을 만들지 않는다(요약 규칙과 실행 상태 변환이 알림).</summary>
    [Fact]
    public void 프로브가_실패하면_규칙별_결과를_만들지_않는다()
    {
        Assert.Empty(new AppCacheRule().Evaluate(AppCacheTestData.Snapshot(ProbeStatus.Failed, AppCacheTestData.Rules(new FakeAppRule(COMMUNITY_ID, "Discord", "observed", 1, 1)))));
        Assert.Empty(new AppCacheRule().Evaluate(new ScanSnapshot(Guid.NewGuid(), [])));
    }

    /// <summary>Squirrel 버전 폴더는 이름만 나열하는 정보이며 크기·사용 여부 판단 문구가 없다.</summary>
    [Fact]
    public void Squirrel_버전_폴더는_이름만_나열한다()
    {
        var snapshot = AppCacheTestData.Snapshot(ProbeStatus.Success, AppCacheTestData.Catalog(), AppCacheTestData.Squirrel(("Discord", ["app-1.0.9258", "app-1.0.9259"]), ("slack", ["app-4.41.105"])));

        var findings = new SquirrelVersionFoldersRule().Evaluate(snapshot);

        Assert.Equal(2, findings.Count);
        var discord = findings[0];
        Assert.Equal(SquirrelVersionFoldersRule.FINDING_ID_PREFIX + "Discord", discord.Id);
        Assert.Equal(Verdict.Info, discord.Verdict);
        Assert.Equal(FindingCategory.AppCache, discord.Category);
        Assert.Contains("2개", discord.Title, StringComparison.Ordinal);
        Assert.Contains("app-1.0.9258", discord.Detail!, StringComparison.Ordinal);
        Assert.Contains("app-1.0.9259", discord.Detail!, StringComparison.Ordinal);
        Assert.Null(discord.Recommendation);
        Assert.Null(discord.Impact);
        Assert.DoesNotContain("MB", discord.Title, StringComparison.Ordinal);
        AssertNoForbiddenPhrases(findings);
    }

    /// <summary>규칙 목록 요약: 지원·건너뜀·탐지 수, 스냅샷 버전·커밋, 사유별 한국어 목록, 탐지 확인 불가 수, 효과 미지원 안내를 담은 정보.</summary>
    [Fact]
    public void 규칙_목록_요약에_지원_미지원_탐지_수를_표시한다()
    {
        var snapshot = AppCacheTestData.Snapshot(
            ProbeStatus.Success,
            AppCacheTestData.Catalog(communityTotal: 4068, communitySupported: 3500, supplementTotal: 7, supplementSupported: 7, detected: 12, unknown: 3,
                unsupported: ["RegistryOnly=500", "UnresolvedVariable=68"], runtime: ["WildcardBoundExceeded=2"]));

        var finding = Assert.Single(new RuleCatalogSummaryRule().Evaluate(snapshot));

        Assert.Equal(RuleCatalogSummaryRule.FINDING_ID, finding.Id);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.AppCache, finding.Category);
        Assert.Contains("지원 3507개", finding.Title, StringComparison.Ordinal);
        Assert.Contains("건너뜀 568개", finding.Title, StringComparison.Ordinal);
        Assert.Contains("탐지 12개", finding.Title, StringComparison.Ordinal);
        Assert.Contains("260915", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("53ae419", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("레지스트리 항목만 있음 500개", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("해석할 수 없는 환경 변수 68개", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("와일드카드 범위 초과 2개", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("3개", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("RegKey", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("CC-BY-SA-4.0", finding.Detail!, StringComparison.Ordinal);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>ProfileList를 읽지 못한 검사는 요약 상세에 밝히고, 읽은 검사는 밝히지 않는다.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void 프로필_목록을_읽지_못하면_요약에_밝힌다(bool available, bool expectNote)
    {
        var snapshot = AppCacheTestData.Snapshot(ProbeStatus.Success, AppCacheTestData.Catalog(profileListAvailable: available));

        var finding = Assert.Single(new RuleCatalogSummaryRule().Evaluate(snapshot));

        Assert.Equal(expectNote, finding.Detail!.Contains("사용자 프로필 목록을 읽지 못해", StringComparison.Ordinal));
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>무결성 확인 실패(파일 없음·SHA-256 불일치)는 확인 불가(ProbeError)와 문제 파일 이름을 상세로 표시한다.</summary>
    [Theory]
    [InlineData(AppCacheProbeContract.CATALOG_MISMATCH)]
    [InlineData(AppCacheProbeContract.CATALOG_MISSING)]
    public void 무결성_확인_실패는_확인_불가다(string state)
    {
        var snapshot = AppCacheTestData.Snapshot(
            ProbeStatus.Failed,
            [
                AppCacheTestData.M(AppCacheProbeContract.CATALOG_STATE, new TextValue(state)),
                AppCacheTestData.M(AppCacheProbeContract.CATALOG_FAILED_FILE, new TextValue("winapp2.ini")),
            ]);

        var finding = Assert.Single(new RuleCatalogSummaryRule().Evaluate(snapshot));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.ProbeError, finding.CannotVerifyReason);
        Assert.Contains(@"rules\winapp2.ini", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains(state, finding.Detail!, StringComparison.Ordinal);
    }

    /// <summary>보호 정책 무효면 확인 불가(ProbeError), 프로브 결과가 없으면 요약도 없다.</summary>
    [Fact]
    public void 보호_정책_무효와_미실행을_구분한다()
    {
        var invalid = AppCacheTestData.Snapshot(ProbeStatus.Failed, [AppCacheTestData.M(AppCacheProbeContract.CATALOG_STATE, new TextValue(AppCacheProbeContract.CATALOG_POLICY_INVALID))]);

        var finding = Assert.Single(new RuleCatalogSummaryRule().Evaluate(invalid));

        Assert.Equal(CannotVerifyReason.ProbeError, finding.CannotVerifyReason);
        Assert.Contains("보호 정책", finding.Title, StringComparison.Ordinal);
        Assert.Empty(new RuleCatalogSummaryRule().Evaluate(new ScanSnapshot(Guid.NewGuid(), [])));
    }
}
