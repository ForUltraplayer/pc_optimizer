/**
 * @file    : CacheBoundarySmokeTests.cs
 * @author  : rudals252
 * @brief   : 실제 OS의 임시 파일 링크 경계와 NuGet HTTP 정리 명령을 소유한 fixture에서만 검증
 */
using System.IO;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Smoke;

/// <summary>개인 캐시·설정을 변경하지 않는 실제 OS 검증입니다.</summary>
[Trait("Category", "Smoke")]
public sealed class CacheBoundarySmokeTests
{
    /// <summary>npm의 실제 clean 명령도 지정한 fixture 캐시만 정리합니다.</summary>
    [Fact]
    public async Task NpmCommandOnlyClearsPinnedFixtureCache()
    {
        using var tree = new TestDirectory();
        var cacheFile = tree.File(@"npm-cache\_cacache\content-v2\sha512\fixture", 12);
        var project = tree.File(@"project\package.json", 12);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        var script = Path.Combine(Path.GetDirectoryName(executable)!, "node_modules", "npm", "bin", "npm-cli.js");
        Assert.True(File.Exists(script));
        var location = new CacheToolLocation(CacheTool.Npm, executable, tree.PathOf("npm-cache"), "fixture", script);
        var result = await CacheToolProcess.RunAsync(location, clear: true, CancellationToken.None);
        Assert.True(result.Success);
        Assert.False(File.Exists(cacheFile));
        Assert.True(File.Exists(project));
    }
    /// <summary>일반 설정만 읽고 파일 링크·중간 링크의 본문을 읽지 않습니다.</summary>
    [Fact]
    public void ConfigFileAndAncestorLinksAreRefused()
    {
        using var tree = new TestDirectory();
        var config = tree.File(@"real\config.ini", 8);
        File.WriteAllText(config, "cache=ok");
        Assert.Equal("cache=ok", SystemPathEnvironment.Instance.ReadSmallTextFile(config, 64));
        Assert.Null(SystemPathEnvironment.Instance.ReadSmallTextFile(config, 2));
        var link = tree.PathOf("file-link.ini");
        var directoryLink = tree.PathOf("directory-link");
        File.CreateSymbolicLink(link, config);
        Directory.CreateSymbolicLink(directoryLink, tree.PathOf("real"));
        Assert.Equal(RootPresence.ReparsePoint, FileSystemDirectoryEntrySource.Instance.ProbeRoot(link));
        Assert.Null(SystemPathEnvironment.Instance.ReadSmallTextFile(link, 64));
        Assert.Null(SystemPathEnvironment.Instance.ReadSmallTextFile(Path.Combine(directoryLink, "config.ini"), 64));
        Assert.False(SystemCacheToolBackend.IsPlainPath(link, false));
        Assert.False(SystemCacheToolBackend.IsPlainPath(directoryLink, true));
        Assert.True(File.Exists(config));
    }

    /// <summary>공식 명령은 지정한 임시 HTTP 캐시만 정리하고 형제 전역 패키지를 보존합니다.</summary>
    [Fact]
    public async Task NuGetCommandOnlyClearsPinnedFixtureHttpCache()
    {
        using var tree = new TestDirectory();
        var cacheFile = tree.File(@"http-cache\server\list_package.dat", 12);
        var package = tree.File(@"global-packages\package\1.0\package.nupkg", 12);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        Assert.True(File.Exists(executable));
        var location = new CacheToolLocation(CacheTool.NuGetHttp, executable, tree.PathOf("http-cache"), "fixture");
        var result = await CacheToolProcess.RunAsync(location, clear: true, CancellationToken.None);
        Assert.True(result.Success);
        Assert.False(File.Exists(cacheFile));
        Assert.True(File.Exists(package));
    }
}
