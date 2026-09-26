/**
 * @file    : ScanOutcomeResolverTests.cs
 * @author  : rudals252
 * @brief   : 검사 단위 결과(Completed/Partial/Cancelled) 도출 규칙 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Engine;

/// <summary>
/// (j) <see cref="ScanOutcomeResolver"/>의 결과 도출 규칙을 확인합니다.
/// </summary>
public class ScanOutcomeResolverTests
{
    /// <summary>
    /// 모두 성공하면 Completed다.
    /// </summary>
    [Fact]
    public void 모두_성공하면_Completed다()
    {
        var outcome = ScanOutcomeResolver.Resolve(
            cancellationRequested: false,
            [EngineTestData.CreateResult("a", ProbeStatus.Success), EngineTestData.CreateResult("b", ProbeStatus.Success)]);

        Assert.Equal(ScanOutcome.Completed, outcome);
    }

    /// <summary>
    /// 정책상 실행하지 않은 항목(권한 필요·온라인 미요청·지원 안 함)만 있으면 Completed다.
    /// </summary>
    [Theory]
    [InlineData(CannotVerifyReason.ElevationRequired)]
    [InlineData(CannotVerifyReason.NotRequested)]
    [InlineData(CannotVerifyReason.Unsupported)]
    public void 정책상_건너뛴_항목만_있으면_Completed다(CannotVerifyReason reason)
    {
        var outcome = ScanOutcomeResolver.Resolve(
            cancellationRequested: false,
            [
                EngineTestData.CreateResult("a", ProbeStatus.Success),
                EngineTestData.CreateResult("b", ProbeStatus.Skipped, issues: [new Issue(reason, "정책")]),
            ]);

        Assert.Equal(ScanOutcome.Completed, outcome);
    }

    /// <summary>
    /// 실패·타임아웃·부분·종료 중 건너뜀이 하나라도 있으면 Partial이다.
    /// </summary>
    [Theory]
    [InlineData(ProbeStatus.Failed, CannotVerifyReason.ProbeError)]
    [InlineData(ProbeStatus.Failed, CannotVerifyReason.Timeout)]
    [InlineData(ProbeStatus.Partial, CannotVerifyReason.AccessDenied)]
    [InlineData(ProbeStatus.Skipped, CannotVerifyReason.ProbeError)]
    [InlineData(ProbeStatus.Cancelled, CannotVerifyReason.Cancelled)]
    public void 실패나_부분_결과가_있으면_Partial이다(ProbeStatus status, CannotVerifyReason reason)
    {
        var outcome = ScanOutcomeResolver.Resolve(
            cancellationRequested: false,
            [
                EngineTestData.CreateResult("a", ProbeStatus.Success),
                EngineTestData.CreateResult("b", status, issues: [new Issue(reason, "사유")]),
            ]);

        Assert.Equal(ScanOutcome.Partial, outcome);
    }

    /// <summary>
    /// 사용자가 취소했으면 다른 결과와 관계없이 Cancelled다.
    /// </summary>
    [Fact]
    public void 사용자가_취소하면_Cancelled다()
    {
        var outcome = ScanOutcomeResolver.Resolve(
            cancellationRequested: true,
            [
                EngineTestData.CreateResult("a", ProbeStatus.Success),
                EngineTestData.CreateResult("b", ProbeStatus.Failed, issues: [new Issue(CannotVerifyReason.Timeout, "시간 초과")]),
            ]);

        Assert.Equal(ScanOutcome.Cancelled, outcome);
    }
}
