/**
 * @file    : OtherUserLocationGuardTests.cs
 * @author  : rudals252
 * @brief   : 다른 사용자 위치 판정기의 ProfileList 기반 프로필 루트·다른 SID 프로필 차단, 리디렉션된 현재 프로필(D:\me) 허용과 드라이브 루트 미사용, Public·Default 허용, 서비스 계정 제외, 휴지통 다른 SID 차단, 값 하나씩만 읽기를 가짜 레지스트리로 검증
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="OtherUserLocationGuard"/>를 검증합니다. 실제 레지스트리·프로필을 읽지 않습니다.
/// </summary>
public sealed class OtherUserLocationGuardTests
{
    private const string CURRENT_SID = "S-1-5-21-1-2-3-1001";
    private const string BOB_SID = "S-1-5-21-1-2-3-1002";
    private const string ALICE_SID = "S-1-5-21-1-2-3-1003";
    private const string KEY = OtherUserLocationGuard.PROFILE_LIST_KEY;

    /// <summary>
    /// 프로필 목록 가짜 레지스트리(현재 사용자 프로필은 D:\me로 리디렉션, bob은 C:\Users, alice는 E:\Profiles).
    /// </summary>
    private static FakeRegistryReader ProfileList(string profilesDirectory = @"%SystemDrive%\Users")
    {
        FakeRegistryReader Profile(FakeRegistryReader registry, string sid, string path)
            => registry.WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, KEY + @"\" + sid, OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE, path);

        var registry = new FakeRegistryReader()
            .WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, KEY, OtherUserLocationGuard.PROFILES_DIRECTORY_VALUE, profilesDirectory)
            .WithSubKeys(RegistryRoot.LocalMachine, RegistryView.Registry64, KEY, new RegistrySubKeyReading(RegistryReadStatus.Found, ["S-1-5-19", CURRENT_SID, BOB_SID, ALICE_SID], null));
        Profile(registry, "S-1-5-19", @"%SystemRoot%\ServiceProfiles\LocalService");
        Profile(registry, CURRENT_SID, @"D:\me");
        Profile(registry, BOB_SID, @"%SystemDrive%\Users\bob");
        Profile(registry, ALICE_SID, @"E:\Profiles\alice");
        return registry;
    }

    /// <summary>
    /// 가짜 환경(현재 프로필 D:\me).
    /// </summary>
    private static FakePathEnvironment Environment(string profile = @"D:\me")
    {
        return new FakePathEnvironment { Profile = profile }.WithVariable("SystemDrive", "C:").WithVariable("SystemRoot", @"C:\Windows");
    }

    /// <summary>
    /// 리디렉션된 현재 프로필(D:\me)과 그 드라이브의 다른 폴더는 막지 않고, ProfileList의 다른 사용자(프로필 루트 안·다른 드라이브 모두)와
    /// 프로필 루트 아래의 알 수 없는 폴더는 막는다. Public·Default와 서비스 계정 프로필은 막지 않는다.
    /// </summary>
    [Theory]
    [InlineData(@"D:\me\AppData\Local\Vendor", false)]
    [InlineData(@"D:\Other\Cache", false)]
    [InlineData(@"C:\Users\bob\AppData\Local\Vendor", true)]
    [InlineData(@"E:\Profiles\alice\AppData", true)]
    [InlineData(@"C:\Users\ghost\AppData", true)]
    [InlineData(@"C:\Users\Public\Documents\Vendor", false)]
    [InlineData(@"C:\Users\Default\AppData\Local\Vendor", false)]
    [InlineData(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\Vendor", false)]
    [InlineData(@"C:\Users", false)]
    public void 프로필_목록으로_다른_사용자_위치를_정한다(string path, bool expected)
    {
        var guard = OtherUserLocationGuard.Create(ProfileList(), Environment(), CURRENT_SID);

        Assert.Equal(expected, guard.IsOtherUserLocation(path));
    }

    /// <summary>휴지통의 다른 SID 폴더는 막고 현재 사용자 SID 폴더는 허용한다.</summary>
    [Fact]
    public void 휴지통의_다른_SID_폴더는_막는다()
    {
        var guard = OtherUserLocationGuard.Create(ProfileList(), Environment(), CURRENT_SID);

        Assert.True(guard.IsOtherUserLocation(@"C:\$Recycle.Bin\" + BOB_SID + @"\$R1.txt"));
        Assert.False(guard.IsOtherUserLocation(@"C:\$Recycle.Bin\" + CURRENT_SID + @"\$R1.txt"));
        Assert.False(guard.IsOtherUserLocation(@"C:\$Recycle.Bin"));
    }

    /// <summary>ProfilesDirectory가 드라이브 루트여도 드라이브 전체를 다른 사용자 위치로 보지 않고, ProfileList가 없으면 %SystemDrive%\Users만 쓴다.</summary>
    [Fact]
    public void 드라이브_루트는_프로필_루트로_쓰지_않는다()
    {
        var driveRoot = OtherUserLocationGuard.Create(ProfileList(@"F:\"), Environment(), CURRENT_SID);
        var noList = OtherUserLocationGuard.Create(new FakeRegistryReader(), Environment(@"C:\Users\me"), CURRENT_SID);

        Assert.False(driveRoot.IsOtherUserLocation(@"F:\Games\Cache"));
        Assert.True(noList.IsOtherUserLocation(@"C:\Users\bob\x"));
        Assert.False(noList.IsOtherUserLocation(@"C:\Users\me\x"));
        Assert.False(noList.IsOtherUserLocation(@"C:\Users\Public\x"));
    }

    /// <summary>레지스트리는 ProfilesDirectory·ProfileImagePath 값 하나씩만 요청하고 키 전체 값을 읽지 않는다.</summary>
    [Fact]
    public void 필요한_값만_읽는다()
    {
        var registry = ProfileList();

        OtherUserLocationGuard.Create(registry, Environment(), CURRENT_SID);

        Assert.Empty(registry.KeyReads);
        Assert.All(registry.StringReads, read => Assert.True(
            read.EndsWith("|" + OtherUserLocationGuard.PROFILES_DIRECTORY_VALUE, StringComparison.Ordinal)
            || read.EndsWith("|" + OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE, StringComparison.Ordinal)));
    }
}
