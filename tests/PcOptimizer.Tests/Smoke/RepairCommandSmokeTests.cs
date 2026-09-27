/**
 * @file    : RepairCommandSmokeTests.cs
 * @author  : rudals252
 * @brief   : 실제 System32 명령 실행 경로 확인 — 무해한 ipconfig /flushdns로 시작·출력 디코딩·종료 코드·진행 콜백을 검증(시스템을 바꾸는 명령은 실행하지 않음)
 */
using PcOptimizer.Core.Troubleshooting;
using PcOptimizer.Probes.Troubleshooting;

namespace PcOptimizer.Tests.Smoke;

/// <summary>DNS 캐시 비우기만 실제로 실행합니다. chkntfs·netsh·DISM·SFC는 실행하지 않습니다.</summary>
[Trait("Category", "Smoke")]
public sealed class RepairCommandSmokeTests
{
    /// <summary>실제 프로세스가 시작되고 출력 줄이 콜백으로 오며 종료 코드 0을 Completed로 해석합니다.</summary>
    [Fact]
    public async Task FlushDnsRunsThroughRealProcessPath()
    {
        var runner = new RepairCommandRunner();
        var lines = new List<string>();
        var progress = new Collector(lines);
        var result = await runner.RunAsync(RepairCommandCatalog.Find(RepairCommandCatalog.FLUSH_DNS)!, progress, default);
        Assert.True(result.Started, result.Code);
        Assert.Equal(RepairCommandRunner.CODE_COMPLETED, result.Code);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(lines);
        Assert.DoesNotContain(lines, l => l.Contains('\0'));
        Assert.False(result.RebootRequired);
    }

    private sealed class Collector(List<string> lines) : IProgress<string>
    {
        public void Report(string value) { lock (lines) { lines.Add(value); } }
    }
}
