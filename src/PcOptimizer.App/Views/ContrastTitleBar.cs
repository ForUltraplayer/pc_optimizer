/**
 * @file    : ContrastTitleBar.cs
 * @author  : rudals252
 * @brief   : WPF UI 제목 표시줄 버튼의 초기 아이콘 색과 표시줄 높이를 동기화
 */
using System.Windows;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace PcOptimizer.App.Views;

/// <summary>기존 창 이동·최소화·최대화·닫기를 유지하면서 짙은 표시줄 위의 아이콘 대비를 보장합니다.</summary>
public sealed class ContrastTitleBar : TitleBar
{
    /// <summary>파생 형식에서도 라이브러리의 원래 제목 표시줄 스타일을 사용합니다.</summary>
    public ContrastTitleBar() => SetResourceReference(StyleProperty, typeof(TitleBar));
    /// <summary>라이브러리 템플릿 적용 시 기본 검정으로 남는 초기 아이콘 색을 명시적으로 맞춥니다.</summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        foreach (var name in new[] { "PART_HelpButton", "PART_MinimizeButton", "PART_MaximizeButton", "PART_CloseButton" })
        {
            if (GetTemplateChild(name) is not TitleBarButton button) { continue; }
            button.SetCurrentValue(TitleBarButton.RenderButtonsForegroundProperty, ButtonsForeground);
            button.SetCurrentValue(TitleBarButton.MouseOverButtonsForegroundProperty, ButtonsForeground);
            button.SetBinding(FrameworkElement.HeightProperty, new Binding(nameof(ActualHeight)) { Source = this });
        }
    }
}
