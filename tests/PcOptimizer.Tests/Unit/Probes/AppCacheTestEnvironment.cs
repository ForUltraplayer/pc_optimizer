/**
 * @file    : AppCacheTestEnvironment.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 프로브 파이프라인 테스트용 가짜 PC(프로필·다른 드라이브 Steam 라이브러리·npm 설정 재정의·보호 폴더 NuGet 설정·Squirrel 앱·Adobe·NVIDIA 캐시, 레지스트리, 보호 정책, 규칙 파일)와 프로브 생성 도우미
 */

// 기본 패키지
using System.IO;
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.ConfigReaders;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// 앱 캐시 프로브용 가짜 PC입니다. 크기는 메타데이터 값일 뿐 실제 파일이 아닙니다.
/// </summary>
internal sealed class AppCacheTestEnvironment
{
    /// <summary>프로필.</summary>
    public const string PROFILE = @"C:\Users\tester";

    /// <summary>%LocalAppData%.</summary>
    public const string LOCAL = PROFILE + @"\AppData\Local";

    /// <summary>%AppData%.</summary>
    public const string ROAMING = PROFILE + @"\AppData\Roaming";

    /// <summary>문서(보호).</summary>
    public const string DOCUMENTS = PROFILE + @"\Documents";

    /// <summary>Steam 설치 폴더(보호되는 Program Files (x86) 아래).</summary>
    public const string STEAM_ROOT = @"C:\Program Files (x86)\Steam";

    /// <summary>다른 드라이브의 Steam 라이브러리.</summary>
    public const string STEAM_LIBRARY = @"D:\SteamLibrary";

    /// <summary>npm 설정 재정의 위치(스캔 루트 밖).</summary>
    public const string NPM_OVERRIDE = @"D:\DevCache\npm";

    /// <summary>보호 정책(문서·Program Files 두 곳).</summary>
    public const string POLICY = """
        { "schemaVersion": 1, "protectedRoots": [
          { "kind": "knownFolder", "folder": "Documents" },
          { "kind": "environmentPath", "path": "%ProgramFiles%" },
          { "kind": "environmentPath", "path": "%ProgramFiles(x86)%" } ] }
        """;

    /// <summary>현재 사용자 가짜 SID.</summary>
    public const string CURRENT_SID = "S-1-5-21-1000-1000-1000-1001";

    /// <summary>다른 사용자 가짜 SID.</summary>
    public const string OTHER_SID = "S-1-5-21-1000-1000-1000-1002";

    /// <summary>게임 본체 파일 크기(셰이더 캐시 합계에 들어가면 안 됨).</summary>
    public const long GAME_BYTES = 999_999;

    /// <summary>가짜 경로 환경.</summary>
    public FakePathEnvironment Environment { get; } = new FakePathEnvironment { Profile = PROFILE }
        .WithVariable("APPDATA", ROAMING)
        .WithVariable("LOCALAPPDATA", LOCAL)
        .WithVariable("ProgramData", @"C:\ProgramData")
        .WithVariable("TEMP", LOCAL + @"\Temp")
        .WithVariable("SystemRoot", @"C:\Windows")
        .WithVariable("SystemDrive", "C:")
        .WithVariable("ProgramFiles", @"C:\Program Files")
        .WithVariable("ProgramFiles(x86)", @"C:\Program Files (x86)")
        .WithVariable("PUBLIC", @"C:\Users\Public")
        .WithVariable(NuGetConfigReader.ENVIRONMENT_VARIABLE, DOCUMENTS + @"\nuget")
        .WithKnownFolder(ProtectedKnownFolder.Documents, DOCUMENTS)
        .WithFile(PROFILE + @"\.npmrc", AppConfigReaderTests.Fixture("npmrc-override.npmrc"))
        .WithFile(STEAM_ROOT + @"\steamapps\libraryfolders.vdf", AppConfigReaderTests.Fixture("libraryfolders.vdf"));

    /// <summary>
    /// 가짜 PC를 만든다.
    /// </summary>
    /// <param name="defaultCaches">npm·pip·NuGet 기본 캐시 폴더를 만들지 여부(false면 설정으로 옮긴 캐시만 있는 PC).</param>
    public AppCacheTestEnvironment(bool defaultCaches = true)
    {
        Source = Tree(defaultCaches);
    }

    /// <summary>가짜 파일 시스템.</summary>
    public FakeDirectoryEntrySource Source { get; }

    /// <summary>가짜 레지스트리(Python·NVIDIA·Steam 설치).</summary>
    public FakeRegistryReader Registry { get; } = new FakeRegistryReader()
        .WithSubKeys(RegistryRoot.CurrentUser, RegistryView.Default, @"Software\Python", new RegistrySubKeyReading(RegistryReadStatus.Found, [], null))
        .WithSubKeys(RegistryRoot.LocalMachine, RegistryView.Registry64, @"Software\NVIDIA Corporation", new RegistrySubKeyReading(RegistryReadStatus.Found, [], null))
        .WithSubKeys(RegistryRoot.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", new RegistrySubKeyReading(RegistryReadStatus.Found, [], null))
        .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY, new RegistryValueEntry("AutoLoginUser", "String", "fakeuser", null))
        .WithString(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY, SteamLibraryReader.USER_VALUE, "c:/program files (x86)/steam")
        .WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY, OtherUserLocationGuard.PROFILES_DIRECTORY_VALUE, @"%SystemDrive%\Users")
        .WithSubKeys(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY,
            new RegistrySubKeyReading(RegistryReadStatus.Found, ["S-1-5-18", CURRENT_SID, OTHER_SID], null))
        .WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY + @"\S-1-5-18", OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE, @"%SystemRoot%\system32\config\systemprofile")
        .WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY + @"\" + CURRENT_SID, OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE, PROFILE)
        .WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY + @"\" + OTHER_SID, OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE, @"C:\Users\other");

    /// <summary>규칙 파일(커뮤니티는 구성 fixture, 보충·메타데이터는 실제 포함 파일).</summary>
    public Dictionary<string, byte[]> RuleFiles { get; } = RuleCatalogLoaderTests.Files(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Winapp2", "probe-community.ini")));

    /// <summary>보호 정책 JSON(null이면 없음).</summary>
    public string? Policy { get; set; } = POLICY;

    /// <summary>
    /// 프로브를 만든다.
    /// </summary>
    public AppCacheProbe Probe()
    {
        var fileScan = new FileScanService(
            () => Policy,
            new ProtectionPolicyResolver(Environment, Registry),
            new ScanRootCatalog(Environment),
            new FileSystemScanner(Source, new FakeFileIdentityReader(), new ManualTimeProvider()),
            Source,
            ScanOptions.DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME,
            new ManualTimeProvider());
        return new AppCacheProbe(
            RuleCatalogLoaderTests.Loader(RuleFiles),
            fileScan,
            Environment,
            Registry,
            Source,
            [
                new NpmConfigReader(Environment, Source),
                new PipConfigReader(Environment, Source),
                new NuGetConfigReader(Environment, Source),
                new SteamLibraryReader(Environment, Registry, Source),
                new AdobeMediaCacheConfigReader(),
            ],
            new FakeClock(),
            new ManualTimeProvider(),
            CURRENT_SID);
    }

    /// <summary>
    /// 검사 컨텍스트.
    /// </summary>
    public static ScanContext Context(bool elevated = false)
    {
        return new ScanContext(Guid.NewGuid(), EngineTestData.USER with { IsElevated = elevated }, false, FakeClock.DEFAULT_NOW);
    }

    /// <summary>
    /// 가짜 트리.
    /// </summary>
    private static FakeDirectoryEntrySource Tree(bool defaultCaches)
    {
        static DirectoryEntry D(string name) => FakeDirectoryEntrySource.Folder(name);
        static DirectoryEntry F(string name, long bytes) => FakeDirectoryEntrySource.File(name, bytes);

        var source = new FakeDirectoryEntrySource()
            .Dir(PROFILE, D("AppData"), D("Documents"), F(".npmrc", 300))
            .Dir(DOCUMENTS, F("private.docx", 999))
            .Dir(PROFILE + @"\AppData", D("Local"), D("Roaming"))
            .Dir(ROAMING, D("Adobe"))
            .Dir(ROAMING + @"\Adobe", D("Common"))
            .Dir(ROAMING + @"\Adobe\Common", D("Media Cache Files"))
            .Dir(ROAMING + @"\Adobe\Common\Media Cache Files", F("clip.cfa", 500))
            .Dir(LOCAL, D("NVIDIA"), D("Discord"), D("slack"), D("Vendor"), D("Temp"))
            .Dir(LOCAL + @"\NVIDIA", D("DXCache"), D("GLCache"))
            .Dir(LOCAL + @"\NVIDIA\DXCache", F("s1", 50))
            .Dir(LOCAL + @"\NVIDIA\GLCache", F("s2", 70))
            .Dir(LOCAL + @"\Discord", F("Update.exe", 10), D("app-1.0.2"), D("app-1.0.1"), D("packages"))
            .Dir(LOCAL + @"\Discord\app-1.0.1")
            .Dir(LOCAL + @"\Discord\app-1.0.2")
            .Dir(LOCAL + @"\slack", D("app-4.0"))
            .Dir(LOCAL + @"\slack\app-4.0")
            .Dir(LOCAL + @"\Vendor", F("a.log", 5), F("keep.log", 7), F("b.txt", 9), D("Sub"))
            .Dir(LOCAL + @"\Vendor\Sub", F("c.log", 11))
            .Dir(LOCAL + @"\Temp", F("t.tmp", 1))
            .Dir(@"C:\ProgramData")
            .Dir(@"C:\", FakeDirectoryEntrySource.Folder("Documents and Settings", FileAttributes.ReparsePoint), D("Users"), D("$Recycle.Bin"))
            .Dir(@"C:\Documents and Settings\Default\Junctioned", F("through-junction.dat", 4444))
            .Dir(@"C:\Users", D("tester"), D("other"), D("Public"))
            .Dir(@"C:\Users\Public", D("Vendor"))
            .Dir(@"C:\Users\Public\Vendor", F("shared.tmp", 60))
            .Dir(@"C:\Users\other", D(".vendor_usage"))
            .Dir(@"C:\Users\other\.vendor_usage", F("usage.dat", 5000))
            .Dir(@"C:\$Recycle.Bin", D(CURRENT_SID), D(OTHER_SID))
            .Dir(@"C:\$Recycle.Bin\" + CURRENT_SID, F("$RABC.txt", 100))
            .Dir(@"C:\$Recycle.Bin\" + OTHER_SID, F("$RDEF.txt", 7777))
            .Dir(@"D:\", D("DevCache"), D("SteamLibrary"), D("NuGetPkgs"), D("PipCache"))
            .Dir(@"D:\NuGetPkgs", F("moved.nupkg", 600))
            .Dir(@"D:\PipCache", F("moved.whl", 700))
            .Dir(@"D:\DevCache", D("npm"))
            .Dir(NPM_OVERRIDE, F("big.tgz", 1000))
            .Dir(STEAM_LIBRARY, D("steamapps"))
            .Dir(STEAM_LIBRARY + @"\steamapps", D("shadercache"), D("common"))
            .Dir(STEAM_LIBRARY + @"\steamapps\shadercache", D("123"))
            .Dir(STEAM_LIBRARY + @"\steamapps\shadercache\123", F("cache.bin", 2000))
            .Dir(STEAM_LIBRARY + @"\steamapps\common", D("Game"))
            .Dir(STEAM_LIBRARY + @"\steamapps\common\Game", F("game.pak", GAME_BYTES));
        if (defaultCaches)
        {
            source
                .Dir(PROFILE, D("AppData"), D("Documents"), D(".nuget"), F(".npmrc", 300))
                .Dir(PROFILE + @"\.nuget", D("packages"))
                .Dir(PROFILE + @"\.nuget\packages", D("newtonsoft.json"))
                .Dir(PROFILE + @"\.nuget\packages\newtonsoft.json", F("newtonsoft.json.13.0.3.nupkg", 400))
                .Dir(LOCAL, D("npm-cache"), D("pip"), D("NVIDIA"), D("Discord"), D("slack"), D("Vendor"), D("Temp"))
                .Dir(LOCAL + @"\npm-cache", D("_cacache"))
                .Dir(LOCAL + @"\npm-cache\_cacache", F("a", 100), F("b", 200))
                .Dir(LOCAL + @"\pip", D("Cache"))
                .Dir(LOCAL + @"\pip\Cache", D("http"))
                .Dir(LOCAL + @"\pip\Cache\http", F("w.whl", 300));
        }

        return source;
    }
}
