/**
 * @file    : AppConfigReaderTests.cs
 * @author  : rudals252
 * @brief   : npm·pip·NuGet·Steam·Adobe 앱 설정 리더의 기본(설정 없음)·재정의(설정 파일)·환경 변수 우선·해석 불가·읽기 실패 fixture와, 결과에 인증 토큰·비밀번호·다른 키 값이 남지 않음을 검증
 */

// 기본 패키지
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes.ConfigReaders;

/// <summary>
/// 앱 설정 리더를 가짜 환경·fixture로 검증합니다. 실제 사용자 설정 파일·레지스트리를 읽지 않습니다.
/// </summary>
public sealed class AppConfigReaderTests
{
    /// <summary>테스트 프로필.</summary>
    public const string PROFILE = @"C:\Users\tester";

    /// <summary>테스트 %APPDATA%.</summary>
    public const string APP_DATA = PROFILE + @"\AppData\Roaming";

    /// <summary>fixture에 든 가짜 비밀 값(결과·측정값·로그·내보내기에 나오면 안 됨).</summary>
    public static readonly string[] SECRETS =
    [
        "npm_FAKESECRETTOKEN0123456789", "fake.user@example.invalid", "PIPFAKESECRET987", "fakeuser", "NUGETFAKESECRET555", "NUGETFAKEAPIKEY777",
    ];

    private static readonly string FIXTURES = Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppConfig");

    /// <summary>
    /// fixture 내용을 읽는다.
    /// </summary>
    public static string Fixture(string name)
    {
        return File.ReadAllText(Path.Combine(FIXTURES, name));
    }

    /// <summary>
    /// 표준 가짜 환경.
    /// </summary>
    internal static FakePathEnvironment Environment()
    {
        return new FakePathEnvironment { Profile = PROFILE }.WithVariable("APPDATA", APP_DATA).WithVariable("LOCALAPPDATA", PROFILE + @"\AppData\Local");
    }

    /// <summary>
    /// 결과 전체를 문자열로 만든다(비밀 값 포함 여부 확인용).
    /// </summary>
    private static string Dump(AppConfigReading reading)
    {
        return reading + JsonSerializer.Serialize(reading);
    }

    /// <summary>
    /// 비밀 값이 없는지 확인한다.
    /// </summary>
    private static void AssertNoSecrets(string text)
    {
        foreach (var secret in SECRETS)
        {
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>npm: 설정 파일이 없거나 cache 키가 없으면 기본 위치만(설정 없음).</summary>
    [Fact]
    public void Npm_설정이_없으면_기본_위치만이다()
    {
        var none = new NpmConfigReader(Environment(), new FakeDirectoryEntrySource()).Read();
        var noKey = new NpmConfigReader(Environment().WithFile(PROFILE + @"\.npmrc", Fixture("npmrc-default.npmrc")), new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.NotConfigured, none.State);
        Assert.Equal(AppConfigReadState.NotConfigured, noKey.State);
        Assert.Empty(noKey.Paths);
        AssertNoSecrets(Dump(noKey));
    }

    /// <summary>npm: 사용자 .npmrc의 cache만 읽고 토큰·이메일은 결과에 남기지 않는다. 읽은 파일은 사용자 .npmrc 하나다.</summary>
    [Fact]
    public void Npm_사용자_npmrc의_cache만_읽는다()
    {
        var environment = Environment().WithFile(PROFILE + @"\.npmrc", Fixture("npmrc-override.npmrc"));

        var reading = new NpmConfigReader(environment, new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.Configured, reading.State);
        Assert.Equal(AppConfigValueOrigin.UserFile, reading.Origin);
        Assert.Equal([@"D:\DevCache\npm"], reading.Paths);
        Assert.Equal([PROFILE + @"\.npmrc"], environment.FileReads);
        AssertNoSecrets(Dump(reading));
    }

    /// <summary>npm: 환경 변수 npm_config_cache가 사용자 설정보다 우선한다.</summary>
    [Fact]
    public void Npm_환경_변수가_우선한다()
    {
        var environment = Environment().WithVariable(NpmConfigReader.ENVIRONMENT_VARIABLE, @"F:\EnvNpmCache\").WithFile(PROFILE + @"\.npmrc", Fixture("npmrc-override.npmrc"));

        var reading = new NpmConfigReader(environment, new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigValueOrigin.Environment, reading.Origin);
        Assert.Equal([@"F:\EnvNpmCache"], reading.Paths);
    }

    /// <summary>npm: 변수 참조가 든 값은 추측하지 않고 해석 불가, 파일이 있는데 읽지 못하면 읽기 실패.</summary>
    [Fact]
    public void Npm_해석_불가와_읽기_실패를_구분한다()
    {
        var invalid = new NpmConfigReader(Environment().WithFile(PROFILE + @"\.npmrc", Fixture("npmrc-invalid.npmrc")), new FakeDirectoryEntrySource()).Read();
        var unreadable = new NpmConfigReader(
            Environment(),
            new FakeDirectoryEntrySource().Dir(PROFILE, FakeDirectoryEntrySource.File(".npmrc", 10))).Read();

        Assert.Equal(AppConfigReadState.Invalid, invalid.State);
        Assert.Empty(invalid.Paths);
        Assert.Equal(AppConfigReadState.Unreadable, unreadable.State);
        AssertNoSecrets(Dump(invalid));
    }

    /// <summary>pip: [global] cache-dir만 읽고 다른 절의 cache-dir·index-url 자격 증명은 무시한다.</summary>
    [Fact]
    public void Pip_global_cache_dir만_읽는다()
    {
        var environment = Environment().WithFile(APP_DATA + @"\pip\pip.ini", Fixture("pip-override.ini"));

        var reading = new PipConfigReader(environment, new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.Configured, reading.State);
        Assert.Equal(AppConfigValueOrigin.UserFile, reading.Origin);
        Assert.Equal([@"E:\PyCache\pip"], reading.Paths);
        AssertNoSecrets(Dump(reading));
    }

    /// <summary>pip: 설정 없음·다른 절만 있음은 기본 위치만, 상대 경로는 해석 불가, 환경 변수 PIP_CACHE_DIR이 우선.</summary>
    [Fact]
    public void Pip_기본_해석불가_환경변수를_처리한다()
    {
        var none = new PipConfigReader(Environment(), new FakeDirectoryEntrySource()).Read();
        var otherSection = new PipConfigReader(Environment().WithFile(APP_DATA + @"\pip\pip.ini", Fixture("pip-default.ini")), new FakeDirectoryEntrySource()).Read();
        var invalid = new PipConfigReader(Environment().WithFile(APP_DATA + @"\pip\pip.ini", Fixture("pip-invalid.ini")), new FakeDirectoryEntrySource()).Read();
        var env = new PipConfigReader(
            Environment().WithVariable(PipConfigReader.ENVIRONMENT_VARIABLE, @"H:\PipEnv").WithFile(APP_DATA + @"\pip\pip.ini", Fixture("pip-override.ini")),
            new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.NotConfigured, none.State);
        Assert.Equal(AppConfigReadState.NotConfigured, otherSection.State);
        Assert.Equal(AppConfigReadState.Invalid, invalid.State);
        Assert.Equal(AppConfigValueOrigin.Environment, env.Origin);
        Assert.Equal([@"H:\PipEnv"], env.Paths);
        AssertNoSecrets(Dump(otherSection));
    }

    /// <summary>NuGet: config 절의 globalPackagesFolder만 읽고 자격 증명·API 키는 결과에 없다.</summary>
    [Fact]
    public void NuGet_globalPackagesFolder만_읽는다()
    {
        var environment = Environment().WithFile(APP_DATA + @"\NuGet\NuGet.Config", Fixture("NuGet-override.Config"));

        var reading = new NuGetConfigReader(environment, new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.Configured, reading.State);
        Assert.Equal([@"D:\NuGetPackages"], reading.Paths);
        AssertNoSecrets(Dump(reading));
    }

    /// <summary>NuGet: config 밖의 같은 키는 무시(설정 없음), DTD가 있으면 해석 불가(외부·엔터티 참조 금지), NUGET_PACKAGES가 우선.</summary>
    [Fact]
    public void NuGet_기본_해석불가_환경변수를_처리한다()
    {
        var outside = new NuGetConfigReader(Environment().WithFile(APP_DATA + @"\NuGet\NuGet.Config", Fixture("NuGet-default.Config")), new FakeDirectoryEntrySource()).Read();
        var dtd = new NuGetConfigReader(Environment().WithFile(APP_DATA + @"\NuGet\NuGet.Config", Fixture("NuGet-invalid.Config")), new FakeDirectoryEntrySource()).Read();
        var env = new NuGetConfigReader(
            Environment().WithVariable(NuGetConfigReader.ENVIRONMENT_VARIABLE, @"C:\Users\tester\Documents\nuget").WithFile(APP_DATA + @"\NuGet\NuGet.Config", Fixture("NuGet-override.Config")),
            new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.NotConfigured, outside.State);
        Assert.Equal(AppConfigReadState.Invalid, dtd.State);
        Assert.Empty(dtd.Paths);
        Assert.Equal(AppConfigValueOrigin.Environment, env.Origin);
        Assert.Equal([@"C:\Users\tester\Documents\nuget"], env.Paths);
        AssertNoSecrets(Dump(dtd));
    }

    /// <summary>Steam vdf: 라이브러리 "path" 값만 뽑고(이스케이프·주석 처리), 닫히지 않은 문자열은 실패.</summary>
    [Fact]
    public void Steam_vdf에서_라이브러리_경로만_뽑는다()
    {
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], SteamLibraryFoldersParser.Parse(Fixture("libraryfolders.vdf")));
        Assert.Null(SteamLibraryFoldersParser.Parse(Fixture("libraryfolders-broken.vdf")));
        Assert.Empty(SteamLibraryFoldersParser.Parse("\"libraryfolders\" { }")!);
    }

    /// <summary>
    /// Steam: 레지스트리 SteamPath(슬래시 경로) 아래 vdf의 각 라이브러리에 대해 steamapps\shadercache만 돌려주며 라이브러리·게임 폴더는 돌려주지 않는다.
    /// 레지스트리는 SteamPath 값 하나만 요청하고 키 전체(자동 로그인 계정 등)는 읽지 않는다.
    /// </summary>
    [Fact]
    public void Steam은_라이브러리별_shadercache만_돌려준다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY, new RegistryValueEntry("AutoLoginUser", "String", "fakeuser", null))
            .WithString(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY, SteamLibraryReader.USER_VALUE, "c:/program files (x86)/steam");
        var environment = Environment().WithFile(@"c:\program files (x86)\steam\steamapps\libraryfolders.vdf", Fixture("libraryfolders.vdf"));

        var reading = new SteamLibraryReader(environment, registry, new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.Configured, reading.State);
        Assert.Equal(AppConfigValueOrigin.Registry, reading.Origin);
        Assert.Equal(2, reading.LibraryCount);
        Assert.Equal([@"c:\program files (x86)\steam\steamapps\shadercache", @"D:\SteamLibrary\steamapps\shadercache"], reading.Paths);
        Assert.All(reading.Paths, path => Assert.EndsWith(@"\steamapps\shadercache", path, StringComparison.OrdinalIgnoreCase));
        AssertNoSecrets(Dump(reading));
        Assert.Empty(registry.KeyReads);
        Assert.Equal([FakeRegistryReader.KeyOf(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY) + "|" + SteamLibraryReader.USER_VALUE], registry.StringReads);
    }

    /// <summary>Steam: 레지스트리가 없으면 설정 없음, HKLM 32비트 InstallPath로 대신 찾고 vdf가 없으면 설치 폴더만, vdf가 깨지면 해석 불가.</summary>
    [Fact]
    public void Steam_기본_대체_해석불가를_처리한다()
    {
        var none = new SteamLibraryReader(Environment(), new FakeRegistryReader(), new FakeDirectoryEntrySource()).Read();
        var machine = new FakeRegistryReader().WithString(RegistryRoot.LocalMachine, RegistryView.Registry32, SteamLibraryReader.MACHINE_KEY, SteamLibraryReader.MACHINE_VALUE, @"E:\Steam");
        var fallback = new SteamLibraryReader(Environment(), machine, new FakeDirectoryEntrySource()).Read();
        var broken = new SteamLibraryReader(Environment().WithFile(@"E:\Steam\steamapps\libraryfolders.vdf", Fixture("libraryfolders-broken.vdf")), machine, new FakeDirectoryEntrySource()).Read();

        Assert.Equal(AppConfigReadState.NotConfigured, none.State);
        Assert.Equal([@"E:\Steam\steamapps\shadercache"], fallback.Paths);
        Assert.Equal(1, fallback.LibraryCount);
        Assert.Equal(AppConfigReadState.Invalid, broken.State);
        Assert.Empty(broken.Paths);
        Assert.Empty(machine.KeyReads);
        Assert.All(machine.StringReads, read => Assert.True(
            read.EndsWith("|" + SteamLibraryReader.USER_VALUE, StringComparison.Ordinal) || read.EndsWith("|" + SteamLibraryReader.MACHINE_VALUE, StringComparison.Ordinal)));
    }

    /// <summary>Adobe: 형식을 검증할 수 없어 어떤 파일도 읽지 않고 항상 형식 미검증을 돌려준다.</summary>
    [Fact]
    public void Adobe는_파일을_읽지_않고_형식_미검증이다()
    {
        var reading = new AdobeMediaCacheConfigReader().Read();

        Assert.Equal(AdobeMediaCacheConfigReader.APP, reading.App);
        Assert.Equal(AppConfigReadState.CannotVerify, reading.State);
        Assert.Empty(reading.Paths);
    }
}
