/**
 * @file    : WuaSearchGatewayTests.cs
 * @author  : rudals252
 * @brief   : WUA 검색 게이트웨이를 늦은 바인딩 가짜 객체로 검증(조건·콜백 전달, 결과 코드별 반환, 드라이버 세부 속성 없음 → 항목 표시, 취소 시 RequestAbort, 세션 없음·멤버 없음 → 오류 코드, 설정 속성 불변, 재부팅 여부)
 */

// 사용자 패키지
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="WuaSearchGateway"/>를 검증합니다. 실제 Windows Update에 연결하지 않습니다.
/// </summary>
public sealed class WuaSearchGatewayTests
{
    private static readonly TimeSpan FAST_POLL = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan SHORT_ABORT_WAIT = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 가짜 세션으로 게이트웨이를 만든다.
    /// </summary>
    private static WuaSearchGateway Gateway(object? session, bool? rebootRequired = false)
    {
        return new WuaSearchGateway(() => session, () => rebootRequired is { } reboot ? new FakeSystemInfo(reboot) : null, FAST_POLL, SHORT_ABORT_WAIT);
    }

    /// <summary>결과 코드와 업데이트 목록·재부팅 여부를 돌려주고, 검색 조건·콜백을 전달하며 검색기 설정을 바꾸지 않는다.</summary>
    [Theory]
    [InlineData(WindowsUpdateProbeContract.RESULT_SUCCEEDED)]
    [InlineData(WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS)]
    [InlineData(WindowsUpdateProbeContract.RESULT_FAILED)]
    [InlineData(WindowsUpdateProbeContract.RESULT_ABORTED)]
    public async Task 결과_코드와_업데이트를_돌려준다(int resultCode)
    {
        var searcher = new FakeUpdateSearcher(new FakeSearchJob(completedAtStart: true), () => new FakeSearchResult(resultCode, new FakeDriverUpdate()));

        var outcome = await Gateway(new FakeUpdateSession(searcher), rebootRequired: true).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, CancellationToken.None);

        Assert.Equal(resultCode, outcome.ResultCode);
        Assert.Null(outcome.ErrorHResult);
        var update = Assert.Single(outcome.Updates);
        Assert.Equal("테스트 제조사 - Display - 1.2.3.4", update.Title);
        Assert.Equal("테스트 모델", update.DriverModel);
        Assert.Equal("테스트 제조사", update.DriverManufacturer);
        Assert.Equal(new DateTime(2026, 3, 4), update.DriverVerDate);
        Assert.Equal("Display", update.DriverClass);
        Assert.Equal(["5000001"], update.KbArticleIds);
        Assert.False(update.IsDownloaded);
        Assert.True(update.DetailsAvailable);
        Assert.True(outcome.RebootRequired);
        Assert.Equal(WindowsUpdateProbeContract.SEARCH_CRITERIA, searcher.Criteria);
        Assert.NotNull(searcher.Callback);
        Assert.Equal(0, searcher.SettingChanges);
    }

    /// <summary>드라이버 세부 속성이 없는 업데이트는 예외 없이 DetailsAvailable=false로 남긴다.</summary>
    [Fact]
    public async Task 세부_속성이_없으면_항목에_표시한다()
    {
        var searcher = new FakeUpdateSearcher(new FakeSearchJob(true), () => new FakeSearchResult(WindowsUpdateProbeContract.RESULT_SUCCEEDED, new FakePlainUpdate()));

        var outcome = await Gateway(new FakeUpdateSession(searcher)).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, CancellationToken.None);

        var update = Assert.Single(outcome.Updates);
        Assert.Equal("세부 정보 없는 업데이트", update.Title);
        Assert.False(update.DetailsAvailable);
        Assert.Null(update.DriverModel);
        Assert.Empty(update.KbArticleIds);
    }

    /// <summary>취소되면 검색 작업에 RequestAbort를 호출하고 취소로 끝난다.</summary>
    [Fact]
    public async Task 취소하면_중단을_요청한다()
    {
        var job = new FakeSearchJob(completedAtStart: false);
        var searcher = new FakeUpdateSearcher(job, () => new FakeSearchResult(WindowsUpdateProbeContract.RESULT_ABORTED));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Gateway(new FakeUpdateSession(searcher)).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, cts.Token));

        Assert.Equal(1, job.AbortRequests);
        Assert.Equal(1, searcher.EndSearchCalls);
    }

    /// <summary>중단 요청 뒤에도 끝나지 않는 작업은 정해진 시간만 기다리고 취소로 끝낸다(EndSearch 호출 없음).</summary>
    [Fact]
    public async Task 끝나지_않는_작업도_기다림을_멈춘다()
    {
        var job = new FakeSearchJob(completedAtStart: false, completesOnAbort: false);
        var searcher = new FakeUpdateSearcher(job, () => new FakeSearchResult(WindowsUpdateProbeContract.RESULT_ABORTED));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Gateway(new FakeUpdateSession(searcher)).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, cts.Token));

        Assert.Equal(1, job.AbortRequests);
        Assert.Equal(0, searcher.EndSearchCalls);
    }

    /// <summary>세션을 만들 수 없으면(구성 요소 없음) 오류 코드를 돌려준다.</summary>
    [Fact]
    public async Task 세션이_없으면_오류_코드다()
    {
        var outcome = await Gateway(session: null).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, CancellationToken.None);

        Assert.Null(outcome.ResultCode);
        Assert.Equal(WuaSearchGateway.ERROR_SESSION_UNAVAILABLE, outcome.ErrorCode);
    }

    /// <summary>필요한 멤버가 없는 객체(예상과 다른 COM 형식)는 예외 없이 멤버 없음 코드다.</summary>
    [Fact]
    public async Task 멤버가_없으면_오류_코드다()
    {
        var outcome = await Gateway(session: new FakeSystemInfo(false)).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, CancellationToken.None);

        Assert.Null(outcome.ResultCode);
        Assert.Equal(WuaSearchGateway.ERROR_MEMBER_MISSING, outcome.ErrorCode);
    }

    /// <summary>시스템 정보를 읽지 못하면 재부팅 여부는 null이다(false로 바꾸지 않음).</summary>
    [Fact]
    public async Task 재부팅_여부를_모르면_null이다()
    {
        var searcher = new FakeUpdateSearcher(new FakeSearchJob(true), () => new FakeSearchResult(WindowsUpdateProbeContract.RESULT_SUCCEEDED));

        var outcome = await Gateway(new FakeUpdateSession(searcher), rebootRequired: null).SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, CancellationToken.None);

        Assert.Null(outcome.RebootRequired);
        Assert.Empty(outcome.Updates);
    }
}
