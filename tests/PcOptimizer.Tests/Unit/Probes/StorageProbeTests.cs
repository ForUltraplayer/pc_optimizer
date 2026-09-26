/**
 * @file    : StorageProbeTests.cs
 * @author  : rudals252
 * @brief   : 볼륨·물리 디스크 프로브가 저장소 네임스페이스 fixture(정상·문자 없는 볼륨·크기 누락·상태 누락·클래스 없음·빈 결과·접근 거부)와 Windows.old 존재 확인을 측정값/상태/Issue로 바꾸는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="VolumeProbe"/>·<see cref="PhysicalDiskProbe"/>를 비식별 fixture로 검증합니다. 실제 파일 시스템·WMI를 쓰지 않습니다.
/// </summary>
public sealed class StorageProbeTests
{
    private const string SYSTEM_ROOT = @"T:\";
    private const ulong GIB = 1024UL * 1024 * 1024;
    private const string DISK_GUID = "{12345678-aaaa-bbbb-cccc-1234567890ab}";

    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 볼륨 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> Volume(char letter, uint driveType, ulong? size, ulong? remaining)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DriveLetter"] = letter,
            ["FileSystemLabel"] = "테스트",
            ["FileSystem"] = "NTFS",
            ["DriveType"] = driveType,
            ["Size"] = size,
            ["SizeRemaining"] = remaining,
            ["HealthStatus"] = (ushort)0,
        };
    }

    /// <summary>
    /// 물리 디스크 fixture 행을 만든다(ObjectId에는 PC 이름 자리표시자가 들어 있다).
    /// </summary>
    private static Dictionary<string, object?> Disk(string deviceId, ushort? health)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DeviceId"] = deviceId,
            ["ObjectId"] = @"{1}\\TEST-PC\root/Microsoft/Windows/Storage/Providers_v2\SPACES_PhysicalDisk.ObjectId=""{aaaaaaaa-0000-0000-0000-000000000000}:PD:" + DISK_GUID.ToUpperInvariant() + @"""",
            ["FriendlyName"] = "테스트 SSD",
            ["MediaType"] = (ushort)4,
            ["BusType"] = (ushort)17,
            ["HealthStatus"] = health,
            ["Size"] = 1000 * GIB,
            ["SpindleSpeed"] = 0u,
        };
    }

    /// <summary>
    /// 볼륨 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunVolumeAsync(FakeWmiClient wmi, string? systemRoot = SYSTEM_ROOT, bool windowsOld = false, List<string>? checkedPaths = null)
    {
        bool Exists(string path)
        {
            checkedPaths?.Add(path);
            return windowsOld;
        }

        return new VolumeProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }, () => systemRoot, Exists).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>
    /// 물리 디스크 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunDiskAsync(FakeWmiClient wmi)
    {
        return new PhysicalDiskProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>저장소 네임스페이스의 볼륨 값과 Windows.old 존재 여부(폴더 경로 한 번만 확인)를 기록하고 Success다.</summary>
    [Fact]
    public async Task 볼륨과_WindowsOld를_기록한다()
    {
        var wmi = new FakeWmiClient().WithRows(VolumeProbe.VOLUME_CLASS, Volume('C', 3, 100 * GIB, 40 * GIB), Volume('\0', 3, GIB, GIB / 2));
        var checkedPaths = new List<string>();

        var result = await RunVolumeAsync(wmi, windowsOld: true, checkedPaths: checkedPaths);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal((WmiNamespaces.STORAGE, VolumeProbe.VOLUME_CLASS), Assert.Single(wmi.Queries));
        Assert.Equal(new TextValue("C"), Assert.Single(result.Measurements, m => m.Name == VolumeProbeContract.VolumeMeasurementName(0, VolumeProbeContract.FIELD_DRIVE_LETTER)).Value);
        Assert.DoesNotContain(result.Measurements, m => m.Name == VolumeProbeContract.VolumeMeasurementName(1, VolumeProbeContract.FIELD_DRIVE_LETTER));
        Assert.Equal(new IntegerValue((long)(40 * GIB)), Assert.Single(result.Measurements, m => m.Name == VolumeProbeContract.VolumeMeasurementName(0, VolumeProbeContract.FIELD_SIZE_REMAINING)).Value);
        Assert.Equal(new BooleanValue(true), Assert.Single(result.Measurements, m => m.Name == VolumeProbeContract.WINDOWS_OLD_EXISTS).Value);
        Assert.Equal(new TextValue("T:"), Assert.Single(result.Measurements, m => m.Name == VolumeProbeContract.SYSTEM_DRIVE).Value);
        Assert.Equal([@"T:\Windows.old"], checkedPaths);
    }

    /// <summary>고정 볼륨의 크기 누락과 시스템 드라이브 불명은 Partial이다. 이동식 볼륨의 크기 누락은 부분 수집이 아니다.</summary>
    [Theory]
    [InlineData(3u, SYSTEM_ROOT, ProbeStatus.Partial)]
    [InlineData(2u, SYSTEM_ROOT, ProbeStatus.Success)]
    [InlineData(2u, null, ProbeStatus.Partial)]
    public async Task 크기_누락과_시스템_드라이브_불명(uint driveType, string? systemRoot, ProbeStatus expected)
    {
        var wmi = new FakeWmiClient().WithRows(VolumeProbe.VOLUME_CLASS, Volume('E', driveType, null, null));

        var result = await RunVolumeAsync(wmi, systemRoot);

        Assert.Equal(expected, result.Status);
    }

    /// <summary>볼륨 조회의 클래스 없음·접근 거부·빈 결과는 Failed다.</summary>
    [Theory]
    [InlineData(WmiQueryStatus.ClassUnavailable, CannotVerifyReason.Unsupported)]
    [InlineData(WmiQueryStatus.AccessDenied, CannotVerifyReason.AccessDenied)]
    [InlineData(WmiQueryStatus.Success, CannotVerifyReason.Unsupported)]
    public async Task 볼륨_조회_실패는_실패다(WmiQueryStatus status, CannotVerifyReason reason)
    {
        var wmi = status == WmiQueryStatus.Success
            ? new FakeWmiClient().WithRows(VolumeProbe.VOLUME_CLASS)
            : new FakeWmiClient().WithFailure(VolumeProbe.VOLUME_CLASS, status);

        var result = await RunVolumeAsync(wmi);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(reason, Assert.Single(result.Issues).Reason);
    }

    /// <summary>물리 디스크는 제공자 GUID만 뽑아 기록하고 ObjectId 전체(PC 이름 포함)는 남기지 않는다.</summary>
    [Fact]
    public async Task 디스크_GUID만_기록한다()
    {
        var wmi = new FakeWmiClient().WithRows(PhysicalDiskProbe.PHYSICAL_DISK_CLASS, Disk("0", 0));

        var result = await RunDiskAsync(wmi);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal((WmiNamespaces.STORAGE, PhysicalDiskProbe.PHYSICAL_DISK_CLASS), Assert.Single(wmi.Queries));
        Assert.Equal(
            new TextValue(DISK_GUID),
            Assert.Single(result.Measurements, m => m.Name == PhysicalDiskProbeContract.DiskMeasurementName(0, PhysicalDiskProbeContract.FIELD_PROVIDER_DISK_ID)).Value);
        Assert.DoesNotContain(result.Measurements, m => m.Value is TextValue text && text.Value.Contains("TEST-PC", StringComparison.Ordinal));
        Assert.Equal(
            new IntegerValue(0),
            Assert.Single(result.Measurements, m => m.Name == PhysicalDiskProbeContract.DiskMeasurementName(0, PhysicalDiskProbeContract.FIELD_HEALTH_STATUS)).Value);
    }

    /// <summary>상태 값이 없는 디스크가 있으면 Partial이며 0(Healthy)으로 채우지 않는다.</summary>
    [Fact]
    public async Task 상태_누락은_부분_수집이다()
    {
        var result = await RunDiskAsync(new FakeWmiClient().WithRows(PhysicalDiskProbe.PHYSICAL_DISK_CLASS, Disk("0", null), Disk("1", 0)));

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == PhysicalDiskProbeContract.DiskMeasurementName(0, PhysicalDiskProbeContract.FIELD_HEALTH_STATUS));
    }

    /// <summary>디스크 조회의 클래스 없음·접근 거부·빈 결과는 Failed다.</summary>
    [Theory]
    [InlineData(WmiQueryStatus.ClassUnavailable, CannotVerifyReason.Unsupported)]
    [InlineData(WmiQueryStatus.AccessDenied, CannotVerifyReason.AccessDenied)]
    [InlineData(WmiQueryStatus.Success, CannotVerifyReason.Unsupported)]
    public async Task 디스크_조회_실패는_실패다(WmiQueryStatus status, CannotVerifyReason reason)
    {
        var wmi = status == WmiQueryStatus.Success
            ? new FakeWmiClient().WithRows(PhysicalDiskProbe.PHYSICAL_DISK_CLASS)
            : new FakeWmiClient().WithFailure(PhysicalDiskProbe.PHYSICAL_DISK_CLASS, status);

        var result = await RunDiskAsync(wmi);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(reason, Assert.Single(result.Issues).Reason);
    }
}
