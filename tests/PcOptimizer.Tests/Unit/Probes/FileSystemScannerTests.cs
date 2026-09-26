/**
 * @file    : FileSystemScannerTests.cs
 * @author  : rudals252
 * @brief   : 파일 순회기의 reparse·placeholder 건너뜀, 접근 거부·열거 도중 실패의 부분 집계, 보호 폴더 미진입, 하드링크 ID 중복 제거·추정 표시, 압축·희소 할당 크기, 볼륨당 순회 1개(세마포어), 시간 예산 초과, 취소, 패턴 위치, 하위 합계를 가짜 열거로 검증
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="FileSystemScanner"/>를 가짜 열거·파일 ID·시간으로 검증합니다. 실제 파일 시스템에 접근하지 않습니다.
/// </summary>
public sealed class FileSystemScannerTests
{
    private const string ROOT = @"X:\root";
    private const long MIB = 1024 * 1024;
    private const FileAttributes RECALL_ON_OPEN = (FileAttributes)0x40000;
    private const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes)0x400000;
    private static readonly TimeSpan BUDGET = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 순회기를 만든다.
    /// </summary>
    private static FileSystemScanner Scanner(FakeDirectoryEntrySource source, FakeFileIdentityReader? identities = null, TimeProvider? time = null)
    {
        return new FileSystemScanner(source, identities ?? new FakeFileIdentityReader(), time ?? new ManualTimeProvider());
    }

    /// <summary>
    /// 루트 하나를 보호 없이 순회한다.
    /// </summary>
    private static Task<TraversalResult> ScanOne(FileSystemScanner scanner, ResolvedProtection? protection = null, IReadOnlyList<FilePatternLocation>? patterns = null)
    {
        return scanner.ScanAsync([new ScanTarget("root", ROOT)], protection ?? new ResolvedProtection([]), patterns ?? [], BUDGET, CancellationToken.None);
    }

    /// <summary>정션·심볼릭 링크(reparse)는 들어가지 않고 세며, placeholder 파일·폴더는 건드리지 않고 세고(reparse보다 우선), 숨김·시스템 파일은 센다.</summary>
    [Fact]
    public async Task reparse와_placeholder는_건너뛰고_센다()
    {
        var source = new FakeDirectoryEntrySource()
            .Dir(
                ROOT,
                FakeDirectoryEntrySource.Folder("junction", FileAttributes.ReparsePoint),
                FakeDirectoryEntrySource.File("link.txt", 10, FileAttributes.ReparsePoint),
                FakeDirectoryEntrySource.File("offline.bin", 1000, FileAttributes.Offline),
                FakeDirectoryEntrySource.File("recall-open.bin", 1000, RECALL_ON_OPEN),
                FakeDirectoryEntrySource.File("cloud.bin", 1000, RECALL_ON_DATA_ACCESS | FileAttributes.ReparsePoint),
                FakeDirectoryEntrySource.Folder("cloud-folder", RECALL_ON_DATA_ACCESS | FileAttributes.ReparsePoint),
                FakeDirectoryEntrySource.File("hidden.dat", 7, FileAttributes.Hidden | FileAttributes.System),
                FakeDirectoryEntrySource.File("plain.txt", 3))
            .Dir(ROOT + @"\junction", FakeDirectoryEntrySource.File("target.bin", 999_999))
            .Dir(ROOT + @"\cloud-folder", FakeDirectoryEntrySource.File("remote.bin", 999_999));

        var result = await ScanOne(Scanner(source));

        var root = Assert.Single(result.Roots);
        Assert.Equal(RootScanState.Scanned, root.State);
        var totals = root.Totals!;
        Assert.Equal(10, totals.Bytes);
        Assert.Equal(2, totals.FileCount);
        Assert.Equal(2, totals.Skips.Reparse);
        Assert.Equal(4, totals.Skips.Placeholder);
        Assert.False(totals.IsPartial);
        Assert.False(totals.IsFullyObserved);
        Assert.Equal([ROOT], source.Enumerated);
    }

    /// <summary>접근 거부 폴더는 사유를 세고 형제는 계속 집계하며, 그 폴더의 합계는 0이 아니라 실패로 알린다.</summary>
    [Fact]
    public async Task 접근_거부는_세고_계속한다()
    {
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("denied"), FakeDirectoryEntrySource.Folder("ok"), FakeDirectoryEntrySource.File("a.txt", 5))
            .Deny(ROOT + @"\denied")
            .Dir(ROOT + @"\ok", FakeDirectoryEntrySource.File("b.txt", 6));

        var result = await ScanOne(Scanner(source));

        var totals = Assert.Single(result.Roots).Totals!;
        Assert.Equal(11, totals.Bytes);
        Assert.Equal(1, totals.Skips.AccessDenied);
        Assert.True(totals.IsPartial);
        Assert.Equal(ScanSkipReason.AccessDenied, result.GetEnumerationFailure(ROOT + @"\denied"));
        Assert.True(result.TryGetDirectoryTotals(ROOT + @"\denied", out var denied));
        Assert.True(denied.IsPartial);
        Assert.True(result.TryGetDirectoryTotals(ROOT + @"\ok", out var ok));
        Assert.True(ok.IsFullyObserved);
    }

    /// <summary>열거 도중 실패하면 그때까지 읽은 항목은 집계하고 사용 중·변경 중으로 센다.</summary>
    [Fact]
    public async Task 열거_도중_실패는_부분_집계로_남긴다()
    {
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.File("1.log", 100), FakeDirectoryEntrySource.File("2.log", 200), FakeDirectoryEntrySource.File("3.log", 400))
            .FailAfter(ROOT, 2);

        var result = await ScanOne(Scanner(source));

        var totals = Assert.Single(result.Roots).Totals!;
        Assert.Equal(300, totals.Bytes);
        Assert.Equal(2, totals.FileCount);
        Assert.Equal(1, totals.Skips.InUse);
        Assert.True(totals.IsPartial);
    }

    /// <summary>보호 폴더는 대소문자가 달라도 들어가지 않고(열거 호출 없음) 보호 제외로 센다.</summary>
    [Fact]
    public async Task 보호_폴더는_들어가지_않는다()
    {
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("Documents"), FakeDirectoryEntrySource.Folder("work"))
            .Dir(ROOT + @"\Documents", FakeDirectoryEntrySource.File("secret.docx", 5000))
            .Dir(ROOT + @"\work", FakeDirectoryEntrySource.Folder("sync"), FakeDirectoryEntrySource.File("w.txt", 1))
            .Dir(ROOT + @"\work\sync", FakeDirectoryEntrySource.File("s.txt", 7000));
        var protection = new ResolvedProtection(
        [
            new ProtectedRoot(@"x:\ROOT\documents", ProtectedRootOrigin.KnownFolder, "Documents"),
            new ProtectedRoot(ROOT + @"\work\sync", ProtectedRootOrigin.CloudSync, "OneDrive"),
        ]);

        var result = await ScanOne(Scanner(source), protection);

        var totals = Assert.Single(result.Roots).Totals!;
        Assert.Equal(1, totals.Bytes);
        Assert.Equal(2, totals.Skips.ProtectedExcluded);
        Assert.DoesNotContain(source.Enumerated, path => path.Contains("Documents", StringComparison.OrdinalIgnoreCase) || path.EndsWith("sync", StringComparison.Ordinal));
        Assert.False(result.TryGetDirectoryTotals(ROOT + @"\Documents", out _));
    }

    /// <summary>보호 루트 자체가 스캔 루트이면 순회하지 않는다.</summary>
    [Fact]
    public async Task 보호_루트_안의_스캔_루트는_순회하지_않는다()
    {
        var source = new FakeDirectoryEntrySource().Dir(ROOT, FakeDirectoryEntrySource.File("a", 1));

        var result = await ScanOne(Scanner(source), new ResolvedProtection([new ProtectedRoot(@"X:\", ProtectedRootOrigin.SystemPath, "x")]));

        var root = Assert.Single(result.Roots);
        Assert.Equal(RootScanState.Protected, root.State);
        Assert.Null(root.Totals);
        Assert.Empty(source.Enumerated);
    }

    /// <summary>64MiB 이상 파일은 파일 ID로 하드링크를 한 번만 세고, 작은 파일은 ID를 확인하지 않아 중복 가능으로 표시한다.</summary>
    [Fact]
    public async Task 큰_파일은_파일_ID로_하드링크를_한_번만_센다()
    {
        var big = FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES;
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("a"), FakeDirectoryEntrySource.Folder("b"))
            .Dir(ROOT + @"\a", FakeDirectoryEntrySource.File("link1.vhdx", big), FakeDirectoryEntrySource.File("other.vhdx", big + 1))
            .Dir(ROOT + @"\b", FakeDirectoryEntrySource.File("link2.vhdx", big));
        var identities = new FakeFileIdentityReader()
            .WithIdentity(ROOT + @"\a\link1.vhdx", 42)
            .WithIdentity(ROOT + @"\b\link2.vhdx", 42)
            .WithIdentity(ROOT + @"\a\other.vhdx", 43);

        var result = await ScanOne(Scanner(source, identities));

        var totals = Assert.Single(result.Roots).Totals!;
        Assert.Equal((2 * big) + 1, totals.Bytes);
        Assert.Equal(2, totals.FileCount);
        Assert.False(totals.DuplicatesPossible);
        Assert.Equal(1, result.HardLinkDuplicateCount);
        Assert.Equal(big, result.HardLinkDuplicateBytes);
    }

    /// <summary>작은 파일은 ID를 조회하지 않고 중복 가능으로, 큰 파일의 ID 조회 실패도 중복 가능으로 표시하되 크기는 센다.</summary>
    [Fact]
    public async Task ID를_확인하지_않은_파일은_중복_가능으로_표시한다()
    {
        var threshold = FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES;
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("small"), FakeDirectoryEntrySource.Folder("unknown"))
            .Dir(ROOT + @"\small", FakeDirectoryEntrySource.File("s.txt", threshold - 1))
            .Dir(ROOT + @"\unknown", FakeDirectoryEntrySource.File("u.iso", threshold));
        var identities = new FakeFileIdentityReader();

        var result = await ScanOne(Scanner(source, identities));

        Assert.Equal([ROOT + @"\unknown\u.iso"], identities.IdentityCalls);
        Assert.True(result.TryGetDirectoryTotals(ROOT + @"\small", out var small));
        Assert.True(small.DuplicatesPossible);
        Assert.True(result.TryGetDirectoryTotals(ROOT + @"\unknown", out var unknown));
        Assert.True(unknown.DuplicatesPossible);
        Assert.Equal(threshold, unknown.Bytes);
        Assert.Equal((2 * threshold) - 1, result.Roots[0].Totals!.Bytes);
    }

    /// <summary>압축·희소 파일은 논리 크기와 할당 크기를 따로 기록한다.</summary>
    [Fact]
    public async Task 압축_희소_파일은_할당_크기를_따로_기록한다()
    {
        var source = new FakeDirectoryEntrySource()
            .Dir(
                ROOT,
                FakeDirectoryEntrySource.File("c.log", 10 * MIB, FileAttributes.Compressed),
                FakeDirectoryEntrySource.File("s.img", 50 * MIB, FileAttributes.SparseFile),
                FakeDirectoryEntrySource.File("n.bin", 1 * MIB));
        var identities = new FakeFileIdentityReader().WithAllocated(ROOT + @"\c.log", 2 * MIB).WithAllocated(ROOT + @"\s.img", 4096);

        var result = await ScanOne(Scanner(source, identities));

        var totals = Assert.Single(result.Roots).Totals!;
        Assert.Equal(61 * MIB, totals.Bytes);
        Assert.Equal(2, totals.CompressedOrSparseFileCount);
        Assert.Equal(60 * MIB, totals.CompressedOrSparseLogicalBytes);
        Assert.Equal((2 * MIB) + 4096, totals.CompressedOrSparseAllocatedBytes);
    }

    /// <summary>하위 합계는 bottom-up으로 더하고, 폴더 수와 이름 패턴 위치(thumbcache)는 따로 센다.</summary>
    [Fact]
    public async Task 하위_합계와_패턴_위치를_집계한다()
    {
        var explorer = ROOT + @"\Explorer";
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("a"), FakeDirectoryEntrySource.Folder("Explorer"), FakeDirectoryEntrySource.File("r", 1))
            .Dir(ROOT + @"\a", FakeDirectoryEntrySource.Folder("b"), FakeDirectoryEntrySource.File("x", 10))
            .Dir(ROOT + @"\a\b", FakeDirectoryEntrySource.File("y", 100))
            .Dir(
                explorer,
                FakeDirectoryEntrySource.File("thumbcache_256.db", 1000),
                FakeDirectoryEntrySource.File("IconCache_48.db", 2000),
                FakeDirectoryEntrySource.File("ExplorerStartupLog.etl", 50000));

        var result = await ScanOne(Scanner(source), patterns: [new FilePatternLocation("thumbs", explorer.ToUpperInvariant(), ["thumbcache_*.db", "iconcache_*.db"])]);

        Assert.True(result.TryGetDirectoryTotals(ROOT + @"\a", out var a));
        Assert.Equal(110, a.Bytes);
        Assert.Equal(1, a.DirectoryCount);
        var root = result.Roots[0].Totals!;
        Assert.Equal(53111, root.Bytes);
        Assert.Equal(3, root.DirectoryCount);
        Assert.Equal(new PatternTally("thumbs", true, 3000, 2, null, true), Assert.Single(result.Patterns));
    }

    /// <summary>없는 루트는 없음, 접근 거부 루트는 접근 거부이며 둘 다 합계가 없다(0바이트로 보고하지 않음).</summary>
    [Fact]
    public async Task 없는_루트와_거부된_루트는_합계가_없다()
    {
        var source = new FakeDirectoryEntrySource().Deny(@"X:\denied");

        var result = await Scanner(source).ScanAsync(
            [new ScanTarget("missing", @"X:\missing"), new ScanTarget("denied", @"X:\denied")], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);

        Assert.Equal([RootScanState.Absent, RootScanState.AccessDenied], result.Roots.Select(r => r.State));
        Assert.All(result.Roots, root => Assert.Null(root.Totals));
    }

    /// <summary>같은 볼륨의 두 루트는 차례로(앞 루트가 끝난 뒤) 순회하고, 다른 볼륨은 그동안 병렬로 순회한다.</summary>
    [Fact]
    public async Task 볼륨당_순회는_한_번에_하나다()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var otherVolumeDone = new ManualResetEventSlim();
        var source = new FakeDirectoryEntrySource()
            .Dir(@"X:\a", FakeDirectoryEntrySource.File("1", 1))
            .Dir(@"X:\b", FakeDirectoryEntrySource.File("2", 2))
            .Dir(@"Y:\c", FakeDirectoryEntrySource.File("3", 3));
        source.OnEnumerate = path =>
        {
            if (path == @"X:\a")
            {
                firstStarted.Set();
                Assert.True(releaseFirst.Wait(WAIT));
            }
            else if (path == @"Y:\c")
            {
                otherVolumeDone.Set();
            }
        };

        var scan = Scanner(source).ScanAsync(
            [new ScanTarget("a", @"X:\a"), new ScanTarget("b", @"X:\b"), new ScanTarget("c", @"Y:\c")], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);

        Assert.True(firstStarted.Wait(WAIT));
        Assert.True(otherVolumeDone.Wait(WAIT));
        Assert.DoesNotContain(@"X:\b", source.Enumerated);
        releaseFirst.Set();
        var result = await scan.WaitAsync(WAIT);

        var order = source.Enumerated.ToList();
        Assert.True(order.IndexOf(@"X:\a") < order.IndexOf(@"X:\b"));
        Assert.All(result.Roots, root => Assert.Equal(RootScanState.Scanned, root.State));
        Assert.Equal(2, result.Volumes.Count);
    }

    /// <summary>같은 순회기에서 겹친 두 검사도 같은 볼륨이면 뒤 검사가 앞 검사가 끝날 때까지 기다린다.</summary>
    [Fact]
    public async Task 겹친_검사도_같은_볼륨이면_기다린다()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var source = new FakeDirectoryEntrySource()
            .Dir(@"X:\a", FakeDirectoryEntrySource.File("1", 1))
            .Dir(@"X:\b", FakeDirectoryEntrySource.File("2", 2));
        source.OnEnumerate = path =>
        {
            if (path == @"X:\a")
            {
                firstStarted.Set();
                Assert.True(releaseFirst.Wait(WAIT));
            }
        };
        var scanner = Scanner(source);

        var first = scanner.ScanAsync([new ScanTarget("a", @"X:\a")], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);
        Assert.True(firstStarted.Wait(WAIT));
        var second = scanner.ScanAsync([new ScanTarget("b", @"X:\b")], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        Assert.DoesNotContain(@"X:\b", source.Enumerated);
        releaseFirst.Set();
        await Task.WhenAll(first, second).WaitAsync(WAIT);
        Assert.Equal([@"X:\a", @"X:\b"], source.Enumerated);
    }

    /// <summary>
    /// 볼륨 시간 예산을 넘기면 남은 폴더는 시간 초과로 세고 멈추며, 그때까지의 집계는 보존하고 같은 볼륨의 다음 루트는 시작하지 않는다.
    /// 예산은 폴더 안 항목마다 확인하므로, 열거 도중 예산을 넘긴 폴더(d2)는 관측한 항목이 없으면 크기 없이 시간 초과로 센다.
    /// </summary>
    [Fact]
    public async Task 시간_예산을_넘기면_부분_집계로_멈춘다()
    {
        var time = new ManualTimeProvider();
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("d1"), FakeDirectoryEntrySource.Folder("d2"), FakeDirectoryEntrySource.Folder("d3"), FakeDirectoryEntrySource.File("r", 1))
            .Dir(ROOT + @"\d1", FakeDirectoryEntrySource.File("f1", 10))
            .Dir(ROOT + @"\d2", FakeDirectoryEntrySource.File("f2", 20))
            .Dir(ROOT + @"\d3", FakeDirectoryEntrySource.File("f3", 40))
            .Dir(@"X:\next", FakeDirectoryEntrySource.File("n", 5));
        source.OnEnumerate = _ => time.Advance(TimeSpan.FromSeconds(50));

        var result = await Scanner(source, time: time).ScanAsync(
            [new ScanTarget("root", ROOT), new ScanTarget("next", @"X:\next")], new ResolvedProtection([]), [], TimeSpan.FromSeconds(120), CancellationToken.None);

        var root = result.Roots[0];
        Assert.Equal(RootScanState.Scanned, root.State);
        Assert.True(root.TimedOut);
        Assert.Equal(3, source.Enumerated.Count);
        Assert.Equal(1 + 40, root.Totals!.Bytes);
        Assert.Equal(2, root.Totals.Skips.Timeout);
        Assert.Equal(ScanSkipReason.Timeout, result.GetEnumerationFailure(ROOT + @"\d2"));
        Assert.True(root.Totals.IsPartial);
        Assert.Equal(RootScanState.TimedOut, result.Roots[1].State);
        Assert.Null(result.Roots[1].Totals);
        Assert.True(Assert.Single(result.Volumes).TimedOut);
    }

    /// <summary>취소하면 OperationCanceledException으로 끝난다.</summary>
    [Fact]
    public async Task 취소를_존중한다()
    {
        using var cts = new CancellationTokenSource();
        var source = new FakeDirectoryEntrySource()
            .Dir(ROOT, FakeDirectoryEntrySource.Folder("a"), FakeDirectoryEntrySource.Folder("b"))
            .Dir(ROOT + @"\a")
            .Dir(ROOT + @"\b");
        source.OnEnumerate = _ => cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Scanner(source).ScanAsync([new ScanTarget("root", ROOT)], new ResolvedProtection([]), [], BUDGET, cts.Token));
        Assert.Single(source.Enumerated);
    }
}
