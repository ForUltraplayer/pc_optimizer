/**
 * @file    : PowerProbeTests.cs
 * @author  : rudals252
 * @brief   : 전원 프로브가 fixture(정상·일부 실패·전체 실패·빈 섀시)를 측정값/상태/Issue로 바꾸는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Hardware;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="PowerProbe"/>의 수집 결과 변환을 fixture로 검증합니다. 실제 Win32 API·WMI를 호출하지 않습니다.
/// </summary>
public sealed class PowerProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 섀시 fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> EnclosureRow(params ushort[] codes)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["ChassisTypes"] = codes };
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakePowerPlatform power, FakeWmiClient wmi)
    {
        return new PowerProbe(power, wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>모든 부분을 읽으면 Success와 원시 값을 남긴다.</summary>
    [Fact]
    public async Task 모두_읽으면_성공이다()
    {
        var wmi = new FakeWmiClient().WithRows(PowerProbe.SYSTEM_ENCLOSURE_CLASS, EnclosureRow(3));

        var result = await RunAsync(new FakePowerPlatform(), wmi);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Empty(result.Issues);
        Assert.Contains(result.Measurements, m => m.Name == PowerProbeContract.ACTIVE_SCHEME_GUID);
        Assert.Contains(result.Measurements, m => m.Name == PowerProbeContract.ACTIVE_SCHEME_NAME);
        var chassis = Assert.Single(result.Measurements, m => m.Name == PowerProbeContract.CHASSIS_TYPES);
        Assert.Equal(["3"], Assert.IsType<TextListValue>(chassis.Value).Values);
        Assert.Contains(result.Measurements, m => m.Name == PowerProbeContract.BATTERY_FLAG && m.Value == new IntegerValue(128));
    }

    /// <summary>섀시 조회가 실패하면 나머지는 유지하고 Partial.</summary>
    [Fact]
    public async Task 섀시_실패는_부분_수집이다()
    {
        var wmi = new FakeWmiClient().WithFailure(PowerProbe.SYSTEM_ENCLOSURE_CLASS, WmiQueryStatus.AccessDenied);

        var result = await RunAsync(new FakePowerPlatform(), wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.AccessDenied, Assert.Single(result.Issues).Reason);
        Assert.DoesNotContain(result.Measurements, m => m.Name == PowerProbeContract.CHASSIS_TYPES);
        Assert.Contains(result.Measurements, m => m.Name == PowerProbeContract.ACTIVE_SCHEME_GUID);
    }

    /// <summary>섀시 코드가 비어 있으면 빈 목록으로 숨기지 않고 Issue를 남긴다.</summary>
    [Fact]
    public async Task 빈_섀시는_Issue를_남긴다()
    {
        var wmi = new FakeWmiClient().WithRows(PowerProbe.SYSTEM_ENCLOSURE_CLASS, EnclosureRow());

        var result = await RunAsync(new FakePowerPlatform(), wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == PowerProbeContract.CHASSIS_TYPES);
    }

    /// <summary>계획 이름만 못 읽으면 GUID는 남기고 Partial.</summary>
    [Fact]
    public async Task 이름_실패는_GUID를_유지한다()
    {
        var wmi = new FakeWmiClient().WithRows(PowerProbe.SYSTEM_ENCLOSURE_CLASS, EnclosureRow(9));

        var result = await RunAsync(new FakePowerPlatform { FriendlyNameError = FakePowerPlatform.SAMPLE_ERROR }, wmi);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Contains(result.Measurements, m => m.Name == PowerProbeContract.ACTIVE_SCHEME_GUID);
        Assert.DoesNotContain(result.Measurements, m => m.Name == PowerProbeContract.ACTIVE_SCHEME_NAME);
    }

    /// <summary>아무것도 읽지 못하면 Failed.</summary>
    [Fact]
    public async Task 전부_실패하면_실패다()
    {
        var power = new FakePowerPlatform { SchemeError = FakePowerPlatform.SAMPLE_ERROR, StatusError = FakePowerPlatform.SAMPLE_ERROR };

        var result = await RunAsync(power, new FakeWmiClient());

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(3, result.Issues.Count);
    }
}
