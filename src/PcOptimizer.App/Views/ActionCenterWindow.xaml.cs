/**
 * @file    : ActionCenterWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 앱 소유 조치 모델을 표시, 창 닫기는 실제 실행 취소나 기록 폐기가 아님
 */
using System.Windows;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App.Views;

/// <summary>다시 열어도 같은 화면 모델과 실행 결과를 사용합니다.</summary>
public partial class ActionCenterWindow : Window
{
    /// <summary>앱 수명의 모델을 받아 표시합니다. 닫힐 때 Dispose하지 않습니다.</summary>
    public ActionCenterWindow(ActionCenterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
