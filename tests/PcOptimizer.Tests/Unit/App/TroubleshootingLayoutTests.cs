/**
 * @file    : TroubleshootingLayoutTests.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함 창의 실제 WPF 레이아웃 검증 — 증상 목록·도구 카드·절차 펼치기·출력 영역 표시(오프스크린, 명령 실행 없음)
 */
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Troubleshooting;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Troubleshooting;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>창을 화면에 띄우지 않고 실제 카탈로그로 렌더합니다.</summary>
public sealed class TroubleshootingLayoutTests
{
    /// <summary>증상 8개+모든 도구 버튼, 첫 증상의 카드가 그려지고, 절차 펼치기 뒤 절차 줄이 보이며, 출력 영역은 실행 전에는 숨겨집니다.</summary>
    [Fact]
    public void WindowRendersSymptomsCardsAndSteps()
    {
        ActionCenterLayoutTests.RunOnSta(() =>
        {
            var links = VendorLinkCatalogLoader.LoadEmbedded().Catalog!;
            var catalog = TroubleshootingCatalogLoader.LoadEmbedded(links).Catalog!;
            var service = new TroubleshootingService(new OperationCoordinator(), null, new RepairCommandRunner(null, @"C:\Windows\System32", "C:"), () => 0, _ => true, @"C:\Windows\System32");
            using var vm = new TroubleshootingViewModel(catalog, service, new WpfUiDispatcher(Dispatcher.CurrentDispatcher), _ => true, _ => true, links);
            var window = new TroubleshootingWindow(vm);
            try
            {
                ActionCenterLayoutTests.Render(window, "troubleshooting-slow", 1);
                var root = (FrameworkElement)window.Content;
                var symptomButtons = ActionCenterLayoutTests.Descendants<Button>(root).Where(b => b.Content is string s && catalog.Symptoms.Any(x => x.Title == s)).ToList();
                Assert.Equal(catalog.Symptoms.Count, symptomButtons.Count);
                var title = ActionCenterLayoutTests.Descendants<TextBlock>(root).Single(t => System.Windows.Automation.AutomationProperties.GetAutomationId(t) == "SymptomTitle");
                Assert.Equal("PC가 느려요", title.Text);
                var output = ActionCenterLayoutTests.Descendants<Border>(root).Single(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "OutputPanel");
                Assert.Equal(Visibility.Collapsed, output.Visibility);
                var cardTitles = ActionCenterLayoutTests.Descendants<TextBlock>(root).Where(t => vm.Cards.Any(c => c.Title == t.Text)).ToList();
                Assert.Equal(vm.Cards.Count, cardTitles.Count);
                Assert.DoesNotContain(ActionCenterLayoutTests.Descendants<TextBlock>(root), t => t.Text.StartsWith("1. ", StringComparison.Ordinal) && t.IsVisible);
                vm.Cards[0].ToggleStepsCommand.Execute(null);
                root.UpdateLayout();
                ActionCenterLayoutTests.Render(window, "troubleshooting-steps", 1);
                Assert.Contains(ActionCenterLayoutTests.Descendants<TextBlock>(root), t => t.Text == vm.Cards[0].Steps[0] && t.IsVisible);
                vm.SelectSymptomCommand.Execute(vm.Symptoms.Last());
                root.UpdateLayout();
                Assert.Equal("모든 도구 보기", title.Text);
                Assert.Equal(catalog.Tools.Count, vm.Cards.Count);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
