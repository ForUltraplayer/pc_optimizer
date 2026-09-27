/**
 * @file    : TroubleshootingWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함 창 코드 비하인드. 앱 수명 화면 모델을 연결하고 명령 실행 중에는 창을 닫지 않는다
 */
using System.Windows;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App.Views;

/// <summary>창을 닫아도 화면 모델과 실행 상태는 메인 창 수명 동안 유지됩니다.</summary>
public partial class TroubleshootingWindow : Window
{
    /// <summary>화면 모델을 연결합니다.</summary>
    public TroubleshootingWindow(TroubleshootingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += (_, e) => { if (viewModel.IsRunning) { e.Cancel = true; viewModel.Status = "명령이 끝나거나 취소한 뒤에 창을 닫을 수 있어요."; } };
    }
}
