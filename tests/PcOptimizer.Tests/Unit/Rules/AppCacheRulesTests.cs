/**
 * @file    : AppCacheRulesTests.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 규칙(관측·부분·없음·보호·병합·관측 실패, 검토 영향/영향 미확인, 사용자 설정 경로/기본 위치만 확인, Adobe·UNC 설정 확인 불가), Squirrel 버전 폴더 정보, 규칙 목록 요약(수·사유·무결성 실패·보호 정책 무효)과 금지 문구 부재를 가짜 스냅샷으로 검증
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

    /// <summary>검토하지 않은 커뮤니티 규칙: 관측 크기 정보, 영향 미확인, 경로는 문장에 없고 측정값에만 있다. 상세 보기·저장소 설정 열기만 제공한다.</summary>
    [Fact]
    public void 커뮤니티_규칙은_관측_크기와_영향_미확인을_표시한다()
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(COMMUNITY_ID, "Discord", AppCacheProbeContract.RULE_STATE_OBSERVED, 1_500_000, 42, SharedWith: ["winapp2:Discord Cache"])));

        Assert.Equal(AppCacheRule.FINDING_ID_PREFIX + COMMUNITY_ID, finding.Id);
        Assert.Equal(FindingCategory.AppCache, finding.Category);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("Discord", finding.Title, StringComparison.Ordinal);
        Assert.Contains("1.5 MB", finding.Title, StringComparison.Ordinal);
        Assert.Contains("42", finding.Evidence, StringComparison.Ordinal);
        Assert.Equal("영향 미확인", finding.Impact!.Benefit);
        Assert.Contains("영향 미확인", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Discord Cache", finding.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\", AppCacheTestData.Text(finding), StringComparison.Ordinal);
        Assert.Contains(finding.Measured, m => m.Name.EndsWith(AppCacheProbeContract.FIELD_PATHS, StringComparison.Ordinal));
        Assert.Equal([typeof(ShowDetailsAction), typeof(OpenSettingsAction)], finding.Actions.Select(a => a.GetType()));
        Assert.Equal(AppCacheRule.STORAGE_SETTINGS_URI, ((OpenSettingsAction)finding.Actions[1]).Uri);
        Assert.Null(finding.Recommendation);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>검토한 보충 규칙은 메타데이터의 영향을 쓰고, 사용자 설정 경로를 적용했는지·기본 위치만 확인했는지 범위를 밝힌다.</summary>
    [Theory]
    [InlineData(AppCacheProbeContract.CONFIG_SOURCE_USER, "사용자 설정 경로")]
    [InlineData(AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY, "기본 위치만 확인")]
    public void 보충_규칙은_검토한_영향과_설정_범위를_표시한다(string configSource, string expected)
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(
            NPM_ID, "npm cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 2_000_000_000, 10, Origin: AppCacheProbeContract.ORIGIN_SUPPLEMENT,
            ConfigSource: configSource, ConfigApp: "npm", Impact: ("다음 설치 때 받을 패키지 사본", "오프라인 설치가 안 될 수 있음", "npm install 때 다시 받음"))));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal("다음 설치 때 받을 패키지 사본", finding.Impact!.Benefit);
        Assert.Equal("오프라인 설치가 안 될 수 있음", finding.Impact.SideEffect);
        Assert.Contains(expected, finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("테스트 버전 메모", finding.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("영향 미확인", AppCacheTestData.Text(finding), StringComparison.Ordinal);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>관리자 권한 검사에서는 사용자 설정 경로를 적용하지 않았다고 밝힌다.</summary>
    [Fact]
    public void 관리자_권한_검사는_기본_위치만_확인했다고_밝힌다()
    {
        var snapshot = AppCacheTestData.Snapshot(
            ProbeStatus.Success,
            AppCacheTestData.Catalog(elevated: true),
            AppCacheTestData.Rules(new FakeAppRule(NPM_ID, "npm cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 10, 1, Origin: AppCacheProbeContract.ORIGIN_SUPPLEMENT,
                ConfigSource: AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY, ConfigApp: "npm", Impact: ("a", "b", "c"))));

        var finding = Assert.Single(new AppCacheRule().Evaluate(snapshot));

        Assert.Contains("관리자 권한 검사", finding.Detail!, StringComparison.Ordinal);
    }

    /// <summary>상태별 판정: 부분 관측은 정보(부분 표시), 없음은 정보, 보호·병합·제외는 크기 없이, 접근 거부·시간 초과는 확인 불가.</summary>
    [Theory]
    [InlineData(AppCacheProbeContract.RULE_STATE_PARTIAL, 500L, Verdict.Info, null, "일부 관측")]
    [InlineData(AppCacheProbeContract.RULE_STATE_ABSENT, 0L, Verdict.Info, null, "관측한 파일이 없어요")]
    [InlineData(AppCacheProbeContract.RULE_STATE_PROTECTED, null, Verdict.CannotVerify, CannotVerifyReason.Unsupported, "보호 폴더")]
    [InlineData(AppCacheProbeContract.RULE_STATE_MERGED, null, Verdict.Info, null, "한 번만")]
    [InlineData(AppCacheProbeContract.RULE_STATE_EXCLUDED, null, Verdict.Info, null, "제외 조건")]
    [InlineData(AppCacheProbeContract.RULE_STATE_ACCESS_DENIED, null, Verdict.CannotVerify, CannotVerifyReason.AccessDenied, "확인하지 못했어요")]
    [InlineData(AppCacheProbeContract.RULE_STATE_TIMED_OUT, null, Verdict.CannotVerify, CannotVerifyReason.Timeout, "확인하지 못했어요")]
    [InlineData(AppCacheProbeContract.RULE_STATE_NOT_OBSERVED, null, Verdict.CannotVerify, CannotVerifyReason.PartialData, "확인하지 못했어요")]
    public void 상태별로_판정한다(string state, long? bytes, Verdict verdict, CannotVerifyReason? reason, string titlePart)
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(COMMUNITY_ID, "Discord", state, bytes, 3, Partial: state == AppCacheProbeContract.RULE_STATE_PARTIAL, ProtectedTargets: 2, SkipAccessDenied: 1)));

        Assert.Equal(verdict, finding.Verdict);
        Assert.Equal(reason, finding.CannotVerifyReason);
        Assert.Contains(titlePart, finding.Title, StringComparison.Ordinal);
        AssertNoForbiddenPhrases([finding]);
    }

    /// <summary>부분 관측과 보호 위치 수·건너뛴 항목은 상세에 남는다.</summary>
    [Fact]
    public void 부분_관측은_건너뛴_항목과_보호_위치를_밝힌다()
    {
        var finding = Assert.Single(Evaluate(new FakeAppRule(
            COMMUNITY_ID, "Discord", AppCacheProbeContract.RULE_STATE_PARTIAL, 800, 4, Partial: true, ProtectedTargets: 1, SkipAccessDenied: 2, SkipTimeout: 1, HasWarning: true)));

        Assert.Contains("부분 합계", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("보호 폴더 안이라 관측하지 않은 위치 1곳", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("접근 거부 2", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("시간 초과 1", finding.Detail!, StringComparison.Ordinal);
        Assert.Contains("주의 문구", finding.Detail!, StringComparison.Ordinal);
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
