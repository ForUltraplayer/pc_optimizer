/**
 * @file    : MainWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 메인 창 코드 비하인드. 좌측 내비게이션 페이지(추천 조치·전체 결과·문제 해결·내 PC 사양·고급·조치/되돌리기·설정)에 화면 모델을 연결하고, 카드의 실행 버튼을 조치 페이지로 보내며, 사양 이미지 저장 대상을 공개한다
 */

// 기본 패키지
using System.Windows;

// 서드파티 패키지
using Wpf.Ui.Controls;

// 사용자 패키지
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.App.Views;

/// <summary>
/// 애플리케이션의 메인 창입니다. 모든 동작은 <see cref="MainViewModel"/>에 있고 여기서는 연결만 합니다.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly IAppLogger _logger;
    private readonly DisplayTrialViewModel? _displayTrials;
    private DisplayTrialWindow? _displayWindow;
    private readonly TroubleshootingViewModel? _troubleshooting;
    /// <summary>
    /// 화면 모델로 메인 창을 만듭니다. 문제 해결·드라이버 안내 모델이 없으면 해당 페이지에 준비 안내만 보입니다.
    /// </summary>
    /// <param name="viewModel">메인 화면 모델.</param>
    public MainWindow(MainViewModel viewModel, IAppLogger? logger = null, DisplayTrialViewModel? displayTrials = null, TroubleshootingViewModel? troubleshooting = null, DriverGuideViewModel? driverGuide = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _displayTrials = displayTrials;
        _troubleshooting = troubleshooting;
        TroubleshootingView.DataContext = troubleshooting;
        UtilitiesView.DataContext = troubleshooting;
        TroubleshootingView.Visibility = troubleshooting is null ? Visibility.Collapsed : Visibility.Visible;
        TroubleshootingUnavailableNote.Visibility = troubleshooting is null ? Visibility.Visible : Visibility.Collapsed;
        DriverGuideView.DataContext = driverGuide;
        DriverGuideExpander.Visibility = driverGuide is null ? Visibility.Collapsed : Visibility.Visible;
        DisplayEvaluationButton.Visibility = displayTrials is null ? Visibility.Collapsed : Visibility.Visible;
        ActionCenterView.Visibility = viewModel.HasActionCenter ? Visibility.Visible : Visibility.Collapsed;
        DataContext = viewModel;
        // 페이지를 바꾸거나 조치 확인·결과가 생기면 본문 스크롤을 맨 위로 올려 새 내용이 묻히지 않게 한다.
        viewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.CurrentPage)) { Dispatcher.InvokeAsync(MainScroll.ScrollToTop, System.Windows.Threading.DispatcherPriority.Loaded); } };
        if (viewModel.Actions is { } actionCenter)
        {
            actionCenter.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ActionCenterViewModel.Preview) or nameof(ActionCenterViewModel.Result) && viewModel.CurrentPage == MainPage.Actions)
                { Dispatcher.InvokeAsync(MainScroll.ScrollToTop, System.Windows.Threading.DispatcherPriority.Loaded); }
            };
        }
        Loaded += async (_, _) =>
        {
            StartScanButton.Focus();
            if (viewModel.Actions is { } actions && actions.RefreshCommand.CanExecute(null)) { await actions.RefreshCommand.ExecuteAsync(null); }
            if (_displayTrials?.RefreshCommand.CanExecute(null) == true) { await _displayTrials.RefreshCommand.ExecuteAsync(null); }
        };
        Closing += (_, e) =>
        {
            if (_displayTrials?.IsWorking == true) { _displayTrials.RequestClose(); e.Cancel = true; }
            if (_troubleshooting?.IsRunning == true) { viewModel.CurrentPage = MainPage.Troubleshooting; e.Cancel = true; }
        };
        Closed += (_, _) => { _displayTrials?.Dispose(); _troubleshooting?.Dispose(); viewModel.Dispose(); };
    }

    /// <summary>내 PC 사양 화면의 이미지 저장 대상(하단 익명화 표기 포함).</summary>
    public FrameworkElement SpecCaptureRoot => SpecView.CaptureRoot;

    private async void PrepareStartupFromCard(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Actions: { } actions } viewModel
            || sender is not FrameworkElement { DataContext: FindingCardViewModel { StartupTarget: { } target } }
            || actions.Choices.FirstOrDefault(c => c.Id == PcOptimizer.Core.Actions.StartupRegistration.ActionFor(target.SourceKey) && c.Target == target) is not { } choice) { return; }
        viewModel.CurrentPage = MainPage.Actions;
        await actions.PrepareAsync(choice.Id, target);
    }
    private async void PrepareAdobeFromCard(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Actions: { } actions } viewModel
            || sender is not FrameworkElement { DataContext: FindingCardViewModel { CanPrepareAdobe: true } }) { return; }
        viewModel.CurrentPage = MainPage.Actions;
        await actions.PrepareAsync(PcOptimizer.Core.Actions.ActionId.AppFiles,
            new PcOptimizer.Core.Actions.ActionTarget.Files(PcOptimizer.Probes.Actions.Files.AdobeCacheTargets.Media));
    }
    private async void PrepareDisplayFromCard(object sender, RoutedEventArgs e)
    {
        if (_displayTrials is null || sender is not FrameworkElement { DataContext: FindingCardViewModel { CanTrialDisplay: true } card }) { return; }
        OpenDisplayTrials(sender, e);
        await _displayTrials.PrepareFindingAsync(card.Finding);
    }
    private void OpenDisplayTrials(object sender, RoutedEventArgs e)
    {
        if (_displayTrials is null) { return; }
        if (_displayWindow is not null) { _displayWindow.Activate(); return; }
        _displayWindow = new DisplayTrialWindow(_displayTrials) { Owner = this };
        _displayWindow.Closed += (_, _) => _displayWindow = null;
        _displayWindow.Show();
    }
    /// <summary>효과 버튼(Tag)으로 조치 페이지의 필터를 정하고 그 페이지로 이동합니다.</summary>
    private void OpenActionCenter(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Actions: { } actions } viewModel) { return; }
        actions.SelectEffectCommand.Execute((sender as FrameworkElement)?.Tag as string);
        viewModel.CurrentPage = MainPage.Actions;
    }

    private async void OpenCacheTools(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.CanOpenCacheTools) { return; }
        if (viewModel.Actions is not null) { OpenActionCenter(sender, e); return; }
        // UI 관문과 별개로 실행기에도 사용자 범위를 전달해 SystemOnly면 관측·실행 전에 거절한다(REV-016).
        var window = new CacheToolsWindow(_logger, viewModel.IsSystemOnly, viewModel.Operations) { Owner = this };
        window.ShowDialog();
        if (window.ViewModel.Outcome is { } outcome) { viewModel.LastCleanupOutcome = outcome; }
        // 검사 중이거나 사양을 읽는 중(프로브 공유)에는 재검사를 시작하지 않는다.
        if (window.ViewModel.NeedsRescan && viewModel.StartScanCommand.CanExecute(null)) { await viewModel.StartScanCommand.ExecuteAsync(null); }
    }
}
