/**
 * @file    : GraphicsSettingsProbeTests.cs
 * @author  : rudals252
 * @brief   : HAGS(시스템 범위)·게임 모드(사용자 범위) 프로브가 레지스트리 fixture(값 있음·없음·DWORD 아님·접근 거부·오류)를 존재/형식/값 측정값과 상태로 구분해 바꾸는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Hardware;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="GraphicsSettingsProbe"/>와 <see cref="GameModeSettingsProbe"/>를 가짜 레지스트리로 검증합니다. 실제 레지스트리를 읽지 않습니다.
/// </summary>
public sealed class GraphicsSettingsProbeTests
{
    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    /// <summary>
    /// HAGS 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunHagsAsync(FakeRegistryReader registry)
    {
        return new GraphicsSettingsProbe(registry, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>
    /// 게임 모드 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunGameModeAsync(FakeRegistryReader registry)
    {
        return new GameModeSettingsProbe(registry, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>
    /// 측정값을 찾는다(없으면 null).
    /// </summary>
    private static MeasurementValue? Value(ProbeResult result, string prefix, string field)
    {
        return result.Measurements.SingleOrDefault(m => m.Name == GraphicsSettingsProbeContract.MeasurementName(prefix, field))?.Value;
    }

    /// <summary>HAGS는 HKLM만 읽는 시스템 범위, 게임 모드는 HKCU를 읽는 사용자 범위이며 서로 다른 프로브 ID다.</summary>
    [Fact]
    public void HAGS는_시스템_범위_게임_모드는_사용자_범위다()
    {
        var hags = new GraphicsSettingsProbe(new FakeRegistryReader(), new FakeClock());
        var gameMode = new GameModeSettingsProbe(new FakeRegistryReader(), new FakeClock());

        Assert.Equal(ProbeScope.System, hags.Scope);
        Assert.Equal(ProbeScope.User, gameMode.Scope);
        Assert.Equal(GraphicsSettingsProbeContract.PROBE_ID, hags.Id);
        Assert.Equal(GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID, gameMode.Id);
    }

    /// <summary>DWORD 값은 존재·형식·값을 각각 기록하고 Success다. 각 프로브는 자기 값만 기록한다.</summary>
    [Fact]
    public async Task 값이_있으면_존재_형식_값을_기록한다()
    {
        var registry = new FakeRegistryReader()
            .WithDword(GraphicsSettingsProbe.HAGS_VALUE_NAME, 2)
            .WithDword(GameModeSettingsProbe.GAME_MODE_VALUE_NAME, 1);

        var hags = await RunHagsAsync(registry);
        var gameMode = await RunGameModeAsync(registry);

        Assert.Equal(ProbeStatus.Success, hags.Status);
        Assert.Equal(new BooleanValue(true), Value(hags, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
        Assert.Equal(new TextValue(GraphicsSettingsProbeContract.KIND_DWORD), Value(hags, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_KIND));
        Assert.Equal(new IntegerValue(2), Value(hags, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
        Assert.Null(Value(hags, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
        Assert.Equal(ProbeStatus.Success, gameMode.Status);
        Assert.Equal(new IntegerValue(1), Value(gameMode, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
        Assert.Null(Value(gameMode, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
    }

    /// <summary>값이 없으면 존재 false만 기록하고(값 측정 없음, 0으로 채우지 않음) Success다.</summary>
    [Fact]
    public async Task 값이_없으면_존재_false만_기록한다()
    {
        var registry = new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.KeyMissing, null, null, null));

        var hags = await RunHagsAsync(registry);
        var gameMode = await RunGameModeAsync(registry);

        Assert.Equal(ProbeStatus.Success, hags.Status);
        Assert.Equal(new BooleanValue(false), Value(hags, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
        Assert.Null(Value(hags, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
        Assert.Equal(ProbeStatus.Success, gameMode.Status);
        Assert.Equal(new BooleanValue(false), Value(gameMode, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, GraphicsSettingsProbeContract.FIELD_EXISTS));
    }

    /// <summary>DWORD가 아닌 값은 형식만 기록하고 정수 값은 만들지 않는다.</summary>
    [Fact]
    public async Task DWORD가_아니면_형식만_기록한다()
    {
        var result = await RunHagsAsync(new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.Found, "String", null, null)));

        Assert.Equal(new TextValue("String"), Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_KIND));
        Assert.Null(Value(result, GraphicsSettingsProbeContract.HAGS_PREFIX, GraphicsSettingsProbeContract.FIELD_VALUE));
    }

    /// <summary>한 프로브의 접근 거부는 존재 여부를 기록하지 않고 Failed(AccessDenied)이며 다른 프로브 결과에 영향을 주지 않는다.</summary>
    [Fact]
    public async Task 접근_거부는_해당_프로브만_실패다()
    {
        var registry = new FakeRegistryReader()
            .With(GraphicsSettingsProbe.HAGS_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.AccessDenied, null, null, "SecurityException"))
            .WithDword(GameModeSettingsProbe.GAME_MODE_VALUE_NAME, 0);

        var hags = await RunHagsAsync(registry);
        var gameMode = await RunGameModeAsync(registry);

        Assert.Equal(ProbeStatus.Failed, hags.Status);
        Assert.Equal(CannotVerifyReason.AccessDenied, Assert.Single(hags.Issues).Reason);
        Assert.Empty(hags.Measurements);
        Assert.Equal(ProbeStatus.Success, gameMode.Status);
    }

    /// <summary>읽기 오류는 측정값 없이 Failed(ProbeError)이며 요약에 예외 형식 이름만 남긴다.</summary>
    [Fact]
    public async Task 읽기_오류는_실패다()
    {
        var result = await RunGameModeAsync(new FakeRegistryReader()
            .With(GameModeSettingsProbe.GAME_MODE_VALUE_NAME, new RegistryValueReading(RegistryReadStatus.Error, null, null, "IOException")));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(CannotVerifyReason.ProbeError, issue.Reason);
        Assert.Contains("IOException", issue.Summary, StringComparison.Ordinal);
    }
}
