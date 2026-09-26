using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

public sealed class ProcessingBudgetReviewTests
{
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
}

