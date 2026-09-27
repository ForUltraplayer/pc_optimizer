/**
 * @file    : TroubleshootingView.xaml.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함 뷰 코드 비하인드(연결만)
 */
using System.Windows.Controls;

namespace PcOptimizer.App.Views;

/// <summary>DataContext는 <see cref="ViewModels.TroubleshootingViewModel"/>입니다.</summary>
public partial class TroubleshootingView : UserControl
{
    /// <summary>뷰를 만듭니다.</summary>
    public TroubleshootingView() => InitializeComponent();
}
