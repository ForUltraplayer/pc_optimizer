/**
 * @file    : GpuViewTests.cs
 * @author  : rudals252
 * @brief   : GPU 대상 선택·재관측·확인 전 무실행·네이티브 지연 시 공통 관문 검증
 */
using System.Windows;
using System.Windows.Controls;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>GPU 화면의 모든 데이터는 대역이며 실제 드라이버를 호출하지 않습니다.</summary>
public sealed class GpuViewTests
{
    internal static GpuOptionsResult Targets(GpuFeature feature) => new([
        new(feature, "one", "시험 게임 / GPU 1", "영향 설명", [new(0, "끄기"), new(1, "켜기")]),
        new(feature, "two", "시험 게임 / GPU 2", "영향 설명", [new(0, "끄기"), new(1, "켜기")])], "대상 두 개를 찾았습니다.");
    private static GpuSettingState State => new("fixture", 0, 1, true);
    /// <summary>선택만으로 실행하지 않으며 대상이 바뀌면 다시 읽어야 합니다.</summary>
    [Fact]
    public async Task SelectionRequiresFreshReadAndSeparateConfirmation()
    {
        AdvancedChoice? selected = null; var reads = 0;
        using var vm = new GpuCardViewModel(GpuFeature.AmdVideo, new OperationCoordinator(), _ => Targets(GpuFeature.AmdVideo),
            _ => { reads++; return State; }, choice => { selected = choice; return Task.CompletedTask; }, new ActionCenterTests.Dispatch());
        Assert.False(vm.PrepareCommand.CanExecute(null)); Assert.Contains("확인 전", vm.StateText);
        await vm.DiscoverCommand.ExecuteAsync(null); Assert.Null(selected); Assert.Equal(1, reads); Assert.Contains("켜기", vm.StateText);
        vm.SelectedTarget = vm.Targets[1]; Assert.False(vm.PrepareCommand.CanExecute(null)); Assert.Null(vm.SelectedChoice);
        await vm.ReadCommand.ExecuteAsync(null); vm.SelectedChoice = vm.Choices[0];
        await vm.PrepareCommand.ExecuteAsync(null); Assert.Equal(new ActionTarget.Gpu(GpuFeature.AmdVideo, "two", 0), selected!.Target);
        Assert.Equal(2, reads); Assert.False(vm.PrepareCommand.CanExecute(null));
    }
    /// <summary>실패 시 이전 상태를 유지하지 않으며 변조 선택값도 확인 화면으로 보내지 않습니다.</summary>
    [Fact]
    public async Task FailureAndInjectedChoiceDisableApply()
    {
        var fail = false;
        using var vm = new GpuCardViewModel(GpuFeature.AmdVideo, new OperationCoordinator(), _ => Targets(GpuFeature.AmdVideo),
            _ => fail ? throw new InvalidOperationException() : State, _ => throw new InvalidOperationException("Must not prepare"), new ActionCenterTests.Dispatch());
        await vm.DiscoverCommand.ExecuteAsync(null); vm.SelectedChoice = new(99, "invalid"); Assert.False(vm.PrepareCommand.CanExecute(null));
        fail = true; await vm.ReadCommand.ExecuteAsync(null); Assert.Contains("확인 불가", vm.StateText); Assert.False(vm.PrepareCommand.CanExecute(null));
    }
    /// <summary>제한 시간이 지나 UI에 반환돼도 실제 네이티브 읽기 종료 전에는 조치가 잠깁니다.</summary>
    [Fact]
    public async Task TimedOutReadKeepsGateUntilRealExitAndDiscardsLateResult()
    {
        var gate = new OperationCoordinator(); using var exit = new ManualResetEventSlim();
        var finished = new TaskCompletionSource();
        gate.Changed += (_, _) => { if (!gate.State.IsBusy) { finished.TrySetResult(); } };
        using var vm = new GpuCardViewModel(GpuFeature.AmdVideo, gate, _ => { exit.Wait(); return Targets(GpuFeature.AmdVideo); },
            _ => State, _ => Task.CompletedTask, new ActionCenterTests.Dispatch(), TimeSpan.FromMilliseconds(50));
        try
        {
            await vm.DiscoverCommand.ExecuteAsync(null); Assert.Equal(OperationPhase.Draining, gate.State.Phase);
            Assert.Null(gate.TryAcquire(OperationKind.Apply)); Assert.Empty(vm.Targets); Assert.Contains("초과", vm.Status);
        }
        finally { exit.Set(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Empty(vm.Targets); Assert.False(vm.PrepareCommand.CanExecute(null));
    }
    /// <summary>고급 GPU 카드도 좁은 창과 세 배율에서 확인 버튼·설명 줄바꿈을 유지합니다.</summary>
    [Theory]
    [InlineData(1.0)][InlineData(1.5)][InlineData(2.0)]
    public void GpuCardsRenderWithoutNestedBodyScroll(double scale) => ActionCenterLayoutTests.RunOnSta(async () =>
    {
        using var vm = new AdvancedViewModel(new OperationCoordinator(), () => [], _ => Task.CompletedTask, new ActionCenterTests.Dispatch(), _ => true,
            (feature, _) => Targets(feature), (_, _) => State);
        foreach (var card in vm.GpuCards) { await card.DiscoverCommand.ExecuteAsync(null); }
        var view = new AdvancedView { DataContext = vm }; var scroll = new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var window = new Window { Content = scroll, Width = 440, Height = 720 };
        try
        {
            ActionCenterLayoutTests.Render(window, "advanced-top-" + scale, scale);
            scroll.ScrollToEnd(); scroll.UpdateLayout();
            ActionCenterLayoutTests.Render(window, "advanced-gpu-" + scale, scale);
            Assert.Equal(3, ActionCenterLayoutTests.Descendants<Button>(view).Count(b => Equals(b.Content, "선택한 설정 적용 확인")));
            var explanation = ActionCenterLayoutTests.Descendants<TextBlock>(view).First(t => t.Text.StartsWith("RTX 영상 초고해상도를", StringComparison.Ordinal));
            Assert.Equal(TextWrapping.Wrap, explanation.TextWrapping); Assert.True(explanation.ActualHeight > 30);
        }
        finally { window.Close(); }
    });
}
