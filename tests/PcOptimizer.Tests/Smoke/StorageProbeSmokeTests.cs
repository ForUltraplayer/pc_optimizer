/**
 * @file    : StorageProbeSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 이 PC에서 실제 볼륨·물리 디스크·TRIM 정책 프로브를 실행해 상태와 측정값을 확인(TRIM은 권한에 따라 건너뜀 또는 성공, 기본 테스트 필터에서 제외)
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Storage;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>
/// 저장소 프로브 실제 PC 스모크 테스트입니다. 볼륨 수·디스크 이름 등 특정 PC 값을 기대값으로 두지 않습니다.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class StorageProbeSmokeTests(ITestOutputHelper output)
{
    /// <summary>
    /// 현재 프로세스 권한으로 검사 컨텍스트를 만든다.
    /// </summary>
    private static ScanContext CurrentContext()
    {
        return new ScanContext(
            Guid.NewGuid(), new UserContext("smoke-user", ScanService.IsCurrentProcessElevated()), OnlineCheckRequested: false, DateTimeOffset.UtcNow);
    }

    /// <summary>실제 볼륨 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 볼륨_프로브가_측정값을_낸다()
    {
        var result = await new VolumeProbe().RunAsync(CurrentContext(), CancellationToken.None);
        HardwareProbeSmokeTests.Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>실제 물리 디스크 프로브가 실패하지 않고 측정값을 낸다.</summary>
    [Fact]
    public async Task 물리_디스크_프로브가_측정값을_낸다()
    {
        var result = await new PhysicalDiskProbe().RunAsync(CurrentContext(), CancellationToken.None);
        HardwareProbeSmokeTests.Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Measurements);
    }

    /// <summary>
    /// TRIM 정책 프로브를 조율기로 실행한다. 일반 권한이면 Skipped/ElevationRequired(fsutil 미실행), 관리자 권한이면 Success와 측정값이다.
    /// </summary>
    [Fact]
    public async Task TRIM_정책_프로브는_권한에_맞게_동작한다()
    {
        var context = CurrentContext();
        var coordinator = new ScanCoordinator(
            [new TrimPolicyProbe()], [], new ScanOptions(), new ScanReportVersions("smoke", "smoke"), SystemClock.Instance, NullAppLogger.Instance);

        var scan = await coordinator.RunScanAsync(context, CancellationToken.None);
        var result = Assert.Single(scan.Snapshot.ProbeResults);
        output.WriteLine($"elevated={context.IsElevated}");
        HardwareProbeSmokeTests.Dump(output, result);

        if (context.IsElevated)
        {
            Assert.Equal(ProbeStatus.Success, result.Status);
            Assert.NotEmpty(result.Measurements);
        }
        else
        {
            Assert.Equal(ProbeStatus.Skipped, result.Status);
            Assert.Equal(CannotVerifyReason.ElevationRequired, Assert.Single(result.Issues).Reason);
        }
    }
}
