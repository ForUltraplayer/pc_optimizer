/**
 * @file    : AppCacheExportPrivacyTests.cs
 * @author  : rudals252
 * @brief   : 기본 내보내기에서 앱 캐시 측정값의 절대 경로(다른 드라이브 Steam 라이브러리·Program Files·UNC)가 드라이브와 관계없이 folder-N 토큰이 되고, %SystemRoot% 아래 시스템 경로와 앱 캐시가 아닌 측정값은 그대로이며, 규칙 ID·앱 이름은 남는지 검증
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
/// 앱 캐시 경로 토큰화(<see cref="PathTokenizer.RegisterAbsolutePath"/>, <see cref="ReportExporter"/>)를 검증합니다. 경로는 모두 가짜입니다.
/// </summary>
public sealed class AppCacheExportPrivacyTests
{
    private const string PROFILE = @"C:\Users\Kimtester";
    private const string WINDOWS = @"C:\Windows";

    /// <summary>등록한 드라이브·UNC 경로는 대소문자와 관계없이 같은 토큰이 되고, 하위 경로 앞부분도 바뀌며, 드라이브 루트·상대 경로는 등록되지 않는다.</summary>
    [Fact]
    public void 등록한_절대_경로를_토큰으로_바꾼다()
    {
        var tokenizer = new PathTokenizer();
        tokenizer.RegisterAbsolutePath(@"D:\SteamLibrary\steamapps\shadercache\");
        tokenizer.RegisterAbsolutePath(@"\\fileserver\share\npm");
        tokenizer.RegisterAbsolutePath(@"E:\");
        tokenizer.RegisterAbsolutePath("relative\\path");

        Assert.Equal(@"D:\folder-1", tokenizer.Tokenize(@"d:\steamlibrary\steamapps\shadercache"));
        Assert.Equal(@"위치 D:\folder-1\123, 끝", tokenizer.Tokenize(@"위치 D:\SteamLibrary\steamapps\shadercache\123, 끝"));
        Assert.Equal(PathTokenizer.NETWORK_ROOT + @"\folder-2", tokenizer.Tokenize(@"\\fileserver\share\npm"));
        Assert.Equal(@"D:\SteamLibrary\steamapps\shadercacheX", tokenizer.Tokenize(@"D:\SteamLibrary\steamapps\shadercacheX"));
        Assert.Equal(@"E:\", tokenizer.Tokenize(@"E:\"));
        Assert.Equal(["folder-1"], tokenizer.FindTokens(@"D:\folder-1"));
    }

    /// <summary>
    /// 앱 캐시 규칙 출력(가짜 측정값)을 기본 내보내기하면 프로필 밖 경로(다른 드라이브·Program Files·UNC 설정 경로)가 JSON에 없고,
    /// %SystemRoot% 아래 경로와 앱 캐시가 아닌 측정값의 경로는 그대로이며, 규칙 ID·이름은 남는다.
    /// </summary>
    [Fact]
    public void 앱_캐시_경로는_드라이브와_관계없이_토큰이_된다()
    {
        var snapshot = AppCacheTestData.Snapshot(
            ProbeStatus.Success,
            AppCacheTestData.Catalog(),
            AppCacheTestData.Rules(
                new FakeAppRule("supplement:Steam shader cache", "Steam shader cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 1000, 1,
                    Paths: [@"D:\Kim Games\SteamLibrary\steamapps\shadercache", @"C:\Program Files (x86)\Steam\steamapps\shadercache"]),
                new FakeAppRule("winapp2:Windows Temp Cache", "Windows Temp Cache", AppCacheProbeContract.RULE_STATE_OBSERVED, 10, 1,
                    Paths: [WINDOWS + @"\ServiceProfiles\LocalService\AppData\Local\Temp"]),
                new FakeAppRule("winapp2:Discord", "Discord", AppCacheProbeContract.RULE_STATE_OBSERVED, 10, 1, Paths: [PROFILE + @"\AppData\Roaming\discord\Cache"])),
            AppCacheTestData.Configs(("nuget", AppCacheProbeContract.CONFIG_STATE_UNC)));
        var findings = new AppCacheRule().Evaluate(snapshot).ToList();
        findings.Add(new Finding(
            "storage.other", FindingCategory.Storage, "다른 측정값", [FileScanTestData.M("fileScan.temp[0].path", new TextValue(@"D:\Kim Games\Other"))],
            string.Empty, Verdict.Info, null, null, null, null, []));
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

        var json = new ReportExporter(new PersonalDataScrubber(PROFILE, "Kimtester", "DESKTOP-FAKE01"), WINDOWS).SerializeAnonymized(report);

        foreach (var forbidden in new[] { "Kim Games\\\\SteamLibrary", "SteamLibrary", "Program Files (x86)", "discord\\\\Cache", "Kimtester" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(@"D:\\folder-", json, StringComparison.Ordinal);
        Assert.Contains(@"C:\\folder-", json, StringComparison.Ordinal);
        Assert.Contains(@"C:\\Windows\\ServiceProfiles\\LocalService\\AppData\\Local\\Temp", json, StringComparison.Ordinal);
        Assert.Contains(@"D:\\Kim Games\\Other", json, StringComparison.Ordinal);
        Assert.Contains("supplement:Steam shader cache", json, StringComparison.Ordinal);
        Assert.Contains("Steam shader cache", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(ReportExporter.EXPORT_KIND_ANONYMIZED, document.RootElement.GetProperty(ReportExporter.EXPORT_KIND_PROPERTY).GetString());
    }
}
