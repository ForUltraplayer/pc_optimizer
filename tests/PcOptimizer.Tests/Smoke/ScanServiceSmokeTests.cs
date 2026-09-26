/**
 * @file    : ScanServiceSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 기본 구성 검사 서비스를 이 PC에서 끝까지 실행하고 익명화 JSON을 임시 폴더에 내보내 메모리·전원·디스플레이·드라이버·그래픽·보안·저장소·시작 프로그램·파일 스캔(임시 위치·미분류)·앱 캐시 Finding과 개인정보·장치 ID·프로필 하위 폴더 이름·앱 캐시 경로·설정 인증 값 제거를 확인
 */

// 기본 패키지
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>
/// App의 검사 서비스 → 규칙 → 익명화 내보내기까지의 종단 스모크 테스트입니다(네트워크·관리자 권한 없음).
/// 결과 JSON은 <see cref="EXPORT_FILE_NAME"/>로 임시 폴더에 남겨 사람이 확인할 수 있게 합니다.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class ScanServiceSmokeTests(ITestOutputHelper output)
{
    /// <summary>내보낸 JSON 파일 이름(임시 폴더).</summary>
    public const string EXPORT_FILE_NAME = "pcoptimizer-smoke-export.json";

    /// <summary>실제 검사를 끝까지 실행하면 P3b까지의 분류별 Finding이 나오고 기본 내보내기에 개인 경로·이름이 없다.</summary>
    [Fact]
    public async Task 실제_검사_결과를_익명화해_내보낸다()
    {
        var service = ScanService.CreateDefault(NullAppLogger.Instance);

        var result = await service.RunScanAsync(onlineCheckRequested: false, CancellationToken.None);

        foreach (var summary in result.Report.ProbeSummaries)
        {
            output.WriteLine($"probe {summary.ProbeId}: {summary.Status}");
        }

        foreach (var finding in result.Report.Findings)
        {
            output.WriteLine($"[{finding.Category}] {finding.Id} | {finding.Verdict} {finding.CannotVerifyReason?.ToString() ?? string.Empty} | {finding.Title}");
        }

        Assert.All(result.Report.ProbeSummaries, summary => Assert.NotEqual(ProbeStatus.Failed, summary.Status));
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Memory && f.Verdict != Verdict.CannotVerify);
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Power && f.Verdict == Verdict.Info);
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Display && f.Id.StartsWith(DisplayRefreshRule.FINDING_ID_PREFIX, StringComparison.Ordinal));
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Driver && f.Id.StartsWith(InstalledDriverRule.FINDING_ID_PREFIX, StringComparison.Ordinal));
        Assert.Contains(result.Report.Findings, f => f.Id == SecurityStatusRule.VBS_FINDING_ID);
        Assert.Contains(result.Report.Findings, f => f.Id == SecurityStatusRule.MEMORY_INTEGRITY_FINDING_ID);
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Graphics);
        Assert.Contains(result.Report.Findings, f => f.Id.StartsWith(StorageSpaceRule.FINDING_ID_PREFIX, StringComparison.Ordinal));
        Assert.Contains(result.Report.Findings, f => f.Id.StartsWith(DiskHealthRule.FINDING_ID_PREFIX, StringComparison.Ordinal));
        Assert.Contains(result.Report.Findings, f => f.Id == StartupItemsRule.SUMMARY_FINDING_ID && f.Verdict == Verdict.Info);
        Assert.Contains(result.Report.Findings, f => f.Id == StartupItemsRule.BOOT_IMPACT_FINDING_ID && f.CannotVerifyReason == CannotVerifyReason.Unsupported);
        Assert.DoesNotContain(result.Report.Findings, f => f.Category == FindingCategory.Startup && f.Verdict == Verdict.Candidate);

        var path = Path.Combine(Path.GetTempPath(), EXPORT_FILE_NAME);
        await new ReportExporter(PersonalDataScrubber.FromEnvironment()).ExportAnonymizedAsync(result.Report, path, CancellationToken.None);
        output.WriteLine($"exported: {path}");

        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(ScanReport.SCHEMA_VERSION, document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"" + Environment.UserName + "\"", json, StringComparison.OrdinalIgnoreCase);

        // 장치 내부 ID(모니터 장치 경로·PnP 인스턴스/하드웨어 ID·볼륨 경로·중괄호 GUID)는 토큰으로 바뀌어 원문이 남지 않는다.
        Assert.DoesNotMatch(new Regex(@"VEN_|DISPLAY#|Volume\{|(?<![A-Za-z0-9_])(?:PCI|ROOT|DISPLAY|USB|SWD)\\\\|\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), json);
        Assert.Contains(DisplayRefreshRule.FINDING_ID_PREFIX + "display-1", json, StringComparison.Ordinal);
        Assert.Contains(InstalledDriverRule.FINDING_ID_PREFIX + "pnp-", json, StringComparison.Ordinal);
        Assert.Contains(DiskHealthRule.FINDING_ID_PREFIX + "guid-", json, StringComparison.Ordinal);

        // 파일 스캔(P4): 요약·임시 위치·미분류 요약 Finding이 있고, 기본 내보내기에 프로필 하위 폴더 경로·이름이 남지 않는다.
        Assert.Contains(result.Report.Findings, f => f.Id == FileScanSummaryRule.SUMMARY_FINDING_ID);
        Assert.Contains(result.Report.Findings, f => f.Id == UnclassifiedFolderRule.SUMMARY_FINDING_ID);
        Assert.Equal(5, result.Report.Findings.Count(f => f.Id.StartsWith(TempLocationsRule.FINDING_ID_PREFIX, StringComparison.Ordinal)));
        foreach (var finding in document.RootElement.GetProperty("findings").EnumerateArray()
            .Where(f => f.GetProperty("id").GetString()!.StartsWith(TempLocationsRule.FINDING_ID_PREFIX, StringComparison.Ordinal)
                || f.GetProperty("id").GetString()!.StartsWith(UnclassifiedFolderRule.FINDING_ID_PREFIX, StringComparison.Ordinal)
                || f.GetProperty("id").GetString()!.StartsWith(FileScanSummaryRule.SUMMARY_FINDING_ID, StringComparison.Ordinal)))
        {
            output.WriteLine($"export {finding.GetProperty("id").GetString()} | {finding.GetProperty("verdict").GetString()} | {finding.GetProperty("title").GetString()}");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profileCandidates = result.Report.Findings
            .Where(f => f.Category == FindingCategory.Unclassified)
            .SelectMany(f => f.Measured)
            .Select(m => m.Value)
            .OfType<TextValue>()
            .Select(value => value.Value)
            .Where(value => value.StartsWith(profile + @"", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        output.WriteLine($"profile candidate paths checked: {profileCandidates.Count}");
        var unclassifiedJson = string.Concat(document.RootElement.GetProperty("findings").EnumerateArray()
            .Where(f => f.GetProperty("category").GetString() == nameof(FindingCategory.Unclassified))
            .Select(f => f.GetRawText()));
        AssertAppCacheExport(result.Report, document, json, profile);

        foreach (var candidatePath in profileCandidates)
        {
            var relative = candidatePath[(profile.Length + 1)..];
            // JSON 문자열 안의 역슬래시는 \\로 기록된다. 같은 상대 경로가 시스템 경로 일부로 나올 수 있으므로(예: ...\NetworkService\AppData\Local) 프로필 자리표시자 기준으로 확인한다.
            Assert.DoesNotContain(
                (PersonalDataScrubber.PROFILE_PLACEHOLDER + @"\" + relative).Replace(@"\", @"\\", StringComparison.Ordinal), json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("'" + Path.GetFileName(candidatePath) + "'", unclassifiedJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 앱 캐시(P5): 규칙 목록 요약 Finding이 있고, 익명화 JSON의 앱 캐시 Finding을 출력하며, 설정 파일 인증 값 표지와
    /// 앱 캐시 측정값의 프로필 밖 절대 경로(%SystemRoot% 아래 제외) 원문이 JSON에 없는지 확인한다.
    /// </summary>
    private void AssertAppCacheExport(ScanReport report, JsonDocument document, string json, string profile)
    {
        Assert.Contains(report.Findings, f => f.Id == RuleCatalogSummaryRule.FINDING_ID);
        foreach (var finding in document.RootElement.GetProperty("findings").EnumerateArray()
            .Where(f => f.GetProperty("category").GetString() == nameof(FindingCategory.AppCache)))
        {
            output.WriteLine($"export {finding.GetProperty("id").GetString()} | {finding.GetProperty("verdict").GetString()} | {finding.GetProperty("title").GetString()}");
        }

        var summary = document.RootElement.GetProperty("findings").EnumerateArray().Single(f => f.GetProperty("id").GetString() == RuleCatalogSummaryRule.FINDING_ID);
        output.WriteLine("catalog evidence: " + summary.GetProperty("evidence").GetString());
        output.WriteLine("catalog detail: " + summary.GetProperty("detail").GetString());

        foreach (var marker in new[] { "_authToken", "_auth=", "ClearTextPassword", "packageSourceCredentials", "AutoLoginUser" })
        {
            Assert.DoesNotContain(marker, json, StringComparison.OrdinalIgnoreCase);
        }

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var rawPaths = report.Findings
            .Where(f => f.Category == FindingCategory.AppCache)
            .SelectMany(f => f.Measured)
            .Where(m => m.Name.StartsWith(AppCacheProbeContract.MEASUREMENT_PREFIX, StringComparison.Ordinal))
            .SelectMany(m => m.Value switch
            {
                TextValue text => [text.Value],
                TextListValue list => list.Values,
                _ => [],
            })
            .Where(value => value.Length > 3 && value[1] == ':' && value[2] == '\\'
                && !value.StartsWith(profile, StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        output.WriteLine($"app cache non-profile paths checked: {rawPaths.Count}");
        foreach (var raw in rawPaths)
        {
            Assert.DoesNotContain(raw.Replace(@"\", @"\\", StringComparison.Ordinal), json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
