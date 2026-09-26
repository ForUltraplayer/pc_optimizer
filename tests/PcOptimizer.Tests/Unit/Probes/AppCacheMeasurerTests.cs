/**
 * @file    : AppCacheMeasurerTests.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 측정기의 공유 스캔 합계 재사용과 하위 대상 몫 빼기, 겹치는 필터 대상의 파일 경로 단위 중복 제거, 대상 열거의 보호·reparse·placeholder·제외 처리, 대상당·전체 시간 예산 초과를 가짜 파일 시스템·수동 시간으로 검증
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="AppCacheMeasurer"/>를 검증합니다. 실제 파일 시스템에 접근하지 않습니다.
/// </summary>
public sealed class AppCacheMeasurerTests
{
    private const string PROFILE = FileScanServiceTests.PROFILE;
    private const string LOCAL = PROFILE + @"\AppData\Local";
    private const string OUTSIDE = @"D:\Cache";

    /// <summary>
    /// 기본 트리에 Vendor 폴더를 더한 공유 스캔 결과.
    /// </summary>
    private static async Task<(DirectoryScanResult Shared, FakeDirectoryEntrySource Source)> ScanAsync()
    {
        var source = FileScanServiceTests.Tree()
            .Dir(PROFILE + @"\AppData\Local", FakeDirectoryEntrySource.Folder("Temp"), FakeDirectoryEntrySource.Folder("Microsoft"), FakeDirectoryEntrySource.Folder("Vendor"))
            .Dir(LOCAL + @"\Vendor", FakeDirectoryEntrySource.File("a.log", 5), FakeDirectoryEntrySource.File("top.bin", 20), FakeDirectoryEntrySource.Folder("Cache"), FakeDirectoryEntrySource.Folder("Sub"))
            .Dir(LOCAL + @"\Vendor\Cache", FakeDirectoryEntrySource.File("c1", 100), FakeDirectoryEntrySource.File("c2", 200))
            .Dir(LOCAL + @"\Vendor\Sub", FakeDirectoryEntrySource.File("c.log", 11), FakeDirectoryEntrySource.File("d.txt", 13));
        var shared = await FileScanServiceTests.Service(source).GetOrScanAsync(FileScanServiceTests.Context(), CancellationToken.None);
        return (shared, source);
    }

    /// <summary>
    /// 후보.
    /// </summary>
    private static ObservationCandidate Candidate(string ruleId, ObservationPrecedence precedence, string directory, bool recurse, params string[] patterns)
    {
        return new ObservationCandidate(ruleId, precedence, directory, patterns, recurse, TargetSource.Default);
    }

    /// <summary>검토 대상(Cache)은 공유 스캔 합계를 그대로 쓰고, 그 위의 커뮤니티 전체 대상(Vendor)은 합계에서 Cache 몫을 뺀다(한 파일은 한 번).</summary>
    [Fact]
    public async Task 공유_스캔_합계에서_하위_대상_몫을_뺀다()
    {
        var (shared, _) = await ScanAsync();
        var plan = ObservationPlanner.Plan(
            [Candidate("winapp2:Vendor", ObservationPrecedence.Community, LOCAL + @"\Vendor", true, "*"), Candidate("supplement:Vendor Cache", ObservationPrecedence.Reviewed, LOCAL + @"\Vendor\Cache", true, "*")],
            [],
            []);

        var results = new AppCacheMeasurer(new FakeDirectoryEntrySource(), new ManualTimeProvider()).Measure(plan, shared, _ => false, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.All(results, result => Assert.True(result.FromSharedScan));
        Assert.Equal(300, results[0].Bytes);
        Assert.Equal(5 + 20 + 11 + 13, results[1].Bytes);
        Assert.Equal(4, results[1].FileCount);
        Assert.Equal(349, results.Sum(result => result.Bytes));
    }

    /// <summary>겹치는 필터 대상은 파일 경로 단위로 한 번만 센다: 앞선 *.log 대상이 센 Sub\c.log를 뒤 대상은 다시 세지 않는다(경로 단위이므로 중복 가능 표시).</summary>
    [Fact]
    public async Task 겹치는_필터_대상은_파일_경로로_중복을_거른다()
    {
        var (shared, source) = await ScanAsync();
        var plan = ObservationPlanner.Plan(
            [Candidate("supplement:Logs", ObservationPrecedence.Reviewed, LOCAL + @"\Vendor", true, "*.log"), Candidate("winapp2:Sub", ObservationPrecedence.Community, LOCAL + @"\Vendor\Sub", false, "*")],
            [],
            []);

        var results = new AppCacheMeasurer(source, new ManualTimeProvider()).Measure(plan, shared, _ => false, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.True(plan.Targets[1].OverlapsEarlierFiltered);
        Assert.Equal(5 + 11, results[0].Bytes);
        Assert.Equal(13, results[1].Bytes);
        Assert.All(results, result => Assert.False(result.FromSharedScan));
        Assert.All(results, result => Assert.True(result.DuplicatesPossible));
    }

    /// <summary>스캔 루트 밖 대상 열거: 보호 하위 폴더·reparse·placeholder는 들어가지 않고 세며, 모든 파일을 빼는 PATH 제외 폴더와 FILE 제외 파일은 세지 않는다.</summary>
    [Fact]
    public async Task 대상_열거는_공유_스캔과_같은_건너뜀_기준을_쓴다()
    {
        var (shared, _) = await ScanAsync();
        var source = new FakeDirectoryEntrySource()
            .Dir(OUTSIDE,
                FakeDirectoryEntrySource.File("keep.dat", 1), FakeDirectoryEntrySource.File("x.dat", 2),
                FakeDirectoryEntrySource.File("cloud.dat", 3, FileAttributes.Offline),
                FakeDirectoryEntrySource.Folder("Protected"), FakeDirectoryEntrySource.Folder("Link", FileAttributes.ReparsePoint),
                FakeDirectoryEntrySource.Folder("Excluded"), FakeDirectoryEntrySource.Folder("Deep"))
            .Dir(OUTSIDE + @"\Protected", FakeDirectoryEntrySource.File("p", 1000))
            .Dir(OUTSIDE + @"\Excluded", FakeDirectoryEntrySource.File("e", 1000))
            .Dir(OUTSIDE + @"\Deep", FakeDirectoryEntrySource.File("y.dat", 4));
        List<ExclusionSpec> exclusions =
        [
            new("winapp2:X", ExcludeKind.File, OUTSIDE, ["keep.dat"]),
            new("winapp2:X", ExcludeKind.Path, OUTSIDE + @"\Excl*", ["*"]),
        ];
        var plan = ObservationPlanner.Plan([Candidate("winapp2:X", ObservationPrecedence.Community, OUTSIDE, true, "*")], exclusions, []);

        var result = Assert.Single(new AppCacheMeasurer(source, new ManualTimeProvider()).Measure(
            plan, shared, path => PathScope.IsSameOrUnder(path, OUTSIDE + @"\Protected"), TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.Equal(TargetState.Observed, result.State);
        Assert.Equal(2 + 4, result.Bytes);
        Assert.Equal(1, result.Skips.ProtectedExcluded);
        Assert.Equal(1, result.Skips.Reparse);
        Assert.Equal(1, result.Skips.Placeholder);
        Assert.DoesNotContain(OUTSIDE + @"\Protected", source.Enumerated);
        Assert.DoesNotContain(OUTSIDE + @"\Excluded", source.Enumerated);
    }

    /// <summary>대상당 5초 예산을 넘기면 받은 항목까지만 세고 남은 폴더를 시간 초과로 센다(부분). 전체 예산이 이미 지났으면 다음 대상은 시작하지 않는다.</summary>
    [Fact]
    public async Task 시간_예산을_넘기면_부분_집계와_시간_초과로_남긴다()
    {
        var (shared, _) = await ScanAsync();
        var time = new ManualTimeProvider();
        var source = new FakeDirectoryEntrySource()
            .Dir(OUTSIDE, FakeDirectoryEntrySource.File("a", 1), FakeDirectoryEntrySource.File("b", 2), FakeDirectoryEntrySource.Folder("Later"))
            .Dir(OUTSIDE + @"\Later", FakeDirectoryEntrySource.File("c", 4))
            .Dir(@"E:\Next", FakeDirectoryEntrySource.File("n", 8));
        source.OnEntry = (_, entry) =>
        {
            if (entry.Name == "b")
            {
                time.Advance(AppCacheMeasurer.APP_CACHE_TARGET_BUDGET + TimeSpan.FromSeconds(1));
            }
        };
        var plan = ObservationPlanner.Plan(
            [Candidate("winapp2:X", ObservationPrecedence.Community, OUTSIDE, true, "*"), Candidate("winapp2:Y", ObservationPrecedence.Community, @"E:\Next", true, "*")],
            [],
            []);

        var results = new AppCacheMeasurer(source, time).Measure(plan, shared, _ => false, TimeSpan.FromSeconds(3), CancellationToken.None);

        Assert.Equal(TargetState.Partial, results[0].State);
        Assert.Equal(1 + 2, results[0].Bytes);
        Assert.True(results[0].Skips.Timeout >= 1);
        Assert.DoesNotContain(OUTSIDE + @"\Later", source.Enumerated);
        Assert.Equal(TargetState.TimedOut, results[1].State);
        Assert.DoesNotContain(@"E:\Next", source.Enumerated);
    }

    /// <summary>
    /// 공유 스캔 합계(<see cref="DirectoryScanResult.TryGetDirectoryTotals"/>)는 보호 폴더·reparse 폴더·클라우드 placeholder 폴더의 내용을 넣지 않고 건너뜀으로만 센다.
    /// 그래서 그 합계를 쓰는 앱 캐시 전체 대상도 그 내용을 세지 않는다.
    /// </summary>
    [Fact]
    public async Task 공유_스캔_합계는_보호_reparse_placeholder_하위를_넣지_않는다()
    {
        var source = FileScanServiceTests.Tree()
            .Dir(PROFILE + @"\AppData\Local", FakeDirectoryEntrySource.Folder("Temp"), FakeDirectoryEntrySource.Folder("Microsoft"), FakeDirectoryEntrySource.Folder("Mixed"))
            .Dir(LOCAL + @"\Mixed",
                FakeDirectoryEntrySource.File("a.bin", 10),
                FakeDirectoryEntrySource.Folder("Link", FileAttributes.ReparsePoint),
                FakeDirectoryEntrySource.Folder("Cloud", FileAttributes.Offline))
            .Dir(LOCAL + @"\Mixed\Link", FakeDirectoryEntrySource.File("through-link.bin", 1000))
            .Dir(LOCAL + @"\Mixed\Cloud", FakeDirectoryEntrySource.File("cloud.bin", 1000));
        var shared = await FileScanServiceTests.Service(source).GetOrScanAsync(FileScanServiceTests.Context(), CancellationToken.None);

        Assert.True(shared.TryGetDirectoryTotals(PROFILE, out var profile));
        Assert.True(profile.Skips.ProtectedExcluded >= 1);
        Assert.DoesNotContain(PROFILE + @"\Documents", source.Enumerated);
        Assert.True(shared.TryGetDirectoryTotals(LOCAL + @"\Mixed", out var mixed));
        Assert.Equal(10, mixed.Bytes);
        Assert.Equal(1, mixed.Skips.Reparse);
        Assert.Equal(1, mixed.Skips.Placeholder);
        Assert.DoesNotContain(LOCAL + @"\Mixed\Link", source.Enumerated);
        Assert.DoesNotContain(LOCAL + @"\Mixed\Cloud", source.Enumerated);

        var plan = ObservationPlanner.Plan([Candidate("winapp2:Mixed", ObservationPrecedence.Community, LOCAL + @"\Mixed", true, "*")], [], []);
        var result = Assert.Single(new AppCacheMeasurer(source, new ManualTimeProvider()).Measure(plan, shared, _ => false, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.True(result.FromSharedScan);
        Assert.Equal(10, result.Bytes);
    }

    /// <summary>대상 폴더의 중간 폴더가 정션이면 OS가 링크를 따라가므로 대상에 들어가지 않고 reparse로 센다(대상 폴더는 열거하지 않음).</summary>
    [Fact]
    public async Task 중간_폴더가_정션이면_대상에_들어가지_않는다()
    {
        var (shared, _) = await ScanAsync();
        var source = new FakeDirectoryEntrySource()
            .Dir(@"D:\", FakeDirectoryEntrySource.Folder("Link", FileAttributes.ReparsePoint))
            .Dir(@"D:\Link\Cache", FakeDirectoryEntrySource.File("x.bin", 500));
        var plan = ObservationPlanner.Plan([Candidate("winapp2:Linked", ObservationPrecedence.Community, @"D:\Link\Cache", true, "*")], [], []);

        var result = Assert.Single(new AppCacheMeasurer(source, new ManualTimeProvider()).Measure(plan, shared, _ => false, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.Equal(TargetState.ReparsePoint, result.State);
        Assert.Equal(0, result.Bytes);
        Assert.Equal(1, result.Skips.Reparse);
        Assert.DoesNotContain(@"D:\Link\Cache", source.Enumerated);
    }
}
