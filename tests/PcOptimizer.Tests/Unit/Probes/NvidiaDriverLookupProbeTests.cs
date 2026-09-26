/**
 * @file    : NvidiaDriverLookupProbeTests.cs
 * @author  : rudals252
 * @brief   : NVIDIA 온라인 조회 프로브를 가짜 WMI와 저장한 HTTP 응답으로 검증(측정값 계약·계열 판정, NVIDIA 없음/WMI 실패 시 요청 없음, 제품 모호·Studio 실패·Game Ready 실패·설치 버전 불명, 베타 제외, 제품 목록 1회 조회, 프로브 속성, 취소)
 */

// 기본 패키지
using System.Text.Json.Nodes;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="NvidiaDriverLookupProbe"/>를 검증합니다. 설치 버전(32.0.16.1656 → 616.56)은 테스트 예시입니다.
/// </summary>
public sealed class NvidiaDriverLookupProbeTests
{
    private const string NVIDIA_PNP = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1\4&TEST&0&0009";
    private const string SECOND_NVIDIA_PNP = @"PCI\VEN_10DE&DEV_0002&SUBSYS_00000000&REV_A1\4&TEST&0&0010";
    private const string VIRTUAL_PNP = @"ROOT\DISPLAY\0000";
    private const string INSTALLED_WINDOWS_VERSION = "32.0.16.1656";

    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, true, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 비디오 컨트롤러 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> Controller(string? name, string? version, string pnpId)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = name,
            ["DriverVersion"] = version,
            ["DriverDate"] = "20260820000000.000000-000",
            ["PNPDeviceID"] = pnpId,
        };
    }

    /// <summary>
    /// NVIDIA 어댑터 하나(+ 가상 어댑터)를 보고하는 WMI를 만든다.
    /// </summary>
    private static FakeWmiClient Wmi(string name = NvidiaFixtures.GPU_NAME, string? version = INSTALLED_WINDOWS_VERSION)
    {
        return new FakeWmiClient().WithRows(
            NvidiaDriverLookupProbe.VIDEO_CONTROLLER_CLASS,
            Controller("Parsec Virtual Display Adapter", "0.45.0.0", VIRTUAL_PNP),
            Controller(name, version, NVIDIA_PNP));
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(IWmiClient wmi, IHttpTransport transport, CancellationToken ct = default)
    {
        return new NvidiaDriverLookupProbe(wmi, new NvidiaLookupClient(transport), new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, ct);
    }

    /// <summary>
    /// 어댑터 0의 측정값을 찾는다.
    /// </summary>
    private static MeasurementValue? Value(ProbeResult result, string field, int index = 0)
    {
        return result.Measurements.SingleOrDefault(m => m.Name == NvidiaLookupProbeContract.AdapterMeasurementName(index, field))?.Value;
    }

    /// <summary>
    /// 계열 필드 측정값을 찾는다.
    /// </summary>
    private static MeasurementValue? Branch(ProbeResult result, string branch, string suffix)
    {
        return Value(result, NvidiaLookupProbeContract.BranchField(branch, suffix));
    }

    /// <summary>제품을 정확히 찾고 두 계열 목록을 받아 측정값 계약대로 기록한다(설치 616.56이 Studio 목록에만 있으면 Studio).</summary>
    [Fact]
    public async Task 두_계열_목록을_측정값으로_기록한다()
    {
        var transport = NvidiaFixtures.CompleteTransport();

        var result = await RunAsync(Wmi(), transport);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(new IntegerValue(1), result.Measurements.Single(m => m.Name == NvidiaLookupProbeContract.ADAPTER_COUNT).Value);
        Assert.Equal(new TextValue(NVIDIA_PNP), Value(result, NvidiaLookupProbeContract.FIELD_PNP_DEVICE_ID));
        Assert.Equal(new TextValue("616.56"), Value(result, NvidiaLookupProbeContract.FIELD_INSTALLED_VERSION));
        Assert.Equal(new TextValue("2026-08-20"), Value(result, NvidiaLookupProbeContract.FIELD_INSTALLED_DATE));
        Assert.Equal(new TextValue(NvidiaLookupProbeContract.STATE_LISTED), Value(result, NvidiaLookupProbeContract.FIELD_LOOKUP_STATE));
        Assert.Equal(new IntegerValue(127), Value(result, NvidiaLookupProbeContract.FIELD_PSID));
        Assert.Equal(new IntegerValue(1041), Value(result, NvidiaLookupProbeContract.FIELD_PFID));
        Assert.Equal(new IntegerValue(1), Value(result, NvidiaLookupProbeContract.FIELD_PRODUCT_MATCH_COUNT));

        var gameReady = Assert.IsType<TextListValue>(Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_VERSIONS));
        Assert.Equal(["617.14", "616.92"], gameReady.Values);
        Assert.Equal(new TextValue("617.14"), Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION));
        Assert.Equal(new TextValue("2026-09-22"), Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_RELEASE_DATE));
        Assert.Equal(new TextValue("GeForce Game Ready Driver"), Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_NAME));
        Assert.Equal(
            new TextValue("https://www.nvidia.com/en-us/drivers/details/279803/"),
            Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_DETAILS_URL));
        Assert.Equal(new BooleanValue(true), Branch(result, NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_AVAILABLE));
        Assert.Equal(new TextValue("616.92"), Branch(result, NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION));
        Assert.Equal(new TextValue("Studio"), Value(result, NvidiaLookupProbeContract.FIELD_BRANCH));

        Assert.Equal(3, transport.Requests.Count);
        Assert.All(result.Measurements, m => Assert.Equal(FakeProbe.OBSERVED_AT, m.ObservedAtUtc));
    }

    /// <summary>설치 버전이 두 계열 목록에 모두 있으면(Game Ready 10개 목록) 계열은 모호로 기록한다.</summary>
    [Fact]
    public async Task 두_목록에_있으면_모호로_기록한다()
    {
        var result = await RunAsync(Wmi(), NvidiaFixtures.CompleteTransport(NvidiaFixtures.GAME_READY_10));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new TextValue("Ambiguous"), Value(result, NvidiaLookupProbeContract.FIELD_BRANCH));
    }

    /// <summary>온라인 확인을 요청하지 않은 컨텍스트로 직접 호출해도 WMI·네트워크 요청 없이 NotRequested로 건너뛴다.</summary>
    [Fact]
    public async Task 온라인_확인을_요청하지_않으면_요청하지_않는다()
    {
        var wmi = Wmi();
        var transport = NvidiaFixtures.CompleteTransport();
        var offline = CONTEXT with { OnlineCheckRequested = false };

        var result = await new NvidiaDriverLookupProbe(wmi, new NvidiaLookupClient(transport), new FakeClock()).RunAsync(offline, CancellationToken.None);

        Assert.Equal(ProbeStatus.Skipped, result.Status);
        Assert.Equal(CannotVerifyReason.NotRequested, Assert.Single(result.Issues).Reason);
        Assert.Empty(transport.Requests);
        Assert.Empty(wmi.Queries);
    }

    /// <summary>NVIDIA 어댑터가 없으면 네트워크 요청 없이 0개 성공이다.</summary>
    [Fact]
    public async Task NVIDIA가_없으면_요청하지_않는다()
    {
        var wmi = new FakeWmiClient().WithRows(NvidiaDriverLookupProbe.VIDEO_CONTROLLER_CLASS, Controller("가상", "0.45.0.0", VIRTUAL_PNP));
        var transport = NvidiaFixtures.CompleteTransport();

        var result = await RunAsync(wmi, transport);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new IntegerValue(0), result.Measurements.Single(m => m.Name == NvidiaLookupProbeContract.ADAPTER_COUNT).Value);
        Assert.Empty(transport.Requests);
    }

    /// <summary>WMI 조회 실패는 네트워크 요청 없이 Failed다.</summary>
    [Fact]
    public async Task WMI_실패는_요청_없이_실패다()
    {
        var wmi = new FakeWmiClient().WithFailure(NvidiaDriverLookupProbe.VIDEO_CONTROLLER_CLASS, WmiQueryStatus.AccessDenied);
        var transport = NvidiaFixtures.CompleteTransport();

        var result = await RunAsync(wmi, transport);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(transport.Requests);
    }

    /// <summary>제품 목록 연결 실패는 Failed(NetworkFailed)이며 설치 정보 측정값은 남긴다.</summary>
    [Fact]
    public async Task 제품_목록_연결_실패는_네트워크_실패다()
    {
        var transport = new FixtureHttpTransport().Throw(NvidiaFixtures.IsProductList, NvidiaFixtures.ConnectionFailure());

        var result = await RunAsync(Wmi(), transport);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(CannotVerifyReason.NetworkFailed, result.Issues[0].Reason);
        Assert.DoesNotContain("fake connection failure", result.Issues[0].Summary, StringComparison.Ordinal);
        Assert.Equal(new TextValue("616.56"), Value(result, NvidiaLookupProbeContract.FIELD_INSTALLED_VERSION));
        Assert.Equal(new TextValue(NvidiaLookupProbeContract.STATE_FAILED), Value(result, NvidiaLookupProbeContract.FIELD_LOOKUP_STATE));
        Assert.Equal(new TextValue(nameof(CannotVerifyReason.NetworkFailed)), Value(result, NvidiaLookupProbeContract.FIELD_FAILURE_REASON));
        Assert.Single(transport.Requests);
    }

    /// <summary>제품 이름이 두 항목과 일치하면 계열 목록을 요청하지 않고 모호로 기록한다(성공).</summary>
    [Fact]
    public async Task 제품이_모호하면_목록을_요청하지_않는다()
    {
        var transport = NvidiaFixtures.CompleteTransport();

        var result = await RunAsync(Wmi("GeForce GTX 1060"), transport);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new TextValue(NvidiaLookupProbeContract.STATE_PRODUCT_AMBIGUOUS), Value(result, NvidiaLookupProbeContract.FIELD_LOOKUP_STATE));
        Assert.Equal(new IntegerValue(2), Value(result, NvidiaLookupProbeContract.FIELD_PRODUCT_MATCH_COUNT));
        Assert.Single(transport.Requests);
    }

    /// <summary>Studio 목록이 HTML 오류 페이지면 Partial이며 Studio 없음·계열 모호로 기록하고 Game Ready 결과는 유지한다.</summary>
    [Fact]
    public async Task 스튜디오_실패는_부분_결과다()
    {
        var transport = new FixtureHttpTransport()
            .On(NvidiaFixtures.IsProductList, NvidiaFixtures.Read(NvidiaFixtures.PRODUCT_LIST))
            .On(NvidiaFixtures.IsGameReady, NvidiaFixtures.Read(NvidiaFixtures.GAME_READY_2))
            .On(NvidiaFixtures.IsStudio, "<html><body>Bad Gateway</body></html>");

        var result = await RunAsync(Wmi(), transport);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.NetworkFailed, Assert.Single(result.Issues).Reason);
        Assert.Equal(new BooleanValue(false), Branch(result, NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_AVAILABLE));
        Assert.Equal(new TextValue("617.14"), Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION));
        Assert.Equal(new TextValue("Ambiguous"), Value(result, NvidiaLookupProbeContract.FIELD_BRANCH));
    }

    /// <summary>Game Ready 목록 스키마 오류는 Failed(ProbeError)다.</summary>
    [Fact]
    public async Task 게임_레디_스키마_오류는_수집_오류다()
    {
        var transport = new FixtureHttpTransport()
            .On(NvidiaFixtures.IsProductList, NvidiaFixtures.Read(NvidiaFixtures.PRODUCT_LIST))
            .On(NvidiaFixtures.IsGameReady, "{ \"Success\" : \"1\", \"IDS\" : [ { \"downloadInfo\" : { \"Success\" : \"1\" } } ], \"Request\" : [ { \"psid\" : \"127\", \"pfid\" : \"1041\", \"osID\" : \"57\" } ] }");

        var result = await RunAsync(Wmi(), transport);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(CannotVerifyReason.ProbeError, result.Issues[0].Reason);
        Assert.Equal(new TextValue(nameof(CannotVerifyReason.ProbeError)), Value(result, NvidiaLookupProbeContract.FIELD_FAILURE_REASON));
    }

    /// <summary>설치 버전을 NVIDIA 표기로 바꿀 수 없으면 조회하지 않고 Partial로 기록한다.</summary>
    [Fact]
    public async Task 설치_버전을_모르면_조회하지_않는다()
    {
        var transport = NvidiaFixtures.CompleteTransport();

        var result = await RunAsync(Wmi(version: "abc"), transport);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(new TextValue(NvidiaLookupProbeContract.STATE_VERSION_UNKNOWN), Value(result, NvidiaLookupProbeContract.FIELD_LOOKUP_STATE));
        Assert.Empty(transport.Requests);
    }

    /// <summary>베타 항목은 계열 최신으로 고르지 않는다(목록 포함 확인에는 남김).</summary>
    [Fact]
    public async Task 베타는_최신으로_고르지_않는다()
    {
        var root = JsonNode.Parse(NvidiaFixtures.Read(NvidiaFixtures.GAME_READY_2))!.AsObject();
        root["IDS"]![0]!["downloadInfo"]!["IsBeta"] = "1";
        var transport = new FixtureHttpTransport()
            .On(NvidiaFixtures.IsProductList, NvidiaFixtures.Read(NvidiaFixtures.PRODUCT_LIST))
            .On(NvidiaFixtures.IsGameReady, root.ToJsonString())
            .On(NvidiaFixtures.IsStudio, NvidiaFixtures.Read(NvidiaFixtures.STUDIO_10));

        var result = await RunAsync(Wmi(), transport);

        Assert.Equal(new TextValue("616.92"), Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION));
        Assert.Contains("617.14", Assert.IsType<TextListValue>(Branch(result, NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_VERSIONS)).Values);
    }

    /// <summary>NVIDIA 어댑터가 둘이면 제품 목록은 한 번만 받는다.</summary>
    [Fact]
    public async Task 제품_목록은_한_번만_받는다()
    {
        var wmi = new FakeWmiClient().WithRows(
            NvidiaDriverLookupProbe.VIDEO_CONTROLLER_CLASS,
            Controller(NvidiaFixtures.GPU_NAME, INSTALLED_WINDOWS_VERSION, NVIDIA_PNP),
            Controller(NvidiaFixtures.GPU_NAME, INSTALLED_WINDOWS_VERSION, SECOND_NVIDIA_PNP));
        var transport = NvidiaFixtures.CompleteTransport();

        var result = await RunAsync(wmi, transport);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Single(transport.Requests, NvidiaFixtures.IsProductList);
        Assert.Equal(new IntegerValue(2), result.Measurements.Single(m => m.Name == NvidiaLookupProbeContract.ADAPTER_COUNT).Value);
    }

    /// <summary>네트워크가 필요한 시스템 범위 프로브이며 관리자 권한이 필요 없고 기본 타임아웃은 네트워크 기본값(30초)이다.</summary>
    [Fact]
    public void 프로브_속성이_맞다()
    {
        var probe = new NvidiaDriverLookupProbe(new FakeWmiClient(), new NvidiaLookupClient(new FixtureHttpTransport()), new FakeClock());

        Assert.Equal(NvidiaLookupProbeContract.PROBE_ID, probe.Id);
        Assert.True(probe.RequiresNetwork);
        Assert.False(probe.RequiresElevation);
        Assert.Equal(PcOptimizer.Core.Abstractions.ProbeScope.System, probe.Scope);
        Assert.Equal(ScanOptions.DEFAULT_NETWORK_TIMEOUT, probe.DefaultTimeout);
        Assert.Equal(FindingCategory.Driver, probe.Category);
    }

    /// <summary>취소는 예외로 전파한다(실행기가 Cancelled/Timeout으로 기록).</summary>
    [Fact]
    public async Task 취소는_전파한다()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(Wmi(), NvidiaFixtures.CompleteTransport(), cts.Token));
    }
}
