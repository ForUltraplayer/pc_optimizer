/**
 * @file    : ReportExporterTests.cs
 * @author  : rudals252
 * @brief   : 기본 JSON 내보내기의 익명화(프로필 경로·사용자명·PC명 치환), 스키마 버전 포함, 파일 저장 단위 테스트
 */

// 기본 패키지
using System.IO;
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="ReportExporter"/>와 <see cref="PersonalDataScrubber"/>의 기본 내보내기 익명화를 검증합니다. 값은 모두 가짜입니다.
/// </summary>
public sealed class ReportExporterTests
{
    private const string PROFILE = @"C:\Users\Kimtester";
    private const string USER = "Kimtester";
    private const string MACHINE = "DESKTOP-FAKE01";

    private static readonly DateTimeOffset NOW = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static readonly PersonalDataScrubber SCRUBBER = new(PROFILE, USER, MACHINE);

    /// <summary>
    /// 개인 정보가 여러 필드에 섞인 가짜 리포트를 만든다.
    /// </summary>
    private static ScanReport CreateReport()
    {
        var measurement = new Measurement(
            "cache.path",
            new TextValue(@"c:\users\kimtester\AppData\Local\Temp"),
            null,
            $"설정 파일 {PROFILE}/.npmrc",
            NOW,
            MeasurementQuality.Observed);
        var list = new Measurement(
            "paths",
            new TextListValue([@"C:/Users/Kimtester/Documents", $@"\\{MACHINE}\share"]),
            null,
            "fake",
            NOW,
            MeasurementQuality.Observed);

        var finding = new Finding(
            id: $"fake:{MACHINE}",
            category: FindingCategory.Storage,
            title: $"{USER}의 폴더",
            measured: [measurement, list],
            evidence: $"PC {MACHINE.ToLowerInvariant()}에서 {PROFILE}\\Downloads 확인",
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: $"사용자 {USER.ToUpperInvariant()}",
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction(), new OpenSettingsAction("ms-settings:display")]);

        return new ScanReport
        {
            ScanId = Guid.NewGuid(),
            StartedAtUtc = NOW,
            CompletedAtUtc = NOW,
            Outcome = ScanOutcome.Completed,
            AppVersion = "1.0.0",
            RulesVersion = "test",
            UserContext = new UserContext("anon-123", IsElevated: false),
            ProbeSummaries = [new ProbeSummary("fake", ProbeStatus.Success, TimeSpan.FromMilliseconds(12), 0, false)],
            Findings = [finding],
        };
    }

    /// <summary>기본 내보내기에서 프로필 경로·사용자명·PC명이 대소문자·구분자와 관계없이 사라진다.</summary>
    [Fact]
    public void 기본_내보내기는_개인_정보를_치환한다()
    {
        var json = new ReportExporter(SCRUBBER).SerializeAnonymized(CreateReport());

        Assert.DoesNotContain("kimtester", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DESKTOP-FAKE01", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"Users\\", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Users/", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PersonalDataScrubber.PROFILE_PLACEHOLDER, json, StringComparison.Ordinal);
        Assert.Contains(PersonalDataScrubber.USER_PLACEHOLDER, json, StringComparison.Ordinal);
        Assert.Contains(PersonalDataScrubber.MACHINE_PLACEHOLDER, json, StringComparison.Ordinal);
    }

    /// <summary>스키마 버전·내보내기 종류가 있고, 치환 대상이 아닌 값은 그대로다.</summary>
    [Fact]
    public void 스키마_버전과_나머지_값을_유지한다()
    {
        var json = new ReportExporter(SCRUBBER).SerializeAnonymized(CreateReport());

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(ScanReport.SCHEMA_VERSION, root.GetProperty("schemaVersion").GetString());
        Assert.Equal(ReportExporter.EXPORT_KIND_ANONYMIZED, root.GetProperty("exportKind").GetString());
        Assert.Equal("Completed", root.GetProperty("outcome").GetString());
        Assert.Equal("anon-123", root.GetProperty("userContext").GetProperty("anonymizedUserId").GetString());
        var finding = root.GetProperty("findings")[0];
        Assert.Equal("Info", finding.GetProperty("verdict").GetString());
        Assert.Equal("openSettings", finding.GetProperty("actions")[1].GetProperty("kind").GetString());
        Assert.Equal("ms-settings:display", finding.GetProperty("actions")[1].GetProperty("uri").GetString());
    }

    /// <summary>사용자명은 단어 경계에서만 치환해 다른 단어를 망가뜨리지 않는다.</summary>
    [Fact]
    public void 사용자명은_단어_경계에서만_치환한다()
    {
        var scrubber = new PersonalDataScrubber(profilePath: null, userName: "kim", machineName: null);

        Assert.Equal("kimchi " + PersonalDataScrubber.USER_PLACEHOLDER, scrubber.Scrub("kimchi Kim"));
    }

    /// <summary>파일로 저장하면 같은 익명화 JSON이 기록된다.</summary>
    [Fact]
    public async Task 파일로_저장한다()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcoptimizer-export-test-{Guid.NewGuid():N}.json");
        try
        {
            var exporter = new ReportExporter(SCRUBBER);
            await exporter.ExportAnonymizedAsync(CreateReport(), path, CancellationToken.None);

            var written = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("kimtester", written, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(ScanReport.SCHEMA_VERSION, written, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
