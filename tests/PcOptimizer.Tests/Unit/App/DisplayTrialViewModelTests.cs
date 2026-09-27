/**
 * @file    : DisplayTrialViewModelTests.cs
 * @author  : rudals252
 * @brief   : 주사율 준비 확인 분리·유지·창 닫기 취소·결과/복구 목록 연결 회귀
 */
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions.Display;
using PcOptimizer.Tests.Unit.Probes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 사용자 화면/설정 대신 플랫폼 대역으로 명령 경계를 검사합니다.</summary>
public sealed class DisplayTrialViewModelTests
{
    /// <summary>선택/준비/확인은 별개이며 유지 후 재검사와 되돌리기까지 연결됩니다.</summary>
    [Fact] public async Task ExplicitPreviewKeepRescanAndUndo()
    {
        var platform = new DisplayTrialTests.Platform(); var gate = new OperationCoordinator(); var store = new ActionCenterTests.Store();
        var service = new DisplayTrialCoordinator(platform, gate, store, () => DisplayTrialTests.Session);
        var rescans = 0;
        using var vm = new DisplayTrialViewModel(service, gate, new ActionCenterTests.Dispatch(), () => { rescans++; return Task.CompletedTask; });
        await vm.RefreshCommand.ExecuteAsync(null); vm.Selected = Assert.Single(vm.Choices);
        await vm.PrepareCommand.ExecuteAsync(null); Assert.True(vm.HasPreview); Assert.Equal(0, platform.Writes);
        var run = vm.ExecuteCommand.ExecuteAsync(null); await Waiting(service); vm.Tick();
        Assert.False(vm.PrepareCommand.CanExecute(null)); Assert.Contains("초", vm.Countdown);
        vm.KeepCommand.Execute(null); await run;
        Assert.Contains("저장했습니다", vm.Status); Assert.Equal(1, rescans); Assert.True(Assert.Single(vm.Records).CanRestore);
        vm.PrepareRestoreCommand.Execute(vm.Records[0]); Assert.True(vm.HasPreview); Assert.Equal(120, platform.Current.Mode.RefreshHz);
        await vm.ExecuteCommand.ExecuteAsync(null); Assert.Equal(60, platform.Current.Mode.RefreshHz); Assert.Equal(2, rescans);
        Assert.False(vm.Records[0].CanRestore);
    }
    /// <summary>창 닫기는 조치를 취소하고 원복 결과를 같은 모델에 남깁니다.</summary>
    [Fact] public async Task CloseRequestsRollbackAndPreservesResult()
    {
        var platform = new DisplayTrialTests.Platform(); var gate = new OperationCoordinator();
        var service = new DisplayTrialCoordinator(platform, gate, new ActionCenterTests.Store(), () => DisplayTrialTests.Session);
        using var vm = new DisplayTrialViewModel(service, gate, new ActionCenterTests.Dispatch(), () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null); vm.Selected = vm.Choices[0]; await vm.PrepareCommand.ExecuteAsync(null);
        var run = vm.ExecuteCommand.ExecuteAsync(null); await Waiting(service);
        vm.RequestClose(); await run;
        Assert.False(vm.IsWorking); Assert.Equal(60, platform.Current.Mode.RefreshHz); Assert.Contains("원래 표시 설정", vm.Status);
    }
    private static async Task Waiting(DisplayTrialCoordinator service)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (service.Status is null) { if (DateTime.UtcNow >= end) { throw new TimeoutException("fixture wait"); } await Task.Delay(1); }
    }
}
