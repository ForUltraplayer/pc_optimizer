/**
 * @file    : ScanSnapshotTests.cs
 * @author  : rudals252
 * @brief   : 규칙 입력용 불변 스냅샷(프로브 조회·측정값 조회·중복 거부) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.Models;

/// <summary>
/// <see cref="ScanSnapshot"/>의 조회 도우미와 불변성을 확인합니다.
/// </summary>
public class ScanSnapshotTests
{
    /// <summary>
    /// 프로브 결과와 측정값을 ID·이름으로 찾을 수 있고, 없는 항목은 없음으로 돌려준다.
    /// </summary>
    [Fact]
    public void 프로브와_측정값을_조회한다()
    {
        var snapshot = EngineTestData.CreateSnapshot(
            EngineTestData.CreateResult("a", ProbeStatus.Success, measurements: [FakeProbe.CreateMeasurement("value", 7)]));

        Assert.True(snapshot.TryGetProbe("a", out var result));
        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.False(snapshot.TryGetProbe("missing", out _));
        Assert.Equal(new IntegerValue(7), snapshot.GetMeasurement("a", "value")?.Value);
        Assert.Null(snapshot.GetMeasurement("a", "missing"));
        Assert.Null(snapshot.GetMeasurement("missing", "value"));
    }

    /// <summary>
    /// 원본 목록을 나중에 바꿔도 스냅샷은 바뀌지 않는다.
    /// </summary>
    [Fact]
    public void 원본_목록이_바뀌어도_스냅샷은_바뀌지_않는다()
    {
        var source = new List<ProbeResult> { EngineTestData.CreateResult("a", ProbeStatus.Success) };
        var snapshot = new ScanSnapshot(Guid.NewGuid(), source);

        source.Add(EngineTestData.CreateResult("b", ProbeStatus.Success));

        Assert.Single(snapshot.ProbeResults);
        Assert.False(snapshot.TryGetProbe("b", out _));
    }

    /// <summary>
    /// 같은 ProbeId가 두 번 들어오면 스냅샷을 만들 수 없다.
    /// </summary>
    [Fact]
    public void 중복_ProbeId는_거부한다()
    {
        Assert.Throws<ArgumentException>(() => EngineTestData.CreateSnapshot(
            EngineTestData.CreateResult("a", ProbeStatus.Success),
            EngineTestData.CreateResult("a", ProbeStatus.Failed)));
    }
}
