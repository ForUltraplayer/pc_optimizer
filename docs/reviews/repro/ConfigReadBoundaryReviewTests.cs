using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;
namespace PcOptimizer.Tests.Unit.Probes;

public sealed class ConfigReadBoundaryReviewTests
{
    private const string Profile = @"C:\Users\tester";
    private const string Config = Profile + @"\.npmrc";

    [Fact]
    public void DeniedConfigMustNotLookAbsent()
    {
        var environment = new FakePathEnvironment { Profile = Profile };
        var source = new FakeDirectoryEntrySource().Deny(Config);
        var result = new NpmConfigReader(environment, source).Read();
        Assert.Equal(AppConfigReadState.Unreadable, result.State);
    }

    [Fact]
    public void ConfigUnderReparseAncestorMustNotBeRead()
    {
        var environment = new FakePathEnvironment { Profile = Profile }
            .WithFile(Config, @"cache=D:\cache\npm");
        var result = new NpmConfigReader(environment, new LinkedProfileSource()).Read();
        Assert.Empty(environment.FileReads);
        Assert.NotEqual(AppConfigReadState.Configured, result.State);
    }

    private sealed class LinkedProfileSource : IDirectoryEntrySource
    {
        public RootPresence ProbeRoot(string path) =>
            path.Equals(Profile, StringComparison.OrdinalIgnoreCase)
                ? RootPresence.ReparsePoint
                : path.Equals(Config, StringComparison.OrdinalIgnoreCase)
                    ? RootPresence.NotDirectory : RootPresence.Directory;
        public IEnumerable<DirectoryEntry> Enumerate(string path) => [];
    }
}

