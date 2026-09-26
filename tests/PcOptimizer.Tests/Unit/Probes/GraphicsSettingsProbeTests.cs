/**
 * @file    : GraphicsSettingsProbeTests.cs
 * @author  : rudals252
 * @brief   : 그래픽 설정 프로브가 레지스트리 fixture(값 있음·없음·DWORD 아님·접근 거부·오류)를 존재/형식/값 측정값과 상태로 구분해 바꾸는지 검증
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
/// <see cref="GraphicsSettingsProbe"/>를 가짜 레지스트리로 검증합니다. 실제 레지스트리를 읽지 않습니다.
/// </summary>
public sealed class GraphicsSettingsProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeRegistryReader registry)
    {
        return new GraphicsSettingsProbe(registry, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>
    /// 측정값을 찾는다(없으면 null).
    /// </summary>
    private static MeasurementValue? Value(ProbeResult result, string prefix, string field)
    {
        return result.Measurements.SingleOrDefault(m => m.Name == GraphicsSettingsProbeContract.MeasurementName(prefix, field))?.Value;
    }

    /// <summary>DWORD 값은 존재·형식·값을 각각 기록하고 Success다.</summary>
    [Fact]
    public async Task 값이_있으면_존재_형식_값을_기록한다()
    {
        var result = await RunAsync(new FakeRegistryReader()
            .WithDword(GraphicsSettingsProbe.HAGS_VALUE_NAME, 2)
            .WithDword(GraphicsSettingsProbe.GAME_MODE_VALUE_NAME, 1));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new BooleanValue(true), Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
        Assert.Equal(new TextValue(GraphicsSettingsProbeContract.KIND_DWORD), Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_KIND));
        Assert.Equal(new IntegerValue(2), Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
        Assert.Equal(new IntegerValue(1), Value(result, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
    }

    /// <summary>값이 없으면 존재 false만 기록하고(값 측정 없음, 0으로 채우지 않음) Success다.</summary>
    [Fact]
    public async Task 값이_없으면_존재_false만_기록한다()
    {
        var result = await RunAsync(new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.KeyMissing, null, null, null)));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new BooleanValue(false), Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
        Assert.Null(Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
        Assert.Equal(new BooleanValue(false), Value(result, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
    }

    /// <summary>DWORD가 아닌 값은 형식만 기록하고 정수 값은 만들지 않는다.</summary>
    [Fact]
    public async Task DWORD가_아니면_형식만_기록한다()
    {
        var result = await RunAsync(new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.Found, "String", null, null)));

        Assert.Equal(new TextValue("String"), Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_KIND));
        Assert.Null(Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
    }

    /// <summary>한 값의 접근 거부는 존재 여부를 기록하지 않고 Partial(AccessDenied)이다.</summary>
    [Fact]
    public async Task 접근_거부는_부분_수집이다()
    {
        var result = await RunAsync(new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.AccessDenied, null, null, "SecurityException"))
            .WithDword(GraphicsSettingsProbe.GAME_MODE_VALUE_NAME, 0));

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.AccessDenied, Assert.Single(result.Issues).Reason);
        Assert.Null(Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
    }

    /// <summary>두 값 모두 읽지 못하면 Failed다.</summary>
    [Fact]
    public async Task 모두_읽지_못하면_실패다()
    {
        var result = await RunAsync(new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.Error, null, null, "IOException"))
            .With(GraphicsSettingsProbe.GAME_MODE_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.AccessDenied, null, null, "UnauthorizedAccessException")));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(2, result.Issues.Count);
    }
}
