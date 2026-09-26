/**
 * @file    : HardwareProbeSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 이 PC에서 실제 메모리·전원 프로브를 실행해 실패하지 않고 측정값을 내는지 확인(기본 테스트 필터에서 제외)
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Hardware;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>
/// 실제 PC 스모크 테스트입니다. 특정 PC 값(모듈 수·속도·계획 이름)을 기대값으로 두지 않고 상태와 측정 존재만 봅니다.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class HardwareProbeSmokeTests(ITestOutputHelper output)
{
    private static readonly ScanContext CONTEXT = new(
        Guid.NewGuid(), new UserContext("smoke-user", IsElevated: false), OnlineCheckRequested: false, DateTimeOffset.UtcNow);

    /// <summary>
    /// 결과를 테스트 출력에 남긴다(측정 이름·값·단위·출처·품질과 Issue).
    /// </summary>
    private void Dump(ProbeResult result)
    {
        output.WriteLine($"probe={result.ProbeId} status={result.Status} measurements={result.Measurements.Count} issues={result.Issues.Count}");
        foreach (var measurement in result.Measurements)
        {
            output.WriteLine($"  {measurement.Name} = {measurement.Value} unit={measurement.Unit ?? "-"} source={measurement.Source} quality={measurement.Quality}");
        }

        foreach (var issue in result.Issues)
        {
            output.WriteLine($"  issue {issue.Reason}: {issue.Summary}");
        }
    }

    /// <summary>실제 메모리 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 메모리_프로브가_측정값을_낸다()
    {
        var result = await new MemoryProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 전원 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 전원_프로브가_측정값을_낸다()
    {
        var result = await new PowerProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }
}
