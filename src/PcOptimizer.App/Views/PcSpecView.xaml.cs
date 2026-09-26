/**
 * @file    : PcSpecView.xaml.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 화면 코드 비하인드. 동작은 PcSpecViewModel에 있고, 이미지 저장 대상(CaptureRoot)만 공개한다
 */

// 기본 패키지
using System.Windows.Controls;

namespace PcOptimizer.App.Views;

/// <summary>
/// 내 PC 사양 화면입니다. DataContext는 <see cref="ViewModels.PcSpecViewModel"/>이며,
/// 이미지 저장은 하단 익명화 표기를 포함한 <c>CaptureRoot</c>를 렌더합니다.
/// </summary>
public partial class PcSpecView : UserControl
{
    /// <summary>
    /// 사양 화면을 만듭니다.
    /// </summary>
    public PcSpecView()
    {
        InitializeComponent();
    }
}
