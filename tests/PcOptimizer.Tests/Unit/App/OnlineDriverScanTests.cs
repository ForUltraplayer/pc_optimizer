/**
 * @file    : OnlineDriverScanTests.cs
 * @author  : rudals252
 * @brief   : 검사 서비스 수준에서 온라인 확인을 켜지 않으면 NVIDIA·WUA 요청이 한 번도 없고(NotRequested), 켜면 조회·비교 결과가 나오며, NVIDIA 실패에도 설치 드라이버 카드가 남고, 익명화 내보내기에 공식 URL·업데이트 제목은 남고 장치 ID는 토큰화되는지 가짜 WMI·저장한 HTTP 응답·가짜 WUA로 검증
 */

// 기본 패키지
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;
using PcOptimizer.Tests.Unit.Rules;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// 온라인 드라이버 확인의 실행 경계와 결과·내보내기를 검증합니다. 실제 네트워크·Windows Update 호출은 없습니다.
/// </summary>
public sealed class OnlineDriverScanTests
{
    private const string NVIDIA_PNP = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1\4&TEST&0&0009";
    private const string WU_TITLE = "테스트 제조사 - Display - 1.2.3.4";

    /// <summary>
    /// 가짜 환경 묶음입니다.
    /// </summary>
    private sealed record OnlineTestEnvironment(ScanService Service, FixtureHttpTransport Transport, FakeUpdateSearchGateway Gateway);

    /// <summary>
    /// NVIDIA 어댑터 하나를 보고하는 가짜 WMI와 저장한 응답·가짜 WUA로 서비스를 만든다.
    /// </summary>
    private static OnlineTestEnvironment CreateEnvironment(FixtureHttpTransport? transport = null)
    {
        var controller = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = NvidiaFixtures.GPU_NAME,
            ["DriverVersion"] = "32.0.16.1656",
            ["DriverDate"] = "20260820000000.000000-000",
            ["PNPDeviceID"] = NVIDIA_PNP,
        };
        var entity = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PNPDeviceID"] = NVIDIA_PNP,
            ["HardwareID"] = new[] { @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1" },
        };
        var wmi = new FakeWmiClient().WithRows(InstalledGpuProbe.VIDEO_CONTROLLER_CLASS, controller).WithRows(InstalledGpuProbe.PNP_ENTITY_CLASS, entity);
        var http = transport ?? NvidiaFixtures.CompleteTransport();
        var gateway = FakeUpdateSearchGateway.Returning(new WuaSearchOutcome(
            WindowsUpdateProbeContract.RESULT_SUCCEEDED,
            [new WuaDriverUpdate(WU_TITLE, "테스트 모델", "테스트 제조사", new DateTime(2026, 3, 4), "Display", ["5000001"], false, true)],
            false,
            null,
            null));
        var clock = new FakeClock { UtcNow = FakeProbe.OBSERVED_AT };
        var service = new ScanService(
            [new InstalledGpuProbe(wmi, clock), new NvidiaDriverLookupProbe(wmi, new NvidiaLookupClient(http), clock), new WindowsUpdateDriverProbe(gateway, clock)],
            [new InstalledDriverRule(), new DriverUpdateRule(DriverRuleTestData.CATALOG), new WindowsUpdateDriverRule()],
            new ScanOptions(),
            new ScanReportVersions("test-app", "test-rules"),
            clock,
            NullAppLogger.Instance,
            () => false);
        return new OnlineTestEnvironment(service, http, gateway);
    }

    /// <summary>온라인 확인을 켜지 않은 검사는 NVIDIA·WUA 요청을 한 번도 하지 않고 NotRequested로 표시하며 설치 드라이버 카드는 남는다.</summary>
    [Fact]
    public async Task 온라인_확인을_켜지_않으면_네트워크_요청이_없다()
    {
        var environment = CreateEnvironment();

        var result = await environment.Service.RunScanAsync(onlineCheckRequested: false, CancellationToken.None);

        Assert.Empty(environment.Transport.Requests);
        Assert.Equal(0, environment.Gateway.Calls);
        foreach (var probeId in new[] { NvidiaLookupProbeContract.PROBE_ID, WindowsUpdateProbeContract.PROBE_ID })
        {
            Assert.True(result.Snapshot.TryGetProbe(probeId, out var probe));
            Assert.Equal(ProbeStatus.Skipped, probe.Status);
            var finding = Assert.Single(result.Report.Findings, f => f.Id == ProbeResultConverter.STATUS_FINDING_ID_PREFIX + probeId);
            Assert.Equal(CannotVerifyReason.NotRequested, finding.CannotVerifyReason);
        }

        Assert.Contains(result.Report.Findings, f => f.Id == InstalledDriverRule.FINDING_ID_PREFIX + NVIDIA_PNP);
        Assert.DoesNotContain(result.Report.Findings, f => f.Id.StartsWith(DriverUpdateRule.FINDING_ID_PREFIX, StringComparison.Ordinal));
    }

    /// <summary>온라인 확인을 켜면 NVIDIA(제품 목록·Game Ready·Studio) 조회와 WUA 검색을 한 번씩 하고 비교·후보 카드가 나온다.</summary>
    [Fact]
    public async Task 온라인_확인을_켜면_조회하고_비교한다()
    {
        var environment = CreateEnvironment();

        var result = await environment.Service.RunScanAsync(onlineCheckRequested: true, CancellationToken.None);

        Assert.Equal(3, environment.Transport.Requests.Count);
        Assert.Equal(1, environment.Gateway.Calls);
        var update = Assert.Single(result.Report.Findings, f => f.Id == DriverUpdateRule.FINDING_ID_PREFIX + NVIDIA_PNP);
        Assert.Equal(Verdict.Candidate, update.Verdict);
        Assert.Contains(result.Report.Findings, f => f.Id == WindowsUpdateDriverRule.CANDIDATES_FINDING_ID);
        Assert.Contains(result.Report.Findings, f => f.Id == InstalledDriverRule.FINDING_ID_PREFIX + NVIDIA_PNP);
    }

    /// <summary>NVIDIA 조회가 네트워크 오류로 실패해도 설치 드라이버 카드는 남고 실패는 NetworkFailed 확인 불가로 표시한다.</summary>
    [Fact]
    public async Task NVIDIA_실패에도_설치_카드가_남는다()
    {
        var environment = CreateEnvironment(new FixtureHttpTransport().Throw(_ => true, NvidiaFixtures.ConnectionFailure()));

        var result = await environment.Service.RunScanAsync(onlineCheckRequested: true, CancellationToken.None);

        Assert.Contains(result.Report.Findings, f => f.Id == InstalledDriverRule.FINDING_ID_PREFIX + NVIDIA_PNP && f.Verdict == Verdict.Info);
        var status = Assert.Single(result.Report.Findings, f => f.Id == ProbeResultConverter.STATUS_FINDING_ID_PREFIX + NvidiaLookupProbeContract.PROBE_ID);
        Assert.Equal(CannotVerifyReason.NetworkFailed, status.CannotVerifyReason);
        Assert.DoesNotContain(result.Report.Findings, f => f.Id.StartsWith(DriverUpdateRule.FINDING_ID_PREFIX, StringComparison.Ordinal));
    }

    /// <summary>익명화 내보내기는 NVIDIA 공식 상세·다운로드 URL과 Windows Update 제목을 그대로 두고, PnP 장치 ID는 토큰으로 바꾼다.</summary>
    [Fact]
    public async Task 내보내기는_공식_URL과_제목을_남기고_장치_ID를_바꾼다()
    {
        var environment = CreateEnvironment();
        var result = await environment.Service.RunScanAsync(onlineCheckRequested: true, CancellationToken.None);
        var scrubber = new PersonalDataScrubber(@"C:\Users\Kimtester", "Kimtester", "DESKTOP-FAKE01");

        var json = new ReportExporter(scrubber).SerializeAnonymized(result.Report);

        using var document = JsonDocument.Parse(json);
        var strings = new List<string>();
        CollectStrings(document.RootElement, strings);
        Assert.Contains(DriverRuleTestData.STUDIO_LATEST.DetailsUrl, strings);
        Assert.Contains(DriverRuleTestData.STUDIO_LATEST.DownloadUrl, strings);
        Assert.Contains(WU_TITLE, strings);
        Assert.DoesNotContain(strings, value => value.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(strings, value => value.StartsWith(DriverUpdateRule.FINDING_ID_PREFIX + "pnp-", StringComparison.Ordinal));
    }

    /// <summary>
    /// JSON 트리의 모든 문자열 값을 모은다.
    /// </summary>
    private static void CollectStrings(JsonElement element, List<string> strings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                strings.Add(element.GetString()!);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectStrings(property.Value, strings);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectStrings(item, strings);
                }

                break;
            default:
                break;
        }
    }
}
