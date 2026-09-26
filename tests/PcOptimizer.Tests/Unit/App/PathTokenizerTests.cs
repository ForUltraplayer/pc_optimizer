/**
 * @file    : PathTokenizerTests.cs
 * @author  : rudals252
 * @brief   : 기본 내보내기의 프로필 하위 경로 토큰화(같은 경로 같은 토큰, 다른 경로 다른 토큰, 대소문자·구분자 무시, 프로필 자체·ProgramData는 유지, 문장 속 경로 경계)와 같은 Finding의 마지막 폴더 이름 치환, 실제 미분류 규칙 출력 내보내기에서 폴더 이름 비노출을 검증
 */

// 기본 패키지
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Rules;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="PathTokenizer"/>와 <see cref="ReportExporter"/>의 경로 토큰화를 검증합니다. 값은 모두 가짜입니다.
/// </summary>
public sealed class PathTokenizerTests
{
    private const string PROFILE = @"C:\Users\Kimtester";
    private const string PLACEHOLDER = PersonalDataScrubber.PROFILE_PLACEHOLDER;

    /// <summary>같은 경로는 대소문자·구분자·끝 구분자와 관계없이 같은 토큰, 다른 경로는 다른 토큰이며 처음 본 순서로 번호를 매긴다.</summary>
    [Fact]
    public void 같은_경로는_같은_토큰_다른_경로는_다른_토큰이다()
    {
        var tokenizer = new PathTokenizer();

        var first = tokenizer.Tokenize(PLACEHOLDER + @"\Projects\Secret Game");
        var same = tokenizer.Tokenize(PLACEHOLDER + @"/projects/SECRET GAME/");
        var other = tokenizer.Tokenize(PLACEHOLDER + @"\Videos2");

        Assert.Equal(PLACEHOLDER + @"\folder-1", first);
        Assert.Equal(PLACEHOLDER + @"\folder-1", same.TrimEnd('/', '\\'));
        Assert.Equal(PLACEHOLDER + @"\folder-2", other);
    }

    /// <summary>프로필 자체와 프로필 밖 경로(ProgramData·시스템 경로)는 그대로 둔다.</summary>
    [Theory]
    [InlineData(PLACEHOLDER)]
    [InlineData(@"C:\ProgramData\Vendor\Cache")]
    [InlineData(@"C:\Windows\Temp")]
    [InlineData("평범한 문장")]
    public void 프로필_밖_경로는_그대로_둔다(string text)
    {
        Assert.Equal(text, new PathTokenizer().Tokenize(text));
    }

    /// <summary>문장 속 경로는 따옴표·쉼표·세미콜론·줄바꿈에서 끝나고, 끝의 마침표는 경로에 넣지 않는다.</summary>
    [Fact]
    public void 문장_속_경로의_경계를_지킨다()
    {
        var tokenizer = new PathTokenizer();

        var quoted = tokenizer.Tokenize("\"" + PLACEHOLDER + @"\AppData\Local\App\run.exe"" --flag");
        var sentence = tokenizer.Tokenize("위치 " + PLACEHOLDER + @"\Data\A; 다음 " + PLACEHOLDER + @"\Data\B.");

        Assert.Equal("\"" + PLACEHOLDER + @"\folder-1"" --flag", quoted);
        Assert.Equal("위치 " + PLACEHOLDER + @"\folder-2; 다음 " + PLACEHOLDER + @"\folder-3.", sentence);
    }

    /// <summary>
    /// 토큰이 가리키는 경로의 마지막 이름은 같은 Finding의 문장에서 작은따옴표로 인용된 곳만 토큰으로 바뀐다.
    /// 인용하지 않은 고정 문구(예: 동기화 루트 이름과 같은 제품 이름 OneDrive)와 이름이 겹치는 다른 인용은 그대로다.
    /// </summary>
    [Fact]
    public void 인용된_마지막_폴더_이름만_토큰으로_바꾼다()
    {
        var tokenizer = new PathTokenizer();
        var tokens = tokenizer.FindTokens(tokenizer.Tokenize(PLACEHOLDER + @"\Work\BigGames") + " " + tokenizer.Tokenize(PLACEHOLDER + @"\OneDrive"));

        var replaced = tokenizer.ReplaceLeafNames("미분류 대용량 폴더 'biggames': 관측 3.0 GB · 'BigGamesX' 유지 · 동기화 폴더는 OneDrive만 감지", tokens);

        Assert.Equal("미분류 대용량 폴더 'folder-1': 관측 3.0 GB · 'BigGamesX' 유지 · 동기화 폴더는 OneDrive만 감지", replaced);
    }

    /// <summary>
    /// 실제 미분류 규칙 출력(가짜 측정값)을 내보내면 프로필 아래 폴더 이름(상위·마지막)이 JSON 어디에도 없고, ID·측정값·제목이 같은 토큰으로 이어진다.
    /// ProgramData 후보 경로는 그대로 남는다.
    /// </summary>
    [Fact]
    public void 미분류_규칙_출력을_내보내면_프로필_폴더_이름이_없다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Unclassified(
        [
            new FakeCandidate(PROFILE + @"\PrivateProjects\BigGames", 3_000_000_000, 12, [".iso=3000000000"], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new FakeCandidate(PROFILE + @"\AppData\Local\SecretVault", 2_000_000_000, 3, [".bin=2000000000"], null),
            new FakeCandidate(@"C:\ProgramData\VendorCache", 1_500_000_000, 1, [], null),
        ],
        [(PROFILE + @"\AppData\Local\HiddenPartial", 5_000_000_000)]));
        var findings = new UnclassifiedFolderRule().Evaluate(snapshot);
        var report = new ScanReport
        {
            ScanId = Guid.NewGuid(),
            StartedAtUtc = FileScanTestData.OBSERVED_AT,
            CompletedAtUtc = FileScanTestData.OBSERVED_AT,
            Outcome = ScanOutcome.Completed,
            AppVersion = "1.0.0",
            RulesVersion = "test",
            UserContext = new UserContext("anon", IsElevated: false),
            ProbeSummaries = [],
            Findings = findings,
        };

        var json = new ReportExporter(new PersonalDataScrubber(PROFILE, "Kimtester", "DESKTOP-FAKE01")).SerializeAnonymized(report);

        foreach (var name in new[] { "PrivateProjects", "BigGames", "SecretVault", "HiddenPartial", "AppData", "Kimtester" })
        {
            Assert.DoesNotContain(name, json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("VendorCache", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var candidate = document.RootElement.GetProperty("findings").EnumerateArray()
            .Single(f => f.GetProperty("id").GetString()!.EndsWith("folder-1", StringComparison.Ordinal));
        Assert.Equal(UnclassifiedFolderRule.FINDING_ID_PREFIX + PLACEHOLDER + @"\folder-1", candidate.GetProperty("id").GetString());
        Assert.Contains("'folder-1'", candidate.GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            candidate.GetProperty("measured").EnumerateArray(),
            m => m.GetProperty("value").TryGetProperty("value", out var value) && value.GetString() == PLACEHOLDER + @"\folder-1");
    }
}
