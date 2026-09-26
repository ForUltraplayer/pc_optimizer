using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

public sealed class IndependentReviewTests
{
    [Fact]
    public async Task BudgetExceededInsideOnlyDirectoryIsReported()
    {
        var time = new ManualTimeProvider();
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root", FakeDirectoryEntrySource.File("a.bin", 100));
        source.OnEnumerate = _ => time.Advance(TimeSpan.FromSeconds(121));
        var scanner = new FileSystemScanner(source, new FakeFileIdentityReader(), time);
        var result = await scanner.ScanAsync([new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [], TimeSpan.FromSeconds(120), CancellationToken.None);
        Assert.True(Assert.Single(result.Roots).TimedOut);
    }

    [Fact]
    public async Task CancellationInsideOnlyDirectoryIsObserved()
    {
        using var cts = new CancellationTokenSource();
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root", FakeDirectoryEntrySource.File("a.bin", 100));
        source.OnEnumerate = _ => cts.Cancel();
        var scanner = new FileSystemScanner(source, new FakeFileIdentityReader(), new ManualTimeProvider());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync([new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [], TimeSpan.FromSeconds(120), cts.Token));
    }

    [Fact]
    public void KnownFolderAtVolumeRootStillProtectsDescendants()
    {
        var environment = new FakePathEnvironment().WithKnownFolder(ProtectedKnownFolder.Documents, @"D:\");
        var policy = new ProtectionPolicy(ProtectionPolicyParser.SUPPORTED_SCHEMA_VERSION, [new KnownFolderRootSpec(ProtectedKnownFolder.Documents)]);
        var protection = new ProtectionPolicyResolver(environment, new FakeRegistryReader()).Resolve(policy);
        Assert.True(protection.IsProtected(@"D:\Temp\private"));
    }
}
