/**
 * @file    : AdvancedActionTests.cs
 * @author  : rudals252
 * @brief   : 실제 설정·재부팅 없이 원본 보존·세션·변조·취소·부팅 요청 경계 검증
 */
using System.IO;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Advanced;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>레지스트리와 부팅은 대역으로만 실행합니다.</summary>
public sealed class AdvancedActionTests
{
    private static readonly ActionSession Session = ActionCenterTests.Session;
    internal sealed class Registry : IAdvancedRegistryPlatform
    {
        internal RollbackValue Value = AdvancedOptions.Absent;
        internal int Writes;
        internal string IdentityValue = "fixture";
        internal bool FailWrite;
        internal Action? BeforeWrite;
        public string Identity(AdvancedOption option, ActionSession session) => IdentityValue;
        public RollbackValue Read(AdvancedOption option, ActionSession session) => Value;
        public bool CompareExchange(AdvancedOption option, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit)
        {
            if (!Value.SameAs(expected)) { return false; }
            beforeCommit(); BeforeWrite?.Invoke();
            if (FailWrite) { throw new ActionUnavailableException("AdvancedWriteFailed"); }
            Value = desired; Writes++; return true;
        }
    }
    private static RestoreCoordinator Service(AdvancedOption option, Registry platform, ActionCenterTests.Store store, Func<ActionSession>? session = null)
        => new(new OperationCoordinator(), store, session ?? (() => Session), [new AdvancedRegistryAdapter(option, session ?? (() => Session), platform)]);
    /// <summary>원본 값 없음도 Pending 후 적용·원복하며 새 ActionId를 codec이 보존합니다.</summary>
    [Theory]
    [InlineData(AdvancedOption.Mpo, AdvancedSetting.Off)]
    [InlineData(AdvancedOption.GameMode, AdvancedSetting.On)]
    [InlineData(AdvancedOption.Hags, AdvancedSetting.Off)]
    public async Task JournalPrecedesWriteAndRestoresExactOriginal(AdvancedOption option, AdvancedSetting setting)
    {
        var native = new Registry { Value = option == AdvancedOption.Hags ? AdvancedOptions.Dword(2) : AdvancedOptions.Absent };
        var original = native.Value; var store = new ActionCenterTests.Store(); var service = Service(option, native, store);
        native.BeforeWrite = () => Assert.True(Assert.Single(store.Catalog.Records).State is RollbackState.Pending or RollbackState.Restoring);
        var id = AdvancedOptions.Action(option);
        var plan = (await service.PrepareApplyAsync(id, new ActionTarget.Advanced(option, setting), default)).Plan!;
        Assert.Equal(0, native.Writes);
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        var record = Assert.Single(store.Catalog.Records);
        var decoded = RollbackCodec.Decode(RollbackCodec.Encode(record, Session.Sid), Session.Sid);
        Assert.Equal(id, decoded.ActionId);
        Assert.True(decoded.Before.SameAs(original));
        var undo = (await service.PrepareRestoreAsync(id, record.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded);
        Assert.True(native.Value.SameAs(original)); Assert.Equal(2, native.Writes);
    }
    /// <summary>기본값 복귀와 작업 전 원복은 다른 값 변경입니다.</summary>
    [Fact]
    public async Task WindowsDefaultCanBeUndoneToPreviousDisabledValue()
    {
        var platform = new Registry { Value = AdvancedOptions.Dword(5) }; var store = new ActionCenterTests.Store();
        var service = Service(AdvancedOption.Mpo, platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Mpo, new ActionTarget.Advanced(AdvancedOption.Mpo, AdvancedSetting.Default), default)).Plan!;
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded); Assert.False(platform.Value.Exists);
        var undo = (await service.PrepareRestoreAsync(ActionId.Mpo, plan.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded); Assert.True(platform.Value.SameAs(AdvancedOptions.Dword(5)));
    }
    /// <summary>미리보기 후 값·control set·세션 변경은 실행 전 거절합니다.</summary>
    [Theory]
    [InlineData("value")][InlineData("identity")][InlineData("session")]
    public async Task ChangedPreviewConditionsNeverWrite(string change)
    {
        var platform = new Registry(); var store = new ActionCenterTests.Store(); var session = Session;
        var service = Service(AdvancedOption.GameMode, platform, store, () => session);
        var plan = (await service.PrepareApplyAsync(ActionId.GameMode, new ActionTarget.Advanced(AdvancedOption.GameMode, AdvancedSetting.On), default)).Plan!;
        if (change == "value") { platform.Value = AdvancedOptions.Dword(0); }
        if (change == "identity") { platform.IdentityValue = "other"; }
        if (change == "session") { session = session with { SessionId = session.SessionId + 1 }; }
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }
    /// <summary>외부 변경은 원복이 덮어쓰지 않습니다.</summary>
    [Fact]
    public async Task RestoreRejectsExternalChanges()
    {
        var platform = new Registry(); var store = new ActionCenterTests.Store(); var service = Service(AdvancedOption.GameMode, platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.GameMode, new ActionTarget.Advanced(AdvancedOption.GameMode, AdvancedSetting.On), default)).Plan!;
        await service.ExecuteAsync(plan.Id, default);
        var undo = (await service.PrepareRestoreAsync(ActionId.GameMode, plan.Id, default)).Plan!;
        platform.Value = AdvancedOptions.Dword(0);
        Assert.Equal("CurrentValueChanged", (await service.ExecuteAsync(undo.Id, default)).Code); Assert.Equal(1, platform.Writes);
    }
    /// <summary>실패한 쓰기는 Pending 원본을 보존합니다.</summary>
    [Fact]
    public async Task FailedWritePreservesPendingRecord()
    {
        var platform = new Registry { FailWrite = true }; var store = new ActionCenterTests.Store(); var service = Service(AdvancedOption.Mpo, platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Mpo, new ActionTarget.Advanced(AdvancedOption.Mpo, AdvancedSetting.Off), default)).Plan!;
        Assert.False((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.Equal(RollbackState.Pending, Assert.Single(store.Catalog.Records).State);
    }
    /// <summary>HAGS 부재·알 수 없는 형식·MPO 강제 On·다른 사용자 HKCU는 거절합니다.</summary>
    [Theory]
    [InlineData("hags")][InlineData("format")][InlineData("mpo-on")][InlineData("scope")][InlineData("wrong-id")]
    public async Task UnsupportedTargetsAreNotPrepared(string kind)
    {
        var platform = new Registry { Value = kind == "format" ? new(true, 1, [1, 2, 3, 4]) : AdvancedOptions.Absent };
        var option = kind == "hags" ? AdvancedOption.Hags : kind == "scope" ? AdvancedOption.GameMode : AdvancedOption.Mpo;
        var service = Service(option, platform, new(), () => kind == "scope" ? Session with { Scope = ActionUserScope.SystemOnly } : Session);
        var result = await service.PrepareApplyAsync(kind == "wrong-id" ? ActionId.GameMode : AdvancedOptions.Action(option),
            new ActionTarget.Advanced(option, kind == "mpo-on" ? AdvancedSetting.On : AdvancedSetting.Off), default);
        Assert.Null(result.Plan); Assert.Equal(0, platform.Writes);
    }
    /// <summary>명령 인자에는 강제 종료·지속 안전 모드 설정이 없습니다.</summary>
    [Theory]
    [InlineData(RestartDestination.Firmware, "/fw")][InlineData(RestartDestination.AdvancedStartup, "/o")]
    public void RestartArgumentsAreFixedAndNeverForce(RestartDestination destination, string mode)
    {
        var info = RestartPlatform.StartInfo(destination);
        Assert.Equal(new[] { "/r", mode, "/t", "0" }, info.ArgumentList); Assert.False(info.UseShellExecute);
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), info.FileName);
    }
    private sealed class Restart : IRestartPlatform
    {
        internal bool Uefi = true; internal int Calls; internal int ExitCode;
        public bool IsUefi() => Uefi;
        public Task<int?> RequestAsync(RestartDestination destination, IActionExecution execution)
        { execution.TryStartProcess(() => { Calls++; return true; }); return Task.FromResult<int?>(ExitCode); }
    }
    /// <summary>확인 단계는 재부팅을 요청하지 않고 별도 실행에서만 요청합니다.</summary>
    [Theory]
    [InlineData(0, "RestartRequested")][InlineData(1, "RestartRejected")]
    public async Task RestartReportsRequestRatherThanArrival(int code, string expected)
    {
        var native = new Restart { ExitCode = code };
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session, [new RestartActionAdapter(() => Session, native, _ => Task.CompletedTask)]);
        var plan = (await service.PrepareAsync(ActionId.Restart, new ActionTarget.Restart(RestartDestination.AdvancedStartup), false, default)).Plan!;
        Assert.Contains("직접 선택", plan.Preview.Details!.TargetLabel); Assert.Equal(0, native.Calls);
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal(expected, result.Code); Assert.Equal(1, native.Calls);
        var text = new ActionResultViewModel(result, ActionId.Restart, false, "");
        Assert.DoesNotContain("조치를 완료", text.Title);
    }
    /// <summary>앱 대기 중 취소는 요청 전에 끝나며 다른 작업도 차단됩니다.</summary>
    [Fact]
    public async Task CancelDuringCountdownNeverStartsShutdown()
    {
        var native = new Restart(); var entered = new TaskCompletionSource(); var gate = new OperationCoordinator();
        var adapter = new RestartActionAdapter(() => Session, native, async ct => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); });
        var service = new ActionCoordinator(gate, () => Session, [adapter]);
        var plan = (await service.PrepareAsync(ActionId.Restart, new ActionTarget.Restart(RestartDestination.Firmware), false, default)).Plan!;
        using var cancellation = new CancellationTokenSource();
        var task = service.ExecuteAsync(plan.Id, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.True(gate.State.IsBusy);
        Assert.Null(gate.TryAcquire(OperationKind.Scan)); cancellation.Cancel();
        var result = await task; Assert.False(result.Succeeded); Assert.Equal(0, native.Calls);
    }
    /// <summary>UEFI가 아닌 환경는 준비 단계에서 막습니다.</summary>
    [Fact]
    public async Task LegacyFirmwareIsRejected()
    {
        var native = new Restart { Uefi = false };
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session, [new RestartActionAdapter(() => Session, native, _ => Task.CompletedTask)]);
        var result = await service.PrepareAsync(ActionId.Restart, new ActionTarget.Restart(RestartDestination.Firmware), false, default);
        Assert.Equal("FirmwareUnavailable", result.Code); Assert.Equal(0, native.Calls);
    }
}
