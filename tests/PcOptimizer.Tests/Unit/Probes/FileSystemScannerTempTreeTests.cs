/**
 * @file    : FileSystemScannerTempTreeTests.cs
 * @author  : rudals252
 * @brief   : 테스트가 만든 실제 임시 폴더 트리에서 파일 순회기(FileSystemEnumerator·Win32 파일 ID)의 중첩 폴더 합계, 숨김·시스템 파일 포함, 보호 하위 폴더 미진입, 확장자 분포, 없는 루트 처리를 검증
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// 실제 파일 시스템 어댑터로 테스트 전용 임시 폴더만 순회합니다(작은 파일만 만듦, 정리는 테스트 루트 안에서만).
/// </summary>
public sealed class FileSystemScannerTempTreeTests : IDisposable
{
    private static readonly TimeSpan BUDGET = TimeSpan.FromSeconds(60);
    private readonly TestDirectory _directory = new();

    /// <summary>
    /// 테스트 폴더를 정리한다.
    /// </summary>
    public void Dispose()
    {
        _directory.Dispose();
    }

    /// <summary>중첩 폴더 합계, 숨김·시스템 파일, 확장자(소문자) 분포를 집계하고 보호 하위 폴더는 들어가지 않는다.</summary>
    [Fact]
    public async Task 실제_폴더_트리를_집계한다()
    {
        _directory.File(@"a\b\deep.txt", 3);
        _directory.File("hidden.bin", 5, FileAttributes.Hidden | FileAttributes.System);
        _directory.File("data.json", 10);
        _directory.File(@"media\x.MP4", 20);
        _directory.File(@"media\y.mp4", 30);
        _directory.File(@"Protected\secret.txt", 100);
        var protection = new ResolvedProtection([new ProtectedRoot(_directory.PathOf("protected"), ProtectedRootOrigin.KnownFolder, "Documents")]);

        var result = await FileSystemScanner.CreateDefault().ScanAsync(
            [new ScanTarget("root", _directory.Root)], protection, [], BUDGET, CancellationToken.None);

        var root = Assert.Single(result.Roots);
        Assert.Equal(RootScanState.Scanned, root.State);
        Assert.Equal(3 + 5 + 10 + 20 + 30, root.Totals!.Bytes);
        Assert.Equal(5, root.Totals.FileCount);
        Assert.Equal(3, root.Totals.DirectoryCount);
        Assert.Equal(1, root.Totals.Skips.ProtectedExcluded);
        Assert.False(root.Totals.IsPartial);
        Assert.False(result.TryGetDirectoryTotals(_directory.PathOf("Protected"), out _));
        Assert.True(result.TryGetDirectoryTotals(_directory.PathOf("a"), out var a));
        Assert.Equal(3, a.Bytes);

        var media = Assert.Single(result.GetDirectoryAggregates([_directory.PathOf("media")]));
        Assert.Equal(50, media.ExtensionBytes[".mp4"]);
        Assert.Equal(2, media.FileCount);
        Assert.True(media.DuplicatesPossible);
        Assert.NotNull(media.NewestWriteUtc);
    }

    /// <summary>없는 루트는 없음이며 합계가 없다.</summary>
    [Fact]
    public async Task 없는_루트는_없음이다()
    {
        var result = await FileSystemScanner.CreateDefault().ScanAsync(
            [new ScanTarget("missing", _directory.PathOf("nope"))], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);

        Assert.Equal(RootScanState.Absent, Assert.Single(result.Roots).State);
        Assert.Null(result.Roots[0].Totals);
    }
}
