/**
 * @file    : CacheToolsWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 공식 캐시 정리 확인 창과 Windows 저장소 설정 연결
 */
using System.Windows;
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.Views;

/// <summary>정리 실행은 버튼과 확인 창을 모두 통과해야 합니다.</summary>
public partial class CacheToolsWindow : Window
{
    private readonly IAppLogger _logger;
    /// <summary>창의 화면 모델입니다.</summary>
    public CacheToolsViewModel ViewModel { get; }

    /// <summary>설치된 도구용 창을 만듭니다.</summary>
    public CacheToolsWindow(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
        InitializeComponent();
        ViewModel = new CacheToolsViewModel(new CacheCleanupService(new SystemCacheToolBackend(_logger), logger: _logger),
            text => System.Windows.MessageBox.Show(this, text, Strings.Cleanup_Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);
        DataContext = ViewModel;
        Closing += (_, args) => args.Cancel = ViewModel.IsBusy;
    }

    private void OpenWindowsCleanup(object sender, RoutedEventArgs e)
    {
        var opened = new SettingsUriPolicy(_logger).TryOpen(SettingsUriPolicy.STORAGE_SENSE_SETTINGS_URI);
        ViewModel.Message = opened ? Strings.Cleanup_WindowsHelp : Strings.Cleanup_SettingsFailed;
    }
}
