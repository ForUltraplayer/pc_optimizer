/**
 * @file    : ScanCoordinatorTests.cs
 * @author  : rudals252
 * @brief   : 실행 조율(예외 격리·타임아웃·취소·정책 차단·늦은 결과 차단·중복 검사 억제·병렬도 제한) 단위 테스트
 */

// 기본 패키지
using System.Diagnostics;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.Engine;

/// <summary>
/// <see cref="ScanCoordinator"/>의 실행 조율 계약을 가짜 프로브로 검증합니다.
/// </summary>
public class ScanCoordinatorTests
{
    /// <summary>검사가 끝났다고 보는 상한 시간. 짧은 타임아웃(200ms)보다 충분히 길게 둔다.</summary>
    private static readonly TimeSpan SCAN_UPPER_BOUND = TimeSpan.FromSeconds(5);

    /// <summary>취소 후 검사가 돌아와야 하는 상한 시간(프로브 타임아웃보다 훨씬 짧음).</summary>
    private static readonly TimeSpan CANCEL_RETURN_BOUND = TimeSpan.FromSeconds(1);

    /// <summary>취소 즉시 반환을 확인할 때 쓰는 긴 프로브 타임아웃.</summary>
    private static readonly TimeSpan LONG_TIMEOUT = TimeSpan.FromSeconds(120);

    /// <summary>비동기 신호를 기다리는 상한 시간.</summary>
    private static readonly TimeSpan SIGNAL_WAIT = TimeSpan.FromSeconds(5);

    private static readonly ScanReportVersions VERSIONS = new("test-app", "test-rules");

    /// <summary>
    /// 테스트용 검사 컨텍스트를 만든다.
    /// </summary>
    private static ScanContext CreateContext(bool elevated = false, bool online = false)
    {
        return new ScanContext(
            Guid.NewGuid(),
            new UserContext("anon-user", elevated),
            online,
            FakeClock.DEFAULT_NOW);
    }

    /// <summary>
    /// 테스트용 조율기를 만든다.
    /// </summary>
    private static ScanCoordinator CreateCoordinator(
        IEnumerable<IProbe> probes,
        IEnumerable<IRule>? rules = null,
        ScanOptions? options = null,
        IAppLogger? logger = null,
        IClock? clock = null)
    {
        return new ScanCoordinator(
            probes,
            rules ?? [],
            options ?? new ScanOptions(),
            VERSIONS,
            clock ?? new FakeClock(),
            logger ?? NullAppLogger.Instance);
    }

    /// <summary>
    /// 리포트에서 특정 프로브의 요약을 찾는다.
    /// </summary>
    private static ProbeSummary SummaryOf(ScanResult result, string probeId)
    {
        return Assert.Single(result.Report.ProbeSummaries, s => s.ProbeId == probeId);
    }

    /// <summary>
    /// 스냅샷에서 특정 프로브 결과를 찾는다.
    /// </summary>
    private static ProbeResult ResultOf(ScanResult result, string probeId)
    {
        Assert.True(result.Snapshot.TryGetProbe(probeId, out var probeResult));
        return probeResult;
    }

    /// <summary>
    /// 로그가 늦은 결과 폐기 기록인지 확인한다.
    /// </summary>
    private static bool IsLateDiscard(LogEntry entry, string probeId)
    {
        return entry.Message.StartsWith(ScanLogEvents.LATE_RESULT_DISCARDED, StringComparison.Ordinal)
            && entry.Message.Contains(probeId, StringComparison.Ordinal);
    }

    /// <summary>
    /// (b) 예외를 던지는 프로브는 Failed/ProbeError가 되고 다른 프로브 결과는 그대로 남는다.
    /// </summary>
    [Fact]
    public async Task 한_프로브의_예외는_다른_프로브_결과를_버리지_않는다()
    {
        var throwing = new ThrowingProbe("throwing");
        var first = new SuccessProbe("ok-1");
        var second = new SuccessProbe("ok-2");
        var coordinator = CreateCoordinator([first, throwing, second]);

        var result = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(ProbeStatus.Failed, SummaryOf(result, "throwing").Status);
        Assert.Equal(ProbeStatus.Success, SummaryOf(result, "ok-1").Status);
        Assert.Equal(ProbeStatus.Success, SummaryOf(result, "ok-2").Status);
        Assert.NotNull(result.Snapshot.GetMeasurement("ok-1", SuccessProbe.MEASUREMENT_NAME));
        Assert.NotNull(result.Snapshot.GetMeasurement("ok-2", SuccessProbe.MEASUREMENT_NAME));

        var issue = Assert.Single(ResultOf(result, "throwing").Issues);
        Assert.Equal(CannotVerifyReason.ProbeError, issue.Reason);
        Assert.Equal(nameof(InvalidOperationException), issue.Summary);
        Assert.DoesNotContain(ThrowingProbe.ERROR_MESSAGE, issue.Summary, StringComparison.Ordinal);

        var finding = Assert.Single(result.Report.Findings, f => f.Verdict == Verdict.CannotVerify);
        Assert.Equal(CannotVerifyReason.ProbeError, finding.CannotVerifyReason);
        Assert.Equal(ScanOutcome.Partial, result.Report.Outcome);
    }

    /// <summary>
    /// (c) 취소 토큰을 무시하고 멈춘 프로브는 Timeout으로 끝나고, 검사는 제한 시간 안에 끝나며 종료 중으로 표시된다.
    /// </summary>
    [Fact]
    public async Task 멈춘_프로브는_Timeout으로_끝나고_검사는_제한_시간_안에_끝난다()
    {
        var hanging = new HangingProbe("hanging");
        var ok = new SuccessProbe("ok");
        var logger = new RecordingLogger();
        var coordinator = CreateCoordinator([hanging, ok], logger: logger);
        var stopwatch = Stopwatch.StartNew();

        var result = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < SCAN_UPPER_BOUND, $"검사가 {stopwatch.Elapsed} 동안 끝나지 않았습니다.");
        var summary = SummaryOf(result, "hanging");
        Assert.Equal(ProbeStatus.Failed, summary.Status);
        Assert.True(summary.IsStillRunning);
        Assert.Equal(CannotVerifyReason.Timeout, Assert.Single(ResultOf(result, "hanging").Issues).Reason);
        Assert.Equal(ProbeStatus.Success, SummaryOf(result, "ok").Status);
        Assert.Contains("hanging", coordinator.DrainingProbeIds);
        Assert.Equal(ScanOutcome.Partial, result.Report.Outcome);
        Assert.Contains(logger.Entries, e => e.Message.StartsWith(ScanLogEvents.PROBE_TIMED_OUT, StringComparison.Ordinal));
    }

    /// <summary>
    /// RunAsync 안에서 스레드를 동기적으로 막는 프로브도 검사 전체를 멈추지 못한다.
    /// </summary>
    [Fact]
    public async Task 동기적으로_막힌_프로브도_검사를_멈추지_않는다()
    {
        using var blocking = new BlockingProbe("blocking");
        var ok = new SuccessProbe("ok");
        var coordinator = CreateCoordinator([blocking, ok]);
        var stopwatch = Stopwatch.StartNew();

        var result = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < SCAN_UPPER_BOUND, $"검사가 {stopwatch.Elapsed} 동안 끝나지 않았습니다.");
        Assert.Equal(ProbeStatus.Failed, SummaryOf(result, "blocking").Status);
        Assert.Equal(CannotVerifyReason.Timeout, Assert.Single(ResultOf(result, "blocking").Issues).Reason);
        Assert.Equal(ProbeStatus.Success, SummaryOf(result, "ok").Status);
    }

    /// <summary>
    /// (d) 사용자 취소 시 실행 중인 협조 프로브는 Cancelled가 되고, 아직 시작하지 않은 프로브는 시작하지 않는다.
    /// </summary>
    [Fact]
    public async Task 사용자_취소시_Cancelled이고_새_프로브를_시작하지_않는다()
    {
        var cancellable = new CancellableProbe("cancellable");
        var waiting = new SuccessProbe("waiting");
        var coordinator = CreateCoordinator([cancellable, waiting], options: new ScanOptions { MaxParallelism = 1 });
        using var cts = new CancellationTokenSource();

        var scan = coordinator.RunScanAsync(CreateContext(), cts.Token);
        await cancellable.Started.WaitAsync(SIGNAL_WAIT);
        await cts.CancelAsync();
        var result = await scan.WaitAsync(SCAN_UPPER_BOUND);

        var cancelledSummary = SummaryOf(result, "cancellable");
        Assert.Equal(ProbeStatus.Cancelled, cancelledSummary.Status);
        Assert.False(cancelledSummary.IsStillRunning);
        Assert.Equal(CannotVerifyReason.Cancelled, Assert.Single(ResultOf(result, "cancellable").Issues).Reason);

        Assert.Equal(0, waiting.InvocationCount);
        Assert.Equal(ProbeStatus.Cancelled, SummaryOf(result, "waiting").Status);
        Assert.Equal(CannotVerifyReason.Cancelled, Assert.Single(ResultOf(result, "waiting").Issues).Reason);

        Assert.Empty(coordinator.DrainingProbeIds);
        Assert.Equal(ScanOutcome.Cancelled, result.Report.Outcome);
    }

    /// <summary>
    /// 사용자 취소 시 취소를 무시하는 프로브를 기다리지 않고 곧바로 돌아온다. 그 호출은 취소 완료가 아니라
    /// 종료 중(IsStillRunning, DrainingProbeIds)으로 표시되고, 이미 끝난 결과는 유지되며, 다음 검사에서는 다시 실행하지 않는다.
    /// </summary>
    [Fact]
    public async Task 취소시_취소를_무시하는_프로브를_기다리지_않고_종료_중으로_표시한다()
    {
        var hanging = new HangingProbe("hanging", LONG_TIMEOUT);
        var ok = new SuccessProbe("ok");
        var coordinator = CreateCoordinator([ok, hanging]);
        using var cts = new CancellationTokenSource();

        var scan = coordinator.RunScanAsync(CreateContext(), cts.Token);
        await hanging.Started.WaitAsync(SIGNAL_WAIT);
        var stopwatch = Stopwatch.StartNew();
        await cts.CancelAsync();
        var result = await scan.WaitAsync(SCAN_UPPER_BOUND);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < CANCEL_RETURN_BOUND, $"취소 후 {stopwatch.Elapsed} 뒤에 돌아왔습니다.");
        var summary = SummaryOf(result, "hanging");
        Assert.Equal(ProbeStatus.Cancelled, summary.Status);
        Assert.True(summary.IsStillRunning);
        var issue = Assert.Single(ResultOf(result, "hanging").Issues);
        Assert.Equal(CannotVerifyReason.Cancelled, issue.Reason);
        Assert.Contains("still finishing after cancel", issue.Summary, StringComparison.Ordinal);
        Assert.Contains("hanging", coordinator.DrainingProbeIds);
        Assert.Equal(ProbeStatus.Success, SummaryOf(result, "ok").Status);
        Assert.Equal(ScanOutcome.Cancelled, result.Report.Outcome);

        var next = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None).WaitAsync(SCAN_UPPER_BOUND);
        Assert.Equal(1, hanging.InvocationCount);
        Assert.Equal(ProbeStatus.Skipped, SummaryOf(next, "hanging").Status);
        Assert.Equal(ScanCoordinator.DRAINING_SKIP_SUMMARY, Assert.Single(ResultOf(next, "hanging").Issues).Summary);
    }

    /// <summary>
    /// (e) 관리자 권한이 필요한 프로브는 일반 권한 검사에서 호출하지 않고 ElevationRequired로 건너뛴다.
    /// </summary>
    [Fact]
    public async Task 권한이_필요한_프로브는_호출하지_않고_ElevationRequired로_건너뛴다()
    {
        var elevatedOnly = new SuccessProbe("elevated-only", requiresElevation: true);
        var coordinator = CreateCoordinator([elevatedOnly]);

        var result = await coordinator.RunScanAsync(CreateContext(elevated: false), CancellationToken.None);

        Assert.Equal(0, elevatedOnly.InvocationCount);
        Assert.Equal(ProbeStatus.Skipped, SummaryOf(result, "elevated-only").Status);
        Assert.Equal(CannotVerifyReason.ElevationRequired, Assert.Single(ResultOf(result, "elevated-only").Issues).Reason);
        var finding = Assert.Single(result.Report.Findings);
        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.ElevationRequired, finding.CannotVerifyReason);
    }

    /// <summary>
    /// (e) 온라인 확인을 요청하지 않으면 네트워크 프로브는 호출하지 않고 NotRequested로 건너뛴다.
    /// </summary>
    [Fact]
    public async Task 온라인_확인을_요청하지_않으면_네트워크_프로브는_NotRequested로_건너뛴다()
    {
        var online = new SuccessProbe("online", requiresNetwork: true);
        var coordinator = CreateCoordinator([online]);

        var result = await coordinator.RunScanAsync(CreateContext(online: false), CancellationToken.None);

        Assert.Equal(0, online.InvocationCount);
        Assert.Equal(ProbeStatus.Skipped, SummaryOf(result, "online").Status);
        Assert.Equal(CannotVerifyReason.NotRequested, Assert.Single(ResultOf(result, "online").Issues).Reason);
        Assert.Equal(ScanOutcome.Completed, result.Report.Outcome);
    }

    /// <summary>
    /// 관리자 권한·온라인 확인 조건을 갖추면 해당 프로브를 실행한다.
    /// </summary>
    [Fact]
    public async Task 조건을_갖추면_권한_및_네트워크_프로브를_실행한다()
    {
        var probe = new SuccessProbe("both", requiresElevation: true, requiresNetwork: true);
        var coordinator = CreateCoordinator([probe]);

        var result = await coordinator.RunScanAsync(CreateContext(elevated: true, online: true), CancellationToken.None);

        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(ProbeStatus.Success, SummaryOf(result, "both").Status);
    }

    /// <summary>
    /// (f) 타임아웃 뒤 늦게 도착한 결과는 리포트에 합쳐지지 않고, 버린 사실이 로그에 남는다.
    /// </summary>
    [Fact]
    public async Task 늦게_도착한_결과는_리포트에_합쳐지지_않고_버린_사실이_기록된다()
    {
        var delayed = new DelayedProbe("delayed");
        var logger = new RecordingLogger();
        var coordinator = CreateCoordinator([delayed], logger: logger);

        var result = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);
        delayed.Complete();
        var discard = await logger.WaitForEntryAsync(e => IsLateDiscard(e, "delayed")).WaitAsync(SIGNAL_WAIT);

        Assert.Contains(result.Report.ScanId.ToString(), discard.Message, StringComparison.Ordinal);
        Assert.Equal(ProbeStatus.Failed, SummaryOf(result, "delayed").Status);
        Assert.Empty(ResultOf(result, "delayed").Measurements);
        Assert.Null(result.Snapshot.GetMeasurement("delayed", DelayedProbe.LATE_MEASUREMENT_NAME));
        Assert.DoesNotContain(
            result.Report.Findings.SelectMany(f => f.Measured),
            m => m.Name == DelayedProbe.LATE_MEASUREMENT_NAME);
        Assert.Empty(coordinator.DrainingProbeIds);
    }

    /// <summary>
    /// (g) 검사가 진행 중일 때 두 번째 검사 요청은 즉시 InvalidOperationException으로 실패한다.
    /// </summary>
    [Fact]
    public async Task 검사_진행_중_두번째_검사_요청은_즉시_실패한다()
    {
        var delayed = new DelayedProbe("delayed", FakeProbe.GENEROUS_TIMEOUT);
        var coordinator = CreateCoordinator([delayed]);

        var firstScan = coordinator.RunScanAsync(CreateContext(), CancellationToken.None);
        await delayed.Started.WaitAsync(SIGNAL_WAIT);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.RunScanAsync(CreateContext(), CancellationToken.None));

        delayed.Complete();
        var first = await firstScan.WaitAsync(SCAN_UPPER_BOUND);
        Assert.Equal(ProbeStatus.Success, SummaryOf(first, "delayed").Status);
        Assert.Equal(1, delayed.InvocationCount);
    }

    /// <summary>
    /// (h) 이전 검사에서 타임아웃된 호출이 아직 종료 중이면 다음 검사에서 다시 실행하지 않고, 종료 후에는 다시 실행한다.
    /// </summary>
    [Fact]
    public async Task 이전_실행이_아직_종료_중인_프로브는_다음_검사에서_다시_실행하지_않는다()
    {
        var delayed = new DelayedProbe("delayed");
        var logger = new RecordingLogger();
        var coordinator = CreateCoordinator([delayed], logger: logger);

        var firstScan = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);
        Assert.Equal(ProbeStatus.Failed, SummaryOf(firstScan, "delayed").Status);
        Assert.Contains("delayed", coordinator.DrainingProbeIds);

        var secondScan = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);
        Assert.Equal(1, delayed.InvocationCount);
        Assert.Equal(ProbeStatus.Skipped, SummaryOf(secondScan, "delayed").Status);
        var issue = Assert.Single(ResultOf(secondScan, "delayed").Issues);
        Assert.Equal(CannotVerifyReason.ProbeError, issue.Reason);
        Assert.Contains("previous run still finishing", issue.Summary, StringComparison.Ordinal);
        Assert.Equal(ScanOutcome.Partial, secondScan.Report.Outcome);

        delayed.Complete();
        await logger.WaitForEntryAsync(e => IsLateDiscard(e, "delayed")).WaitAsync(SIGNAL_WAIT);
        Assert.Empty(coordinator.DrainingProbeIds);

        var thirdScan = coordinator.RunScanAsync(CreateContext(), CancellationToken.None);
        await delayed.WhenInvokedAsync(2).WaitAsync(SIGNAL_WAIT);
        delayed.Complete();
        var third = await thirdScan.WaitAsync(SCAN_UPPER_BOUND);
        Assert.Equal(ProbeStatus.Success, SummaryOf(third, "delayed").Status);
    }

    /// <summary>
    /// 동시 실행 수는 MaxParallelism을 넘지 않는다.
    /// </summary>
    [Fact]
    public async Task 동시_실행_수는_MaxParallelism을_넘지_않는다()
    {
        var first = new DelayedProbe("p1", FakeProbe.GENEROUS_TIMEOUT);
        var second = new DelayedProbe("p2", FakeProbe.GENEROUS_TIMEOUT);
        var third = new DelayedProbe("p3", FakeProbe.GENEROUS_TIMEOUT);
        var coordinator = CreateCoordinator([first, second, third], options: new ScanOptions { MaxParallelism = 2 });

        var scan = coordinator.RunScanAsync(CreateContext(), CancellationToken.None);
        await Task.WhenAll(first.Started, second.Started).WaitAsync(SIGNAL_WAIT);
        Assert.Equal(0, third.InvocationCount);

        first.Complete();
        await third.Started.WaitAsync(SIGNAL_WAIT);
        second.Complete();
        third.Complete();
        var result = await scan.WaitAsync(SCAN_UPPER_BOUND);

        Assert.All(result.Report.ProbeSummaries, s => Assert.Equal(ProbeStatus.Success, s.Status));
        Assert.Equal(ScanOutcome.Completed, result.Report.Outcome);
    }

    /// <summary>
    /// 프로브가 다른 ProbeId를 담은 결과를 돌려주면 성공으로 받지 않고 Failed/ProbeError로 처리한다.
    /// </summary>
    [Fact]
    public async Task 반환한_ProbeId가_다르면_ProbeError로_처리한다()
    {
        var wrong = new WrongIdProbe("wrong");
        var coordinator = CreateCoordinator([wrong]);

        var result = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(ProbeStatus.Failed, SummaryOf(result, "wrong").Status);
        Assert.Equal(CannotVerifyReason.ProbeError, Assert.Single(ResultOf(result, "wrong").Issues).Reason);
        Assert.False(result.Snapshot.TryGetProbe("wrong-other", out _));
    }

    /// <summary>
    /// 부분 결과의 측정값은 규칙에 전달되고, 부분 수집 사실은 CannotVerify(PartialData)로 함께 남는다.
    /// </summary>
    [Fact]
    public async Task 부분_결과의_측정값은_규칙에_전달되고_PartialData도_남는다()
    {
        var partial = new PartialProbe("partial");
        var rule = new MeasurementEchoRule("echo", "partial", PartialProbe.MEASUREMENT_NAME);
        var coordinator = CreateCoordinator([partial], rules: [rule]);

        var result = await coordinator.RunScanAsync(CreateContext(), CancellationToken.None);

        Assert.Equal(1, rule.EvaluateCount);
        Assert.Contains(result.Report.Findings, f => f.Verdict == Verdict.Info && f.Id == "echo:partial");
        Assert.Contains(result.Report.Findings, f => f.CannotVerifyReason == CannotVerifyReason.PartialData);
        Assert.Equal(ScanOutcome.Partial, result.Report.Outcome);
    }

    /// <summary>
    /// 모두 성공하면 Completed이며, 리포트에 검사 ID·시각·버전·사용자 컨텍스트·스키마 버전이 담긴다.
    /// </summary>
    [Fact]
    public async Task 모두_성공하면_Completed이고_리포트_머리글이_채워진다()
    {
        var clock = new FakeClock();
        var coordinator = CreateCoordinator([new SuccessProbe("ok")], clock: clock);
        var context = CreateContext();

        var result = await coordinator.RunScanAsync(context, CancellationToken.None);

        var report = result.Report;
        Assert.Equal(ScanReport.SCHEMA_VERSION, report.SchemaVersion);
        Assert.Equal("1.0", report.SchemaVersion);
        Assert.Equal(context.ScanId, report.ScanId);
        Assert.Equal(context.ScanId, result.Snapshot.ScanId);
        Assert.Equal(context.StartedAtUtc, report.StartedAtUtc);
        Assert.Equal(clock.UtcNow, report.CompletedAtUtc);
        Assert.Equal(VERSIONS.AppVersion, report.AppVersion);
        Assert.Equal(VERSIONS.RulesVersion, report.RulesVersion);
        Assert.Equal(context.UserContext, report.UserContext);
        Assert.Equal(ScanOutcome.Completed, report.Outcome);
        Assert.Empty(report.Findings);
        Assert.Equal(clock.UtcNow, ResultOf(result, "ok").StartedAtUtc);
    }

    /// <summary>
    /// 같은 Id의 프로브를 두 번 등록하면 조율기를 만들 수 없다.
    /// </summary>
    [Fact]
    public void 중복_ProbeId는_생성시_거부한다()
    {
        Assert.Throws<ArgumentException>(() => CreateCoordinator([new SuccessProbe("dup"), new SuccessProbe("dup")]));
    }
}
