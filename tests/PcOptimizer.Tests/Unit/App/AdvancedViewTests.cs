/**
 * @file    : AdvancedViewTests.cs
 * @author  : rudals252
 * @brief   : 실제 PC 접근 없는 고급 화면 관측·명령 전달·좁은 창 렌더 검증
 */
using System.Windows;
using System.Windows.Controls;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>표시 테스트는 대역 관측만 사용합니다.</summary>
public sealed class AdvancedViewTests
{
    private static IReadOnlyList<AdvancedObservation> Observations() => Enum.GetValues<AdvancedOption>()
        .Select(o => new AdvancedObservation(o, o == AdvancedOption.Hags ? null : AdvancedSetting.Default, o != AdvancedOption.Hags,
            "저장된 설정값만 확인했습니다. 실제 GPU 처리 상태와 재부팅 후 효력은 별도 확인해야 합니다.")).ToArray();
    /// <summary>읽기 전·미지원 버튼은 비활성이고 등록된 선택만 확인 화면으로 보냅니다.</summary>
    [Fact]
    public async Task ObservationEnablesOnlySupportedChoicesAndNeverExecutes()
    {
        AdvancedChoice? selected = null; var gate = new OperationCoordinator();
        using var vm = new AdvancedViewModel(gate, Observations, choice => { selected = choice; return Task.CompletedTask; }, new ActionCenterTests.Dispatch(), _ => true);
        var mpo = vm.Cards.Single(c => c.Option == AdvancedOption.Mpo);
        Assert.False(vm.PrepareCommand.CanExecute(mpo.Choices[0]));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(selected); Assert.True(vm.PrepareCommand.CanExecute(mpo.Choices[0]));
        Assert.False(vm.Cards.Single(c => c.Option == AdvancedOption.Hags).CanChange);
        Assert.False(vm.PrepareCommand.CanExecute(new AdvancedChoice("주입", ActionId.Power, new ActionTarget.Power(Guid.NewGuid()))));
        await vm.PrepareCommand.ExecuteAsync(mpo.Choices[0]); Assert.Equal(mpo.Choices[0], selected);
        using var busy = gate.TryAcquire(OperationKind.Apply);
        Assert.False(vm.RefreshCommand.CanExecute(null)); Assert.False(vm.PrepareCommand.CanExecute(mpo.Choices[0]));
    }
    /// <summary>일부 조회 실패 시 예전 켜짐 상태를 새 결과로 남기지 않습니다.</summary>
    [Fact]
    public async Task FailedRefreshDisablesPreviouslyAvailableActions()
    {
        var fail = false;
        using var vm = new AdvancedViewModel(new OperationCoordinator(), () => fail ? throw new InvalidOperationException() : Observations(),
            _ => Task.CompletedTask, new ActionCenterTests.Dispatch(), _ => false);
        await vm.RefreshCommand.ExecuteAsync(null); Assert.Contains(vm.Cards, c => c.CanChange);
        fail = true; await vm.RefreshCommand.ExecuteAsync(null); Assert.All(vm.Cards, c => Assert.False(c.CanChange));
    }
    /// <summary>좁은 뷰에서 내부 ScrollViewer 없이 선택 버튼과 한국어 설명을 표시합니다.</summary>
    [Theory]
    [InlineData(1.0)][InlineData(1.5)][InlineData(2.0)]
    public void AdvancedPageRendersAtThreeScales(double scale) => ActionCenterLayoutTests.RunOnSta(async () =>
    {
        using var vm = new AdvancedViewModel(new OperationCoordinator(), Observations, _ => Task.CompletedTask, new ActionCenterTests.Dispatch(), _ => true);
        await vm.RefreshCommand.ExecuteAsync(null);
        var view = new AdvancedView { DataContext = vm };
        var window = new Window { Content = view, Width = 440, Height = 720 };
        try
        {
            ActionCenterLayoutTests.Render(window, "advanced-" + scale, scale);
            Assert.Empty(ActionCenterLayoutTests.Descendants<ScrollViewer>(view));
            var buttons = ActionCenterLayoutTests.Descendants<Button>(view).ToArray();
            Assert.Contains(buttons, b => Equals(b.Content, "BIOS/UEFI 진입 확인"));
            Assert.Contains(buttons, b => Equals(b.Content, "끄기 확인") && b.IsEnabled);
            Assert.Contains(buttons, b => Equals(b.Content, "켜기 확인") && !b.IsEnabled);
            var explanation = ActionCenterLayoutTests.Descendants<TextBlock>(view).First(t => t.Text.StartsWith("화면 깜박임", StringComparison.Ordinal));
            Assert.Equal(TextWrapping.Wrap, explanation.TextWrapping);
            Assert.True(explanation.ActualHeight > 30, "좁은 화면의 설명은 여러 줄로 표시해야 합니다.");
        }
        finally { window.Close(); }
    });
}
