/**
 * @file    : DriverOnlineTests.cs
 * @author  : rudals252
 * @brief   : [Online] 이 PC에서 실제 NVIDIA 공개 조회 한 번과 실제 Windows Update 드라이버 검색 한 번, 그리고 온라인 확인을 켠 기본 구성 검사→익명화 내보내기를 실행해 실패가 아니고 필드가 있는지 확인(기본·Smoke 필터에서 제외, 명시적으로만 실행)
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
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Tests.Smoke;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Online;

/// <summary>
/// 네트워크·Windows Update에 실제로 연결하는 온라인 테스트입니다. 특정 버전·날짜·후보 수를 기대값으로 두지 않고 상태와 필드 존재만 봅니다.
/// 실행: <c>dotnet test --filter "Category=Online"</c>.
/// </summary>
[Trait("Category", "Online")]
public sealed class DriverOnlineTests(ITestOutputHelper output)
{
    /// <summary>내보낸 JSON 파일 이름(임시 폴더).</summary>
    public const string EXPORT_FILE_NAME = "pcoptimizer-online-export.json";

    private static readonly ScanContext CONTEXT = new(
        Guid.NewGuid(), new UserContext("online-user", IsElevated: false), OnlineCheckRequested: true, DateTimeOffset.UtcNow);

    /// <summary>실제 NVIDIA 조회: NVIDIA 어댑터가 있으면 제품 매핑·Game Ready 최신 필드가 있고 실패가 아니다.</summary>
    [Fact]
    public async Task 실제_NVIDIA_조회는_실패하지_않는다()
    {
        using var cts = new CancellationTokenSource(ScanOptions.DEFAULT_NETWORK_TIMEOUT);

        var result = await new NvidiaDriverLookupProbe().RunAsync(CONTEXT, cts.Token);
        HardwareProbeSmokeTests.Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        var count = Assert.IsType<IntegerValue>(result.Measurements.Single(m => m.Name == NvidiaLookupProbeContract.ADAPTER_COUNT).Value).Value;
        if (count == 0)
        {
            output.WriteLine("NVIDIA 어댑터 없음: 조회 요청 없이 끝남");
            return;
        }

        string? Text(string field) => (result.Measurements.SingleOrDefault(m => m.Name == NvidiaLookupProbeContract.AdapterMeasurementName(0, field))?.Value as TextValue)?.Value;
        var state = Text(NvidiaLookupProbeContract.FIELD_LOOKUP_STATE);
        Assert.NotNull(state);
        if (state == NvidiaLookupProbeContract.STATE_LISTED)
        {
            Assert.NotNull(Text(NvidiaLookupProbeContract.BranchField(NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION)));
            Assert.NotNull(Text(NvidiaLookupProbeContract.BranchField(NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_DETAILS_URL)));
            Assert.NotNull(Text(NvidiaLookupProbeContract.FIELD_BRANCH));
        }
    }

    /// <summary>실제 Windows Update 드라이버 검색: 실패가 아니고 결과 코드와 후보 수가 있다.</summary>
    [Fact]
    public async Task 실제_WUA_검색은_실패하지_않는다()
    {
        using var cts = new CancellationTokenSource(ScanOptions.DEFAULT_WUA_SEARCH_TIMEOUT);

        var result = await new WindowsUpdateDriverProbe().RunAsync(CONTEXT, cts.Token);
        HardwareProbeSmokeTests.Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.Contains(result.Measurements, m => m.Name == WindowsUpdateProbeContract.RESULT_CODE);
        Assert.Contains(result.Measurements, m => m.Name == WindowsUpdateProbeContract.UPDATE_COUNT);
    }

    /// <summary>
    /// 온라인 확인을 켠 기본 구성 검사를 끝까지 실행하고 익명화 JSON을 내보내, NVIDIA·WUA·제조사 Finding을 출력하고 개인 경로·장치 ID 원문이 없는지 확인한다.
    /// </summary>
    [Fact]
    public async Task 온라인_검사를_익명화해_내보낸다()
    {
        var service = ScanService.CreateDefault(NullAppLogger.Instance);

        var result = await service.RunScanAsync(onlineCheckRequested: true, CancellationToken.None);

        foreach (var summary in result.Report.ProbeSummaries)
        {
            output.WriteLine($"probe {summary.ProbeId}: {summary.Status} ({summary.Duration.TotalMilliseconds:F0} ms)");
        }

        var driverFindings = result.Report.Findings.Where(f => f.Category == FindingCategory.Driver).ToList();
        foreach (var finding in driverFindings)
        {
            output.WriteLine($"[{finding.Category}] {finding.Id} | {finding.Verdict} {finding.CannotVerifyReason?.ToString() ?? string.Empty} | {finding.Title}");
            output.WriteLine($"    evidence: {finding.Evidence}");
            if (finding.Detail is { } detail)
            {
                output.WriteLine($"    detail: {detail.Replace('\n', ' ')}");
            }

            foreach (var link in finding.Actions.OfType<OpenLinkAction>())
            {
                output.WriteLine($"    link: {link.Label} -> {link.Url}");
            }
        }

        Assert.Contains(driverFindings, f => f.Id == OemSupportRule.FINDING_ID);
        Assert.DoesNotContain(result.Report.ProbeSummaries, s => s.ProbeId == NvidiaLookupProbeContract.PROBE_ID && s.Status == ProbeStatus.Skipped);
        Assert.DoesNotContain(result.Report.ProbeSummaries, s => s.ProbeId == WindowsUpdateProbeContract.PROBE_ID && s.Status == ProbeStatus.Skipped);

        var path = Path.Combine(Path.GetTempPath(), EXPORT_FILE_NAME);
        await new ReportExporter(PersonalDataScrubber.FromEnvironment()).ExportAnonymizedAsync(result.Report, path, CancellationToken.None);
        output.WriteLine($"exported: {path}");

        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace(@"\", @"\\", StringComparison.Ordinal), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"VEN_|DISPLAY#|Volume\{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), json);
        foreach (var finding in document.RootElement.GetProperty("findings").EnumerateArray()
            .Where(f => f.GetProperty("category").GetString() == nameof(FindingCategory.Driver)))
        {
            output.WriteLine($"export {finding.GetProperty("id").GetString()} | {finding.GetProperty("verdict").GetString()} | {finding.GetProperty("title").GetString()}");
        }
    }
}
