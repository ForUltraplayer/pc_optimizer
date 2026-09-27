/**
 * @file    : ScrollChain.cs
 * @author  : rudals252
 * @brief   : 중첩 뷰의 휠 입력을 움직일 수 있는 가장 가까운 세로 스크롤 영역으로 전달
 */
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PcOptimizer.App.Services;

/// <summary>안쪽 목록의 끝에서도 바깥 페이지 스크롤이 이어지도록 합니다. 포커스·선택은 바꾸지 않습니다.</summary>
public static class ScrollChain
{
    /// <summary>페이지 또는 독립 스크롤 영역의 휠 전달을 활성화합니다.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ScrollChain), new PropertyMetadata(false, Changed));
    /// <summary>중첩 휠 전달 사용 여부입니다.</summary>
    public static bool GetIsEnabled(DependencyObject value) => (bool)value.GetValue(IsEnabledProperty);
    /// <summary>중첩 휠 전달을 설정합니다.</summary>
    public static void SetIsEnabled(DependencyObject value, bool enabled) => value.SetValue(IsEnabledProperty, enabled);
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not UIElement element) { return; }
        if ((bool)e.OldValue) { element.PreviewMouseWheel -= Wheel; }
        if ((bool)e.NewValue) { element.PreviewMouseWheel += Wheel; }
    }
    private static void Wheel(object sender, MouseWheelEventArgs e)
    {
        // Ctrl 확대/Shift 가로 이동 및 시스템의 휠 스크롤 비활성 설정을 가로채지 않는다.
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers != ModifierKeys.None || SystemParameters.WheelScrollLines == 0) { return; }
        var target = FindTarget(e.OriginalSource as DependencyObject, e.Delta);
        if (target is null) { return; }
        var lines = SystemParameters.WheelScrollLines;
        var step = lines < 0 ? target.ViewportHeight : lines * (target.CanContentScroll ? 1d : 16d);
        target.ScrollToVerticalOffset(target.VerticalOffset - e.Delta / 120d * step);
        e.Handled = true; // 한 입력은 한 영역에서만 소비한다. 재발행/중복 이동 없음.
    }
    internal static ScrollViewer? FindTarget(DependencyObject? source, int delta)
    {
        for (var node = source; node is not null; node = Parent(node))
        {
            if (node is ScrollViewer { IsEnabled: true } scroll && scroll.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
                && scroll.ScrollableHeight > 0 && (delta < 0 ? scroll.VerticalOffset < scroll.ScrollableHeight - 0.01 : scroll.VerticalOffset > 0.01))
            { return scroll; }
        }
        return null;
    }
    private static DependencyObject? Parent(DependencyObject value) => value switch
    {
        Visual or Visual3D => VisualTreeHelper.GetParent(value),
        FrameworkContentElement content => content.Parent,
        ContentElement content => ContentOperations.GetParent(content),
        _ => LogicalTreeHelper.GetParent(value),
    };
}
