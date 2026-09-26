/**
 * @file    : ModelImmutabilityTests.cs
 * @author  : rudals252
 * @brief   : 모델 목록 속성(ProbeResult·TextListValue·Finding)이 생성 후 외부에서 바뀌지 않는지 검증하는 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.Models;

/// <summary>
/// 규칙이 받는 스냅샷이 불변이 되도록, 모델이 넘겨받은 목록을 복사해 읽기 전용 보기로 보관하는지 확인합니다.
/// </summary>
public class ModelImmutabilityTests
{
    /// <summary>
    /// ProbeResult에 넘긴 목록을 나중에 바꿔도 저장된 목록은 바뀌지 않고, 캐스팅으로도 바꿀 수 없다.
    /// </summary>
    [Fact]
    public void ProbeResult는_넘겨받은_목록을_복사해_읽기_전용으로_보관한다()
    {
        var measurements = new List<Measurement> { FakeProbe.CreateMeasurement("a") };
        var issues = new List<Issue> { new(CannotVerifyReason.AccessDenied, "요약") };
        var result = new ProbeResult(
            "probe", ProbeStatus.Partial, measurements, issues, FakeProbe.OBSERVED_AT, TimeSpan.Zero, EngineTestData.USER);

        measurements.Add(FakeProbe.CreateMeasurement("b"));
        issues.Clear();

        Assert.Single(result.Measurements);
        Assert.Single(result.Issues);
        Assert.Null(result.Measurements as List<Measurement>);
        Assert.Null(result.Measurements as Measurement[]);
        Assert.Throws<NotSupportedException>(() => ((IList<Issue>)result.Issues).Clear());
    }

    /// <summary>
    /// with 식으로 목록을 바꿔 넣어도 같은 방식으로 복사·보호된다.
    /// </summary>
    [Fact]
    public void ProbeResult의_with_식도_목록을_복사한다()
    {
        var original = EngineTestData.CreateResult("probe", ProbeStatus.Success);
        var measurements = new List<Measurement> { FakeProbe.CreateMeasurement("a") };

        var changed = original with { Measurements = measurements };
        measurements.Add(FakeProbe.CreateMeasurement("b"));

        Assert.Single(changed.Measurements);
        Assert.Null(changed.Measurements as List<Measurement>);
    }

    /// <summary>
    /// 문자열 목록 측정값도 넘겨받은 목록을 복사해 읽기 전용으로 보관한다.
    /// </summary>
    [Fact]
    public void TextListValue는_넘겨받은_목록을_복사해_읽기_전용으로_보관한다()
    {
        var values = new List<string> { "C:" };
        var value = new TextListValue(values);

        values.Add("D:");

        Assert.Equal(["C:"], value.Values);
        Assert.Null(value.Values as List<string>);
        Assert.Null(value.Values as string[]);
    }

    /// <summary>
    /// Finding의 측정값·동작 목록은 배열로 캐스팅해 바꿀 수 없다.
    /// </summary>
    [Fact]
    public void Finding의_목록은_캐스팅으로도_바꿀_수_없다()
    {
        var finding = new Finding(
            id: "id",
            category: FindingCategory.Display,
            title: "제목",
            measured: [FakeProbe.CreateMeasurement("a")],
            evidence: "근거",
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);

        Assert.Null(finding.Measured as Measurement[]);
        Assert.Null(finding.Actions as FindingAction[]);
        Assert.Throws<NotSupportedException>(() => ((IList<FindingAction>)finding.Actions)[0] = new KeepAction());
    }
}
