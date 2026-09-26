/**
 * @file    : DisplayProbeTests.cs
 * @author  : rudals252
 * @brief   : 디스플레이 프로브가 fixture 경로·모드(해상도 변경 필요·인터레이스 제외, 복제, 원격, 이름/모드 실패, 조회 실패·빈 경로)를 측정값/상태/Issue로 바꾸고 핫플러그 뒤에도 규칙 ID가 유지되는지 검증
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
/// <see cref="DisplayProbe"/>의 수집 결과 변환을 가짜 디스플레이 API로 검증합니다. 실제 Win32 API를 호출하지 않습니다.
/// </summary>
public sealed class DisplayProbeTests
{
    private const string PATH_A = @"\\?\DISPLAY#TST0001#5&aaaa&0&UID1#{guid}";
    private const string PATH_B = @"\\?\DISPLAY#TST0002#5&aaaa&0&UID2#{guid}";
    private const string PATH_C = @"\\?\DISPLAY#TST0003#5&aaaa&0&UID3#{guid}";
    private const string GDI_1 = @"\\.\DISPLAY1";
    private const string GDI_2 = @"\\.\DISPLAY2";
    private const string GDI_3 = @"\\.\DISPLAY3";

    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);
    private static readonly DisplayMode CURRENT_120 = new(2560, 1600, 0, 32, false, 120);

    /// <summary>
    /// 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeDisplayPlatform platform)
    {
        return new DisplayProbe(platform, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>
    /// 측정값 하나를 찾는다.
    /// </summary>
    private static MeasurementValue Value(ProbeResult result, int index, string field)
    {
        return Assert.Single(result.Measurements, m => m.Name == DisplayProbeContract.TargetMeasurementName(index, field)).Value;
    }

    /// <summary>
    /// 기본 한 대 구성을 만든다(현재 120Hz, 같은 해상도 130Hz 모드와 제외돼야 할 모드 포함).
    /// </summary>
    private static FakeDisplayPlatform SingleDisplay()
    {
        var platform = new FakeDisplayPlatform { Topology = new DisplayTopologyReading(0, [FakeDisplayPlatform.Path(4352, PATH_A, GDI_1, signalHz: 119.998)]) };
        platform.Modes[GDI_1] = FakeDisplayPlatform.ModeList(
            CURRENT_120,
            new DisplayMode(2560, 1600, 0, 32, false, 60),
            new DisplayMode(2560, 1600, 0, 32, false, 120),
            new DisplayMode(2560, 1600, 0, 32, false, 130),
            new DisplayMode(1920, 1080, 0, 32, false, 240),
            new DisplayMode(2560, 1600, 0, 32, true, 144),
            new DisplayMode(2560, 1600, 0, 16, false, 165));
        platform.AdapterNames[GDI_1] = "테스트 GPU";
        return platform;
    }

    /// <summary>경로·이름·현재 모드·같은 조건 주사율(해상도 변경 필요·인터레이스·다른 색 깊이 제외)을 기록하고 Success다.</summary>
    [Fact]
    public async Task 대상별_현재_모드와_같은_조건_주사율을_기록한다()
    {
        var result = await RunAsync(SingleDisplay());

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(new IntegerValue(1), Assert.Single(result.Measurements, m => m.Name == DisplayProbeContract.TARGET_COUNT).Value);
        Assert.Equal(new BooleanValue(false), Assert.Single(result.Measurements, m => m.Name == DisplayProbeContract.REMOTE_SESSION).Value);
        Assert.Equal(new TextValue(PATH_A), Value(result, 0, DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH));
        Assert.Equal(new TextValue("테스트 GPU"), Value(result, 0, DisplayProbeContract.FIELD_ADAPTER_NAME));
        Assert.Equal(new IntegerValue(120), Value(result, 0, DisplayProbeContract.FIELD_REFRESH_HZ));
        Assert.Equal(new DecimalValue(119.998), Value(result, 0, DisplayProbeContract.FIELD_SIGNAL_REFRESH_HZ));
        Assert.Equal(["60", "120", "130"], Assert.IsType<TextListValue>(Value(result, 0, DisplayProbeContract.FIELD_SAME_MODE_REFRESH_RATES)).Values);
        Assert.Equal(new IntegerValue(130), Value(result, 0, DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ));
        Assert.Equal(new IntegerValue(6), Value(result, 0, DisplayProbeContract.FIELD_ENUMERATED_MODE_COUNT));
        Assert.Equal(new BooleanValue(false), Value(result, 0, DisplayProbeContract.FIELD_CLONED));
    }

    /// <summary>프로브 결과를 규칙에 넣으면 같은 해상도의 130Hz가 후보가 된다(프로브·규칙 연결).</summary>
    [Fact]
    public async Task 프로브_결과가_규칙_후보로_이어진다()
    {
        var result = await RunAsync(SingleDisplay());

        var finding = Assert.Single(new DisplayRefreshRule().Evaluate(new ScanSnapshot(Guid.NewGuid(), [result])));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(DisplayRefreshRule.FINDING_ID_PREFIX + PATH_A, finding.Id);
    }

    /// <summary>두 활성 경로가 같은 원본(어댑터+원본 ID)을 쓰면 둘 다 복제로 기록한다.</summary>
    [Fact]
    public async Task 같은_원본을_쓰는_경로는_복제다()
    {
        var platform = SingleDisplay();
        platform.Topology = new DisplayTopologyReading(0, [
            FakeDisplayPlatform.Path(1, PATH_A, GDI_1, sourceId: 0),
            FakeDisplayPlatform.Path(2, PATH_B, GDI_1, sourceId: 0),
            FakeDisplayPlatform.Path(3, PATH_C, GDI_2, sourceId: 1),
        ]);
        platform.Modes[GDI_2] = FakeDisplayPlatform.ModeList(CURRENT_120, CURRENT_120);

        var result = await RunAsync(platform);

        Assert.Equal(new BooleanValue(true), Value(result, 0, DisplayProbeContract.FIELD_CLONED));
        Assert.Equal(new BooleanValue(true), Value(result, 1, DisplayProbeContract.FIELD_CLONED));
        Assert.Equal(new BooleanValue(false), Value(result, 2, DisplayProbeContract.FIELD_CLONED));
    }

    /// <summary>원격 세션 여부를 기록한다.</summary>
    [Fact]
    public async Task 원격_세션을_기록한다()
    {
        var platform = SingleDisplay();
        platform.Remote = true;

        var result = await RunAsync(platform);

        Assert.Equal(new BooleanValue(true), Assert.Single(result.Measurements, m => m.Name == DisplayProbeContract.REMOTE_SESSION).Value);
    }

    /// <summary>경로 조회 실패·구조체 배치 불일치는 측정값 없이 Failed(ProbeError)다.</summary>
    [Theory]
    [InlineData(87)]
    [InlineData(IDisplayPlatform.LAYOUT_MISMATCH_ERROR)]
    public async Task 경로_조회_실패는_실패다(int error)
    {
        var result = await RunAsync(new FakeDisplayPlatform { Topology = new DisplayTopologyReading(error, []) });

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Equal(CannotVerifyReason.ProbeError, Assert.Single(result.Issues).Reason);
    }

    /// <summary>활성 경로가 하나도 없으면 빈 성공이 아니라 Failed다.</summary>
    [Fact]
    public async Task 빈_경로는_실패다()
    {
        var result = await RunAsync(new FakeDisplayPlatform { Topology = new DisplayTopologyReading(0, []) });

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Empty(result.Measurements);
        Assert.Single(result.Issues);
    }

    /// <summary>원본 이름을 읽지 못한 대상은 모드를 열거하지 않고(빈 장치 이름 전달 없음) Partial이며, 다른 대상은 유지한다.</summary>
    [Fact]
    public async Task 원본_이름_실패는_부분_수집이다()
    {
        var platform = SingleDisplay();
        platform.Topology = new DisplayTopologyReading(0, [
            FakeDisplayPlatform.Path(1, PATH_A, null, sourceNameError: 31),
            FakeDisplayPlatform.Path(2, PATH_B, GDI_1, sourceId: 1),
        ]);

        var result = await RunAsync(platform);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.PartialData, Assert.Single(result.Issues).Reason);
        Assert.Equal([GDI_1], platform.EnumeratedDevices);
        Assert.DoesNotContain(result.Measurements, m => m.Name == DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_REFRESH_HZ));
        Assert.Contains(result.Measurements, m => m.Name == DisplayProbeContract.TargetMeasurementName(1, DisplayProbeContract.FIELD_REFRESH_HZ));
    }

    /// <summary>모니터 이름 조회 실패는 Issue를 남기고 나머지 값은 유지한다.</summary>
    [Fact]
    public async Task 모니터_이름_실패는_부분_수집이다()
    {
        var platform = SingleDisplay();
        platform.Topology = new DisplayTopologyReading(0, [FakeDisplayPlatform.Path(1, null, GDI_1, targetNameError: 1168)]);

        var result = await RunAsync(platform);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH));
        Assert.Contains(result.Measurements, m => m.Name == DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ));
    }

    /// <summary>현재 모드를 읽지 못하거나 모드 목록이 비면 Partial이며 같은 조건 목록을 만들지 않는다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 모드_실패는_부분_수집이다(bool missingCurrent)
    {
        var platform = SingleDisplay();
        platform.Modes[GDI_1] = missingCurrent
            ? FakeDisplayPlatform.ModeList(null, CURRENT_120)
            : FakeDisplayPlatform.ModeList(CURRENT_120);

        var result = await RunAsync(platform);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.DoesNotContain(result.Measurements, m => m.Name == DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_SAME_MODE_REFRESH_RATES));
    }

    /// <summary>모니터를 새로 연결해 열거 순서·GDI 번호가 바뀌어도 기존 모니터의 규칙 Finding ID는 같다(핫플러그).</summary>
    [Fact]
    public async Task 핫플러그_뒤에도_ID가_유지된다()
    {
        var before = SingleDisplay();
        var after = SingleDisplay();
        after.Topology = new DisplayTopologyReading(0, [
            FakeDisplayPlatform.Path(9, PATH_C, GDI_1, sourceId: 0),
            FakeDisplayPlatform.Path(4352, PATH_A, GDI_2, sourceId: 1, signalHz: 119.998),
        ]);
        after.Modes[GDI_2] = before.Modes[GDI_1];
        after.Modes[GDI_1] = FakeDisplayPlatform.ModeList(CURRENT_120, CURRENT_120);

        var rule = new DisplayRefreshRule();
        var beforeIds = rule.Evaluate(new ScanSnapshot(Guid.NewGuid(), [await RunAsync(before)])).Select(f => f.Id).ToList();
        var afterIds = rule.Evaluate(new ScanSnapshot(Guid.NewGuid(), [await RunAsync(after)])).Select(f => f.Id).ToList();

        Assert.Equal([DisplayRefreshRule.FINDING_ID_PREFIX + PATH_A], beforeIds);
        Assert.Contains(DisplayRefreshRule.FINDING_ID_PREFIX + PATH_A, afterIds);
        Assert.Contains(DisplayRefreshRule.FINDING_ID_PREFIX + PATH_C, afterIds);
    }
}
