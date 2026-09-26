/**
 * @file    : WindowsUpdateDriverRuleTests.cs
 * @author  : rudals252
 * @brief   : Windows Update 드라이버 검색 규칙의 후보 n개(설정 열기·제목 목록·전체 제조사 포괄 주장 금지), 성공 0건 '후보 없음', 일부 오류 성공 안내, 재부팅 필요 정보 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="WindowsUpdateDriverRule"/>을 검증합니다. 제목은 테스트 예시입니다.
/// </summary>
public sealed class WindowsUpdateDriverRuleTests
{
    private static readonly WindowsUpdateDriverRule RULE = new();

    /// <summary>
    /// 결과로 규칙을 평가한다.
    /// </summary>
    private static IReadOnlyList<Finding> Evaluate(ProbeStatus status, int resultCode, bool? reboot, params string[] titles)
    {
        return RULE.Evaluate(HardwareRuleTestData.Snapshot(DriverRuleTestData.WindowsUpdateResult(status, resultCode, reboot, titles)));
    }

    /// <summary>후보가 있으면 Candidate 하나이며 Windows 업데이트 설정 열기와 제목 목록, 전체 제조사를 포괄하지 않는다는 안내가 있다.</summary>
    [Fact]
    public void 후보가_있으면_설정_열기와_제목을_준다()
    {
        var finding = Assert.Single(Evaluate(ProbeStatus.Success, WindowsUpdateProbeContract.RESULT_SUCCEEDED, false, "테스트 제조사 - Display - 1.2.3.4", "테스트 제조사 - Net - 5.6.7.8"));

        Assert.Equal(Verdict.Candidate, finding.Verdict);
        Assert.Equal(WindowsUpdateDriverRule.CANDIDATES_FINDING_ID, finding.Id);
        Assert.Contains("Windows Update에서 제공되는 드라이버 후보 2개", finding.Title, StringComparison.Ordinal);
        Assert.Equal("ms-settings:windowsupdate", Assert.Single(finding.Actions.OfType<OpenSettingsAction>()).Uri);
        Assert.Contains("테스트 제조사 - Display - 1.2.3.4", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("테스트 제조사 - Net - 5.6.7.8", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("모든 제조사의 최신 드라이버를 모은 목록은 아니에요", finding.Detail, StringComparison.Ordinal);
        Assert.NotNull(finding.Recommendation);
        Assert.NotNull(finding.Impact);
        Assert.DoesNotContain("권장", DriverRuleTestData.VisibleText(finding), StringComparison.Ordinal);
    }

    /// <summary>성공 결과 0건은 Info '해당 서비스에서 후보 없음'이며 전체 최신이라고 주장하지 않는다.</summary>
    [Fact]
    public void 성공_0건은_후보_없음_정보다()
    {
        var finding = Assert.Single(Evaluate(ProbeStatus.Success, WindowsUpdateProbeContract.RESULT_SUCCEEDED, false));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("해당 서비스에서 후보 없음", finding.Title, StringComparison.Ordinal);
        Assert.Contains("모든 제조사의 최신 드라이버를 확인한 결과는 아니에요", finding.Evidence, StringComparison.Ordinal);
    }

    /// <summary>일부 오류와 함께 성공하면 0건이어도 '후보 없음' 대신 부분 결과 안내 Info를 준다.</summary>
    [Fact]
    public void 일부_오류_성공은_부분_안내다()
    {
        var findings = Evaluate(ProbeStatus.Partial, WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS, null);

        var finding = Assert.Single(findings);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(WindowsUpdateDriverRule.PARTIAL_FINDING_ID, finding.Id);
        Assert.DoesNotContain(findings, f => f.Id == WindowsUpdateDriverRule.NONE_FINDING_ID);
    }

    /// <summary>일부 오류와 함께 성공했어도 찾은 후보는 후보로 보여 준다.</summary>
    [Fact]
    public void 일부_오류_성공에서도_후보는_보여_준다()
    {
        var findings = Evaluate(ProbeStatus.Partial, WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS, null, "테스트 드라이버");

        Assert.Contains(findings, f => f.Id == WindowsUpdateDriverRule.CANDIDATES_FINDING_ID && f.Verdict == Verdict.Candidate);
        Assert.Contains(findings, f => f.Id == WindowsUpdateDriverRule.PARTIAL_FINDING_ID);
    }

    /// <summary>재부팅 필요면 별도 Info를 추가한다.</summary>
    [Fact]
    public void 재부팅_필요는_정보다()
    {
        var findings = Evaluate(ProbeStatus.Success, WindowsUpdateProbeContract.RESULT_SUCCEEDED, true);

        var reboot = Assert.Single(findings, f => f.Id == WindowsUpdateDriverRule.REBOOT_FINDING_ID);
        Assert.Equal(Verdict.Info, reboot.Verdict);
    }

    /// <summary>프로브가 건너뜀·실패·취소면 규칙은 빈 결과다(엔진이 사유 카드를 만듦).</summary>
    [Theory]
    [InlineData(ProbeStatus.Skipped)]
    [InlineData(ProbeStatus.Failed)]
    [InlineData(ProbeStatus.Cancelled)]
    public void 프로브가_성공하지_않으면_빈_결과다(ProbeStatus status)
    {
        Assert.Empty(Evaluate(status, WindowsUpdateProbeContract.RESULT_FAILED, null));
    }
}
