/**
 * @file    : DeliveryOptimizationActionTests.cs
 * @author  : rudals252
 * @brief   : 제공자 대역으로 고정 ID·고정 보관 제외·다운로드 관문·부분 결과 검증
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.SystemCleanup;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>Windows 실제 캐시를 정리하지 않습니다.</summary>
public sealed class DeliveryOptimizationActionTests
{
    private static readonly ActionTarget Target = new ActionTarget.DeliveryCache();
    private static readonly ActionSession Session = ActionCenterTests.Session;
    private static ActionCoordinator Service(Platform platform, Func<ActionSession>? session = null)
        => new(new OperationCoordinator(), session ?? (() => Session), [new DeliveryOptimizationAdapter(platform, session ?? (() => Session))]);
    /// <summary>고정 보관·새로 생긴 ID는 남기고 미리보기 집합만 처리합니다.</summary>
    [Fact]
    public async Task DeleteOnlyPreviewIdsAndNeverPinned()
    {
        var platform = new Platform(); platform.Files.Add(new("pinned", 55, 2, true));
        var service = Service(platform); var plan = (await service.PrepareAsync(ActionId.DeliveryOptimization, Target, false, default)).Plan!;
        Assert.Equal(300, plan.Preview.Details!.EstimatedLogicalBytes); Assert.Empty(platform.Deleted);
        platform.Files.Add(new("new", 70, 2, false));
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Succeeded, result.Code); Assert.Equal(new[] { "first", "second" }, platform.Deleted);
        Assert.Equal(2, result.Effect!.ChangedFiles); Assert.Equal(0, result.Effect.AfterLogicalBytes);
        Assert.Equal(new[] { "pinned", "new" }, platform.Files.Select(f => f.Id));
    }
    /// <summary>다운로드·중지·알 수 없는 상태·중복 ID·빈 캐시는 쓰기 전 거절합니다.</summary>
    [Theory]
    [InlineData("downloading", "DeliveryBusy")][InlineData("paused", "DeliveryBusy")]
    [InlineData("unknown", "DeliveryStateUnavailable")][InlineData("duplicate", "DeliveryStateUnavailable")]
    [InlineData("empty", "NoEligibleFiles")][InlineData("pinned", "NoEligibleFiles")][InlineData("read", "DeliveryStateUnavailable")]
    public async Task CannotPrepareUnsafeOrEmptyState(string condition, string expected)
    {
        var platform = new Platform();
        if (condition is "downloading" or "paused" or "unknown") { platform.Files[0] = platform.Files[0] with { Status = condition == "downloading" ? (byte)0 : condition == "paused" ? (byte)3 : (byte)99 }; }
        if (condition == "duplicate") { platform.Files.Add(platform.Files[0]); }
        if (condition == "empty") { platform.Files.Clear(); }
        if (condition == "pinned") { platform.Files = platform.Files.Select(f => f with { Pinned = true }).ToList(); }
        if (condition == "read") { platform.ReadFails = true; }
        var result = await Service(platform).PrepareAsync(ActionId.DeliveryOptimization, Target, false, default);
        Assert.Null(result.Plan); Assert.Equal(expected, result.Code); Assert.Empty(platform.Deleted);
    }
    /// <summary>실행 전 상태/크기/고정 여부/세션 변경과 제공자 사전 거절을 차단합니다.</summary>
    [Theory]
    [InlineData("download")][InlineData("size")][InlineData("pinned")][InlineData("session")][InlineData("contract")]
    public async Task RevalidateBeforeFirstWrite(string condition)
    {
        var platform = new Platform(); var session = Session; var service = Service(platform, () => session);
        var plan = (await service.PrepareAsync(ActionId.DeliveryOptimization, Target, false, default)).Plan!;
        if (condition == "download") { platform.Files[0] = platform.Files[0] with { Status = 0 }; }
        if (condition == "size") { platform.Files[0] = platform.Files[0] with { Bytes = 2 }; }
        if (condition == "pinned") { platform.Files[0] = platform.Files[0] with { Pinned = true }; }
        if (condition == "session") { session = session with { Sid = "other" }; }
        if (condition == "contract") { platform.ContractFails = true; }
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.False(result.Succeeded); Assert.Empty(platform.Deleted);
    }
    /// <summary>중간 실패에도 이미 확인한 삭제 개수를 보존합니다.</summary>
    [Theory]
    [InlineData("busy")][InlineData("read")][InlineData("delete")][InlineData("unchanged")]
    public async Task PartialFailurePreservesObservedProgress(string failure)
    {
        var platform = new Platform();
        platform.AfterDelete = () =>
        {
            if (platform.Deleted.Count != 1) { return; }
            if (failure == "busy") { platform.Files[0] = platform.Files[0] with { Status = 0 }; }
            if (failure == "read") { platform.ReadFails = true; }
            if (failure == "delete") { platform.DeleteFails = true; }
            if (failure == "unchanged") { platform.IgnoreDelete = true; }
        };
        var service = Service(platform); var plan = (await service.PrepareAsync(ActionId.DeliveryOptimization, Target, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Started); Assert.False(result.Succeeded);
        Assert.Equal(failure == "read" ? 0 : 1, result.Effect!.ChangedFiles);
        Assert.True(result.Effect.FailedFiles > 0);
    }
    /// <summary>시스템 범위에서도 지원하며 사라진 대상은 다른 ID로 대체하지 않습니다.</summary>
    [Fact]
    public async Task SystemOnlyCanPrepareAndMissingEntriesAreNotReplaced()
    {
        var platform = new Platform(); var service = Service(platform, () => Session with { Scope = ActionUserScope.SystemOnly });
        var plan = (await service.PrepareAsync(ActionId.DeliveryOptimization, Target, false, default)).Plan!;
        platform.Files.Clear(); platform.Files.Add(new("replacement", 999, 2, false));
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal("NoEligibleFiles", result.Code); Assert.Empty(platform.Deleted); Assert.Equal(2, result.Effect!.SkippedFiles);
    }
    /// <summary>동기 제공자가 취소에 즉시 응답하지 않으면 실제 반환까지 공통 관문을 유지합니다.</summary>
    [Fact]
    public async Task CancellationRetainsGateUntilProviderReturns()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var platform = new Platform { AfterDelete = () => { entered.TrySetResult(); if (!release.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException(); } } };
        var gate = new OperationCoordinator();
        var service = new ActionCoordinator(gate, () => Session, [new DeliveryOptimizationAdapter(platform, () => Session)]);
        var plan = (await service.PrepareAsync(ActionId.DeliveryOptimization, Target, false, default)).Plan!;
        using var cancellation = new CancellationTokenSource();
        var running = service.ExecuteAsync(plan.Id, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
            Assert.Equal("Draining", (await running).Code);
            Assert.True(gate.State.IsBusy);
            Assert.Equal("Busy", (await service.PrepareAsync(ActionId.DeliveryOptimization, Target, false, default)).Code);
        }
        finally { release.Set(); }
        for (var i = 0; i < 100 && gate.State.IsBusy; i++) { await Task.Delay(10); }
        Assert.False(gate.State.IsBusy); Assert.Single(platform.Deleted);
    }
    /// <summary>제공자 메서드 준비 실패를 일부 삭제로 표현하지 않습니다.</summary>
    [Fact]
    public void ProviderPreparationFailureDoesNotClaimDeletion()
    {
        var view = new PcOptimizer.App.ViewModels.ActionResultViewModel(new(Guid.NewGuid(), false, false, "DeliveryDeleteFailed"), ActionId.DeliveryOptimization, false, "");
        Assert.Contains("삭제를 시작하지 않았습니다", view.Detail);
        Assert.DoesNotContain("이미 처리", view.Detail);
    }
    internal sealed class Platform : IDeliveryCachePlatform
    {
        internal List<DeliveryCacheEntry> Files = [new("first", 100, 2, false), new("second", 200, 2, false)];
        internal List<string> Deleted = [];
        internal bool ReadFails, DeleteFails, ContractFails, IgnoreDelete;
        internal Action? AfterDelete;
        public IReadOnlyList<DeliveryCacheEntry> Read(CancellationToken ct) => ReadFails ? throw new ActionUnavailableException("DeliveryStateUnavailable") : Files.ToArray();
        public void Delete(string id, Action beforeDelete)
        {
            if (ContractFails) { throw new ActionUnavailableException("DeliveryContractUnsupported"); }
            beforeDelete();
            if (DeleteFails) { throw new ActionUnavailableException("DeliveryDeleteFailed"); }
            Deleted.Add(id);
            if (!IgnoreDelete) { Files.RemoveAll(f => f.Id == id); }
            AfterDelete?.Invoke();
        }
    }
}
