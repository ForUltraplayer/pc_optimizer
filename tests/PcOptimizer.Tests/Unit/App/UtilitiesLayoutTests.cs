/**
 * @file    : UtilitiesLayoutTests.cs
 * @author  : rudals252
 * @brief   : 유틸리티 탭 뷰의 실제 WPF 레이아웃 검증 — 분류 섹션과 카드가 그려지고, 카드 버튼은 공식 사이트 열기이며 절차 펼치기가 동작한다(오프스크린)
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

/// <summary>실제 카탈로그로 렌더하며 링크는 열지 않습니다.</summary>
public sealed class UtilitiesLayoutTests
{
    /// <summary>분류 제목 수와 카드 수가 카탈로그의 외부 도구와 같고, 첫 카드의 절차를 펼치면 절차 줄이 배치됩니다.</summary>
    [Fact]
    public void UtilitiesViewRendersGroupsAndCards()
    {
        ActionCenterLayoutTests.RunOnSta(() =>
        {
            var links = VendorLinkCatalogLoader.LoadEmbedded().Catalog!;
            var catalog = TroubleshootingCatalogLoader.LoadEmbedded(links).Catalog!;
            var service = new TroubleshootingService(new OperationCoordinator(), null, new RepairCommandRunner(null, @"C:\Windows\System32", "C:"), () => 0, _ => true, @"C:\Windows\System32");
            using var vm = new TroubleshootingViewModel(catalog, service, new WpfUiDispatcher(Dispatcher.CurrentDispatcher), _ => true, _ => true, links);
            var window = new Window { Content = new UtilitiesView { DataContext = vm }, Width = 900, Height = 800 };
            try
            {
                var root = (FrameworkElement)window.Content;
                var size = new Size(900, 800);
                root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                ActionCenterLayoutTests.Render(window, "utilities", 1);
                var headings = ActionCenterLayoutTests.Descendants<TextBlock>(root).Where(t => vm.UtilityGroups.Any(g => g.Category == t.Text)).ToList();
                Assert.Equal(vm.UtilityGroups.Count, headings.Count);
                var external = catalog.Tools.Where(t => t.Mode == ToolMode.ExternalGuide).ToList();
                var titles = ActionCenterLayoutTests.Descendants<TextBlock>(root).Where(t => external.Any(x => x.Name == t.Text)).ToList();
                Assert.Equal(external.Count, titles.Count);
                var buttons = ActionCenterLayoutTests.Descendants<Button>(root).Where(b => b.Content is "공식 사이트 열기").ToList();
                Assert.Equal(external.Count, buttons.Count);
                var first = vm.UtilityGroups[0].Cards[0];
                first.ToggleStepsCommand.Execute(null); root.UpdateLayout();
                Assert.Contains(ActionCenterLayoutTests.Descendants<TextBlock>(root), t => t.Text == first.Steps[0] && t.ActualHeight > 0);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
