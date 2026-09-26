/**
 * @file    : ProbeResultConverterTests.cs
 * @author  : rudals252
 * @brief   : 프로브 상태(건너뜀/실패/취소/부분)를 CannotVerify Finding으로 변환하는 규칙 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.Engine;

/// <summary>
/// <see cref="ProbeResultConverter"/>가 실패·미실행을 빈 결과나 Ok로 바꾸지 않고 CannotVerify로 드러내는지 확인합니다.
/// </summary>
public class ProbeResultConverterTests
{
    private static readonly Dictionary<string, FindingCategory> CATEGORIES = new()
    {
        ["skipped"] = FindingCategory.Driver,
        ["failed"] = FindingCategory.Memory,
        ["cancelled"] = FindingCategory.Storage,
        ["partial"] = FindingCategory.Display,
        ["ok"] = FindingCategory.Power,
    };

    /// <summary>
    /// (i) 건너뜀·실패·취소 프로브마다 정확히 하나의 CannotVerify가 나오며, 분류와 사유는 프로브 분류와 첫 Issue를 따른다.
    /// 성공 프로브에서는 아무 Finding도 만들지 않는다.
    /// </summary>
    [Fact]
    public void 건너뜀_실패_취소_프로브마다_CannotVerify를_정확히_하나씩_만든다()
    {
        var snapshot = EngineTestData.CreateSnapshot(
            EngineTestData.CreateResult("skipped", ProbeStatus.Skipped, issues: [new Issue(CannotVerifyReason.NotRequested, "온라인 미요청")]),
            EngineTestData.CreateResult("failed", ProbeStatus.Failed, issues:
            [
                new Issue(CannotVerifyReason.Timeout, "시간 초과"),
                new Issue(CannotVerifyReason.ProbeError, "두 번째 사유"),
            ]),
            EngineTestData.CreateResult("cancelled", ProbeStatus.Cancelled, issues: [new Issue(CannotVerifyReason.Cancelled, "사용자 취소")]),
            EngineTestData.CreateResult("ok", ProbeStatus.Success, measurements: [FakeProbe.CreateMeasurement("value")]));

        var findings = ProbeResultConverter.Convert(snapshot, CATEGORIES);

        Assert.Equal(3, findings.Count);
        Assert.All(findings, f => Assert.Equal(Verdict.CannotVerify, f.Verdict));
        Assert.All(findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Title)));
        Assert.All(findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Evidence)));

        var skipped = Assert.Single(findings, f => f.Category == FindingCategory.Driver);
        Assert.Equal(CannotVerifyReason.NotRequested, skipped.CannotVerifyReason);
        var failed = Assert.Single(findings, f => f.Category == FindingCategory.Memory);
        Assert.Equal(CannotVerifyReason.Timeout, failed.CannotVerifyReason);
        var cancelled = Assert.Single(findings, f => f.Category == FindingCategory.Storage);
        Assert.Equal(CannotVerifyReason.Cancelled, cancelled.CannotVerifyReason);
        Assert.DoesNotContain(findings, f => f.Category == FindingCategory.Power);
        Assert.Equal(3, findings.Select(f => f.Id).Distinct().Count());
    }

    /// <summary>
    /// Issue 없이 실패·취소·건너뜀으로 끝난 결과도 빈 결과로 사라지지 않고 상태에 맞는 기본 사유로 CannotVerify가 된다.
    /// </summary>
    [Theory]
    [InlineData(ProbeStatus.Failed, CannotVerifyReason.ProbeError)]
    [InlineData(ProbeStatus.Cancelled, CannotVerifyReason.Cancelled)]
    [InlineData(ProbeStatus.Skipped, CannotVerifyReason.Unsupported)]
    public void Issue가_없어도_상태별_기본_사유로_CannotVerify를_만든다(ProbeStatus status, CannotVerifyReason expected)
    {
        var snapshot = EngineTestData.CreateSnapshot(EngineTestData.CreateResult("failed", status));

        var finding = Assert.Single(ProbeResultConverter.Convert(snapshot, CATEGORIES));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(expected, finding.CannotVerifyReason);
    }

    /// <summary>
    /// 부분 결과는 CannotVerify(PartialData) 하나를 추가로 만든다.
    /// </summary>
    [Fact]
    public void 부분_결과는_PartialData_CannotVerify를_만든다()
    {
        var snapshot = EngineTestData.CreateSnapshot(EngineTestData.CreateResult(
            "partial",
            ProbeStatus.Partial,
            measurements: [FakeProbe.CreateMeasurement("value")],
            issues: [new Issue(CannotVerifyReason.AccessDenied, "일부 접근 거부")]));

        var finding = Assert.Single(ProbeResultConverter.Convert(snapshot, CATEGORIES));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.PartialData, finding.CannotVerifyReason);
        Assert.Equal(FindingCategory.Display, finding.Category);
    }

    /// <summary>
    /// 분류 표에 없는 프로브는 Unclassified로 표시한다.
    /// </summary>
    [Fact]
    public void 분류를_모르는_프로브는_Unclassified로_표시한다()
    {
        var snapshot = EngineTestData.CreateSnapshot(EngineTestData.CreateResult("unknown", ProbeStatus.Failed));

        var finding = Assert.Single(ProbeResultConverter.Convert(snapshot, CATEGORIES));

        Assert.Equal(FindingCategory.Unclassified, finding.Category);
    }
}
