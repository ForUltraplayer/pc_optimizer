/**
 * @file    : ScanServiceSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 기본 구성 검사 서비스를 이 PC에서 끝까지 실행하고 익명화 JSON을 임시 폴더에 내보내 메모리·전원 Finding과 개인정보 제거를 확인
 */

// 기본 패키지
using System.IO;
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
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

    /// <summary>실제 검사를 끝까지 실행하면 메모리·전원 Finding이 나오고 기본 내보내기에 개인 경로·이름이 없다.</summary>
    [Fact]
    public async Task 실제_검사_결과를_익명화해_내보낸다()
    {
        var service = ScanService.CreateDefault(NullAppLogger.Instance);

        var result = await service.RunScanAsync(onlineCheckRequested: false, CancellationToken.None);

        foreach (var finding in result.Report.Findings)
        {
            output.WriteLine($"[{finding.Category}] {finding.Verdict} {finding.CannotVerifyReason?.ToString() ?? string.Empty} | {finding.Title}");
        }

        Assert.All(result.Report.ProbeSummaries, summary => Assert.NotEqual(ProbeStatus.Failed, summary.Status));
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Memory && f.Verdict != Verdict.CannotVerify);
        Assert.Contains(result.Report.Findings, f => f.Category == FindingCategory.Power && f.Verdict == Verdict.Info);

        var path = Path.Combine(Path.GetTempPath(), EXPORT_FILE_NAME);
        await new ReportExporter(PersonalDataScrubber.FromEnvironment()).ExportAnonymizedAsync(result.Report, path, CancellationToken.None);
        output.WriteLine($"exported: {path}");

        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(ScanReport.SCHEMA_VERSION, document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"" + Environment.UserName + "\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
