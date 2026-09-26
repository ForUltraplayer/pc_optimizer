/**
 * @file    : AppCacheProbeTests.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 프로브 전체 흐름(가짜 PC): 보충 규칙 탐지·관측과 앱 카드, 기본 폴더 없이 설정으로 옮긴 캐시 탐지, 겹친 커뮤니티 규칙 병합, 실제 파싱 항목의 Section= 앱 카드 묶음(범주 Games·Adobe 제외), 설정 재정의(스캔 루트 밖), Steam 게임 본체 제외, NuGet 보호 폴더 설정 경로 우회 금지, Squirrel 이름만(메타데이터 목록), 다른 사용자·Public·정션 경로, ProfileList 없음 기록, 관리자 검사의 기본 위치만, 무결성·보호 정책 실패, 인증 토큰이 측정값·로그·내보내기에 남지 않음을 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.ConfigReaders;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="PcOptimizer.Probes.Applications.AppCacheProbe"/>를 가짜 PC로 검증합니다. 실제 파일·레지스트리·네트워크에 접근하지 않습니다.
/// </summary>
public sealed class AppCacheProbeTests
{
    private const string PROBE_ID = AppCacheProbeContract.PROBE_ID;

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static async Task<(ProbeResult Result, ScanSnapshot Snapshot)> RunAsync(AppCacheTestEnvironment environment, bool elevated = false)
    {
        var result = await environment.Probe().RunAsync(AppCacheTestEnvironment.Context(elevated), CancellationToken.None);
        return (result, new ScanSnapshot(Guid.NewGuid(), [result]));
    }

    /// <summary>
    /// 규칙 ID로 규칙 측정값 인덱스를 찾는다.
    /// </summary>
    private static int RuleIndex(ScanSnapshot snapshot, string ruleId)
    {
        var count = snapshot.GetMeasurement(PROBE_ID, AppCacheProbeContract.RULE_COUNT)!.Value is IntegerValue value ? value.Value : 0;
        for (var index = 0; index < count; index++)
        {
            if (Text(snapshot, AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, index, AppCacheProbeContract.FIELD_ID)) == ruleId)
            {
                return index;
            }
        }

        throw new InvalidOperationException(ruleId + " 규칙 결과 없음");
    }

    /// <summary>
    /// 문자열 측정값.
    /// </summary>
    private static string? Text(ScanSnapshot snapshot, string name)
    {
        return snapshot.GetMeasurement(PROBE_ID, name)?.Value is TextValue text ? text.Value : null;
    }

    /// <summary>
    /// 규칙 필드 값.
    /// </summary>
    private static MeasurementValue? Field(ScanSnapshot snapshot, string ruleId, string field)
    {
        return snapshot.GetMeasurement(PROBE_ID, AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, RuleIndex(snapshot, ruleId), field))?.Value;
    }

    /// <summary>보충 규칙: 기본 위치(공유 스캔 합계)와 설정 재정의 위치(스캔 루트 밖 대상 열거)를 합치고 사용자 설정 경로로 표시한다.</summary>
    [Fact]
    public async Task Npm은_기본_위치와_설정_재정의_위치를_합친다()
    {
        var (result, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new IntegerValue(1300), Field(snapshot, "supplement:npm cache", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.CONFIG_SOURCE_USER), Field(snapshot, "supplement:npm cache", AppCacheProbeContract.FIELD_CONFIG_SOURCE));
        var paths = Assert.IsType<TextListValue>(Field(snapshot, "supplement:npm cache", AppCacheProbeContract.FIELD_PATHS)).Values;
        Assert.Contains(AppCacheTestEnvironment.NPM_OVERRIDE, paths);
    }

    /// <summary>같은 위치를 가리킨 커뮤니티 규칙(Python·NVIDIA)은 검토한 보충 규칙에서 한 번만 세고, 병합 상태와 소유 규칙을 남긴다.</summary>
    [Fact]
    public async Task 겹친_커뮤니티_규칙은_보충_규칙에서_한_번만_센다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        Assert.Equal(new IntegerValue(300), Field(snapshot, "supplement:pip cache", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_MERGED), Field(snapshot, "winapp2:Python", AppCacheProbeContract.FIELD_STATE));
        Assert.Null(Field(snapshot, "winapp2:Python", AppCacheProbeContract.FIELD_BYTES));
        Assert.Contains("supplement:pip cache", Assert.IsType<TextListValue>(Field(snapshot, "winapp2:Python", AppCacheProbeContract.FIELD_SHARED_WITH)).Values);
        Assert.Contains("winapp2:Python", Assert.IsType<TextListValue>(Field(snapshot, "supplement:pip cache", AppCacheProbeContract.FIELD_SHARED_WITH)).Values);
        Assert.Equal(new IntegerValue(120), Field(snapshot, "supplement:NVIDIA and Direct3D shader caches", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_MERGED), Field(snapshot, "winapp2:NVIDIA Shader Cache", AppCacheProbeContract.FIELD_STATE));
    }

    /// <summary>Steam은 다른 드라이브 라이브러리의 shadercache만 세고 게임 본체는 넣지 않으며, Program Files 아래 기본·설치 위치는 보호로 남긴다.</summary>
    [Fact]
    public async Task Steam은_shadercache만_세고_게임_본체를_넣지_않는다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        Assert.Equal(new IntegerValue(2000), Field(snapshot, "supplement:Steam shader cache", AppCacheProbeContract.FIELD_BYTES));
        var protectedTargets = Assert.IsType<IntegerValue>(Field(snapshot, "supplement:Steam shader cache", AppCacheProbeContract.FIELD_PROTECTED_TARGETS));
        Assert.True(protectedTargets.Value >= 2);
        var paths = Assert.IsType<TextListValue>(Field(snapshot, "supplement:Steam shader cache", AppCacheProbeContract.FIELD_PATHS)).Values;
        Assert.All(paths, path => Assert.EndsWith(@"\steamapps\shadercache", path, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshot.ProbeResults[0].Measurements, m => m.Value is IntegerValue value && value.Value >= AppCacheTestEnvironment.GAME_BYTES);
    }

    /// <summary>NUGET_PACKAGES가 보호 폴더(문서)를 가리키면 순회하지 않고 보호 상태로 표시하며, 기본 위치만 센다(보호 정책 우회 금지).</summary>
    [Fact]
    public async Task NuGet_설정_경로는_보호_정책을_우회하지_않는다()
    {
        var environment = new AppCacheTestEnvironment();
        var (_, snapshot) = await RunAsync(environment);

        Assert.Equal(new IntegerValue(400), Field(snapshot, "supplement:NuGet global packages", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY), Field(snapshot, "supplement:NuGet global packages", AppCacheProbeContract.FIELD_CONFIG_SOURCE));
        Assert.DoesNotContain(AppCacheTestEnvironment.DOCUMENTS, environment.Source.Enumerated);
        var findings = new AppCacheRule().Evaluate(snapshot);
        var config = Assert.Single(findings, f => f.Id == AppCacheRule.CONFIG_FINDING_ID_PREFIX + "nuget");
        Assert.Equal(CannotVerifyReason.Unsupported, config.CannotVerifyReason);
        Assert.Contains("보호 폴더", config.Evidence, StringComparison.Ordinal);
    }

    /// <summary>Squirrel은 메타데이터 목록(확인한 Discord만)의 앱 폴더에 Update.exe가 있을 때 app-* 이름만 모으고(목록에 없는 slack은 제외), 크기를 재지 않는다.</summary>
    [Fact]
    public async Task Squirrel은_설치_구조가_맞는_앱의_버전_폴더_이름만_모은다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        var findings = new SquirrelVersionFoldersRule().Evaluate(snapshot);

        var discord = Assert.Single(findings);
        Assert.Equal(SquirrelVersionFoldersRule.FINDING_ID_PREFIX + "Discord", discord.Id);
        Assert.Contains("app-1.0.1, app-1.0.2", discord.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain(discord.Measured, m => m.Name.EndsWith(AppCacheProbeContract.FIELD_BYTES, StringComparison.Ordinal));
    }

    /// <summary>
    /// 규칙 제외(FILE keep.log)가 적용되고, 보호 폴더 규칙은 보호 상태, 탐지되지 않은 규칙은 결과가 없으며, 이 PC에서 변수를 해석하지 못한 규칙은 통째로 건너뛰고 요약 수에 남는다.
    /// </summary>
    [Fact]
    public async Task 제외_보호_미탐지_실행시_미지원을_구분한다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        Assert.Equal(new IntegerValue(16), Field(snapshot, "winapp2:Vendor Logs", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_PROTECTED), Field(snapshot, "winapp2:Vendor In Program Files", AppCacheProbeContract.FIELD_STATE));
        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_MERGED), Field(snapshot, "winapp2:Mixed Merged And Protected", AppCacheProbeContract.FIELD_STATE));
        Assert.Equal(new IntegerValue(2), Field(snapshot, "winapp2:Mixed Merged And Protected", AppCacheProbeContract.FIELD_PROTECTED_TARGETS));
        Assert.Throws<InvalidOperationException>(() => RuleIndex(snapshot, "winapp2:Not Installed"));
        Assert.Throws<InvalidOperationException>(() => RuleIndex(snapshot, "winapp2:Registry Only Unsupported"));
        Assert.Throws<InvalidOperationException>(() => RuleIndex(snapshot, "winapp2:Unresolved Music"));
        var runtime = Assert.IsType<TextListValue>(snapshot.GetMeasurement(PROBE_ID, AppCacheProbeContract.RUNTIME_UNSUPPORTED_BY_REASON)!.Value).Values;
        Assert.Equal(["UnresolvedVariable=1"], runtime);
        var summary = Assert.Single(new RuleCatalogSummaryRule().Evaluate(snapshot));
        Assert.Equal(Verdict.Info, summary.Verdict);
        Assert.Contains("해석할 수 없는 환경 변수 1개", summary.Detail!, StringComparison.Ordinal);
    }

    /// <summary>커뮤니티 규칙의 와일드카드가 다른 사용자 프로필이나 휴지통의 다른 SID 폴더로 가도 들어가지 않는다(현재 사용자 몫만 셈).</summary>
    [Fact]
    public async Task 다른_사용자_위치에는_들어가지_않는다()
    {
        var environment = new AppCacheTestEnvironment();
        var (_, snapshot) = await RunAsync(environment);

        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_PROTECTED), Field(snapshot, "winapp2:Other Users Wildcard", AppCacheProbeContract.FIELD_STATE));
        Assert.Equal(new IntegerValue(100), Field(snapshot, "winapp2:Windows Recycle Bin", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new IntegerValue(1), Field(snapshot, "winapp2:Windows Recycle Bin", AppCacheProbeContract.FIELD_SKIP_PROTECTED));
        Assert.DoesNotContain(@"C:\Users\other\.vendor_usage", environment.Source.Enumerated);
        Assert.DoesNotContain(@"C:\$Recycle.Bin\" + AppCacheTestEnvironment.OTHER_SID, environment.Source.Enumerated);
    }

    /// <summary>
    /// 실제 파싱한 커뮤니티 항목의 앱 카드 묶음: winapp2 Section= 텍스트가 같은 Chrome 섹션 3개는 카드 하나(규칙별 상세 줄 유지), Edge 섹션은 카드 하나이며,
    /// 숫자 LangSecRef·숫자 Section= 값만 같은 규칙은 묶지 않고 규칙 이름(끝의 " *" 제거)으로 따로 보인다.
    /// </summary>
    [Fact]
    public async Task 같은_Section의_커뮤니티_규칙은_앱_카드_하나로_합친다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment(communityFixture: AppCacheTestEnvironment.GROUPING_FIXTURE));
        var cards = new AppCacheRule().Evaluate(snapshot).Where(f => f.Id.StartsWith(AppCacheRule.FINDING_ID_PREFIX, StringComparison.Ordinal)).ToList();

        var chrome = Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Google Chrome Web Browser");
        var edge = Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Microsoft Edge Web Browser");
        Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Vendor Alpha Logs");
        Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Vendor Beta Temp");
        Assert.DoesNotContain(cards, f => f.Id.Contains("3021", StringComparison.Ordinal) || f.Id.Contains("Google Chrome Bookmark", StringComparison.Ordinal));
        Assert.Equal("Google Chrome Web Browser: 규칙 위치의 파일 관측 120 B", chrome.Title);
        Assert.Contains("winapp2:Google Chrome Bookmark Backups: 30 B", chrome.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Google Chrome Bookmark Favicons: 40 B", chrome.Detail!, StringComparison.Ordinal);
        Assert.Contains("winapp2:Google Chrome Storage Quota Manager: 50 B", chrome.Detail!, StringComparison.Ordinal);
        Assert.Equal("Microsoft Edge Web Browser: 규칙 위치의 파일 관측 70 B", edge.Title);
        Assert.Equal(new TextValue("Google Chrome Web Browser"), Field(snapshot, "winapp2:Google Chrome Bookmark Favicons", AppCacheProbeContract.FIELD_APP));
    }

    /// <summary>
    /// 실제 파싱한 범주·제품군 Section 항목: Section=Games(스냅샷 521개 공유)인 서로 다른 게임 3개와 Section=Adobe(31개 공유)인 Adobe 제품 2개는
    /// 한 카드로 묶지 않고 규칙 이름별 카드다. 같은 fixture의 Chrome·Edge Section 묶음은 그대로다.
    /// </summary>
    [Fact]
    public async Task 범주_Section의_게임과_Adobe_규칙은_규칙별_카드다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment(communityFixture: AppCacheTestEnvironment.GROUPING_FIXTURE));
        var cards = new AppCacheRule().Evaluate(snapshot).Where(f => f.Id.StartsWith(AppCacheRule.FINDING_ID_PREFIX, StringComparison.Ordinal)).ToList();

        Assert.DoesNotContain(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Games" || f.Id == AppCacheRule.FINDING_ID_PREFIX + "Adobe");
        Assert.Equal("3 Stars of Destiny: 규칙 위치의 파일 관측 11 B", Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "3 Stars of Destiny").Title);
        Assert.Equal("3SwitcheD: 규칙 위치의 파일 관측 19 B", Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "3SwitcheD").Title);
        Assert.Equal("AMID EVIL: 규칙 위치의 파일 관측 23 B", Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "AMID EVIL").Title);
        Assert.Equal("Adobe Camera Raw: 규칙 위치의 파일 관측 13 B", Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Adobe Camera Raw").Title);
        Assert.Equal("Adobe InDesign: 규칙 위치의 파일 관측 17 B", Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Adobe InDesign").Title);
        Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Google Chrome Web Browser");
        Assert.Single(cards, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Microsoft Edge Web Browser");
    }

    /// <summary>
    /// ProfileList를 읽지 못한 PC: 목록을 읽지 못했다는 측정값을 남기고 규칙 목록 요약이 이를 밝히며,
    /// 현재 프로필의 부모 폴더(C:\Users) 추정으로 다른 사용자(C:\Users\other)는 계속 막는다.
    /// </summary>
    [Fact]
    public async Task 프로필_목록을_읽지_못하면_측정값과_요약에_남긴다()
    {
        var (_, listed) = await RunAsync(new AppCacheTestEnvironment());
        var environment = new AppCacheTestEnvironment(profileList: false);
        var (_, snapshot) = await RunAsync(environment);
        var summary = Assert.Single(new RuleCatalogSummaryRule().Evaluate(snapshot));

        Assert.Equal(new BooleanValue(true), listed.GetMeasurement(PROBE_ID, AppCacheProbeContract.PROFILE_LIST_AVAILABLE)?.Value);
        Assert.Equal(new BooleanValue(false), snapshot.GetMeasurement(PROBE_ID, AppCacheProbeContract.PROFILE_LIST_AVAILABLE)?.Value);
        Assert.Contains("사용자 프로필 목록을 읽지 못해", summary.Detail!, StringComparison.Ordinal);
        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_PROTECTED), Field(snapshot, "winapp2:Other Users Wildcard", AppCacheProbeContract.FIELD_STATE));
        Assert.DoesNotContain(@"C:\Users\other\.vendor_usage", environment.Source.Enumerated);
    }

    /// <summary>보충 규칙 6종이 이 가짜 PC에서 탐지되어 앱 카드(검토 영향·캐시 문구·설정 열기)로 나오고 금지 문구가 없다. 커뮤니티 규칙 카드는 중립 문구·영향 미확인이다.</summary>
    [Fact]
    public async Task 보충_규칙이_탐지되고_정보로_나온다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        var findings = new AppCacheRule().Evaluate(snapshot);

        foreach (var app in new[] { "Adobe 미디어 캐시", "npm", "pip", "NuGet", "Steam 셰이더 캐시", "NVIDIA·Direct3D 셰이더 캐시" })
        {
            var finding = Assert.Single(findings, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + app);
            Assert.Equal(Verdict.Info, finding.Verdict);
            Assert.Contains("캐시·임시 파일 후보", finding.Title, StringComparison.Ordinal);
            Assert.Contains(finding.Actions, action => action is OpenSettingsAction);
            Assert.DoesNotContain("영향 미확인", finding.Impact!.Benefit, StringComparison.Ordinal);
            foreach (var phrase in Rules.AppCacheTestData.FORBIDDEN_PHRASES)
            {
                Assert.DoesNotContain(phrase, Rules.AppCacheTestData.Text(finding), StringComparison.Ordinal);
            }
        }

        Assert.Contains(findings, f => f.Id == AppCacheRule.CONFIG_FINDING_ID_PREFIX + "adobe" && f.CannotVerifyReason == CannotVerifyReason.Unsupported);
        var community = Assert.Single(findings, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "Vendor Logs");
        Assert.Equal("영향 미확인", community.Impact!.Benefit);
        Assert.DoesNotContain("캐시", community.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(community.Actions, action => action is OpenSettingsAction);
    }

    /// <summary>
    /// npm·pip·NuGet 기본 캐시 폴더가 없어도 설정 값(.npmrc cache=, PIP_CACHE_DIR, NUGET_PACKAGES)이 있으면 그 보충 규칙을 탐지하고 설정 경로를 재며,
    /// 카드는 "사용자 설정 경로"를 밝힌다(설정 값 자체가 탐지 근거).
    /// </summary>
    [Fact]
    public async Task 설정으로_옮긴_캐시는_기본_폴더가_없어도_탐지하고_잰다()
    {
        var environment = new AppCacheTestEnvironment(defaultCaches: false);
        environment.Environment
            .WithVariable(PcOptimizer.Probes.Applications.ConfigReaders.NuGetConfigReader.ENVIRONMENT_VARIABLE, @"D:\NuGetPkgs")
            .WithVariable(PcOptimizer.Probes.Applications.ConfigReaders.PipConfigReader.ENVIRONMENT_VARIABLE, @"D:\PipCache");

        var (_, snapshot) = await RunAsync(environment);
        var findings = new AppCacheRule().Evaluate(snapshot);

        foreach (var (ruleId, bytes, app) in new[] { ("supplement:npm cache", 1000L, "npm"), ("supplement:pip cache", 700L, "pip"), ("supplement:NuGet global packages", 600L, "NuGet") })
        {
            Assert.Equal(new IntegerValue(bytes), Field(snapshot, ruleId, AppCacheProbeContract.FIELD_BYTES));
            Assert.Equal(new TextValue(AppCacheProbeContract.CONFIG_SOURCE_USER), Field(snapshot, ruleId, AppCacheProbeContract.FIELD_CONFIG_SOURCE));
            Assert.Contains("사용자 설정 경로", Assert.Single(findings, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + app).Detail!, StringComparison.Ordinal);
        }
    }

    /// <summary>%Public%(공용 폴더)을 가리키는 규칙은 다른 사용자 위치로 막지 않고 자기 상태로 잰다.</summary>
    [Fact]
    public async Task 공용_폴더_규칙은_다른_사용자_위치로_막지_않는다()
    {
        var (_, snapshot) = await RunAsync(new AppCacheTestEnvironment());

        Assert.Equal(new IntegerValue(60), Field(snapshot, "winapp2:Public Vendor", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_OBSERVED), Field(snapshot, "winapp2:Public Vendor", AppCacheProbeContract.FIELD_STATE));
    }

    /// <summary>중간 폴더가 정션(Documents and Settings)인 FileKey 경로는 따라가지 않고 reparse로 센다: 그 아래 폴더를 열거하지 않고 크기도 없다.</summary>
    [Fact]
    public async Task 중간_정션을_거치는_경로는_따라가지_않는다()
    {
        var environment = new AppCacheTestEnvironment();

        var (_, snapshot) = await RunAsync(environment);

        Assert.Equal(new TextValue(AppCacheProbeContract.RULE_STATE_ABSENT), Field(snapshot, "winapp2:Junction Path", AppCacheProbeContract.FIELD_STATE));
        Assert.Null(Field(snapshot, "winapp2:Junction Path", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new IntegerValue(1), Field(snapshot, "winapp2:Junction Path", AppCacheProbeContract.FIELD_SKIP_REPARSE));
        Assert.DoesNotContain(environment.Source.Enumerated, path => path.StartsWith(@"C:\Documents and Settings", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>관리자 권한 검사는 사용자 설정 파일을 읽지 않고(설정 경로 미적용) 기본 위치만 보며 그 사실을 측정값에 남긴다.</summary>
    [Fact]
    public async Task 관리자_권한_검사는_사용자_설정_경로를_적용하지_않는다()
    {
        var environment = new AppCacheTestEnvironment();
        var (_, snapshot) = await RunAsync(environment, elevated: true);

        Assert.Equal(new BooleanValue(true), snapshot.GetMeasurement(PROBE_ID, AppCacheProbeContract.ELEVATED_DEFAULTS_ONLY)!.Value);
        Assert.Equal(new IntegerValue(300), Field(snapshot, "supplement:npm cache", AppCacheProbeContract.FIELD_BYTES));
        Assert.Equal(new TextValue(AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY), Field(snapshot, "supplement:npm cache", AppCacheProbeContract.FIELD_CONFIG_SOURCE));
        Assert.DoesNotContain(environment.Environment.FileReads, path => path.EndsWith(".npmrc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(AppCacheTestEnvironment.NPM_OVERRIDE, environment.Source.Enumerated);
    }

    /// <summary>포함 규칙 파일의 해시가 다르면 Failed + ProbeError(리소스 문장)이고, 요약 규칙은 확인 불가와 문제 파일을 표시한다. 파일 순회는 하지 않는다.</summary>
    [Fact]
    public async Task 무결성_확인_실패는_Failed와_확인_불가다()
    {
        var environment = new AppCacheTestEnvironment();
        environment.RuleFiles["supplement.ini"] = [.. environment.RuleFiles["supplement.ini"], (byte)'\n'];

        var (result, snapshot) = await RunAsync(environment);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(CannotVerifyReason.ProbeError, Assert.Single(result.Issues).Reason);
        Assert.Contains("supplement.ini", result.Issues[0].Summary, StringComparison.Ordinal);
        Assert.Empty(environment.Source.Enumerated);
        var finding = Assert.Single(new RuleCatalogSummaryRule().Evaluate(snapshot));
        Assert.Equal(CannotVerifyReason.ProbeError, finding.CannotVerifyReason);
        Assert.Contains(AppCacheProbeContract.CATALOG_MISMATCH, finding.Detail!, StringComparison.Ordinal);
        Assert.Empty(new AppCacheRule().Evaluate(snapshot));
    }

    /// <summary>보호 정책이 없으면 앱 캐시 위치도 순회하지 않고 Failed다.</summary>
    [Fact]
    public async Task 보호_정책이_없으면_순회하지_않는다()
    {
        var environment = new AppCacheTestEnvironment { Policy = null };

        var (result, snapshot) = await RunAsync(environment);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(AppCacheProbeContract.CATALOG_POLICY_INVALID, Text(snapshot, AppCacheProbeContract.CATALOG_STATE));
        Assert.Empty(environment.Source.Enumerated);
        Assert.Contains("PcOptimizer.Rules.protect.json", Assert.Single(result.Issues).Summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// 설정 파일의 인증 토큰·이메일·비밀번호·API 키·Steam 계정 값은 측정값, 실행 조율기 로그, 기본 내보내기 어디에도 없다.
    /// 기본 내보내기에는 다른 드라이브 경로(npm 설정·Steam 라이브러리)의 원문도 없다.
    /// </summary>
    [Fact]
    public async Task 비밀_값은_측정값_로그_내보내기에_없다()
    {
        var environment = new AppCacheTestEnvironment();
        var logger = new RecordingLogger();
        var coordinator = new ScanCoordinator(
            [environment.Probe()],
            [new RuleCatalogSummaryRule(), new AppCacheRule(), new SquirrelVersionFoldersRule()],
            new ScanOptions(),
            new ScanReportVersions("1.0.0", "test"),
            new FakeClock(),
            logger);

        var scan = await coordinator.RunScanAsync(AppCacheTestEnvironment.Context(), CancellationToken.None);
        var json = new ReportExporter(new PersonalDataScrubber(AppCacheTestEnvironment.PROFILE, "tester", "DESKTOP-FAKE01"), @"C:\Windows").SerializeAnonymized(scan.Report);

        var measurementText = string.Join('\n', scan.Snapshot.ProbeResults.SelectMany(result => result.Measurements).Select(m => m.Value switch
        {
            TextValue text => text.Value,
            TextListValue list => string.Join('|', list.Values),
            _ => string.Empty,
        }));
        var logText = string.Join('\n', logger.Entries.Select(entry => entry.Message));
        Assert.Contains(scan.Report.Findings, f => f.Id == AppCacheRule.FINDING_ID_PREFIX + "npm");
        foreach (var secret in AppConfigReaderTests.SECRETS)
        {
            Assert.DoesNotContain(secret, measurementText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, logText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        }

        // JSON 문자열 안의 역슬래시는 \\로 기록된다. Program Files (x86)라는 이름 자체는 검토 메타데이터 문장에 있으므로 실제 경로 형태로 확인한다.
        foreach (var raw in new[] { "DevCache", "SteamLibrary", @"Program Files (x86)\\Steam\\steamapps", "tester" })
        {
            Assert.DoesNotContain(raw, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
