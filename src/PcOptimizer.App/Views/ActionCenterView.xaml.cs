/**
 * @file    : ActionCenterView.xaml.cs
 * @author  : rudals252
 * @brief   : 조치 뷰 코드 비하인드. 확인·결과가 생기면 목록 위로 스크롤해 확인 화면이 묻히지 않게 한다
 */
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App.Views;

/// <summary>DataContext는 앱 수명의 <see cref="ActionCenterViewModel"/>입니다. 뷰가 사라져도 Dispose하지 않습니다.</summary>
public partial class ActionCenterView : UserControl
{
    private ActionCenterViewModel? _viewModel;
    /// <summary>뷰를 만들고 DataContext 변화에 따라 상태 구독을 옮깁니다.</summary>
    public ActionCenterView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_viewModel is not null) { _viewModel.PropertyChanged -= OnStateChanged; }
            _viewModel = e.NewValue as ActionCenterViewModel;
            if (_viewModel is not null) { _viewModel.PropertyChanged += OnStateChanged; }
        };
        Unloaded += (_, _) => { if (_viewModel is not null) { _viewModel.PropertyChanged -= OnStateChanged; } };
        Loaded += (_, _) => { if (_viewModel is not null) { _viewModel.PropertyChanged -= OnStateChanged; _viewModel.PropertyChanged += OnStateChanged; } };
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionCenterViewModel.Preview) && _viewModel?.Preview is not null ||
            e.PropertyName == nameof(ActionCenterViewModel.Result) && _viewModel?.Result is not null)
        { Dispatcher.InvokeAsync(ActionScroll.ScrollToTop, System.Windows.Threading.DispatcherPriority.Loaded); }
    }
}
