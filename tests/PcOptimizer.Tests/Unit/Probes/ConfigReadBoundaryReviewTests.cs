/**
 * @file    : ConfigReadBoundaryReviewTests.cs
 * @author  : rudals252
 * @brief   : REV-006/007 설정 본문 읽기의 링크 경계와 실패 전파 회귀 검증
 */
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;
namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>설정 읽기 경계를 검증합니다.</summary>
public sealed class ConfigReadBoundaryReviewTests
{
    private const string Profile = @"C:\Users\tester";
    private const string Config = Profile + @"\.npmrc";

    /// <summary>접근 거부는 설정 부재가 아닙니다.</summary>
    [Fact]
    public void DeniedConfigMustNotLookAbsent()
    {
        var environment = new FakePathEnvironment { Profile = Profile };
        var source = new FakeDirectoryEntrySource().Deny(Config);
        var result = new NpmConfigReader(environment, source).Read();
        Assert.Equal(AppConfigReadState.Unreadable, result.State);
    }

    /// <summary>중간 정션을 통해 본문을 읽지 않습니다.</summary>
    [Fact]
    public void ConfigUnderReparseAncestorMustNotBeRead()
    {
        var environment = new FakePathEnvironment { Profile = Profile }
            .WithFile(Config, @"cache=D:\cache\npm");
        var result = new NpmConfigReader(environment, new LinkedProfileSource()).Read();
        Assert.Empty(environment.FileReads);
        Assert.NotEqual(AppConfigReadState.Configured, result.State);
    }

    /// <summary>파일 링크·placeholder·오류에서는 본문 읽기 요청이 없습니다.</summary>
    [Theory]
    [InlineData(RootPresence.ReparsePoint)]
    [InlineData(RootPresence.Placeholder)]
    [InlineData(RootPresence.AccessDenied)]
    [InlineData(RootPresence.Error)]
    public void UnsafeLeafIsNotRead(RootPresence presence)
    {
        var environment = new FakePathEnvironment { Profile = Profile }.WithFile(Config, @"cache=D:\cache\npm");
        var result = new NpmConfigReader(environment, new LeafSource(presence)).Read();
        Assert.Empty(environment.FileReads);
        Assert.Equal(AppConfigReadState.Unreadable, result.State);
    }

    /// <summary>기본 캐시가 없어도 읽기 실패 사유가 카드에 남고 설치를 추정하지 않습니다.</summary>
    [Fact]
    public async Task UnreadableConfigSurvivesWithoutDefaultCache()
    {
        var environment = new AppCacheTestEnvironment(defaultCaches: false);
        environment.Source.Deny(Config);
        var result = await environment.Probe().RunAsync(AppCacheTestEnvironment.Context(), CancellationToken.None);
        var findings = new AppCacheRule().Evaluate(new ScanSnapshot(Guid.NewGuid(), [result]));
        var finding = Assert.Single(findings, f => f.Id == AppCacheRule.CONFIG_FINDING_ID_PREFIX + "npm");
        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.DoesNotContain(environment.Environment.FileReads, path => path == Config);
    }

    private sealed class LeafSource(RootPresence presence) : IDirectoryEntrySource
    {
        public RootPresence ProbeRoot(string path) => path == Config ? presence : RootPresence.Directory;
        public IEnumerable<DirectoryEntry> Enumerate(string path) => [];
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
