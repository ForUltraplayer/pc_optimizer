/**
 * @file    : MainWindowLayoutTests.cs
 * @author  : rudals252
 * @brief   : 메인 창을 화면에 띄우지 않고 고정 폭으로 배치해, 긴 경로가 든 카드 문장이 가로로 넘치지 않고 줄바꿈되는지와 판정 배지 텍스트를 검증
 */

// 기본 패키지
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.App.Fakes;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// XAML 카드 템플릿의 줄바꿈·배지 표시를 레이아웃 계산만으로 검증합니다(창을 표시하지 않음).
/// </summary>
public sealed class MainWindowLayoutTests
{
    private const double LAYOUT_WIDTH = 900;
    private const double LAYOUT_HEIGHT = 2000;
    private const int PATH_SEGMENTS = 40;
    private const int MIN_WRAPPED_LINES = 2;

    private static readonly string LONG_PATH =
        @"C:\ProgramData\" + string.Join(@"\", Enumerable.Range(0, PATH_SEGMENTS).Select(i => $"very-long-folder-name-{i}"));

    /// <summary>
    /// 긴 경로가 든 CannotVerify를 돌려주는 규칙이 있는 뷰모델로 검사를 한 번 실행한다.
    /// </summary>
    private static MainViewModel CreateScannedViewModel()
    {
        var service = new ScanService(
            [new ThrowingProbe("fixture.failing") { Category = FindingCategory.Storage }],
            [new LongTextRule()],
            new ScanOptions(),
            new ScanReportVersions("test-app", "test-rules"),
            new FakeClock(),
            NullAppLogger.Instance,
            () => false);
        var vm = new MainViewModel(
            service,
            new ReportExporter(new PersonalDataScrubber(null, null, null)),
            new FixedExportPathPicker(null),
            new SettingsUriPolicy(NullAppLogger.Instance, _ => { }),
            new ImmediateUiDispatcher(),
            NullAppLogger.Instance);
        vm.StartScanCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        return vm;
    }

    /// <summary>
    /// 시각 트리에서 조건에 맞는 요소를 모두 찾는다.
    /// </summary>
    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// STA 스레드에서 작업을 실행하고 예외를 호출자에게 다시 던진다.
    /// </summary>
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("STA 레이아웃 검사 실패", failure);
        }
    }

    /// <summary>긴 경로 문장은 카드 폭 안에서 여러 줄로 줄바꿈되고, 판정은 배지 텍스트로 보인다.</summary>
    [Fact]
    public void 긴_경로는_줄바꿈되고_판정은_텍스트로_보인다()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow(CreateScannedViewModel());
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(LAYOUT_WIDTH, LAYOUT_HEIGHT));
            root.Arrange(new Rect(0, 0, LAYOUT_WIDTH, LAYOUT_HEIGHT));
            root.UpdateLayout();

            var texts = Descendants<TextBlock>(root).ToList();
            var evidence = Assert.Single(texts, t => t.Text.Contains(LONG_PATH, StringComparison.Ordinal));
            Assert.Equal(TextWrapping.Wrap, evidence.TextWrapping);
            Assert.True(evidence.ActualWidth <= LAYOUT_WIDTH, $"폭 {evidence.ActualWidth}");
            var singleLine = evidence.FontSize * evidence.FontFamily.LineSpacing;
            Assert.True(evidence.ActualHeight >= singleLine * MIN_WRAPPED_LINES, $"높이 {evidence.ActualHeight}");

            Assert.Contains(texts, t => t.Text == DisplayText.Verdict(Verdict.CannotVerify));
            window.Close();
        });
    }

    /// <summary>
    /// 긴 경로를 근거에 담은 Info를 내는 테스트용 규칙입니다.
    /// </summary>
    private sealed class LongTextRule : IRule
    {
        /// <inheritdoc />
        public string Id => "fixture.long-text";

        /// <inheritdoc />
        public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
        {
            return
            [
                new Finding("fixture:long", FindingCategory.Storage, "long path fixture", [], LONG_PATH, Verdict.Info, null, null, null, null, []),
            ];
        }
    }
}
