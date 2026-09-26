/**
 * @file    : WindowsUpdateDriverProbeTests.cs
 * @author  : rudals252
 * @brief   : Windows Update 드라이버 검색 프로브의 결과 코드별 상태(성공·부분·실패·중단), HRESULT 사유 분류와 16진수 요약, 세부 정보 없음 → 부분, 측정값 계약, 프로브 속성, 실행기 타임아웃 → Timeout과 중단 요청 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="WindowsUpdateDriverProbe"/>를 가짜 게이트웨이로 검증합니다.
/// </summary>
public sealed class WindowsUpdateDriverProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, true, FakeProbe.OBSERVED_AT);

    private static readonly WuaDriverUpdate UPDATE = new(
        "테스트 제조사 - Display - 1.2.3.4", "테스트 모델", "테스트 제조사", new DateTime(2026, 3, 4), "Display", ["5000001"], false, true);

    /// <summary>
    /// 결과로 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(WuaSearchOutcome outcome)
    {
        return new WindowsUpdateDriverProbe(FakeUpdateSearchGateway.Returning(outcome), new FakeClock()).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>성공(2)은 Success이며 업데이트별 제목·모델·제조사·날짜·클래스·KB·내려받음 여부와 재부팅 여부를 기록한다.</summary>
    [Fact]
    public async Task 성공은_업데이트별_측정값을_기록한다()
    {
        var result = await RunAsync(new WuaSearchOutcome(WindowsUpdateProbeContract.RESULT_SUCCEEDED, [UPDATE], true, null, null));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Empty(result.Issues);
        MeasurementValue Value(string name) => result.Measurements.Single(m => m.Name == name).Value;
        Assert.Equal(new IntegerValue(2), Value(WindowsUpdateProbeContract.RESULT_CODE));
        Assert.Equal(new IntegerValue(1), Value(WindowsUpdateProbeContract.UPDATE_COUNT));
        Assert.Equal(new TextValue(UPDATE.Title!), Value(WindowsUpdateProbeContract.UpdateMeasurementName(0, WindowsUpdateProbeContract.FIELD_TITLE)));
        Assert.Equal(new TextValue("테스트 모델"), Value(WindowsUpdateProbeContract.UpdateMeasurementName(0, WindowsUpdateProbeContract.FIELD_DRIVER_MODEL)));
        Assert.Equal(new TextValue("2026-03-04"), Value(WindowsUpdateProbeContract.UpdateMeasurementName(0, WindowsUpdateProbeContract.FIELD_DRIVER_DATE)));
        Assert.Equal(new BooleanValue(false), Value(WindowsUpdateProbeContract.UpdateMeasurementName(0, WindowsUpdateProbeContract.FIELD_IS_DOWNLOADED)));
        Assert.Equal(["5000001"], Assert.IsType<TextListValue>(Value(WindowsUpdateProbeContract.UpdateMeasurementName(0, WindowsUpdateProbeContract.FIELD_KB_IDS))).Values);
        Assert.Equal(new BooleanValue(true), Value(WindowsUpdateProbeContract.REBOOT_REQUIRED));
    }

    /// <summary>결과 코드별 상태·사유: 3 → Partial(PartialData), 4 → Failed(ProbeError), 5 → Cancelled, 알 수 없는 코드 → Failed.</summary>
    [Theory]
    [InlineData(WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS, ProbeStatus.Partial, CannotVerifyReason.PartialData)]
    [InlineData(WindowsUpdateProbeContract.RESULT_FAILED, ProbeStatus.Failed, CannotVerifyReason.ProbeError)]
    [InlineData(WindowsUpdateProbeContract.RESULT_ABORTED, ProbeStatus.Cancelled, CannotVerifyReason.Cancelled)]
    [InlineData(1, ProbeStatus.Failed, CannotVerifyReason.ProbeError)]
    public async Task 결과_코드를_상태로_바꾼다(int resultCode, ProbeStatus expectedStatus, CannotVerifyReason expectedReason)
    {
        var result = await RunAsync(new WuaSearchOutcome(resultCode, [], null, null, null));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedReason, result.Issues[0].Reason);
    }

    /// <summary>COM 실패는 HRESULT로 사유를 나누고 요약에는 16진수 코드만 남긴다(정책 차단·접근 거부·네트워크).</summary>
    [Theory]
    [InlineData(unchecked((int)0x8024402C), CannotVerifyReason.NetworkFailed, "0x8024402C")]
    [InlineData(unchecked((int)0x80072EE2), CannotVerifyReason.NetworkFailed, "0x80072EE2")]
    [InlineData(unchecked((int)0x80070005), CannotVerifyReason.AccessDenied, "0x80070005")]
    [InlineData(unchecked((int)0x8024002E), CannotVerifyReason.AccessDenied, "0x8024002E")]
    [InlineData(unchecked((int)0x80240020), CannotVerifyReason.ProbeError, "0x80240020")]
    public async Task HRESULT는_사유와_코드로_기록한다(int hresult, CannotVerifyReason expected, string code)
    {
        var result = await RunAsync(new WuaSearchOutcome(null, [], null, hresult, null));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(expected, issue.Reason);
        Assert.Contains(code, issue.Summary, StringComparison.Ordinal);
        Assert.Empty(result.Measurements);
    }

    /// <summary>게이트웨이 오류 코드(세션 없음 등)는 Failed(ProbeError)다.</summary>
    [Fact]
    public async Task 오류_코드는_수집_오류다()
    {
        var result = await RunAsync(new WuaSearchOutcome(null, [], null, null, WuaSearchGateway.ERROR_SESSION_UNAVAILABLE));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(CannotVerifyReason.ProbeError, result.Issues[0].Reason);
    }

    /// <summary>드라이버 세부 정보를 못 읽은 업데이트가 있으면 제목은 남기고 Partial이다(예외 없음).</summary>
    [Fact]
    public async Task 세부_정보가_없으면_부분_결과다()
    {
        var plain = new WuaDriverUpdate("세부 정보 없는 업데이트", null, null, null, null, [], null, false);

        var result = await RunAsync(new WuaSearchOutcome(WindowsUpdateProbeContract.RESULT_SUCCEEDED, [plain], false, null, null));

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.PartialData, Assert.Single(result.Issues).Reason);
        Assert.Equal(new IntegerValue(1), result.Measurements.Single(m => m.Name == WindowsUpdateProbeContract.DETAILS_UNAVAILABLE_COUNT).Value);
        Assert.Contains(result.Measurements, m => m.Value == new TextValue("세부 정보 없는 업데이트"));
    }

    /// <summary>온라인 확인을 요청하지 않은 컨텍스트로 직접 호출해도 검색하지 않고 NotRequested로 건너뛴다.</summary>
    [Fact]
    public async Task 온라인_확인을_요청하지_않으면_검색하지_않는다()
    {
        var gateway = FakeUpdateSearchGateway.Returning(new WuaSearchOutcome(WindowsUpdateProbeContract.RESULT_SUCCEEDED, [], null, null, null));

        var result = await new WindowsUpdateDriverProbe(gateway, new FakeClock()).RunAsync(CONTEXT with { OnlineCheckRequested = false }, CancellationToken.None);

        Assert.Equal(ProbeStatus.Skipped, result.Status);
        Assert.Equal(CannotVerifyReason.NotRequested, Assert.Single(result.Issues).Reason);
        Assert.Equal(0, gateway.Calls);
    }

    /// <summary>게이트웨이에는 설치 안 됨·숨기지 않음·드라이버 조건으로만 검색을 요청한다.</summary>
    [Fact]
    public async Task 검색_조건을_지킨다()
    {
        var gateway = FakeUpdateSearchGateway.Returning(new WuaSearchOutcome(WindowsUpdateProbeContract.RESULT_SUCCEEDED, [], null, null, null));

        await new WindowsUpdateDriverProbe(gateway, new FakeClock()).RunAsync(CONTEXT, CancellationToken.None);

        Assert.Equal("IsInstalled=0 and IsHidden=0 and Type='Driver'", gateway.Criteria);
    }

    /// <summary>네트워크가 필요한 시스템 범위 프로브이며 기본 타임아웃은 WUA 검색 기본값(120초)이다.</summary>
    [Fact]
    public void 프로브_속성이_맞다()
    {
        var probe = new WindowsUpdateDriverProbe(FakeUpdateSearchGateway.Returning(new WuaSearchOutcome(2, [], null, null, null)), new FakeClock());

        Assert.Equal(WindowsUpdateProbeContract.PROBE_ID, probe.Id);
        Assert.True(probe.RequiresNetwork);
        Assert.False(probe.RequiresElevation);
        Assert.Equal(ProbeScope.System, probe.Scope);
        Assert.Equal(ScanOptions.DEFAULT_WUA_SEARCH_TIMEOUT, probe.DefaultTimeout);
    }

    /// <summary>실행기 타임아웃이 지나면 결과는 Failed(Timeout)이고 게이트웨이는 검색 작업에 중단을 요청한다.</summary>
    [Fact]
    public async Task 타임아웃은_시간_초과이고_중단을_요청한다()
    {
        var job = new FakeSearchJob(completedAtStart: false);
        var searcher = new FakeUpdateSearcher(job, () => new FakeSearchResult(WindowsUpdateProbeContract.RESULT_ABORTED));
        var gateway = new WuaSearchGateway(() => new FakeUpdateSession(searcher), () => null, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(200));
        var options = new ScanOptions
        {
            ProbeTimeoutOverrides = new Dictionary<string, TimeSpan>(StringComparer.Ordinal) { [WindowsUpdateProbeContract.PROBE_ID] = TimeSpan.FromMilliseconds(100) },
        };
        var coordinator = new ScanCoordinator(
            [new WindowsUpdateDriverProbe(gateway, new FakeClock())], [], options, new ScanReportVersions("test", "test"), new FakeClock(), NullAppLogger.Instance);

        var scan = await coordinator.RunScanAsync(CONTEXT, CancellationToken.None);

        var result = Assert.Single(scan.Snapshot.ProbeResults);
        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(CannotVerifyReason.Timeout, result.Issues[0].Reason);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (job.AbortRequests == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(1, job.AbortRequests);
    }
}
