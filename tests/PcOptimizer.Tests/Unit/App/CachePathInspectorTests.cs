/**
 * @file    : CachePathInspectorTests.cs
 * @author  : rudals252
 * @brief   : 실제 실행 전 보호 관문의 별칭·권한·경로·링크·예산·HTTP 캐시 경계 검증
 */
using System.IO;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>정리 실행기가 호출하는 검사 클래스를 가짜 OS 공급자로 직접 검증합니다.</summary>
public sealed class CachePathInspectorTests
{
    private const string PROFILE = @"C:\Users\tester";
    private const string CACHE = PROFILE + @"\cache";
    private const string DOCUMENTS = PROFILE + @"\Private Documents";
    private const string POLICY = """{"schemaVersion":1,"protectedRoots":[{"kind":"knownFolder","folder":"Documents"}]}""";
    private readonly FakeDirectoryEntrySource _files = new FakeDirectoryEntrySource()
        .Dir(@"C:\").Dir(@"C:\Users").Dir(PROFILE).Dir(CACHE)
        .Dir(@"C:\tools", FakeDirectoryEntrySource.File("python.exe", 10));
    private readonly AliasEnvironment _environment = new();
    private readonly ManualTimeProvider _time = new();
    private bool _elevated;
    private static CacheToolLocation Location(string path = CACHE, CacheTool tool = CacheTool.Pip)
        => new(tool, @"C:\tools\python.exe", path, "fixture");
    private CachePathInspector Inspector(int maxEntries = 100000, string policy = POLICY, IDirectoryEntrySource? source = null)
        => new(_environment, new FakeRegistryReader(), source ?? _files, () => _elevated, () => "S-1-5-21-1",
            () => policy, _time, TimeSpan.FromSeconds(15), maxEntries);

    /// <summary>일반 캐시만 허용하며 파일 본문 없이 논리 크기를 관측합니다.</summary>
    [Fact]
    public void AllowsOrdinaryCacheAndMissingLeaf()
    {
        _files.Dir(CACHE, FakeDirectoryEntrySource.File("item", 123));
        Assert.Equal(new CacheInspection(true, 123, null), Inspector().Inspect(Location(), default));
        Assert.Equal(new CacheInspection(true, 0, null), Inspector().Inspect(Location(CACHE + @"\new"), default));
    }

    /// <summary>관리자 권한과 보호 정책 로딩 실패는 파일 열거 전에 거절합니다.</summary>
    [Theory]
    [InlineData(true, POLICY, "NormalUserRequired")]
    [InlineData(false, "{}", "ProtectionUnavailable")]
    public void RejectsAuthorityOrMissingPolicy(bool elevated, string policy, string reason)
    {
        _elevated = elevated;
        Assert.Equal(reason, Inspector(policy: policy).Inspect(Location(), default).Reason);
        Assert.Empty(_files.Enumerated);
    }

    /// <summary>프로필 밖·다른 사용자·보호 폴더·그 상위·장치 경로를 거절합니다.</summary>
    [Theory]
    [InlineData(@"D:\cache", "OutsideUserProfile")]
    [InlineData(@"C:\Users\other\cache", "ProtectedPath")]
    [InlineData(DOCUMENTS, "ProtectedPath")]
    [InlineData(PROFILE, "ProtectedPath")]
    [InlineData(PROFILE + @"\PRIVAT~1", "ProtectedPath")]
    [InlineData(PROFILE + @"\PRIVAT~1\missing", "ProtectedPath")]
    [InlineData(PROFILE + @"\UNKNOWN~1", "InspectionFailed")]
    [InlineData(@"\\?\C:\cache", "InspectionFailed")]
    [InlineData(CACHE + ":stream", "InspectionFailed")]
    public void RejectsUnsafeTargets(string path, string reason)
    {
        Assert.Equal(reason, Inspector().Inspect(Location(path), default).Reason);
        Assert.Empty(_files.Enumerated);
    }

    /// <summary>프로필 자체가 짧은 이름이어도 긴 이름으로 보호와 소속을 판정합니다.</summary>
    [Fact]
    public void NormalizesProfileAndProtectedAncestor()
    {
        _environment.ShortProfile = true;
        Assert.True(Inspector().Inspect(Location(), default).Allowed);
        _environment.Documents = CACHE + @"\protected";
        Assert.Equal("ProtectedPath", Inspector().Inspect(Location(), default).Reason);
    }

    /// <summary>루트와 상위 폴더의 정션은 추적하지 않습니다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsRootAndAncestorJunction(bool root)
    {
        if (root) { _files.Dir(PROFILE, FakeDirectoryEntrySource.Folder("cache", FileAttributes.ReparsePoint)); }
        else { _files.Dir(@"C:\Users", FakeDirectoryEntrySource.Folder("tester", FileAttributes.ReparsePoint)); }
        Assert.Equal("LinkOrPlaceholder", Inspector().Inspect(Location(), default).Reason);
        Assert.Empty(_files.Enumerated);
    }

    /// <summary>하위 파일의 링크와 클라우드 속성도 내용 읽기 전에 거절합니다.</summary>
    [Theory]
    [InlineData(FileAttributes.ReparsePoint)]
    [InlineData(FileAttributes.Offline)]
    [InlineData((FileAttributes)0x40000)]
    [InlineData((FileAttributes)0x400000)]
    public void RejectsLinkedOrPlaceholderChildren(FileAttributes attributes)
    {
        _files.Dir(CACHE, FakeDirectoryEntrySource.File("item.dat", 1, attributes));
        Assert.Equal("LinkOrPlaceholder", Inspector().Inspect(Location(), default).Reason);
    }

    /// <summary>부분 열거와 항목 제한 초과 결과는 실행 허가에 쓰지 않습니다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsIncompleteEnumeration(bool limit)
    {
        _files.Dir(CACHE, FakeDirectoryEntrySource.File("one", 10), FakeDirectoryEntrySource.File("two", 20));
        if (!limit) { _files.FailAfter(CACHE, 1); }
        var result = Inspector(maxEntries: limit ? 1 : 100).Inspect(Location(), default);
        Assert.False(result.Allowed);
        Assert.Equal(limit ? "InspectionIncomplete" : "InspectionFailed", result.Reason);
    }

    /// <summary>빈 폴더의 마지막 열거에서 시간이 초과돼도 완료로 오인하지 않습니다.</summary>
    [Fact]
    public void RejectsBudgetExceededAtEndOfEmptyEnumeration()
    {
        _files.OnEnumerate = _ => _time.Advance(TimeSpan.FromSeconds(16));
        Assert.Equal("InspectionIncomplete", Inspector().Inspect(Location(), default).Reason);
    }

    /// <summary>접근 거부는 존재하지 않는 빈 캐시로 취급하지 않습니다.</summary>
    [Fact]
    public void RejectsUnreadableRootAndChangedTool()
    {
        _files.Deny(CACHE);
        Assert.False(Inspector().Inspect(Location(), default).Allowed);
        _files.Deny(@"C:\tools");
        Assert.Equal("ToolChanged", Inspector().Inspect(Location(), default).Reason);
    }

    /// <summary>NuGet HTTP 캐시에는 dat 이외의 파일이 하나라도 있으면 실행하지 않습니다.</summary>
    [Theory]
    [InlineData("list.dat", true)]
    [InlineData("package.nupkg", false)]
    public void HttpCacheRejectsGlobalPackages(string name, bool allowed)
    {
        _files.Dir(CACHE, FakeDirectoryEntrySource.File(name, 20));
        var result = Inspector().Inspect(Location(tool: CacheTool.NuGetHttp), default);
        Assert.Equal(allowed, result.Allowed);
        if (!allowed) { Assert.Equal("UnexpectedHttpCacheContent", result.Reason); }
    }

    /// <summary>루트 placeholder와 접근 거부는 빈 캐시로 허용하지 않습니다.</summary>
    [Theory]
    [InlineData(RootPresence.Placeholder)]
    [InlineData(RootPresence.AccessDenied)]
    [InlineData(RootPresence.Error)]
    public void RejectsUnobservableRoot(RootPresence presence)
    {
        Assert.False(Inspector(source: new RootOverride(_files, presence)).Inspect(Location(), default).Allowed);
        Assert.Empty(_files.Enumerated);
    }

    /// <summary>하위 폴더가 정션이면 내용을 열거하지 않습니다.</summary>
    [Fact]
    public void DoesNotTraverseChildJunction()
    {
        _files.Dir(CACHE, FakeDirectoryEntrySource.Folder("link", FileAttributes.ReparsePoint));
        Assert.False(Inspector().Inspect(Location(), default).Allowed);
        Assert.Equal([CACHE], _files.Enumerated.ToArray());
    }

    private sealed class RootOverride(IDirectoryEntrySource inner, RootPresence presence) : IDirectoryEntrySource
    {
        public RootPresence ProbeRoot(string path) => path == CACHE ? presence : inner.ProbeRoot(path);
        public IEnumerable<DirectoryEntry> Enumerate(string path) => inner.Enumerate(path);
    }

    private sealed class AliasEnvironment : IPathEnvironment
    {
        public bool ShortProfile { get; set; }
        public string Documents { get; set; } = DOCUMENTS;
        public string? GetEnvironmentVariable(string name) => name == "SystemDrive" ? "C:" : null;
        public string? GetKnownFolderPath(ProtectedKnownFolder folder) => folder == ProtectedKnownFolder.Documents ? Documents : null;
        public string? GetUserProfilePath() => ShortProfile ? @"C:\Users\TESTER~1" : PROFILE;
        public string NormalizePath(string path) => PathScope.Normalize(path.Replace("PRIVAT~1", "Private Documents", StringComparison.OrdinalIgnoreCase)
            .Replace("TESTER~1", "tester", StringComparison.OrdinalIgnoreCase));
        public string? ReadSmallTextFile(string path, int maxBytes) => null;
    }
}
