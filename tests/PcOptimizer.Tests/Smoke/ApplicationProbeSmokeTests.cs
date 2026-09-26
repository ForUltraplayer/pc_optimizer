/**
 * @file    : ApplicationProbeSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 이 PC에서 실제 시작 프로그램 프로브를 실행해 실패하지 않고 항목 수·읽지 못한 위치 측정값을 내며, 규칙이 요약·부팅 영향·항목별 정보를 만드는지 확인(기본 테스트 필터에서 제외)
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>
/// 실제 PC 스모크 테스트입니다. 시작 항목 수·이름을 기대값으로 두지 않고 상태와 측정·판정 존재만 봅니다.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class ApplicationProbeSmokeTests(ITestOutputHelper output)
{
    private static readonly ScanContext CONTEXT = new(
        Guid.NewGuid(), new UserContext("smoke-user", IsElevated: false), OnlineCheckRequested: false, DateTimeOffset.UtcNow);

    /// <summary>실제 시작 프로그램 프로브가 실패하지 않고 항목 수를 내며, 규칙이 항목 수만큼 정보와 요약·부팅 영향을 만든다.</summary>
    [Fact]
    public async Task 시작_프로그램_프로브가_측정값을_낸다()
    {
        var result = await new StartupItemsProbe().RunAsync(CONTEXT, CancellationToken.None);
        HardwareProbeSmokeTests.Dump(output, result);

        Assert.NotEqual(ProbeStatus.Failed, result.Status);
        var count = Assert.IsType<IntegerValue>(Assert.Single(result.Measurements, m => m.Name == StartupItemsProbeContract.ITEM_COUNT).Value).Value;

        var findings = new StartupItemsRule().Evaluate(new ScanSnapshot(CONTEXT.ScanId, [result]));
        foreach (var finding in findings)
        {
            output.WriteLine($"{finding.Id} | {finding.Verdict} {finding.CannotVerifyReason?.ToString() ?? string.Empty} | {finding.Title}");
        }

        Assert.Equal(count, findings.Count(f => f.Id.StartsWith(StartupItemsRule.FINDING_ID_PREFIX, StringComparison.Ordinal)));
        Assert.Contains(findings, f => f.Id == StartupItemsRule.SUMMARY_FINDING_ID);
        Assert.Contains(findings, f => f.Id == StartupItemsRule.BOOT_IMPACT_FINDING_ID);
        Assert.DoesNotContain(findings, f => f.Verdict == Verdict.Candidate);
    }
}
