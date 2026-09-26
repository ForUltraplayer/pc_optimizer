/**
 * @file    : MainWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 메인 진단 창 코드 비하인드. 화면 모델을 DataContext로 연결하고 첫 포커스를 검사 시작 버튼에 둔다
 */

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
    /// <summary>
    /// 화면 모델로 메인 창을 만듭니다.
    /// </summary>
    /// <param name="viewModel">메인 화면 모델.</param>
    public MainWindow(MainViewModel viewModel, IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => StartScanButton.Focus();
        Closed += (_, _) => viewModel.Dispose();
    }

    private async void OpenCacheTools(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.CanOpenCacheTools) { return; }
        var window = new CacheToolsWindow(_logger) { Owner = this };
        window.ShowDialog();
        if (window.ViewModel.NeedsRescan) { await viewModel.StartScanCommand.ExecuteAsync(null); }
    }
}
