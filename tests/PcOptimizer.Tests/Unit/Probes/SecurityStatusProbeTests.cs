/**
 * @file    : SecurityStatusProbeTests.cs
 * @author  : rudals252
 * @brief   : 보안 상태 프로브가 DeviceGuard 네임스페이스 fixture(정상·일부 값 누락·클래스 없음·빈 결과·접근 거부)를 측정값/상태/Issue로 바꾸는지 검증
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
/// <see cref="SecurityStatusProbe"/>의 수집 결과 변환을 비식별 fixture로 검증합니다.
/// </summary>
public sealed class SecurityStatusProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// DeviceGuard fixture 행을 만든다.
    /// </summary>
    private static Dictionary<string, object?> Row(uint? vbs, uint[]? configured, uint[]? running, uint[]? available)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["VirtualizationBasedSecurityStatus"] = vbs,
            ["SecurityServicesConfigured"] = configured,
            ["SecurityServicesRunning"] = running,
            ["AvailableSecurityProperties"] = available,
        };
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeWmiClient wmi)
    {
        return new SecurityStatusProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>DeviceGuard 네임스페이스를 조회해 VBS 상태와 서비스 목록을 따로 기록하고 Success다.</summary>
    [Fact]
    public async Task 값을_따로_기록한다()
    {
        var wmi = new FakeWmiClient().WithRows(SecurityStatusProbe.DEVICE_GUARD_CLASS, Row(2, [2], [2], [1, 2, 3]));

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal((WmiNamespaces.DEVICE_GUARD, SecurityStatusProbe.DEVICE_GUARD_CLASS), Assert.Single(wmi.Queries));
        Assert.Equal(new IntegerValue(2), Assert.Single(result.Measurements, m => m.Name == SecurityStatusProbeContract.VBS_STATUS).Value);
        Assert.Equal(["2"], Assert.IsType<TextListValue>(Assert.Single(result.Measurements, m => m.Name == SecurityStatusProbeContract.SERVICES_RUNNING).Value).Values);
        Assert.Equal(4, result.Measurements.Count);
    }

    /// <summary>빈 배열은 빈 목록으로 기록한다(조회 성공, 항목 없음).</summary>
    [Fact]
    public async Task 빈_배열은_빈_목록이다()
    {
        var result = await RunAsync(new FakeWmiClient().WithRows(SecurityStatusProbe.DEVICE_GUARD_CLASS, Row(0, [], [], [])));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Empty(Assert.IsType<TextListValue>(Assert.Single(result.Measurements, m => m.Name == SecurityStatusProbeContract.SERVICES_CONFIGURED).Value).Values);
    }

    /// <summary>일부 값이 비어 있으면 Partial이며 빈 값을 0·빈 목록으로 채우지 않는다.</summary>
    [Fact]
    public async Task 일부_값_누락은_부분_수집이다()
    {
        var result = await RunAsync(new FakeWmiClient().WithRows(SecurityStatusProbe.DEVICE_GUARD_CLASS, Row(null, [2], null, [1])));

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == SecurityStatusProbeContract.VBS_STATUS);
        Assert.DoesNotContain(result.Measurements, m => m.Name == SecurityStatusProbeContract.SERVICES_RUNNING);
    }

    /// <summary>클래스·네임스페이스 없음(Unsupported)·접근 거부·빈 결과·모든 값 없음은 Failed다.</summary>
    [Theory]
    [InlineData("missing", CannotVerifyReason.Unsupported)]
    [InlineData("denied", CannotVerifyReason.AccessDenied)]
    [InlineData("empty", CannotVerifyReason.Unsupported)]
    [InlineData("allNull", CannotVerifyReason.Unsupported)]
    public async Task 조회_실패는_실패다(string condition, CannotVerifyReason reason)
    {
        var wmi = condition switch
        {
            "missing" => new FakeWmiClient(),
            "denied" => new FakeWmiClient().WithFailure(SecurityStatusProbe.DEVICE_GUARD_CLASS, WmiQueryStatus.AccessDenied),
            "empty" => new FakeWmiClient().WithRows(SecurityStatusProbe.DEVICE_GUARD_CLASS),
            _ => new FakeWmiClient().WithRows(SecurityStatusProbe.DEVICE_GUARD_CLASS, Row(null, null, null, null)),
        };

        var result = await RunAsync(wmi);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(reason, Assert.Single(result.Issues).Reason);
    }
}
