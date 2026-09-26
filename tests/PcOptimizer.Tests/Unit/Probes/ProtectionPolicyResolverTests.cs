/**
 * @file    : ProtectionPolicyResolverTests.cs
 * @author  : rudals252
 * @brief   : 보호 정책 해석(리디렉션된 Known Folder, 환경 변수 경로와 해석 실패, OneDrive 환경 변수·레지스트리 계정, Dropbox info.json path, 손상 JSON·UNC 무시, 중복 제거)과 보호 경로 판정(대소문자 무시·경계)을 가짜 환경·레지스트리로 검증
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="ProtectionPolicyResolver"/>와 <see cref="ResolvedProtection"/>을 검증합니다. 실제 환경·레지스트리·파일을 읽지 않습니다.
/// </summary>
public sealed class ProtectionPolicyResolverTests
{
    private const string PROFILE = @"C:\Users\tester";
    private const string ACCOUNTS_KEY = @"Software\Microsoft\OneDrive\Accounts";
    private const string DROPBOX_INFO = @"C:\Users\tester\AppData\Local\Dropbox\info.json";

    private static readonly ProtectionPolicy POLICY = new(
        ProtectionPolicyParser.SUPPORTED_SCHEMA_VERSION,
        [
            new KnownFolderRootSpec(ProtectedKnownFolder.Documents),
            new KnownFolderRootSpec(ProtectedKnownFolder.Desktop),
            new EnvironmentPathRootSpec("%ProgramFiles%"),
            new EnvironmentPathRootSpec("%ProgramFiles(x86)%"),
            new EnvironmentPathRootSpec(@"%SystemRoot%\System32"),
            new CloudSyncEnvironmentRootSpec("OneDrive", "OneDrive"),
            new CloudSyncRegistryRootSpec("OneDrive", ACCOUNTS_KEY, "UserFolder"),
            new CloudSyncJsonFileRootSpec("Dropbox", @"%LocalAppData%\Dropbox\info.json", "path"),
        ]);

    /// <summary>
    /// 기본 가짜 환경(문서는 D:로 리디렉션, x86 Program Files 없음)을 만든다.
    /// </summary>
    private static FakePathEnvironment Environment()
    {
        return new FakePathEnvironment { Profile = PROFILE }
            .WithKnownFolder(ProtectedKnownFolder.Documents, @"D:\Redirected\Docs\")
            .WithKnownFolder(ProtectedKnownFolder.Desktop, PROFILE + @"\Desktop")
            .WithVariable("ProgramFiles", @"C:\Program Files")
            .WithVariable("SystemRoot", @"C:\Windows")
            .WithVariable("LocalAppData", PROFILE + @"\AppData\Local")
            .WithVariable("OneDrive", PROFILE + @"\OneDrive");
    }

    /// <summary>
    /// OneDrive 계정 두 개(하나는 값 없음)를 가진 가짜 레지스트리를 만든다.
    /// </summary>
    private static FakeRegistryReader Registry()
    {
        return new FakeRegistryReader()
            .WithSubKeys(RegistryRoot.CurrentUser, RegistryView.Registry64, ACCOUNTS_KEY, new RegistrySubKeyReading(RegistryReadStatus.Found, ["Personal", "Business1", "Empty"], null))
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, ACCOUNTS_KEY + @"\Personal", new RegistryValueEntry("UserFolder", "String", PROFILE + @"\OneDrive", null))
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, ACCOUNTS_KEY + @"\Business1", new RegistryValueEntry("userfolder", "String", PROFILE + @"\OneDrive - Contoso", null));
    }

    /// <summary>Known Folder는 리디렉션된 위치로, 환경 경로는 펼친 값으로, 동기화 루트는 환경 변수·레지스트리·info.json에서 해석하고 같은 경로는 한 번만 둔다.</summary>
    [Fact]
    public void 정책을_실제_경로로_해석한다()
    {
        var environment = Environment().WithFile(DROPBOX_INFO, """{ "personal": { "path": "C:\\Users\\tester\\Dropbox", "host": 1 }, "business": { "path": "E:\\Work Dropbox" } }""");

        var protection = new ProtectionPolicyResolver(environment, Registry()).Resolve(POLICY);

        Assert.Equal(
            [
                new ProtectedRoot(@"D:\Redirected\Docs", ProtectedRootOrigin.KnownFolder, "Documents"),
                new ProtectedRoot(PROFILE + @"\Desktop", ProtectedRootOrigin.KnownFolder, "Desktop"),
                new ProtectedRoot(@"C:\Program Files", ProtectedRootOrigin.SystemPath, "%ProgramFiles%"),
                new ProtectedRoot(@"C:\Windows\System32", ProtectedRootOrigin.SystemPath, @"%SystemRoot%\System32"),
                new ProtectedRoot(PROFILE + @"\OneDrive", ProtectedRootOrigin.CloudSync, "OneDrive"),
                new ProtectedRoot(PROFILE + @"\OneDrive - Contoso", ProtectedRootOrigin.CloudSync, "OneDrive"),
                new ProtectedRoot(PROFILE + @"\Dropbox", ProtectedRootOrigin.CloudSync, "Dropbox"),
                new ProtectedRoot(@"E:\Work Dropbox", ProtectedRootOrigin.CloudSync, "Dropbox"),
            ],
            protection.Roots);

        // ProgramFiles(x86) 변수가 없는 것 하나만 해석 실패로 센다(레지스트리의 값 없는 계정은 해당 없음).
        Assert.Equal(1, protection.UnresolvedCount);
        Assert.Equal([DROPBOX_INFO], environment.FileReads);
    }

    /// <summary>동기화 앱이 없으면(레지스트리 키·info.json 없음) 해석 실패로 세고 예외 없이 계속한다.</summary>
    [Fact]
    public void 동기화_앱이_없으면_건너뛴다()
    {
        var environment = new FakePathEnvironment { Profile = PROFILE }.WithVariable("LocalAppData", PROFILE + @"\AppData\Local");

        var protection = new ProtectionPolicyResolver(environment, new FakeRegistryReader()).Resolve(POLICY);

        Assert.Empty(protection.Roots);
        Assert.Equal(POLICY.Roots.Count, protection.UnresolvedCount);
    }

    /// <summary>손상된 info.json·UNC 동기화 경로·상대 경로는 보호 루트로 쓰지 않는다.</summary>
    [Theory]
    [InlineData("{ \"personal\": ")]
    [InlineData("""{ "personal": { "path": "\\\\server\\share\\Dropbox" } }""")]
    [InlineData("""{ "personal": { "path": "Dropbox" } }""")]
    [InlineData("""{ "personal": { "path": 3 } }""")]
    [InlineData("[]")]
    public void 쓸_수_없는_동기화_경로는_무시한다(string json)
    {
        var environment = new FakePathEnvironment { Profile = PROFILE }
            .WithVariable("LocalAppData", PROFILE + @"\AppData\Local")
            .WithFile(DROPBOX_INFO, json);

        var protection = new ProtectionPolicyResolver(environment, new FakeRegistryReader()).Resolve(POLICY);

        Assert.DoesNotContain(protection.Roots, root => root.Label == "Dropbox");
    }

    /// <summary>보호 판정은 같은 경로와 하위만 참이며 대소문자·끝 구분자를 무시하고 이름이 겹치는 형제는 거짓이다.</summary>
    [Fact]
    public void 보호_경로_판정은_디렉터리_경계를_지킨다()
    {
        var protection = new ResolvedProtection([new ProtectedRoot(PROFILE + @"\Documents", ProtectedRootOrigin.KnownFolder, "Documents")]);

        Assert.True(protection.IsProtected(PROFILE + @"\Documents"));
        Assert.True(protection.IsProtected(@"c:\users\TESTER\documents\"));
        Assert.True(protection.IsProtected(PROFILE + @"\Documents\a\b.txt"));
        Assert.False(protection.IsProtected(PROFILE + @"\Documents2"));
        Assert.False(protection.IsProtected(PROFILE));
    }
}
