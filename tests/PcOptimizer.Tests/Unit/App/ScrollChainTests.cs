/**
 * @file    : ScrollChainTests.cs
 * @author  : rudals252
 * @brief   : 실제 중첩 WPF 트리에서 목록·빈 스크롤·경계의 휠 전달과 중복 이동 방지 검증
 */
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PcOptimizer.App.Services;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>창 표시나 실제 마우스 입력 없이 WPF 라우팅 이벤트로 재현합니다.</summary>
public sealed class ScrollChainTests
{
    /// <summary>안쪽→중간→바깥으로 내려가며 위쪽 경계에서는 역방향으로 이어집니다.</summary>
    [Fact]
    public void WheelContinuesAcrossThreeNestedBoundaries()
    {
        ActionCenterLayoutTests.RunOnSta(() =>
        {
            var leaf = new Border { Height = 700 };
            var inner = new ScrollViewer { Content = leaf, Height = 100 };
            var middleBody = new StackPanel(); middleBody.Children.Add(inner); middleBody.Children.Add(new Border { Height = 700 });
            var middle = new ScrollViewer { Content = middleBody, Height = 180 };
            var body = new StackPanel(); body.Children.Add(middle); body.Children.Add(new Border { Height = 900 });
            var outer = new ScrollViewer { Content = body, Height = 320 };
            ScrollChain.SetIsEnabled(outer, true); ScrollChain.SetIsEnabled(middle, true); ScrollChain.SetIsEnabled(inner, true);
            Layout(outer);
            Assert.Same(inner, ScrollChain.FindTarget(leaf, -120)); Wheel(leaf, -120); Layout(outer);
            Assert.True(inner.VerticalOffset > 0); Assert.Equal(0, middle.VerticalOffset); Assert.Equal(0, outer.VerticalOffset);
            inner.ScrollToBottom(); Layout(outer);
            Assert.Same(middle, ScrollChain.FindTarget(leaf, -120)); Wheel(leaf, -120); Layout(outer);
            Assert.True(middle.VerticalOffset > 0); Assert.Equal(0, outer.VerticalOffset);
            middle.ScrollToBottom(); Layout(outer);
            Assert.Same(outer, ScrollChain.FindTarget(leaf, -120)); Wheel(leaf, -120); Layout(outer);
            Assert.True(outer.VerticalOffset > 0);
            inner.ScrollToTop(); middle.ScrollToTop(); Layout(outer);
            Assert.Same(outer, ScrollChain.FindTarget(leaf, 120)); Wheel(leaf, 120); Layout(outer); Assert.Equal(0, outer.VerticalOffset);
            Assert.Null(ScrollChain.FindTarget(leaf, 120));
            return Task.CompletedTask;
        });
    }
    /// <summary>페이지에 내장되어 자체 스크롤이 없는 뷰도 바깥 페이지를 움직입니다.</summary>
    [Fact]
    public void UnboundedInnerViewerDoesNotSwallowWheel()
    {
        ActionCenterLayoutTests.RunOnSta(() =>
        {
            var leaf = new TextBlock { Text = "list", Height = 900 };
            var inner = new ScrollViewer { Content = leaf };
            var body = new StackPanel(); body.Children.Add(inner);
            var outer = new ScrollViewer { Content = body, Height = 320 };
            ScrollChain.SetIsEnabled(outer, true); ScrollChain.SetIsEnabled(inner, true); Layout(outer);
            Assert.Equal(0, inner.ScrollableHeight); Assert.Same(outer, ScrollChain.FindTarget(leaf, -120));
            var before = Keyboard.FocusedElement;
            Wheel(leaf, -60); Layout(outer); var half = outer.VerticalOffset; Assert.True(half > 0);
            Wheel(leaf, -60); Layout(outer); Assert.Equal(half * 2, outer.VerticalOffset, 3);
            Assert.Same(before, Keyboard.FocusedElement);
            ScrollChain.SetIsEnabled(outer, false); ScrollChain.SetIsEnabled(inner, false);
            var disabled = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
            leaf.RaiseEvent(disabled); Layout(outer); Assert.False(disabled.Handled); Assert.Equal(half * 2, outer.VerticalOffset, 3);
            return Task.CompletedTask;
        });
    }
    private static void Layout(FrameworkElement root) { root.Measure(new Size(500, 320)); root.Arrange(new Rect(0, 0, 500, 320)); root.UpdateLayout(); }
    private static void Wheel(UIElement leaf, int delta)
    {
        var e = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        leaf.RaiseEvent(e); Assert.True(e.Handled);
    }
}
