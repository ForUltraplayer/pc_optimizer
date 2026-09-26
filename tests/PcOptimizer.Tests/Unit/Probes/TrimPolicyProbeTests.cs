/**
 * @file    : TrimPolicyProbeTests.cs
 * @author  : rudals252
 * @brief   : TRIM 정책 프로브의 관리자 권한 요구, System32 fsutil 전체 경로·읽기 전용 인수·15초 제한 시간, fixture 출력 해석, 실패 경로(없음·시작 실패·시간 초과·종료 코드·해석 실패)와 일반 권한 건너뜀 검증
 */

// 기본 패키지
using System.IO;
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="TrimPolicyProbe"/>를 가짜 실행기로 검증합니다. 실제 fsutil을 실행하지 않습니다.
/// </summary>
public sealed class TrimPolicyProbeTests
{
    private const string SYSTEM_DIRECTORY = @"T:\Windows\System32";

    private static readonly ScanContext ELEVATED_CONTEXT = new(Guid.NewGuid(), EngineTestData.USER with { IsElevated = true }, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// fixture 출력을 읽는다(CP949 원시 바이트를 프로브 실행기와 같은 방식으로 디코딩).
    /// </summary>
    private static string CapturedOutput()
    {
        return Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fsutil", "ko-captured-cp949.bin")));
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeProcessRunner runner)
    {
        return new TrimPolicyProbe(runner, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }, () => SYSTEM_DIRECTORY).RunAsync(ELEVATED_CONTEXT, CancellationToken.None);
    }

    /// <summary>관리자 권한이 필요한 로컬 프로브이며 제한 시간은 fsutil 제한 시간보다 길다.</summary>
    [Fact]
    public void 관리자_권한이_필요하다()
    {
        var probe = new TrimPolicyProbe();

        Assert.True(probe.RequiresElevation);
        Assert.False(probe.RequiresNetwork);
        Assert.Equal(TimeSpan.FromSeconds(15), TrimPolicyProbe.FSUTIL_TIMEOUT);
        Assert.True(probe.DefaultTimeout > TrimPolicyProbe.FSUTIL_TIMEOUT);
    }

    /// <summary>System32의 fsutil.exe 전체 경로를 읽기 전용 조회 인수로 15초 제한과 함께 실행하고 파일 시스템별 값을 기록한다.</summary>
    [Fact]
    public async Task System32_fsutil을_읽기_전용으로_실행한다()
    {
        var runner = new FakeProcessRunner(new ProcessRunResult(ProcessRunStatus.Completed, 0, CapturedOutput(), null));

        var result = await RunAsync(runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(@"T:\Windows\System32\fsutil.exe", call.Path);
        Assert.Equal(["behavior", "query", "DisableDeleteNotify"], call.Arguments);
        Assert.Equal(TrimPolicyProbe.FSUTIL_TIMEOUT, call.Timeout);
        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new IntegerValue(0), Assert.Single(result.Measurements, m => m.Name == TrimPolicyProbeContract.DisableDeleteNotifyName("NTFS")).Value);
        Assert.Equal(new IntegerValue(0), Assert.Single(result.Measurements, m => m.Name == TrimPolicyProbeContract.DisableDeleteNotifyName("ReFS")).Value);
    }

    /// <summary>실행 파일 없음·시작 실패·시간 초과·0이 아닌 종료 코드·해석 실패는 측정값 없이 Failed다.</summary>
    [Theory]
    [InlineData(ProcessRunStatus.NotFound, null, "", CannotVerifyReason.Unsupported)]
    [InlineData(ProcessRunStatus.StartFailed, null, "", CannotVerifyReason.ProbeError)]
    [InlineData(ProcessRunStatus.TimedOut, null, "", CannotVerifyReason.Timeout)]
    [InlineData(ProcessRunStatus.Completed, 1, "NTFS DisableDeleteNotify = 0", CannotVerifyReason.ProbeError)]
    [InlineData(ProcessRunStatus.Completed, 0, "관계없는 출력", CannotVerifyReason.ProbeError)]
    public async Task 실행_실패는_실패다(ProcessRunStatus status, int? exitCode, string output, CannotVerifyReason reason)
    {
        var result = await RunAsync(new FakeProcessRunner(new ProcessRunResult(status, exitCode, output, null)));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(reason, Assert.Single(result.Issues).Reason);
    }

    /// <summary>일반 권한 검사에서는 조율기가 프로브를 실행하지 않고 Skipped/ElevationRequired로 남긴다(fsutil 실행 없음).</summary>
    [Fact]
    public async Task 일반_권한에서는_건너뛴다()
    {
        var runner = new FakeProcessRunner(new ProcessRunResult(ProcessRunStatus.Completed, 0, CapturedOutput(), null));
        var probe = new TrimPolicyProbe(runner, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }, () => SYSTEM_DIRECTORY);
        var coordinator = new ScanCoordinator([probe], [], new ScanOptions(), new ScanReportVersions("test", "test"), new FakeClock(), new RecordingLogger());
        var context = new ScanContext(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

        var scan = await coordinator.RunScanAsync(context, CancellationToken.None);

        var result = Assert.Single(scan.Snapshot.ProbeResults);
        Assert.Equal(ProbeStatus.Skipped, result.Status);
        Assert.Equal(CannotVerifyReason.ElevationRequired, Assert.Single(result.Issues).Reason);
        Assert.Empty(runner.Calls);
    }
}
