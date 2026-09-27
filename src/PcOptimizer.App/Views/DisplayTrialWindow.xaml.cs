/**
 * @file    : DisplayTrialWindow.xaml.cs
 * @author  : rudals252
 * @brief   : 표시 전용 타이머·창 닫기 시 중단 요청과 실제 작업 종료 대기
 */
using System.Windows;
using System.Windows.Threading;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App.Views;

/// <summary>워커의 원복 책임을 UI 타이머나 창 수명으로 옮기지 않습니다.</summary>
public partial class DisplayTrialWindow : Window
{
    /// <summary>앱 수명 화면 모델을 연결하고 재열기 시 결과를 유지합니다.</summary>
    public DisplayTrialWindow(DisplayTrialViewModel viewModel)
    {
        InitializeComponent(); DataContext = viewModel;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => viewModel.Tick();
        Loaded += (_, _) => timer.Start();
        Closing += (_, e) => { viewModel.RequestClose(); if (viewModel.IsWorking) { e.Cancel = true; } };
        Closed += (_, _) => timer.Stop();
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
