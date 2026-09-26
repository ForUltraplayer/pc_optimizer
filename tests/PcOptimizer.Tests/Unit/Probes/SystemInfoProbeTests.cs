/**
 * @file    : SystemInfoProbeTests.cs
 * @author  : rudals252
 * @brief   : 시스템 정보 프로브가 fixture(정상·일부 실패·값 누락·전부 실패)를 측정값/상태/Issue로 바꾸는지 검증
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
/// <see cref="SystemInfoProbe"/>의 수집 결과 변환을 비식별 fixture로 검증합니다.
/// </summary>
public sealed class SystemInfoProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 행 하나를 만든다.
    /// </summary>
    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            row[key] = value;
        }

        return row;
    }

    /// <summary>
    /// 정상 fixture를 만든다.
    /// </summary>
    private static FakeWmiClient Complete()
    {
        return new FakeWmiClient()
            .WithRows(SystemInfoProbe.COMPUTER_SYSTEM_CLASS, Row(("Manufacturer", "테스트 제조사"), ("Model", "테스트 모델"), ("SystemFamily", "테스트 제품군")))
            .WithRows(PowerProbe.SYSTEM_ENCLOSURE_CLASS, Row(("ChassisTypes", new ushort[] { 3 })))
            .WithRows(SystemInfoProbe.BIOS_CLASS, Row(("SMBIOSBIOSVersion", "T1")));
    }

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeWmiClient wmi)
    {
        return new SystemInfoProbe(wmi, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>제조사·모델·제품군·섀시·BIOS 버전을 모두 읽으면 Success다.</summary>
    [Fact]
    public async Task 모두_읽으면_성공이다()
    {
        var result = await RunAsync(Complete());

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new TextValue("테스트 제조사"), Assert.Single(result.Measurements, m => m.Name == SystemInfoProbeContract.MANUFACTURER).Value);
        Assert.Equal(["3"], Assert.IsType<TextListValue>(Assert.Single(result.Measurements, m => m.Name == SystemInfoProbeContract.CHASSIS_TYPES).Value).Values);
        Assert.Equal(new TextValue("T1"), Assert.Single(result.Measurements, m => m.Name == SystemInfoProbeContract.BIOS_VERSION).Value);
    }

    /// <summary>섀시 조회가 접근 거부되면 나머지는 유지하고 Partial이다.</summary>
    [Fact]
    public async Task 섀시_실패는_부분_수집이다()
    {
        var result = await RunAsync(Complete().WithFailure(PowerProbe.SYSTEM_ENCLOSURE_CLASS, WmiQueryStatus.AccessDenied));

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.AccessDenied, Assert.Single(result.Issues).Reason);
        Assert.Contains(result.Measurements, m => m.Name == SystemInfoProbeContract.MODEL);
    }

    /// <summary>모델 값이 비어 있으면 Partial이다.</summary>
    [Fact]
    public async Task 값_누락은_부분_수집이다()
    {
        var result = await RunAsync(Complete().WithRows(SystemInfoProbe.COMPUTER_SYSTEM_CLASS, Row(("Manufacturer", "제조사"), ("Model", "  "))));

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == SystemInfoProbeContract.MODEL);
    }

    /// <summary>모든 조회가 실패하면 Failed다(빈 성공 아님).</summary>
    [Fact]
    public async Task 전부_실패하면_실패다()
    {
        var result = await RunAsync(new FakeWmiClient());

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(3, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal(CannotVerifyReason.Unsupported, issue.Reason));
    }
}
