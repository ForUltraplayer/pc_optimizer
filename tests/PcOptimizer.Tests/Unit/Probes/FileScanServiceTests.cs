/**
 * @file    : FileScanServiceTests.cs
 * @author  : rudals252
 * @brief   : 공유 파일 스캔 서비스의 보호 정책 무효 시 순회 안 함, 검사 ID별 한 번 순회·새 검사 ID 재순회, 임시 위치 관측(중첩 위치 합계·부분·없음·접근 거부·패턴), 임의 디렉터리 합계 조회를 가짜 환경·열거로 검증
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="FileScanService"/>를 검증합니다. 실제 파일 시스템·환경을 읽지 않습니다.
/// </summary>
public sealed class FileScanServiceTests
{
    /// <summary>테스트 프로필 경로.</summary>
    public const string PROFILE = @"C:\Users\tester";

    /// <summary>유효한 최소 보호 정책(문서 폴더).</summary>
    public const string POLICY = """{ "schemaVersion": 1, "protectedRoots": [ { "kind": "knownFolder", "folder": "Documents" } ] }""";

    private const string TEMP = PROFILE + @"\AppData\Local\Temp";
    private const string EXPLORER = PROFILE + @"\AppData\Local\Microsoft\Windows\Explorer";

    /// <summary>
    /// 표준 가짜 환경(문서 폴더 보호).
    /// </summary>
    internal static FakePathEnvironment Environment()
    {
        return new FakePathEnvironment { Profile = PROFILE }
            .WithKnownFolder(ProtectedKnownFolder.Documents, PROFILE + @"\Documents")
            .WithVariable("ProgramData", @"C:\ProgramData")
            .WithVariable("TEMP", TEMP)
            .WithVariable("SystemRoot", @"C:\Windows")
            .WithVariable("LocalAppData", PROFILE + @"\AppData\Local");
    }

    /// <summary>
    /// 프로필·ProgramData·Windows 임시 폴더가 있는 가짜 트리(업데이트 다운로드·배달 최적화 없음/거부).
    /// </summary>
    internal static FakeDirectoryEntrySource Tree()
    {
        return new FakeDirectoryEntrySource()
            .Dir(PROFILE, FakeDirectoryEntrySource.Folder("Documents"), FakeDirectoryEntrySource.Folder("AppData"), FakeDirectoryEntrySource.File("ntuser.dat", 100, FileAttributes.Hidden))
            .Dir(PROFILE + @"\Documents", FakeDirectoryEntrySource.File("private.docx", 999))
            .Dir(PROFILE + @"\AppData", FakeDirectoryEntrySource.Folder("Local"))
            .Dir(PROFILE + @"\AppData\Local", FakeDirectoryEntrySource.Folder("Temp"), FakeDirectoryEntrySource.Folder("Microsoft"))
            .Dir(TEMP, FakeDirectoryEntrySource.File("a.tmp", 10), FakeDirectoryEntrySource.Folder("locked"))
            .Deny(TEMP + @"\locked")
            .Dir(PROFILE + @"\AppData\Local\Microsoft", FakeDirectoryEntrySource.Folder("Windows"))
            .Dir(PROFILE + @"\AppData\Local\Microsoft\Windows", FakeDirectoryEntrySource.Folder("Explorer"))
            .Dir(EXPLORER, FakeDirectoryEntrySource.File("thumbcache_96.db", 300), FakeDirectoryEntrySource.File("other.etl", 5))
            .Dir(@"C:\ProgramData", FakeDirectoryEntrySource.File("p.bin", 20))
            .Dir(@"C:\Windows\Temp", FakeDirectoryEntrySource.File("w.log", 7))
            .Deny(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization");
    }

    /// <summary>
    /// 가짜 의존성으로 서비스를 만든다.
    /// </summary>
    internal static FileScanService Service(FakeDirectoryEntrySource source, string? policy = POLICY, FakePathEnvironment? environment = null)
    {
        var env = environment ?? Environment();
        return new FileScanService(
            () => policy,
            new ProtectionPolicyResolver(env, new FakeRegistryReader()),
            new ScanRootCatalog(env),
            new FileSystemScanner(source, new FakeFileIdentityReader(), new ManualTimeProvider()),
            source,
            ScanOptions.DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME,
            new ManualTimeProvider());
    }

    /// <summary>
    /// 새 검사 컨텍스트.
    /// </summary>
    public static ScanContext Context()
    {
        return new ScanContext(Guid.NewGuid(), EngineTestData.USER, false, FakeClock.DEFAULT_NOW);
    }

    /// <summary>보호 정책이 없거나 무효이면 아무 디렉터리도 열거하지 않는다.</summary>
    [Theory]
    [InlineData(null, ProtectionPolicyError.Missing)]
    [InlineData("""{ "schemaVersion": 1, "protectedRoots": [ { "kind": "everything" } ] }""", ProtectionPolicyError.UnknownKind)]
    public async Task 정책이_무효이면_순회하지_않는다(string? policy, ProtectionPolicyError expected)
    {
        var source = Tree();

        var result = await Service(source, policy).GetOrScanAsync(Context(), CancellationToken.None);

        Assert.False(result.IsPolicyValid);
        Assert.Equal(expected, result.PolicyError);
        Assert.Null(result.Traversal);
        Assert.Empty(source.Enumerated);
    }

    /// <summary>같은 검사 ID는 한 번만 순회해 같은 결과를 돌려주고, 새 검사 ID는 다시 순회한다.</summary>
    [Fact]
    public async Task 검사_ID마다_한_번만_순회한다()
    {
        var source = Tree();
        var service = Service(source);
        var context = Context();

        var first = await service.GetOrScanAsync(context, CancellationToken.None);
        var enumeratedAfterFirst = source.Enumerated.Count;
        var second = await service.GetOrScanAsync(context, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(enumeratedAfterFirst, source.Enumerated.Count);

        var third = await service.GetOrScanAsync(Context(), CancellationToken.None);
        Assert.NotSame(first, third);
        Assert.Equal(2 * enumeratedAfterFirst, source.Enumerated.Count);
    }

    /// <summary>
    /// 중첩 위치(TEMP)는 프로필 순회 합계에서, 패턴 위치는 패턴 파일만, 없음·접근 거부는 크기 없이 알린다. 보호 폴더는 열거하지 않는다.
    /// </summary>
    [Fact]
    public async Task 임시_위치를_관측한다()
    {
        var source = Tree();

        var result = await Service(source).GetOrScanAsync(Context(), CancellationToken.None);

        Assert.True(result.IsPolicyValid);
        var locations = result.Locations.ToDictionary(location => location.Id);
        Assert.Equal(new LocationObservation(FileScanProbeContract.LOCATION_USER_TEMP, TEMP, false, LocationState.Partial, 10, 1, true), locations[FileScanProbeContract.LOCATION_USER_TEMP]);
        Assert.Equal(LocationState.Observed, locations[FileScanProbeContract.LOCATION_WINDOWS_TEMP].State);
        Assert.Equal(7, locations[FileScanProbeContract.LOCATION_WINDOWS_TEMP].Bytes);
        Assert.Equal(LocationState.Absent, locations[FileScanProbeContract.LOCATION_UPDATE_DOWNLOAD].State);
        Assert.Null(locations[FileScanProbeContract.LOCATION_UPDATE_DOWNLOAD].Bytes);
        Assert.Equal(LocationState.AccessDenied, locations[FileScanProbeContract.LOCATION_DELIVERY_OPTIMIZATION].State);
        Assert.Null(locations[FileScanProbeContract.LOCATION_DELIVERY_OPTIMIZATION].Bytes);
        Assert.Equal(300, locations[FileScanProbeContract.LOCATION_THUMBNAIL_CACHE].Bytes);
        Assert.DoesNotContain(source.Enumerated, path => path.EndsWith("Documents", StringComparison.Ordinal));

        Assert.True(result.TryGetDirectoryTotals(PROFILE, out var profile));
        Assert.Equal(100 + 10 + 300 + 5, profile.Bytes);
        Assert.Equal(1, profile.Skips.ProtectedExcluded);
        Assert.Equal(1, profile.Skips.AccessDenied);
        Assert.False(result.TryGetDirectoryTotals(PROFILE + @"\Documents", out _));
    }
}
