/**
 * @file    : RuleEvaluatorTests.cs
 * @author  : rudals252
 * @brief   : 규칙 평가기(규칙별 Finding 수집, 규칙 예외 격리) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.Engine;

/// <summary>
/// <see cref="RuleEvaluator"/>의 수집·격리 동작을 확인합니다.
/// </summary>
public class RuleEvaluatorTests
{
    /// <summary>
    /// (i) 모든 규칙을 스냅샷에 적용해 나온 Finding을 모은다.
    /// </summary>
    [Fact]
    public void 모든_규칙의_Finding을_모은다()
    {
        var snapshot = EngineTestData.CreateSnapshot(
            EngineTestData.CreateResult("a", ProbeStatus.Success, measurements: [FakeProbe.CreateMeasurement("value")]),
            EngineTestData.CreateResult("b", ProbeStatus.Success, measurements: [FakeProbe.CreateMeasurement("value")]));
        var evaluator = new RuleEvaluator(
            [
                new MeasurementEchoRule("rule-a", "a", "value"),
                new MeasurementEchoRule("rule-b", "b", "value"),
                new MeasurementEchoRule("rule-missing", "a", "no-such-measurement"),
            ],
            new RecordingLogger());

        var findings = evaluator.Evaluate(snapshot);

        Assert.Equal(["rule-a:a", "rule-b:b"], findings.Select(f => f.Id).Order());
    }

    /// <summary>
    /// 한 규칙이 예외를 던져도 다른 규칙의 Finding은 남고, 실패한 규칙은 빈 결과가 아니라 CannotVerify로 드러나며 로그가 남는다.
    /// </summary>
    [Fact]
    public void 규칙_예외는_격리되고_CannotVerify로_드러난다()
    {
        var snapshot = EngineTestData.CreateSnapshot(
            EngineTestData.CreateResult("a", ProbeStatus.Success, measurements: [FakeProbe.CreateMeasurement("value")]));
        var logger = new RecordingLogger();
        var evaluator = new RuleEvaluator(
            [new ThrowingRule("broken"), new MeasurementEchoRule("rule-a", "a", "value")],
            logger);

        var findings = evaluator.Evaluate(snapshot);

        Assert.Contains(findings, f => f.Id == "rule-a:a");
        var failure = Assert.Single(findings, f => f.Verdict == Verdict.CannotVerify);
        Assert.Equal(CannotVerifyReason.ProbeError, failure.CannotVerifyReason);
        Assert.Contains("broken", failure.Id, StringComparison.Ordinal);
        Assert.Contains(logger.Entries, e => e.Level == "Error" && e.Message.Contains("broken", StringComparison.Ordinal));
    }
}
