/**
 * @file    : MainWindowLayoutTests.cs
 * @author  : rudals252
 * @brief   : 메인 창을 화면에 띄우지 않고 고정 폭으로 배치해, 긴 경로가 든 카드 문장이 가로로 넘치지 않고 줄바꿈되는지, 판정 배지 텍스트, 관리자 권한 재검사 버튼 활성·배너 표시, 공식 링크 버튼 표시, 카드의 안전 배지·설명 3줄 렌더, 요약 타일 두 개(바로 할 수 있는 것은 0이면 숨김)와 정리 창 버튼 노출 조건을 검증
 */

// 기본 패키지
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.App.Fakes;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// XAML 카드 템플릿의 줄바꿈·배지 표시를 레이아웃 계산만으로 검증합니다(창을 표시하지 않음).
/// </summary>
public sealed class MainWindowLayoutTests
{
    /// <summary>진단과 정리 화면을 세 배율로 렌더링합니다. 실제 모니터 DPI 변경 검증과는 별개입니다.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void RenderDiagnosticAndCleanupAtThreeScales(double scale)
    {
        RunOnSta(() =>
        {
            var main = new MainWindow(CreateScannedViewModel(overview: true, rule: new ImprovementRule()));
            var idle = new MainWindow(CreateScannedViewModel(overview: true, scan: false));
            var empty = new MainWindow(CreateScannedViewModel(overview: true));
            var narrow = new MainWindow(CreateScannedViewModel(overview: true, rule: new ImprovementRule()));
            var tools = new CacheToolsWindow();
            var completed = new MainWindow(CreateScannedViewModel(overview: true, rule: new ImprovementRule()));
            ((MainViewModel)completed.DataContext).LastCleanupOutcome = new CleanupOutcomeViewModel("npm",
                new(true, 2_000_000, "Completed") { BeforeBytes = 1_202_000_000 });
            var outcomeTools = new CacheToolsWindow();
            outcomeTools.ViewModel.Outcome = ((MainViewModel)completed.DataContext).LastCleanupOutcome;
            tools.ViewModel.Message = "npm 캐시\n실행할 도구: C:\\Program Files\\nodejs\\node.exe\n캐시 위치: C:\\Users\\예시\\AppData\\Local\\npm-cache\n관측한 논리 크기: 123.4 MB\n이 확인 결과는 5분 동안 유효합니다.";
            foreach (var (window, name, size) in new (Window, string, Size)[]
            {
                (main, "diagnostic", new Size(1100, 800)), (tools, "cache-tools", new Size(720, 760)),
                (idle, "before-scan", new Size(1100, 800)), (empty, "no-candidates", new Size(1100, 800)),
                (narrow, "diagnostic-narrow", new Size(720, 680)),
                (completed, "cleanup-summary", new Size(1100, 840)), (outcomeTools, "cleanup-completed", new Size(720, 760)),
            })
            {
                var root = (FrameworkElement)window.Content;
                if (root is Panel panel && panel.Background is null) { panel.Background = Brushes.White; }
                if (root is Control control) { control.Background = Brushes.White; }
                root.Measure(size);
                root.Arrange(new Rect(size));
                root.UpdateLayout();
                Assert.NotEmpty(Descendants<TextBlock>(root));
                var output = Environment.GetEnvironmentVariable("PCOPTIMIZER_UI_ARTIFACTS");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, $"{name}-{scale * 100:0}.png"));
                    encoder.Save(file);
                }
                window.Close();
            }
        });
    }
    private const double LAYOUT_WIDTH = 900;
    private const double LAYOUT_HEIGHT = 2000;
    private const int PATH_SEGMENTS = 40;
    private const int MIN_WRAPPED_LINES = 2;
    private const double CARD_LAYOUT_WIDTH = 720;
    private const double CARD_LAYOUT_HEIGHT = 800;
    private const int EXPLANATION_LINES = 3;

    private const string LINK_URL = "https://www.nvidia.com/en-us/drivers/details/279803/";
    private const string LINK_LABEL = "공식 배포 설명";

    private static readonly string LONG_PATH =
        @"C:\ProgramData\" + string.Join(@"\", Enumerable.Range(0, PATH_SEGMENTS).Select(i => $"very-long-folder-name-{i}"));

    /// <summary>
    /// 긴 경로가 든 CannotVerify를 돌려주는 규칙이 있는 뷰모델로 검사를 한 번 실행한다.
    /// </summary>
    private static MainViewModel CreateScannedViewModel(bool isElevated = false, ScanLaunchMode launchMode = ScanLaunchMode.Normal,
        bool overview = false, bool scan = true, IRule? rule = null, IActionAvailability? availability = null)
    {
        var elevation = new FakeElevationState(isElevated);
        var service = new ScanService(
            [new ThrowingProbe("fixture.failing") { Category = FindingCategory.Storage }],
            [rule ?? new LongTextRule()],
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
            new LinkPolicy(null, NullAppLogger.Instance, _ => { }),
            new ImmediateUiDispatcher(),
            NullAppLogger.Instance,
            elevation,
            new ElevationRelauncher(new RecordingProcessStarter(), elevation, () => null, NullAppLogger.Instance),
            launchMode,
            availability ?? new FixedActionAvailability(false));
        if (scan)
        {
            vm.StartScanCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            // 고정 규칙이 예외(예: Candidate 불변식 위반)로 조용히 실패하면 카드가 빠진 채 통과할 수 있으므로 막는다.
            var findings = vm.LastResult?.Report.Findings ?? [];
            Assert.DoesNotContain(findings, f => f.Id.StartsWith(RuleEvaluator.RULE_FAILURE_FINDING_ID_PREFIX, StringComparison.Ordinal));
        }
        vm.ShowAllResults = !overview;
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
    /// 시각 트리의 모든 <see cref="TextBlock"/>을 찾는다.
    /// </summary>
    private static IEnumerable<TextBlock> FindTextBlocks(DependencyObject root) => Descendants<TextBlock>(root);

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
            var model = CreateScannedViewModel();
            model.Cards.Single(c => c.Title == "long path fixture").ToggleDetailsCommand.Execute(null);
            var window = new MainWindow(model);
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(LAYOUT_WIDTH, LAYOUT_HEIGHT));
            root.Arrange(new Rect(0, 0, LAYOUT_WIDTH, LAYOUT_HEIGHT));
            root.UpdateLayout();

            var texts = Descendants<TextBlock>(root).ToList();
            var evidence = Assert.Single(texts, t => t.Text.Contains(LONG_PATH, StringComparison.Ordinal) && t.Visibility == Visibility.Visible);
            Assert.Equal(TextWrapping.Wrap, evidence.TextWrapping);
            Assert.True(evidence.ActualWidth <= LAYOUT_WIDTH, $"폭 {evidence.ActualWidth}");
            var singleLine = evidence.FontSize * evidence.FontFamily.LineSpacing;
            Assert.True(evidence.ActualHeight >= singleLine * MIN_WRAPPED_LINES, $"높이 {evidence.ActualHeight}");

            Assert.Contains(texts, t => t.Text == DisplayText.Verdict(Verdict.CannotVerify));
            window.Close();
        });
    }

    /// <summary>카드에 안전 배지 텍스트와 설명 3줄이 렌더되고 가로로 넘치지 않는다.</summary>
    [Fact]
    public void CardRendersSafetyBadgeAndExplanationLines()
    {
        RunOnSta(() =>
        {
            var model = CreateScannedViewModel(overview: true, rule: new ImprovementRule());
            var explainedCards = model.VisibleCards.Count(c => c.HasExplanation);
            Assert.True(explainedCards > 0);
            var window = new MainWindow(model);
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(CARD_LAYOUT_WIDTH, CARD_LAYOUT_HEIGHT));
            root.Arrange(new Rect(0, 0, CARD_LAYOUT_WIDTH, CARD_LAYOUT_HEIGHT));
            root.UpdateLayout();
            var badge = FindTextBlocks(root).First(t => t.Text == Strings.Safety_Safe);
            Assert.True(badge.ActualWidth > 0);
            var lines = FindTextBlocks(root).Where(t => t.Text is "테스트 설명" or "테스트 효과" or "테스트 주의").ToList();
            // 고정 규칙의 Candidate 카드마다 같은 설명 3줄을 쓰므로 카드 수 × 3줄이 렌더되어야 한다.
            Assert.Equal(EXPLANATION_LINES * explainedCards, lines.Count);
            Assert.Equal(EXPLANATION_LINES, lines.Select(t => t.Text).Distinct().Count());
            Assert.All(lines, t => Assert.Equal(TextWrapping.Wrap, t.TextWrapping));
            Assert.All(lines, t => Assert.True(t.ActualWidth > 0 && t.ActualWidth <= CARD_LAYOUT_WIDTH, $"폭 {t.ActualWidth}"));
            window.Close();
        });
    }

    /// <summary>요약 타일은 두 개이며, 바로 할 수 있는 것이 0이면 그 타일을 숨기고 직접 해야 하는 것만 보인다. 정리 창 버튼은 보호 위치 도구가 있을 때만 보인다.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void SummaryTilesSplitDoNowAndDoManually(bool executable, bool toolsAvailable)
    {
        RunOnSta(() =>
        {
            var model = CreateScannedViewModel(overview: true, rule: new ImprovementRule(),
                availability: new FixedActionAvailability(executable, toolsAvailable));
            var window = new MainWindow(model);
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(CARD_LAYOUT_WIDTH, CARD_LAYOUT_HEIGHT));
            root.Arrange(new Rect(0, 0, CARD_LAYOUT_WIDTH, CARD_LAYOUT_HEIGHT));
            root.UpdateLayout();

            var doNow = Assert.Single(Descendants<Border>(root), b => AutomationProperties.GetAutomationId(b) == "DoNowTile");
            var doManually = Assert.Single(Descendants<Border>(root), b => AutomationProperties.GetAutomationId(b) == "DoManuallyTile");
            var toolsButton = Assert.Single(Descendants<Button>(root), b => AutomationProperties.GetAutomationId(b) == "OpenCacheToolsButton");
            Assert.Equal(executable ? Visibility.Visible : Visibility.Collapsed, doNow.Visibility);
            Assert.Equal(Visibility.Visible, doManually.Visibility);
            Assert.Equal(toolsAvailable ? Visibility.Visible : Visibility.Collapsed, toolsButton.Visibility);
            var texts = FindTextBlocks(doManually).Select(t => t.Text).ToList();
            Assert.Contains(model.DoManuallyText, texts);
            Assert.Contains(Strings.Overview_DoManuallyHelp, texts);
            Assert.Contains(Strings.Overview_DoNowHelp, FindTextBlocks(doNow).Select(t => t.Text));
            Assert.All(FindTextBlocks(doManually), t => Assert.Equal(TextWrapping.Wrap, t.TextWrapping));
            window.Close();
        });
    }

    /// <summary>허용된 OpenLink는 카드에 이름표 버튼으로 보이고, 링크 URL은 도움말로 읽을 수 있다(누르기 전에는 열지 않음).</summary>
    [Fact]
    public void 공식_링크는_카드_버튼으로_보인다()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow(CreateScannedViewModel());
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(LAYOUT_WIDTH, LAYOUT_HEIGHT));
            root.Arrange(new Rect(0, 0, LAYOUT_WIDTH, LAYOUT_HEIGHT));
            root.UpdateLayout();

            var button = Assert.Single(Descendants<Button>(root), b => Equals(b.Content, LINK_LABEL));
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.Equal(LINK_URL, AutomationProperties.GetHelpText(button));
            window.Close();
        });
    }

    /// <summary>
    /// 창을 배치하고 관리자 권한 재검사 버튼과 배너를 찾는다.
    /// </summary>
    private static (Button Button, TextBlock Banner) LayoutElevationControls(MainWindow window)
    {
        ((Expander)window.FindName("OptionsExpander")).IsExpanded = true;
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(LAYOUT_WIDTH, LAYOUT_HEIGHT));
        root.Arrange(new Rect(0, 0, LAYOUT_WIDTH, LAYOUT_HEIGHT));
        root.UpdateLayout();
        var button = Assert.Single(Descendants<Button>(root), b => AutomationProperties.GetAutomationId(b) == "ElevatedRescanButton");
        var banner = Assert.Single(Descendants<TextBlock>(root), t => AutomationProperties.GetAutomationId(t) == "ElevatedBanner");
        return (button, banner);
    }

    /// <summary>일반 권한 창에서는 관리자 권한 재검사 버튼이 켜져 있고 배너는 보이지 않는다.</summary>
    [Fact]
    public void 일반_권한에서는_재검사_버튼이_켜진다()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow(CreateScannedViewModel(isElevated: false));
            var (button, banner) = LayoutElevationControls(window);

            Assert.True(button.IsEnabled);
            Assert.Equal(Strings.Button_ElevatedRescan, button.Content);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)banner.Parent).Visibility);
            window.Close();
        });
    }

    /// <summary>관리자 권한 창에서는 재검사 버튼이 꺼지고, 다른 계정으로 승격된 인스턴스는 원래 창 안내 배너를 보인다.</summary>
    [Fact]
    public void 관리자_권한에서는_버튼이_꺼지고_배너가_보인다()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow(CreateScannedViewModel(isElevated: true, ScanLaunchMode.ElevatedDifferentUser));
            var (button, banner) = LayoutElevationControls(window);

            Assert.False(button.IsEnabled);
            Assert.Equal(Strings.Banner_ElevatedDifferentUser, banner.Text);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)banner.Parent).Visibility);
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
                new Finding(
                    "fixture:link", FindingCategory.Driver, "link fixture", [], "link evidence", Verdict.Info, null, null, null, null, [new OpenLinkAction(LINK_URL, LINK_LABEL)]),
            ];
        }
    }

    // Fictional fixtures, never a claim about the user's current PC.
    private sealed class ImprovementRule : IRule
    {
        public string Id => "fixture.improvements";
        public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot) =>
        [
            new Finding("fixture:display", FindingCategory.Display, "모니터의 주사율을 더 높일 수 있습니다", [],
                "현재 2560 × 1440 · 120 Hz → 같은 해상도의 후보 130 Hz", Verdict.Candidate, null, null,
                new Recommendation("디스플레이 설정에서 130 Hz를 선택하고 화면을 확인하세요.", "현재 해상도와 색 깊이를 유지할 때"),
                new Impact("화면 움직임이 더 부드럽게 보일 수 있어요", "전력 소비가 늘 수 있습니다. 화면이 불안정하면 기존 값으로 되돌리세요."),
                [new OpenSettingsAction(SettingsUriPolicy.DISPLAY_SETTINGS_URI), new KeepAction(), new ShowDetailsAction()],
                explanation: new Explanation("테스트 설명", "테스트 효과", "테스트 주의"), safety: SafetyLevel.Safe),
            new Finding("fixture:driver", FindingCategory.Driver, "설치된 드라이버보다 새로운 버전이 있습니다", [],
                "예시 설치 버전과 공식 배포 버전을 비교했습니다.", Verdict.Candidate, null, null,
                new Recommendation("공식 배포 설명에서 변경 내용을 확인하세요.", "기기와 운영체제에 맞는 버전인지 확인한 뒤 설치"),
                new Impact("새 버전의 오류 수정과 호환성 개선을 받을 수 있습니다", "업데이트 효과는 프로그램과 기기에 따라 다릅니다."),
                [new OpenLinkAction(LINK_URL, LINK_LABEL), new KeepAction()],
                explanation: new Explanation("테스트 설명", "테스트 효과", "테스트 주의"), safety: SafetyLevel.Safe),
            new Finding("fixture:ok", FindingCategory.Storage, "TRIM이 활성화되어 있습니다", [], "OS 보고값", Verdict.Ok, null, null, null, null, []),
        ];
    }
}
