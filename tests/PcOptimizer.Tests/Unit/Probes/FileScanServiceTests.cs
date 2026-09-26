/**
 * @file    : FileScanServiceTests.cs
 * @author  : rudals252
 * @brief   : 공유 파일 스캔 서비스의 보호 정책 무효 시 순회 안 함, 포함 보호 정책만 읽기(출력 폴더 protect.json 미사용), 검사 ID별 한 번 순회·새 검사 ID 재순회, 임시 위치 관측(중첩 위치 합계·부분·없음·접근 거부·패턴), 임의 디렉터리 합계 조회를 가짜 환경·열거로 검증
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

    /// <summary>예전에 정책을 복사하던 출력 폴더 위치(이제 읽지 않음).</summary>
    public static readonly string OUTPUT_POLICY = Path.Combine(AppContext.BaseDirectory, "rules", "protect.json");

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

    /// <summary>
    /// 포함 보호 정책은 Probes 어셈블리 리소스로만 읽는다. 출력 폴더에 protect.json을 복사하지 않으며, 포함 정책은 유효해서 순회가 시작된다.
    /// </summary>
    [Fact]
    public async Task 포함_보호_정책만_읽고_출력_폴더의_protect_json은_쓰지_않는다()
    {
        Assert.False(File.Exists(OUTPUT_POLICY), "protect.json 파일이 출력 폴더에 있다");
        Assert.Contains(FileScanService.POLICY_RESOURCE_NAME, typeof(FileScanService).Assembly.GetManifestResourceNames());

        var policy = FileScanService.ReadBundledPolicy();
        var result = await Service(Tree(), policy).GetOrScanAsync(Context(), CancellationToken.None);

        Assert.True(ProtectionPolicyParser.Parse(policy!).IsValid);
        Assert.True(result.IsPolicyValid);
        Assert.NotNull(result.Traversal);
    }

    /// <summary>
    /// 사용자 쓰기 가능한 출력 폴더에 무효(보호 루트 없음) protect.json을 놓아도 포함 정책을 쓴다. 같은 클래스라 위 검사와 동시에 실행되지 않으며, 끝나면 만든 파일을 지운다.
    /// </summary>
    [Fact]
    public void 출력_폴더의_protect_json을_바꿔도_포함_정책을_쓴다()
    {
        const string TAMPERED = """{ "schemaVersion": 1, "protectedRoots": [ { "kind": "everything" } ] }""";
        Directory.CreateDirectory(Path.GetDirectoryName(OUTPUT_POLICY)!);
        File.WriteAllText(OUTPUT_POLICY, TAMPERED);
        try
        {
            var policy = FileScanService.ReadBundledPolicy();

            Assert.NotEqual(TAMPERED, policy);
            Assert.True(ProtectionPolicyParser.Parse(policy!).IsValid);
        }
        finally
        {
            File.Delete(OUTPUT_POLICY);
        }
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

    /// <summary>
    /// 문서 Known Folder 위치를 읽지 못해도 프로필 기본 위치(Documents)는 보호되어 열거하지 않는다(실패 시 닫힘).
    /// </summary>
    [Fact]
    public async Task 위치를_못_읽은_Known_Folder의_기본_위치는_열거하지_않는다()
    {
        var source = Tree();
        var environment = new FakePathEnvironment { Profile = PROFILE }
            .WithVariable("ProgramData", @"C:\ProgramData")
            .WithVariable("TEMP", TEMP)
            .WithVariable("SystemRoot", @"C:\Windows")
            .WithVariable("LocalAppData", PROFILE + @"\AppData\Local");

        var result = await Service(source, environment: environment).GetOrScanAsync(Context(), CancellationToken.None);

        Assert.Equal(1, result.Protection!.UnresolvedCount);
        Assert.DoesNotContain(source.Enumerated, path => path.EndsWith(@"\Documents", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.TryGetDirectoryTotals(PROFILE, out var profile));
        Assert.Equal(1, profile.Skips.ProtectedExcluded);
    }

    /// <summary>리디렉션된 문서 폴더가 있어도 프로필에 남은 기본 Documents 폴더는 열거하지 않는다.</summary>
    [Fact]
    public async Task 리디렉션_뒤_남은_기본_폴더도_열거하지_않는다()
    {
        var source = Tree();
        var environment = Environment().WithKnownFolder(ProtectedKnownFolder.Documents, PROFILE + @"\OneDrive\Documents");

        var result = await Service(source, environment: environment).GetOrScanAsync(Context(), CancellationToken.None);

        Assert.DoesNotContain(source.Enumerated, path => path.EndsWith(@"\Documents", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Protection!.IsProtected(PROFILE + @"\OneDrive\Documents"));
        Assert.True(result.Protection.IsProtected(PROFILE + @"\Documents"));
    }
}
