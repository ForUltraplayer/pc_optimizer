/**
 * @file    : Winapp2ParserTests.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 파서의 지원 문법(Detect/DetectFile OR, FileKey 패턴·RECURSE·REMOVESELF, FILE/PATH 제외, RegKey 기록, 앱 카드 묶음 기준(숫자가 아닌 Section 값 또는 이름))과 미지원 처리(DetectOS·알 수 없는 키·해석 불가 변수·볼륨 루트·UNC·REG 제외·탐지 없음·레지스트리 전용·HKU·지시어·와일드카드 한도)를 스냅샷 발췌·구성 fixture로 검증
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Tests.Unit.Cleaning;

/// <summary>
/// <see cref="Winapp2Parser"/>를 검증합니다. 발췌 fixture는 스냅샷 원문 그대로이며, 구성 fixture는 스냅샷에 없는 문법을 형식만 따라 만든 것입니다.
/// </summary>
public sealed class Winapp2ParserTests
{
    private static readonly string FIXTURE_FOLDER = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Winapp2");

    /// <summary>
    /// fixture 하나를 해석한다.
    /// </summary>
    internal static Winapp2ParseResult ParseFixture(string name, RuleOrigin origin = RuleOrigin.Community)
    {
        return Winapp2Parser.Parse(File.ReadAllText(Path.Combine(FIXTURE_FOLDER, name)), origin);
    }

    /// <summary>
    /// 이름으로 규칙을 찾는다.
    /// </summary>
    private static CleaningRule Rule(Winapp2ParseResult result, string name)
    {
        return Assert.Single(result.Rules, rule => rule.Name == name);
    }

    /// <summary>
    /// 앱 카드 묶음 기준: Section= 텍스트 값이 있으면 그 값(LangSecRef가 함께 있어도), 숫자 LangSecRef만 있거나 Section= 값이 숫자면 규칙 이름(" *" 제거)이다.
    /// </summary>
    [Fact]
    public void 앱_묶음_기준은_숫자가_아닌_Section_값_또는_규칙_이름이다()
    {
        const string TEXT = """
            [Google Chrome Caches *]
            Section=Google Chrome Web Browser
            DetectFile=%LocalAppData%\Google\Chrome*
            FileKey1=%LocalAppData%\Google\Chrome*\User Data|*-journal|RECURSE

            [Both Keys *]
            LangSecRef=3021
            Section=Vendor Suite
            DetectFile=%LocalAppData%\Vendor
            FileKey1=%LocalAppData%\Vendor|*.log

            [Lang Only *]
            LangSecRef=3022
            DetectFile=%LocalAppData%\Vendor
            FileKey1=%LocalAppData%\Vendor|*.tmp

            [Numeric Section *]
            Section=3021
            DetectFile=%LocalAppData%\Vendor
            FileKey1=%LocalAppData%\Vendor|*.bak
            """;

        var result = Winapp2Parser.Parse(TEXT, RuleOrigin.Community);

        Assert.Equal("Google Chrome Web Browser", Rule(result, "Google Chrome Caches").AppGroup);
        Assert.Equal("Vendor Suite", Rule(result, "Both Keys").AppGroup);
        Assert.Equal("Lang Only", Rule(result, "Lang Only").AppGroup);
        Assert.Equal("Numeric Section", Rule(result, "Numeric Section").AppGroup);
    }

    /// <summary>Discord 발췌: 탐지 키 3개(OR), FileKey 13개, 세미콜론 패턴·RECURSE·REMOVESELF(하위 포함으로 관측)·와일드카드 경로를 그대로 보존한다.</summary>
    [Fact]
    public void Discord_발췌를_지원_규칙으로_해석한다()
    {
        var discord = Rule(ParseFixture("snapshot-excerpts.ini"), "Discord");

        Assert.True(discord.IsSupported);
        Assert.Equal("winapp2:Discord", discord.Id);
        Assert.Equal(RuleOrigin.Community, discord.Origin);
        Assert.Equal("3022", discord.Section);
        Assert.Equal(3, discord.DetectKeys.Count);
        Assert.All(discord.DetectKeys, key => Assert.Equal(RuleRegistryHive.CurrentUser, key.Hive));
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DiscordPTB", discord.DetectKeys[2].SubKey);
        Assert.Equal(13, discord.FileKeys.Count);

        var logs = discord.FileKeys[2];
        Assert.Equal(@"%AppData%\Discord*", logs.PathTemplate);
        Assert.Equal(["*-journal", "*.old", "*.tmp", "LOG", "modules.log", "Network Persistent State", "QuotaManager"], logs.Patterns);
        Assert.True(logs.Recurse);
        Assert.False(logs.RemoveSelf);

        var cache = discord.FileKeys[3];
        Assert.Equal(@"%AppData%\Discord*\*Cache", cache.PathTemplate);
        Assert.True(cache.Recurse);
        Assert.True(cache.RemoveSelf);

        var reports = discord.FileKeys[5];
        Assert.Equal(["*"], reports.Patterns);
        Assert.False(reports.Recurse);
        Assert.Equal(0, discord.RegKeyCount);
    }

    /// <summary>NuGet 발췌: DetectFile 하나, 와일드카드 폴더(*Cache)의 REMOVESELF FileKey.</summary>
    [Fact]
    public void NuGet_발췌를_해석한다()
    {
        var nuget = Rule(ParseFixture("snapshot-excerpts.ini"), "Microsoft NuGet Package Cache");

        Assert.True(nuget.IsSupported);
        Assert.Empty(nuget.DetectKeys);
        Assert.Equal([@"%LocalAppData%\NuGet"], nuget.DetectFiles);
        var key = Assert.Single(nuget.FileKeys);
        Assert.Equal(@"%LocalAppData%\NuGet\*Cache", key.PathTemplate);
        Assert.True(key.Recurse);
    }

    /// <summary>FILE 제외(Everything)와 PATH 제외(Steam Games)는 끝 구분자를 떼고 종류·패턴을 보존한다.</summary>
    [Fact]
    public void FILE과_PATH_제외를_해석한다()
    {
        var result = ParseFixture("snapshot-excerpts.ini");
        var everything = Rule(result, "Everything");
        var steamGames = Rule(result, "Steam Games");

        Assert.True(everything.IsSupported);
        Assert.Equal(2, everything.ExcludeKeys.Count);
        Assert.Equal(ExcludeKind.File, everything.ExcludeKeys[0].Kind);
        Assert.Equal(@"%AppData%\Everything", everything.ExcludeKeys[0].PathTemplate);
        Assert.Equal(["Filters.csv"], everything.ExcludeKeys[0].Patterns);
        Assert.Equal(2, everything.DetectFiles.Count);

        Assert.True(steamGames.IsSupported);
        Assert.Equal(3, steamGames.ExcludeKeys.Count);
        Assert.All(steamGames.ExcludeKeys, exclude => Assert.Equal(ExcludeKind.Path, exclude.Kind));
        Assert.Equal(@"%ProgramFiles%\Steam\steamapps\common\Supreme Ruler 1936\Cache", steamGames.ExcludeKeys[0].PathTemplate);
        Assert.Equal(["*"], steamGames.ExcludeKeys[0].Patterns);
    }

    /// <summary>스냅샷 발췌의 미지원 규칙은 사유별로 건너뛴다(RegKey만, %UserName%, HKU 탐지, 패턴 없는 FileKey, 삭제 지시어, REG 제외).</summary>
    [Theory]
    [InlineData("3delite Filesystem Dialogs Library", UnsupportedRuleReason.RegistryOnly, null)]
    [InlineData("GroupWise Messenger", UnsupportedRuleReason.UnresolvedVariable, "UserName")]
    [InlineData("7-Zip", UnsupportedRuleReason.UnsupportedDetectRoot, "HKU")]
    [InlineData("Dropbox", UnsupportedRuleReason.MalformedEntry, "FileKey")]
    [InlineData("Steam", UnsupportedRuleReason.UnsupportedDirective, "RemoveEmptyFoldersOnly")]
    [InlineData("Windows Shell - Folder/Dialog Box View Settings", UnsupportedRuleReason.UnsupportedExclude, "REG")]
    public void 스냅샷_발췌의_미지원_규칙을_사유와_함께_건너뛴다(string name, UnsupportedRuleReason reason, string? detail)
    {
        var rule = Rule(ParseFixture("snapshot-excerpts.ini"), name);

        Assert.False(rule.IsSupported);
        Assert.Equal(reason, rule.UnsupportedReason);
        Assert.Equal(detail, rule.UnsupportedDetail);
    }

    /// <summary>RegKey는 효과를 지원하지 않는 항목으로 개수만 기록한다.</summary>
    [Fact]
    public void RegKey는_개수만_기록한다()
    {
        var result = ParseFixture("snapshot-excerpts.ini");

        Assert.Equal(1, Rule(result, "3delite Filesystem Dialogs Library").RegKeyCount);
        Assert.Equal(14, Rule(result, "7-Zip").RegKeyCount);
    }

    /// <summary>발췌 요약: 전체 10, 지원 4, 사유별 미지원 수.</summary>
    [Fact]
    public void 발췌_요약에_지원_미지원_수를_기록한다()
    {
        var report = ParseFixture("snapshot-excerpts.ini").Report;

        Assert.Equal(10, report.TotalRules);
        Assert.Equal(4, report.SupportedRules);
        Assert.Equal(6, report.UnsupportedRules);
        Assert.Equal(1, report.UnsupportedByReason[UnsupportedRuleReason.RegistryOnly]);
        Assert.Equal(1, report.UnsupportedByReason[UnsupportedRuleReason.UnsupportedExclude]);
        Assert.False(report.UnsupportedByReason.ContainsKey(UnsupportedRuleReason.DetectOs));
        Assert.Equal(0, report.IgnoredLines);
    }

    /// <summary>구성 fixture: 스냅샷에 없는 문법(DetectOS·알 수 없는 키·SpecialDetect·볼륨 루트·UNC·탐지 없음·와일드카드 한도·알 수 없는 제외/플래그·패턴 구분자)은 규칙 전체를 건너뛴다.</summary>
    [Theory]
    [InlineData("OS Limited Fake App", UnsupportedRuleReason.DetectOs, "DetectOS")]
    [InlineData("Unknown Key Fake App", UnsupportedRuleReason.UnknownKey, "FutureKey")]
    [InlineData("Special Detect Fake App", UnsupportedRuleReason.SpecialDetect, "SpecialDetect")]
    [InlineData("Volume Root Fake App", UnsupportedRuleReason.VolumeRootPath, "FileKey")]
    [InlineData("Volume Root Slash Fake App", UnsupportedRuleReason.VolumeRootPath, "FileKey")]
    [InlineData("Unc Fake App", UnsupportedRuleReason.UncOrDevicePath, "FileKey")]
    [InlineData("No Detection Fake App", UnsupportedRuleReason.NoSafeDetection, null)]
    [InlineData("Too Deep Fake App", UnsupportedRuleReason.WildcardBoundExceeded, "FileKey")]
    [InlineData("Unknown Exclude Flag Fake App", UnsupportedRuleReason.UnsupportedExclude, "FOLDER")]
    [InlineData("Unknown Flag Fake App", UnsupportedRuleReason.UnsupportedDirective, "RECURSEALL")]
    [InlineData("Pattern Separator Fake App", UnsupportedRuleReason.MalformedEntry, "FileKey")]
    public void 구성_fixture의_미지원_문법을_건너뛴다(string name, UnsupportedRuleReason reason, string? detail)
    {
        var rule = Rule(ParseFixture("constructed-cases.ini"), name);

        Assert.False(rule.IsSupported);
        Assert.Equal(reason, rule.UnsupportedReason);
        Assert.Equal(detail, rule.UnsupportedDetail);
    }

    /// <summary>미지원 규칙은 일부만 적용하지 않도록 관측 대상을 비워 둔다(탐지·FileKey·제외 없음).</summary>
    [Fact]
    public void 미지원_규칙은_관측_대상을_남기지_않는다()
    {
        var rule = Rule(ParseFixture("constructed-cases.ini"), "Unknown Exclude Flag Fake App");

        Assert.Empty(rule.FileKeys);
        Assert.Empty(rule.ExcludeKeys);
        Assert.Empty(rule.DetectKeys);
        Assert.Empty(rule.DetectFiles);
    }

    /// <summary>Default·Warning·HKLM·HKCR 탐지, 와일드카드 DetectFile, FILE/PATH 제외, RegKey를 함께 가진 규칙을 지원한다.</summary>
    [Fact]
    public void 제외와_여러_탐지를_가진_규칙을_지원한다()
    {
        var rule = Rule(ParseFixture("constructed-cases.ini"), "Exclude File And Path Fake App");

        Assert.True(rule.IsSupported);
        Assert.True(rule.HasWarning);
        Assert.Equal(1, rule.RegKeyCount);
        Assert.Equal([RuleRegistryHive.LocalMachine, RuleRegistryHive.ClassesRoot], rule.DetectKeys.Select(key => key.Hive));
        Assert.Equal("FakeVendor.Document", rule.DetectKeys[1].SubKey);
        Assert.Equal([@"%LocalAppData%\FakeVendor\Excl*"], rule.DetectFiles);
        Assert.Equal(2, rule.FileKeys.Count);
        Assert.Equal(["*.log", "*.tmp"], rule.FileKeys[0].Patterns);
        Assert.Equal(ExcludeKind.File, rule.ExcludeKeys[0].Kind);
        Assert.Equal(["keep.log"], rule.ExcludeKeys[0].Patterns);
        Assert.Equal(ExcludeKind.Path, rule.ExcludeKeys[1].Kind);
        Assert.Equal(@"%LocalAppData%\FakeVendor\Excludes\Saved", rule.ExcludeKeys[1].PathTemplate);
    }

    /// <summary>섹션 밖의 주석 아닌 줄은 무시하고 개수를 기록한다.</summary>
    [Fact]
    public void 섹션_밖_줄은_무시하고_센다()
    {
        var report = ParseFixture("constructed-cases.ini").Report;

        Assert.Equal(12, report.TotalRules);
        Assert.Equal(1, report.SupportedRules);
        Assert.Equal(1, report.IgnoredLines);
    }

    /// <summary>같은 이름의 섹션이 두 번 나오면 두 번째는 형식 오류로 건너뛴다. 보충 규칙 ID는 supplement: 접두사를 쓴다.</summary>
    [Fact]
    public void 중복_섹션과_보충_규칙_ID를_처리한다()
    {
        const string TEXT = "[Dup *]\nDetectFile=%LocalAppData%\\Dup\nFileKey1=%LocalAppData%\\Dup|*\n\n[Dup *]\nDetectFile=%LocalAppData%\\Dup\nFileKey1=%LocalAppData%\\Dup|*\n";

        var result = Winapp2Parser.Parse(TEXT, RuleOrigin.Supplement);

        Assert.Equal(2, result.Rules.Count);
        Assert.True(result.Rules[0].IsSupported);
        Assert.Equal("supplement:Dup", result.Rules[0].Id);
        Assert.Equal(UnsupportedRuleReason.MalformedEntry, result.Rules[1].UnsupportedReason);
    }

    /// <summary>CRLF 줄 끝·앞뒤 공백·UTF-8 BOM이 있어도 같은 결과를 낸다.</summary>
    [Fact]
    public void 줄_끝과_BOM에_영향받지_않는다()
    {
        const string TEXT = "\uFEFF[Crlf App *]\r\nDetect=HKCU\\Software\\Crlf\r\nFileKey1=%LocalAppData%\\Crlf|*.log|RECURSE  \r\n";

        var rule = Assert.Single(Winapp2Parser.Parse(TEXT, RuleOrigin.Community).Rules);

        Assert.True(rule.IsSupported);
        Assert.Equal("Crlf App", rule.Name);
        Assert.True(rule.FileKeys[0].Recurse);
    }
}
