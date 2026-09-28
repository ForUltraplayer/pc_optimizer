/**
 * @file    : ActionCenterView.xaml.cs
 * @author  : rudals252
 * @brief   : 카드 내부 조치는 위치를 유지하고 다른 페이지에서 진입한 조치만 별도 확인 영역으로 안내
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
        if (_viewModel?.UsesStandaloneFeedback == true &&
            (e.PropertyName == nameof(ActionCenterViewModel.Preview) && _viewModel.Preview is not null ||
            e.PropertyName == nameof(ActionCenterViewModel.Result) && _viewModel.Result is not null))
        { Dispatcher.InvokeAsync(ActionScroll.ScrollToTop, System.Windows.Threading.DispatcherPriority.Loaded); }
    }
}
