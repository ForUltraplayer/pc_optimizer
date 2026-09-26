/**
 * @file    : ScanRootCatalogTests.cs
 * @author  : rudals252
 * @brief   : 스캔 루트 계획의 중첩 루트 제거(프로필 안 TEMP·탐색기 캐시, 같은 경로), 다른 사용자 프로필·볼륨 루트 거부, 해석 실패, 보호 루트 안 루트 제외, 미분류 선정 루트·제외 경로, 볼륨 수 계산을 가짜 환경으로 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="ScanRootCatalog"/>를 검증합니다. 실제 환경 변수·파일 시스템을 읽지 않습니다.
/// </summary>
public sealed class ScanRootCatalogTests
{
    private const string PROFILE = @"C:\Users\tester";

    /// <summary>
    /// 표준 가짜 환경을 만든다.
    /// </summary>
    private static FakePathEnvironment Environment(string temp = PROFILE + @"\AppData\Local\Temp")
    {
        return new FakePathEnvironment { Profile = PROFILE }
            .WithVariable("ProgramData", @"C:\ProgramData")
            .WithVariable("TEMP", temp)
            .WithVariable("SystemRoot", @"C:\Windows")
            .WithVariable("LocalAppData", PROFILE + @"\AppData\Local");
    }

    /// <summary>
    /// 루트 ID로 계획을 찾는다.
    /// </summary>
    private static ScanRootPlan Root(ScanPlan plan, string id)
    {
        return plan.Roots.Single(root => root.Id == id);
    }

    /// <summary>프로필 안의 TEMP·탐색기 캐시는 중첩으로 따로 순회하지 않고, 시스템 임시 위치는 각각 순회한다.</summary>
    [Fact]
    public void 중첩_루트는_따로_순회하지_않는다()
    {
        var plan = new ScanRootCatalog(Environment()).Build(new ResolvedProtection([]));

        Assert.Equal(new ScanRootPlan(FileScanProbeContract.ROOT_PROFILE, PROFILE, ScanRootPlanState.Planned), Root(plan, FileScanProbeContract.ROOT_PROFILE));
        Assert.Equal(ScanRootPlanState.Planned, Root(plan, FileScanProbeContract.ROOT_PROGRAM_DATA).State);
        Assert.Equal(
            new ScanRootPlan(FileScanProbeContract.LOCATION_USER_TEMP, PROFILE + @"\AppData\Local\Temp", ScanRootPlanState.Nested, FileScanProbeContract.ROOT_PROFILE),
            Root(plan, FileScanProbeContract.LOCATION_USER_TEMP));
        Assert.Equal(ScanRootPlanState.Nested, Root(plan, FileScanProbeContract.LOCATION_THUMBNAIL_CACHE).State);
        Assert.Equal(@"C:\Windows\Temp", Root(plan, FileScanProbeContract.LOCATION_WINDOWS_TEMP).Path);
        Assert.Equal(ScanRootPlanState.Planned, Root(plan, FileScanProbeContract.LOCATION_WINDOWS_TEMP).State);
        Assert.Equal(@"C:\Windows\SoftwareDistribution\Download", Root(plan, FileScanProbeContract.LOCATION_UPDATE_DOWNLOAD).Path);
        Assert.Equal(
            @"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization",
            Root(plan, FileScanProbeContract.LOCATION_DELIVERY_OPTIMIZATION).Path);
        Assert.Equal([PROFILE, @"C:\ProgramData"], plan.SelectionRoots);
        Assert.Equal([ScanRootCatalog.THUMBNAIL_CACHE_PATTERN, ScanRootCatalog.ICON_CACHE_PATTERN], plan.Locations.Single(l => l.Id == FileScanProbeContract.LOCATION_THUMBNAIL_CACHE).FilePatterns);
    }

    /// <summary>같은 경로의 루트(TEMP가 Windows 임시 폴더와 같음)는 한 번만 순회한다.</summary>
    [Fact]
    public void 같은_경로의_루트는_한_번만_순회한다()
    {
        var plan = new ScanRootCatalog(Environment(temp: @"c:\windows\temp\")).Build(new ResolvedProtection([]));

        var planned = plan.Roots.Where(root => root.State == ScanRootPlanState.Planned).Select(root => root.Path!.ToUpperInvariant()).ToList();
        Assert.Equal(planned.Count, planned.Distinct().Count());
        Assert.Contains(plan.Roots, root => root.State == ScanRootPlanState.Nested && root.Path!.Equals(@"c:\windows\temp", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>다른 사용자 프로필 안의 경로와 볼륨 루트는 거부한다.</summary>
    [Theory]
    [InlineData(@"C:\Users\other\AppData\Local\Temp")]
    [InlineData(@"C:\")]
    [InlineData(@"\\server\share\temp")]
    public void 다른_프로필과_볼륨_루트는_거부한다(string temp)
    {
        var plan = new ScanRootCatalog(Environment(temp)).Build(new ResolvedProtection([]));

        Assert.Equal(ScanRootPlanState.Rejected, Root(plan, FileScanProbeContract.LOCATION_USER_TEMP).State);
        Assert.DoesNotContain(plan.Roots, root => root.State == ScanRootPlanState.Planned && root.Path!.StartsWith(@"C:\Users\other", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>환경 변수가 없으면 해석 실패이며, 보호 루트 안의 루트는 순회하지 않는다.</summary>
    [Fact]
    public void 해석_실패와_보호_루트_안의_루트를_구분한다()
    {
        var environment = new FakePathEnvironment { Profile = PROFILE }
            .WithVariable("ProgramData", @"C:\ProgramData")
            .WithVariable("TEMP", @"D:\Sync\Temp");
        var protection = new ResolvedProtection([new ProtectedRoot(@"D:\Sync", ProtectedRootOrigin.CloudSync, "OneDrive")]);

        var plan = new ScanRootCatalog(environment).Build(protection);

        Assert.Equal(ScanRootPlanState.Protected, Root(plan, FileScanProbeContract.LOCATION_USER_TEMP).State);
        Assert.Equal(ScanRootPlanState.Unresolved, Root(plan, FileScanProbeContract.LOCATION_WINDOWS_TEMP).State);
        Assert.Contains(@"D:\Sync", plan.ExcludedPaths);
        Assert.Contains(@"D:\Sync\Temp", plan.ExcludedPaths);
    }

    /// <summary>볼륨 수는 파일 시스템에 접근하지 않고 펼친 경로의 드라이브 문자로 센다.</summary>
    [Fact]
    public void 볼륨_수를_센다()
    {
        Assert.Equal(1, new ScanRootCatalog(Environment()).CountPlannedVolumes());
        Assert.Equal(2, new ScanRootCatalog(Environment(temp: @"D:\Temp")).CountPlannedVolumes());
        Assert.Equal(1, new ScanRootCatalog(new FakePathEnvironment()).CountPlannedVolumes());
    }
}
