/**
 * @file    : PowerActionTests.cs
 * @author  : rudals252
 * @brief   : 대역 전원 API와 실제 복구 조율기로 사전 저장·전원 조건·원복·실패 기록 검증
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Power;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 PC의 전원 계획은 변경하지 않습니다.</summary>
public sealed class PowerActionTests
{
    private static readonly ActionSession Session = ActionCenterTests.Session;
    private static readonly ActionTarget Target = new ActionTarget.Power(PowerActionAdapter.HighPerformance);
    private static RestoreCoordinator Service(Platform platform, ActionCenterTests.Store store, Func<ActionSession>? session = null)
        => new(new OperationCoordinator(), store, session ?? (() => Session), [new PowerActionAdapter(platform, session ?? (() => Session))]);

    /// <summary>실제 쓰기 전에 Pending 저장을 확인하고 재조회 후 Applied, 되돌리기 후 Restored입니다.</summary>
    [Fact]
    public async Task ApplyAndRestorePersistBeforeWriteAndVerifyActualValue()
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store();
        platform.BeforeSet = () => Assert.True(Assert.Single(store.Catalog.Records).State is RollbackState.Pending or RollbackState.Restoring);
        var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        Assert.Empty(store.Catalog.Records); Assert.Equal(0, platform.Writes);
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.Equal(PowerActionAdapter.HighPerformance, platform.Active);
        var record = Assert.Single(store.Catalog.Records); Assert.Equal(RollbackState.Applied, record.State);
        var undo = (await service.PrepareRestoreAsync(ActionId.Power, record.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded);
        Assert.Equal(PowerActionAdapter.Balanced, platform.Active);
        Assert.Equal(RollbackState.Restored, Assert.Single(store.Catalog.Records).State);
    }
    /// <summary>미설치/배터리/알 수 없는 전원/조회 실패/범위는 준비 단계에서 차단합니다.</summary>
    [Theory]
    [InlineData("missing", "PowerSchemeMissing")][InlineData("battery", "PowerNeedsAc")]
    [InlineData("unknown", "PowerNeedsAc")][InlineData("read", "PowerReadFailed")][InlineData("scope", "ScopeExcluded")]
    public async Task UnsafePreparationNeverWrites(string condition, string expected)
    {
        var platform = new Platform();
        if (condition == "missing") { platform.Schemes.Remove(PowerActionAdapter.HighPerformance); }
        if (condition == "battery") { platform.Ac = 0; }
        if (condition == "unknown") { platform.Ac = 255; }
        if (condition == "read") { platform.ReadFails = true; }
        var service = Service(platform, new(), () => condition == "scope" ? Session with { Scope = ActionUserScope.SystemOnly } : Session);
        var result = await service.PrepareApplyAsync(ActionId.Power, Target, default);
        Assert.Null(result.Plan); Assert.Equal(expected, result.Code); Assert.Equal(0, platform.Writes);
    }
    /// <summary>미리보기 뒤 대상/전원/현재 계획이 바뀌면 변경하지 않습니다.</summary>
    [Theory]
    [InlineData("missing", "PowerSchemeMissing")][InlineData("battery", "PowerNeedsAc")][InlineData("external", "CurrentValueChanged")]
    public async Task ChangedConditionsRejectBeforeJournalOrWrite(string condition, string expected)
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        if (condition == "missing") { platform.Schemes.Remove(PowerActionAdapter.HighPerformance); }
        if (condition == "battery") { platform.Ac = 0; }
        if (condition == "external") { platform.Active = PowerActionAdapter.PowerSaver; }
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal(expected, result.Code); Assert.False(result.Started); Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }
    /// <summary>API 실패·실제 값 불일치·후속 읽기 실패는 기록을 남기고 성공으로 처리하지 않습니다.</summary>
    [Theory]
    [InlineData("write", "PowerWriteFailed")][InlineData("unchanged", "VerificationFailed")][InlineData("read", "PowerReadFailed")]
    public async Task FailedWriteOrVerificationLeavesPending(string failure, string code)
    {
        var platform = new Platform { FailWrite = failure == "write", IgnoreWrite = failure == "unchanged" };
        if (failure == "read") { platform.AfterSet = () => platform.ReadFails = true; }
        var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Succeeded); Assert.Equal(code, result.Code);
        Assert.Equal(RollbackState.Pending, Assert.Single(store.Catalog.Records).State);
    }
    /// <summary>적용 뒤 외부 변경은 되돌리기가 덮어쓰지 않습니다.</summary>
    [Fact]
    public async Task ExternalChangeBlocksUndo()
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        await service.ExecuteAsync(plan.Id, default);
        var undo = (await service.PrepareRestoreAsync(ActionId.Power, plan.Id, default)).Plan!;
        platform.Active = PowerActionAdapter.PowerSaver;
        Assert.Equal("CurrentValueChanged", (await service.ExecuteAsync(undo.Id, default)).Code);
        Assert.Equal(1, platform.Writes); Assert.Equal(PowerActionAdapter.PowerSaver, platform.Active);
    }
    /// <summary>이미 선택한 값이면 저장하거나 쓰지 않습니다.</summary>
    [Fact]
    public async Task AlreadyAppliedDoesNotWrite()
    {
        var platform = new Platform { Active = PowerActionAdapter.HighPerformance }; var store = new ActionCenterTests.Store();
        var service = Service(platform, store); var plan = (await service.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        Assert.Equal("AlreadyApplied", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }
    /// <summary>복구 쓰기 실패·계획 소실은 기록을 없애지 않습니다.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task RestoreFailureRemainsVisible(bool missing)
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Power, Target, default)).Plan!;
        await service.ExecuteAsync(plan.Id, default);
        var undo = (await service.PrepareRestoreAsync(ActionId.Power, plan.Id, default)).Plan!;
        if (missing) { platform.Schemes.Remove(PowerActionAdapter.Balanced); } else { platform.FailWrite = true; }
        var result = await service.ExecuteAsync(undo.Id, default);
        Assert.False(result.Succeeded); Assert.Equal(missing ? "PowerSchemeMissing" : "PowerWriteFailed", result.Code);
        Assert.Equal(missing ? RollbackState.Applied : RollbackState.Restoring, Assert.Single(store.Catalog.Records).State);
        Assert.Equal(PowerActionAdapter.HighPerformance, platform.Active);
    }
    /// <summary>절전 방향이어도 미리보기 이후 전원 상태가 바뀌면 새 확인을 요구합니다.</summary>
    [Fact]
    public async Task PowerTransitionInvalidatesPreview()
    {
        var platform = new Platform { Active = PowerActionAdapter.HighPerformance }; var service = Service(platform, new());
        var plan = (await service.PrepareApplyAsync(ActionId.Power, new ActionTarget.Power(PowerActionAdapter.Balanced), default)).Plan!;
        platform.Ac = 0;
        Assert.Equal("PowerStateChanged", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(0, platform.Writes);
    }
    internal sealed class Platform : IPowerSettingsPlatform
    {
        internal Guid Active = PowerActionAdapter.Balanced;
        internal HashSet<Guid> Schemes = [PowerActionAdapter.Balanced, PowerActionAdapter.HighPerformance, PowerActionAdapter.PowerSaver];
        internal byte Ac = 1;
        internal bool ReadFails, FailWrite, IgnoreWrite;
        internal int Writes;
        internal Action? BeforeSet, AfterSet;
        public IReadOnlyList<Guid> Installed() => Schemes.ToArray();
        public Guid Current() => ReadFails ? throw new ActionUnavailableException("PowerReadFailed") : Active;
        public byte AcStatus() => Ac;
        public void Set(Guid scheme)
        {
            BeforeSet?.Invoke(); Writes++;
            if (FailWrite) { throw new ActionUnavailableException("PowerWriteFailed"); }
            if (!IgnoreWrite) { Active = scheme; }
            AfterSet?.Invoke();
        }
    }
}
