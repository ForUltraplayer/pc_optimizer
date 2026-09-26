/**
 * @file    : HardwareProbeSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 이 PC에서 실제 메모리·전원·디스플레이·시스템 정보·그래픽 설정(HAGS)·게임 모드·보안 상태 프로브를 실행해 실패하지 않고 측정값을 내는지 확인(기본 테스트 필터에서 제외)
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Drivers;
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
    /// 결과를 테스트 출력에 남긴다(측정 이름·값·단위·출처·품질과 Issue). 다른 스모크 테스트도 함께 쓴다.
    /// </summary>
    /// <param name="writer">테스트 출력.</param>
    /// <param name="result">프로브 결과.</param>
    internal static void Dump(ITestOutputHelper writer, ProbeResult result)
    {
        writer.WriteLine($"probe={result.ProbeId} status={result.Status} measurements={result.Measurements.Count} issues={result.Issues.Count}");
        foreach (var measurement in result.Measurements)
        {
            var value = measurement.Value is TextListValue list ? "[" + string.Join(", ", list.Values) + "]" : measurement.Value.ToString();
            writer.WriteLine($"  {measurement.Name} = {value} unit={measurement.Unit ?? "-"} source={measurement.Source} quality={measurement.Quality}");
        }

        foreach (var issue in result.Issues)
        {
            writer.WriteLine($"  issue {issue.Reason}: {issue.Summary}");
        }
    }

    /// <summary>실제 메모리 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 메모리_프로브가_측정값을_낸다()
    {
        var result = await new MemoryProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 전원 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 전원_프로브가_측정값을_낸다()
    {
        var result = await new PowerProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 디스플레이 프로브가 실패하지 않고 대상별 측정값을 낸다(모니터 수·주사율은 기대값으로 두지 않음).</summary>
    [Fact]
    public async Task 디스플레이_프로브가_측정값을_낸다()
    {
        var result = await new DisplayProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 시스템 정보 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 시스템_정보_프로브가_측정값을_낸다()
    {
        var result = await new SystemInfoProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 그래픽 설정 프로브가 실패하지 않고 존재 여부 측정값을 낸다(값 부재도 성공 측정).</summary>
    [Fact]
    public async Task 그래픽_설정_프로브가_측정값을_낸다()
    {
        var result = await new GraphicsSettingsProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 게임 모드(HKCU) 프로브가 실패하지 않고 존재 여부 측정값을 낸다(값 부재도 성공 측정).</summary>
    [Fact]
    public async Task 게임_모드_프로브가_측정값을_낸다()
    {
        var result = await new GameModeSettingsProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 보안 상태 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 보안_상태_프로브가_측정값을_낸다()
    {
        var result = await new SecurityStatusProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 설치 GPU 프로브가 실패하지 않고 측정값을 낸다(네트워크 없음).</summary>
    [Fact]
    public async Task 설치_GPU_프로브가_측정값을_낸다()
    {
        var result = await new InstalledGpuProbe().RunAsync(CONTEXT, CancellationToken.None);
        Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }
}
