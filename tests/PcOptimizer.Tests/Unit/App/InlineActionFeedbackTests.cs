/**
 * @file    : InlineActionFeedbackTests.cs
 * @author  : rudals252
 * @brief   : 클릭한 위치의 진행·거절·별도 실행 확인·원복 및 결과 유지 회귀
 */
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Advanced;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 사용자 파일·설정을 바꾸지 않고 공통 조율기를 통해 검증합니다.</summary>
public sealed class InlineActionFeedbackTests
{
    private sealed class TempAdapter : IActionAdapter
    {
        internal int Executions;
        internal bool Reject;
        internal TaskCompletionSource? Release;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ActionDefinition Definition => new(ActionId.UserFiles, ActionScope.CurrentUser);
        public async Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
        {
            Entered.TrySetResult(); if (Release is not null) { await Release.Task.WaitAsync(ct); }
            if (Reject) { throw new ActionUnavailableException("NoEligibleFiles"); }
            return new(target, "오래된 임시파일을 정리합니다.", "최근 파일은 남깁니다.", new("시험용 임시파일", "별도 실행 확인 후 정리합니다.", 1024));
        }
        public Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
        { execution.MarkStarted(); Executions++; return Task.FromResult(new ActionResult(plan.Id, true, true, "Completed")); }
    }
    private static ActionChoice TempChoice(string key = "fixture-temp") => new(ActionId.UserFiles, new ActionTarget.Files(key), "오래된 임시파일 정리", "공간 확보");
    private static ActionCenterViewModel Create(TempAdapter adapter, params ActionChoice[] choices) => new(
        new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(), () => ActionCenterTests.Session, [adapter], []),
        new ActionCenterTests.Dispatch(), () => Task.CompletedTask, choices);
    /// <summary>오래된 임시파일도 대상 확인만으로 삭제하지 않으며 진행·확인이 같은 카드에 나타납니다.</summary>
    [Fact]
    public async Task TempCleanupRequiresSeparateExecuteAndShowsLocalProgress()
    {
        var adapter = new TempAdapter { Release = new(TaskCreationOptions.RunContinuationsAsynchronously) }; var choice = TempChoice();
        using var vm = Create(adapter, choice); var feedback = vm.FeedbackFor(choice);
        var prepare = vm.PrepareChoiceCommand.ExecuteAsync(choice); await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(feedback.IsWorking); Assert.Contains("확인", feedback.Status); Assert.False(vm.ExecuteCommand.CanExecute(null));
        Assert.Equal(0, adapter.Executions); adapter.Release.SetResult(); await prepare;
        Assert.Same(vm.Preview, feedback.Preview); Assert.Null(vm.StandaloneFeedback.Preview); Assert.False(vm.UsesStandaloneFeedback);
        Assert.Equal(0, adapter.Executions); Assert.True(vm.ExecuteCommand.CanExecute(null));
        await vm.ExecuteCommand.ExecuteAsync(null); Assert.Equal(1, adapter.Executions); Assert.True(feedback.HasResult); Assert.False(feedback.CanUndo);
    }
    /// <summary>거절 이유가 화면 상단에만 표시되지 않고 누른 카드에 남습니다.</summary>
    [Fact]
    public async Task RejectionIsVisibleAtSourceAndNeverExecutes()
    {
        var adapter = new TempAdapter { Reject = true }; var choice = TempChoice(); using var vm = Create(adapter, choice);
        await vm.PrepareChoiceCommand.ExecuteAsync(choice); var feedback = vm.FeedbackFor(choice);
        Assert.True(feedback.HasContent); Assert.Contains("정리할 파일이 없습니다", feedback.Status);
        Assert.False(feedback.HasPreview); Assert.False(feedback.IsWorking); Assert.Equal(0, adapter.Executions);
    }
    /// <summary>다른 항목을 준비해도 이전 완료 결과는 해당 카드에 유지됩니다.</summary>
    [Fact]
    public async Task ResultsStayAtTheirOwnCardsAndOnlyOneConfirmationIsActive()
    {
        var adapter = new TempAdapter(); var first = TempChoice(); var second = TempChoice("fixture-second"); using var vm = Create(adapter, first, second);
        await vm.PrepareChoiceCommand.ExecuteAsync(first); await vm.ExecuteCommand.ExecuteAsync(null); var result = vm.FeedbackFor(first).Result;
        await vm.PrepareChoiceCommand.ExecuteAsync(second); Assert.Same(result, vm.FeedbackFor(first).Result); Assert.True(vm.FeedbackFor(second).HasPreview);
        await vm.PrepareChoiceCommand.ExecuteAsync(first); Assert.False(vm.FeedbackFor(second).HasPreview); Assert.True(vm.FeedbackFor(first).HasPreview);
        vm.DismissPreviewCommand.Execute(null); Assert.False(vm.FeedbackFor(first).HasPreview); Assert.Equal(1, adapter.Executions);
    }
    /// <summary>복원 가능한 설정만 같은 카드에서 되돌리기를 준비하며 바로 쓰지 않습니다.</summary>
    [Fact]
    public async Task SettingCanPrepareUndoAtSameCardWithoutImmediateWrite()
    {
        var native = new AdvancedActionTests.Registry(); var session = ActionCenterTests.Session;
        var adapter = new AdvancedRegistryAdapter(AdvancedOption.Mpo, () => session, native);
        var workflow = new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(), () => session, [], [adapter]);
        var choice = new ActionChoice(ActionId.Mpo, new ActionTarget.Advanced(AdvancedOption.Mpo, AdvancedSetting.Off), "MPO", "화면 설정");
        using var vm = new ActionCenterViewModel(workflow, new ActionCenterTests.Dispatch(), () => Task.CompletedTask, [choice]);
        await vm.PrepareChoiceCommand.ExecuteAsync(choice); await vm.ExecuteCommand.ExecuteAsync(null);
        var feedback = vm.FeedbackFor(choice); Assert.True(feedback.CanUndo); Assert.Equal(1, native.Writes);
        await vm.UndoFeedbackCommand.ExecuteAsync(feedback); Assert.True(feedback.Preview!.IsRestore); Assert.Equal(1, native.Writes);
        await vm.ExecuteCommand.ExecuteAsync(null); Assert.Equal(2, native.Writes); Assert.False(feedback.CanUndo); Assert.True(feedback.Result!.IsRestore);
    }
    /// <summary>공식 연결 실패도 반복 클릭하거나 다른 카드를 눌러도 해당 위치에 표시합니다.</summary>
    [Fact]
    public void ManualLinkFailureStaysAtItsOwnCard()
    {
        using var vm = new ActionCenterViewModel(new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(), () => ActionCenterTests.Session, [], []),
            new ActionCenterTests.Dispatch(), () => Task.CompletedTask, openSettings: _ => false);
        var choice = vm.ManualChoices.First(c => c.HasSettings); vm.OpenManualCommand.Execute(choice);
        Assert.Contains("열지 못했습니다", vm.FeedbackFor(choice).Status); Assert.False(vm.FeedbackFor(choice).HasResult);
        vm.OpenManualCommand.Execute(choice); Assert.Contains("열지 못했습니다", vm.FeedbackFor(choice).Status);
        var second = vm.ManualChoices.Last(c => c.HasSettings); vm.OpenManualCommand.Execute(second);
        Assert.Contains("열지 못했습니다", vm.FeedbackFor(second).Status);
    }
    /// <summary>늦은 재검사 알림은 그 작업의 카드에 남고 새로 선택한 항목으로 이동하지 않습니다.</summary>
    [Fact]
    public async Task DelayedRescanStaysAtCompletedCard()
    {
        var adapter = new TempAdapter(); var first = TempChoice(); var second = TempChoice("second");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var vm = new ActionCenterViewModel(
            new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(), () => ActionCenterTests.Session, [adapter], []),
            new ActionCenterTests.Dispatch(), () => { entered.TrySetResult(); return finish.Task; }, [first, second]);
        await vm.PrepareChoiceCommand.ExecuteAsync(first);
        var execute = vm.ExecuteCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await vm.PrepareChoiceCommand.ExecuteAsync(second);
            Assert.True(vm.FeedbackFor(second).HasPreview);
        }
        finally { finish.TrySetResult(); }
        await execute.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("재검사가 끝났습니다", vm.FeedbackFor(first).RescanStatus);
        Assert.Empty(vm.FeedbackFor(second).RescanStatus);
    }
}
