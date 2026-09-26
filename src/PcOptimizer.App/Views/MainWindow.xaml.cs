/**
 * @file    : MainWindow.xaml.cs
 * @author  : rudals252
 * @brief   : P0 골격 단계의 빈 메인 창 코드 비하인드. 창 제목을 리소스 문자열에서 읽어 설정한다.
 */

// 기본 패키지
using System.Resources;
using System.Windows;

namespace PcOptimizer.App.Views;

/// <summary>
/// 애플리케이션의 메인 창입니다. P0 단계에서는 빈 창만 제공하며,
/// 실제 진단 화면 구성은 이후 작업(P2)에서 채워진다.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 한국어 UI 문자열을 담고 있는 리소스 매니저입니다.
    /// </summary>
    private static readonly ResourceManager UiStrings = new(
        "PcOptimizer.App.Resources.Strings",
        typeof(MainWindow).Assembly);

    /// <summary>
    /// 메인 창을 초기화하고 리소스에서 읽은 제목을 설정합니다.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Title = UiStrings.GetString("MainWindow_Title") ?? string.Empty;
    }
}
