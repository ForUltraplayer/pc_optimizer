/**
 * @file    : MainWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 메인 진단 창 코드 비하인드. 화면 모델을 DataContext로 연결하고 첫 포커스를 검사 시작 버튼에 두며, 사양 이미지 저장 대상(SpecCaptureRoot)을 공개한다
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
    private TroubleshootingWindow? _troubleshootingWindow;
    private readonly DriverGuideViewModel? _driverGuide;
    private DriverGuideWindow? _driverGuideWindow;
    /// <summary>
    /// 화면 모델로 메인 창을 만듭니다.
    /// </summary>
    /// <param name="viewModel">메인 화면 모델.</param>
    public MainWindow(MainViewModel viewModel, IAppLogger? logger = null, DisplayTrialViewModel? displayTrials = null, TroubleshootingViewModel? troubleshooting = null, DriverGuideViewModel? driverGuide = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _displayTrials = displayTrials;
        _troubleshooting = troubleshooting;
        _driverGuide = driverGuide;
        DriverGuideButton.Visibility = driverGuide is null ? Visibility.Collapsed : Visibility.Visible;
        TroubleshootingButton.Visibility = troubleshooting is null ? Visibility.Collapsed : Visibility.Visible;
        DisplayEvaluationButton.Visibility = displayTrials is null ? Visibility.Collapsed : Visibility.Visible;
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            StartScanButton.Focus();
            if (viewModel.Actions is { } actions && actions.RefreshCommand.CanExecute(null)) { await actions.RefreshCommand.ExecuteAsync(null); }
            if (_displayTrials?.RefreshCommand.CanExecute(null) == true) { await _displayTrials.RefreshCommand.ExecuteAsync(null); }
        };
        Closing += (_, e) => { if (_displayTrials?.IsWorking == true) { _displayTrials.RequestClose(); e.Cancel = true; } };
        Closed += (_, _) => { _displayTrials?.Dispose(); _troubleshooting?.Dispose(); viewModel.Dispose(); };
    }
    /// <summary>드라이버 안내 창을 엽니다(하나만, 재열기 시 앞으로).</summary>
    private void OpenDriverGuide(object sender, RoutedEventArgs e)
    {
        if (_driverGuide is null) { return; }
        if (_driverGuideWindow is not null) { _driverGuideWindow.Activate(); return; }
        _driverGuideWindow = new DriverGuideWindow(_driverGuide) { Owner = this };
        _driverGuideWindow.Closed += (_, _) => _driverGuideWindow = null;
        _driverGuideWindow.Show();
    }
    /// <summary>문제 해결 도구함 창을 엽니다(하나만, 재열기 시 앞으로).</summary>
    private void OpenTroubleshooting(object sender, RoutedEventArgs e)
    {
        if (_troubleshooting is null) { return; }
        if (_troubleshootingWindow is not null) { _troubleshootingWindow.Activate(); return; }
        _troubleshootingWindow = new TroubleshootingWindow(_troubleshooting) { Owner = this };
        _troubleshootingWindow.Closed += (_, _) => _troubleshootingWindow = null;
        _troubleshootingWindow.Show();
    }

    /// <summary>내 PC 사양 화면의 이미지 저장 대상(하단 익명화 표기 포함).</summary>
    public FrameworkElement SpecCaptureRoot => SpecView.CaptureRoot;
    private ActionCenterWindow? _actionWindow;
    private async void PrepareStartupFromCard(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Actions: { } actions }
            || sender is not FrameworkElement { DataContext: FindingCardViewModel { StartupTarget: { } target } }
            || actions.Choices.FirstOrDefault(c => c.Id == PcOptimizer.Core.Actions.StartupRegistration.ActionFor(target.SourceKey) && c.Target == target) is not { } choice) { return; }
        OpenActionCenter(sender, e);
        await actions.PrepareAsync(choice.Id, target);
    }
    private async void PrepareAdobeFromCard(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Actions: { } actions }
            || sender is not FrameworkElement { DataContext: FindingCardViewModel { CanPrepareAdobe: true } }) { return; }
        OpenActionCenter(sender, e);
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
    private void OpenActionCenter(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Actions: { } actions }) { return; }
        actions.SelectEffectCommand.Execute((sender as FrameworkElement)?.Tag as string);
        if (_actionWindow is not null) { _actionWindow.Activate(); return; }
        _actionWindow = new ActionCenterWindow(actions) { Owner = this };
        _actionWindow.Closed += (_, _) => _actionWindow = null;
        _actionWindow.Show();
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
