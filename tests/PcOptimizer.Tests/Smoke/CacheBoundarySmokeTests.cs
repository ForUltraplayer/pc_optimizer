/**
 * @file    : CacheBoundarySmokeTests.cs
 * @author  : rudals252
 * @brief   : 실제 OS의 임시 파일 링크 경계와 NuGet HTTP 정리 명령을 소유한 fixture에서만 검증
 */
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Smoke;

/// <summary>개인 캐시·설정을 변경하지 않는 실제 OS 검증입니다.</summary>
// 프로세스 전역 실행 관문을 공유하므로 실제 도구 fixture끼리는 직렬 실행한다.
[Collection("OfficialCacheTools")]
[Trait("Category", "Smoke")]
public sealed class CacheBoundarySmokeTests(Xunit.Abstractions.ITestOutputHelper output)
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
    /// <summary>실제 OS 경로 정규화로 존재하는 보호 폴더와 없는 말단을 거절합니다. 짧은 이름 지원 여부는 별도로 기록합니다.</summary>
    [Fact]
    public void NativePathNormalizationProtectsExistingAndMissingLeaf()
    {
        using var tree = new TestDirectory();
        var protectedPath = tree.PathOf("Private Documents Folder");
        Directory.CreateDirectory(protectedPath);
        var buffer = new StringBuilder(32768);
        Assert.NotEqual(0u, GetShortPathName(protectedPath, buffer, buffer.Capacity));
        var shortPath = buffer.ToString();
        output.WriteLine($"Native short alias available: {shortPath.Contains('~', StringComparison.Ordinal)}");
        Assert.Equal(protectedPath, CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, shortPath));
        var environment = new ProtectedFixtureEnvironment(tree.PathOf(""), protectedPath);
        var inspector = new CachePathInspector(environment, new FakeRegistryReader(), FileSystemDirectoryEntrySource.Instance,
            () => false, () => "fixture", () => """{"schemaVersion":1,"protectedRoots":[{"kind":"knownFolder","folder":"Documents"}]}""",
            TimeProvider.System, TimeSpan.FromSeconds(15));
        foreach (var path in new[] { shortPath, Path.Combine(shortPath, "missing-leaf") })
        {
            Assert.Equal("ProtectedPath", inspector.Inspect(new(CacheTool.Pip, "unused.exe", path, "fixture"), default).Reason);
        }
        Assert.True(Directory.Exists(protectedPath));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathName(string path, StringBuilder buffer, int length);

    private sealed class ProtectedFixtureEnvironment(string profile, string documents) : IPathEnvironment
    {
        public string? GetEnvironmentVariable(string name) => null;
        public string? GetKnownFolderPath(ProtectedKnownFolder folder) => folder == ProtectedKnownFolder.Documents ? documents : null;
        public string? GetUserProfilePath() => profile;
        public string NormalizePath(string path) => SystemPathEnvironment.Instance.NormalizePath(path);
        public string? ReadSmallTextFile(string path, int maxBytes) => null;
    }

}
