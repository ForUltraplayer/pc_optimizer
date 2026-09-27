/**
 * @file    : DriverGuideView.xaml.cs
 * @author  : rudals252
 * @brief   : 드라이버 안내 뷰 코드 비하인드(연결만)
 */
using System.Windows.Controls;

namespace PcOptimizer.App.Views;

/// <summary>DataContext는 <see cref="ViewModels.DriverGuideViewModel"/>입니다.</summary>
public partial class DriverGuideView : UserControl
{
    /// <summary>뷰를 만듭니다.</summary>
    public DriverGuideView() => InitializeComponent();
}
