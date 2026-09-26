/**
 * @file    : DeviceIdTokenizerTests.cs
 * @author  : rudals252
 * @brief   : 장치 ID 토큰화기의 종류별 치환(모니터 경로·PnP 인스턴스/하드웨어 ID·인터페이스 경로·볼륨 경로·GUID), 같은 ID 같은 토큰, 다른 ID 다른 토큰, 첫 등장 순서, 비대상 문자열 보존 검증
 */

// 기본 패키지
using System.Text.RegularExpressions;

// 사용자 패키지
using PcOptimizer.App.Services;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="DeviceIdTokenizer"/>를 비식별 fixture 문자열로 검증합니다. 모든 ID는 가짜 값입니다.
/// </summary>
public sealed class DeviceIdTokenizerTests
{
    /// <summary>가짜 모니터 장치 경로 A.</summary>
    public const string MONITOR_A = @"\\?\DISPLAY#TST0001#5&1a2b3c4d&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    /// <summary>가짜 모니터 장치 경로 B.</summary>
    public const string MONITOR_B = @"\\?\DISPLAY#TST0002#5&1a2b3c4d&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    /// <summary>가짜 PCI 인스턴스 ID.</summary>
    public const string PCI_INSTANCE = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1\4&ABCDEF0&0&0009";

    /// <summary>가짜 PCI 하드웨어 ID(인스턴스 없음).</summary>
    public const string PCI_HARDWARE_ID = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1";

    /// <summary>가짜 ROOT 열거 인스턴스 ID.</summary>
    public const string ROOT_INSTANCE = @"ROOT\DISPLAY\0000";

    /// <summary>가짜 DISPLAY 열거 인스턴스 ID.</summary>
    public const string DISPLAY_INSTANCE = @"DISPLAY\TST0001\5&1A2B3C4D&0&UID4353";

    /// <summary>가짜 어댑터 인터페이스 경로.</summary>
    public const string ADAPTER_PATH = @"\\?\PCI#VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1#4&abcdef0&0&0009#{5b45201d-f2f2-4f3b-85bb-30ff1f953599}";

    /// <summary>가짜 볼륨 경로.</summary>
    public const string VOLUME_PATH = @"\\?\Volume{11111111-2222-3333-4444-555555555555}\";

    /// <summary>가짜 저장소 제공자 디스크 GUID.</summary>
    public const string DISK_GUID = "{12345678-aaaa-bbbb-cccc-1234567890ab}";

    /// <summary>원문 장치 ID 흔적(어느 것도 남으면 안 됨).</summary>
    private static readonly Regex RAW_ID_PATTERN = new(
        @"VEN_|DISPLAY#|\\\\\?\\|\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-|UID43|&0&0009",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>종류별로 알맞은 토큰으로 바꾼다.</summary>
    [Theory]
    [InlineData(MONITOR_A, "display-1")]
    [InlineData(PCI_INSTANCE, "pnp-1")]
    [InlineData(PCI_HARDWARE_ID, "pnp-1")]
    [InlineData(ROOT_INSTANCE, "pnp-1")]
    [InlineData(DISPLAY_INSTANCE, "pnp-1")]
    [InlineData(@"Root\Parsec\VDA", "pnp-1")]
    [InlineData(@"USB\VID_0000&PID_0000\5&ABCDEF&0&3", "pnp-1")]
    [InlineData(@"FOO\BAR_1\3&11583659&0&01", "pnp-1")]
    [InlineData(ADAPTER_PATH, "pnp-1")]
    [InlineData(VOLUME_PATH, "volume-1")]
    [InlineData(DISK_GUID, "guid-1")]
    public void 종류별로_토큰화한다(string raw, string expected)
    {
        Assert.Equal(expected, new DeviceIdTokenizer().Tokenize(raw));
    }

    /// <summary>같은 원문 ID는 문자열·호출이 달라도(대소문자 무시) 같은 토큰이다.</summary>
    [Fact]
    public void 같은_ID는_같은_토큰이다()
    {
        var tokenizer = new DeviceIdTokenizer();

        var first = tokenizer.Tokenize("display-refresh:" + MONITOR_A);
        var second = tokenizer.Tokenize("장치 경로 " + MONITOR_A.ToUpperInvariant() + " 확인");

        Assert.Equal("display-refresh:display-1", first);
        Assert.Equal("장치 경로 display-1 확인", second);
    }

    /// <summary>서로 다른 ID는 서로 다른 토큰이며 종류별 번호는 처음 본 순서다.</summary>
    [Fact]
    public void 다른_ID는_다른_토큰이다()
    {
        var tokenizer = new DeviceIdTokenizer();

        Assert.Equal("display-1", tokenizer.Tokenize(MONITOR_A));
        Assert.Equal("pnp-1", tokenizer.Tokenize(PCI_INSTANCE));
        Assert.Equal("display-2", tokenizer.Tokenize(MONITOR_B));
        Assert.Equal("pnp-2 pnp-1", tokenizer.Tokenize(ROOT_INSTANCE + " " + PCI_INSTANCE));
        Assert.Equal("guid-1", tokenizer.Tokenize(DISK_GUID));
    }

    /// <summary>토큰 번호는 토큰화기 인스턴스(내보내기 한 번)마다 새로 시작한다.</summary>
    [Fact]
    public void 인스턴스마다_번호가_새로_시작한다()
    {
        var first = new DeviceIdTokenizer();
        first.Tokenize(MONITOR_A);

        Assert.Equal("display-1", new DeviceIdTokenizer().Tokenize(MONITOR_B));
    }

    /// <summary>문장 속 여러 ID(대체 ID·하드웨어 ID 목록 포함)를 모두 바꾸고 원문 흔적을 남기지 않는다.</summary>
    [Fact]
    public void 문장_속_ID를_모두_바꾼다()
    {
        var tokenizer = new DeviceIdTokenizer();
        var text = "adapter:" + ADAPTER_PATH + "|target:4353 · 하드웨어 ID " + PCI_HARDWARE_ID + " · " + DISPLAY_INSTANCE
            + " · 볼륨 " + VOLUME_PATH + " · 디스크 " + DISK_GUID;

        var result = tokenizer.Tokenize(text);

        Assert.Equal("adapter:pnp-1|target:4353 · 하드웨어 ID pnp-2 · pnp-3 · 볼륨 volume-1 · 디스크 guid-1", result);
        Assert.DoesNotMatch(RAW_ID_PATTERN, result);
    }

    /// <summary>레지스트리·파일 경로, GDI 표시 이름, 중괄호 없는 GUID, 일반 문장은 바꾸지 않는다.</summary>
    [Theory]
    [InlineData(@"Registry HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode")]
    [InlineData(@"Registry HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled")]
    [InlineData(@"Directory.Exists(<SystemDrive>\Windows.old)")]
    [InlineData(@"C:\Windows\System32\fsutil.exe")]
    [InlineData(@"\\.\DISPLAY3")]
    [InlineData("27FM24001Q (DISPLAY1)의 현재 모드: 2560×1440 240Hz")]
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")]
    [InlineData("WMI MSFT_PhysicalDisk.ObjectId (PD GUID)")]
    [InlineData("display.target[0].monitorDevicePath")]
    public void 장치_ID가_아니면_그대로_둔다(string text)
    {
        Assert.Equal(text, new DeviceIdTokenizer().Tokenize(text));
    }
}
