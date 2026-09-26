/**
 * @file    : ProcessingBudgetReviewTests.cs
 * @author  : rudals252
 * @brief   : 독립 검토 원장 REV-002 3차 재현(열거는 즉시 끝나고 파일 ID 조회 처리 단계에서 시간 예산을 넘기는 경우)을 정식 회귀 테스트로 편입하고, 받은 항목의 논리 크기 보존과 추가 OS 조회 중단을 확인
 */

// 사용자 패키지
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// 처리 단계 시간 예산 재현 테스트입니다(docs/reviews/repro/ProcessingBudgetReviewTests.cs에서 가져옴, 원래 단언은 그대로 유지).
/// </summary>
public sealed class ProcessingBudgetReviewTests
{
    /// <summary>파일 ID 조회 처리 중 예산(120초)을 넘기면 루트는 시간 초과로 보고된다.</summary>
    [Fact]
    public async Task IdentityProcessingBudgetOverrunIsReported()
    {
        var time = new ManualTimeProvider();
        const long size = FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES;
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root",
            FakeDirectoryEntrySource.File("a.bin", size),
            FakeDirectoryEntrySource.File("b.bin", size),
            FakeDirectoryEntrySource.File("c.bin", size));
        var identities = new FakeFileIdentityReader();
        identities.OnIdentity = _ => time.Advance(TimeSpan.FromSeconds(121));
        var scanner = new FileSystemScanner(source, identities, time);
        var result = await scanner.ScanAsync(
            [new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [],
            TimeSpan.FromSeconds(120), CancellationToken.None);

        Assert.True(Assert.Single(result.Roots).TimedOut,
            $"120s budget exceeded during ID processing: calls={identities.IdentityCalls.Count}, elapsed={time.GetTimestamp() / time.TimestampFrequency}s; root TimedOut=false");
    }

    /// <summary>
    /// 처리 단계에서 예산을 넘긴 뒤에는 새 파일 ID 조회를 하지 않고(초과는 조회 한 번으로 제한), 이미 받은 세 파일의 논리 크기는 모두 세며,
    /// 중복 가능·조회 생략 수·시간 초과(사유 한 번)로 품질을 낮춘다. 볼륨도 시간 초과다.
    /// </summary>
    [Fact]
    public async Task IdentityLookupsStopAfterBudgetButLogicalSizesArePreserved()
    {
        var time = new ManualTimeProvider();
        const long size = FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES;
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root",
            FakeDirectoryEntrySource.File("a.bin", size),
            FakeDirectoryEntrySource.File("b.bin", size),
            FakeDirectoryEntrySource.File("c.bin", size, System.IO.FileAttributes.Compressed));
        var identities = new FakeFileIdentityReader()
            .WithIdentity(@"X:\root\a.bin", 1)
            .WithIdentity(@"X:\root\b.bin", 2)
            .WithIdentity(@"X:\root\c.bin", 3)
            .WithAllocated(@"X:\root\c.bin", 4096);
        identities.OnIdentity = _ => time.Advance(TimeSpan.FromSeconds(121));
        var scanner = new FileSystemScanner(source, identities, time);

        var result = await scanner.ScanAsync(
            [new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [],
            TimeSpan.FromSeconds(120), CancellationToken.None);

        var root = Assert.Single(result.Roots);
        Assert.True(root.TimedOut);
        Assert.Single(identities.IdentityCalls);
        Assert.Equal(3 * size, root.Totals!.Bytes);
        Assert.Equal(3, root.Totals.FileCount);
        Assert.True(root.Totals.DuplicatesPossible);
        Assert.Equal(2, root.Totals.LookupsSkipped);
        Assert.Equal(0, root.Totals.CompressedOrSparseFileCount);
        Assert.Equal(1, root.Totals.Skips.Timeout);
        Assert.Equal(1, root.Totals.Skips.Incomplete);
        Assert.Equal(ScanSkipReason.Timeout, result.GetEnumerationFailure(@"X:\root"));
        Assert.True(Assert.Single(result.Volumes).TimedOut);
        Assert.Empty(identities.AllocatedCalls);
    }

    /// <summary>
    /// 64MiB 이상이면서 압축된 파일 하나의 파일 ID 조회가 예산을 넘기면, 같은 파일의 할당 크기 조회는 하지 않는다
    /// (예산을 넘긴 뒤 수행되는 OS 조회는 진행 중이던 한 번뿐). 논리 크기는 세고, 조회 생략 수와 시간 초과를 기록한다.
    /// </summary>
    [Fact]
    public async Task AllocatedSizeLookupIsSkippedWhenIdentityLookupCrossesBudget()
    {
        var time = new ManualTimeProvider();
        const long size = FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES;
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root",
            FakeDirectoryEntrySource.File("big-compressed.bin", size, System.IO.FileAttributes.Compressed));
        var identities = new FakeFileIdentityReader()
            .WithIdentity(@"X:\root\big-compressed.bin", 1)
            .WithAllocated(@"X:\root\big-compressed.bin", 4096);
        identities.OnIdentity = _ => time.Advance(TimeSpan.FromSeconds(121));
        var scanner = new FileSystemScanner(source, identities, time);

        var result = await scanner.ScanAsync(
            [new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [],
            TimeSpan.FromSeconds(120), CancellationToken.None);

        var root = Assert.Single(result.Roots);
        Assert.Single(identities.IdentityCalls);
        Assert.Empty(identities.AllocatedCalls);
        Assert.True(root.TimedOut);
        Assert.Equal(size, root.Totals!.Bytes);
        Assert.Equal(1, root.Totals.LookupsSkipped);
        Assert.Equal(0, root.Totals.CompressedOrSparseFileCount);
        Assert.Equal(1, root.Totals.Skips.Timeout);
        Assert.True(Assert.Single(result.Volumes).TimedOut);
    }
}
