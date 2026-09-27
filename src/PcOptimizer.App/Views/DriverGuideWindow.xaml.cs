/**
 * @file    : DriverGuideWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 드라이버 안내 창 코드 비하인드. 앱 수명 화면 모델을 연결만 한다
 */
using System.Windows;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App.Views;

/// <summary>검사 결과가 갱신되면 화면 모델이 스스로 다시 채웁니다.</summary>
public partial class DriverGuideWindow : Window
{
    /// <summary>화면 모델을 연결합니다.</summary>
    public DriverGuideWindow(DriverGuideViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
