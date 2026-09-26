/**
 * @file    : FakeRules.cs
 * @author  : rudals252
 * @brief   : 규칙 평가 테스트용 가짜 규칙(측정값을 Info로 옮기는 규칙, 예외를 던지는 규칙)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Engine.Fakes;

/// <summary>
/// 지정한 프로브의 측정값이 있으면 Info Finding 하나를 내는 가짜 규칙입니다.
/// </summary>
internal sealed class MeasurementEchoRule : IRule
{
    private readonly string _probeId;
    private readonly string _measurementName;

    /// <summary>가짜 규칙을 만든다.</summary>
    public MeasurementEchoRule(string id, string probeId, string measurementName)
    {
        Id = id;
        _probeId = probeId;
        _measurementName = measurementName;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>Evaluate가 호출된 횟수.</summary>
    public int EvaluateCount { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        EvaluateCount++;
        var measurement = snapshot.GetMeasurement(_probeId, _measurementName);
        if (measurement is null)
        {
            return [];
        }

        return
        [
            new Finding(
                id: $"{Id}:{_probeId}",
                category: FindingCategory.Display,
                title: "측정값을 확인했어요",
                measured: [measurement],
                evidence: "가짜 규칙",
                verdict: Verdict.Info,
                cannotVerifyReason: null,
                detail: null,
                recommendation: null,
                impact: null,
                actions: []),
        ];
    }
}

/// <summary>
/// 평가 중 예외를 던지는 가짜 규칙입니다.
/// </summary>
internal sealed class ThrowingRule : IRule
{
    /// <summary>가짜 규칙을 만든다.</summary>
    public ThrowingRule(string id)
    {
        Id = id;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        throw new InvalidOperationException("규칙 결함");
    }
}
