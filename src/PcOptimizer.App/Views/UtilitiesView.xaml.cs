/**
 * @file    : UtilitiesView.xaml.cs
 * @author  : rudals252
 * @brief   : 유틸리티 탭 뷰 코드 비하인드(연결만)
 */
using System.Windows.Controls;

namespace PcOptimizer.App.Views;

/// <summary>DataContext는 <see cref="ViewModels.TroubleshootingViewModel"/>(UtilityGroups)입니다.</summary>
public partial class UtilitiesView : UserControl
{
    /// <summary>뷰를 만듭니다.</summary>
    public UtilitiesView() => InitializeComponent();
}
