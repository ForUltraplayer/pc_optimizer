/**
 * @file    : ActionCenterLayoutTests.cs
 * @author  : rudals252
 * @brief   : 실제 WPF 디스패처·닫기/재열기·기본 버튼·긴 대상·세 배율 오프스크린 검증
 */
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>창을 화면에 띄우거나 키보드 입력을 보내지 않고 WPF 레이아웃/명령을 검사합니다.</summary>
public sealed class ActionCenterLayoutTests
{
    /// <summary>긴 경로가 가로 폭을 넘지 않으며 100/150/200% 렌더에서도 세로 스크롤로 접근합니다.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void PreviewAndResultFitNarrowWindow(double scale)
    {
        RunOnSta(async () =>
        {
            var adapter = new ActionCenterTests.Adapter { TargetLabel = @"C:\fixture\" + string.Join("\\", Enumerable.Repeat("긴-대상-폴더-이름", 12)) };
            using var vm = ActionCenterTests.Create(adapter);
            await vm.PrepareAsync(ActionId.Power, ActionCenterTests.Target);
            var window = new ActionCenterWindow(vm);
            try
            {
                Render(window, $"action-preview-{scale * 100:0}", scale);
                var root = (FrameworkElement)window.Content;
                var target = Descendants<TextBlock>(root).Single(t => AutomationProperties.GetAutomationId(t) == "ActionTarget");
                Assert.True(target.ActualWidth <= 440);
                Assert.True(target.ActualHeight > target.FontSize * 2);
                var confirm = Descendants<Button>(root).Single(b => AutomationProperties.GetAutomationId(b) == "ConfirmAction");
                var close = Descendants<Button>(root).Single(b => AutomationProperties.GetAutomationId(b) == "CloseActionCenter");
                Assert.False(confirm.IsDefault); // 무심코 누른 Enter로 실행하지 않는다.
                Assert.True(close.IsCancel); // Esc는 창 닫기이며 실제 실행 취소가 아니다.
                await vm.ExecuteCommand.ExecuteAsync(null);
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                Assert.False(vm.IsBlocked);
                Render(window, $"action-result-{scale * 100:0}", scale);
                var actual = Descendants<TextBlock>(root).Single(t => AutomationProperties.GetAutomationId(t) == "ActionActualEffect");
                Assert.Contains("감소", actual.Text);
                Assert.True(actual.ActualWidth <= 440);
                var busy = Descendants<TextBlock>(root).Single(t => AutomationProperties.GetAutomationId(t) == "ActionBusyNote");
                Assert.Equal(Visibility.Collapsed, busy.Visibility);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>실제 디스패처에서 늦은 완료·목록/결과/재검사 알림은 UI 스레드로 돌아옵니다.</summary>
    [Fact]
    public void ClosingWindowKeepsLiveOperationAndUpdatesOnUiThread()
    {
        RunOnSta(async () =>
        {
            var adapter = new ActionCenterTests.Adapter { Release = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            var gate = new OperationCoordinator();
            var dispatcher = Dispatcher.CurrentDispatcher;
            var rescan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var violations = new List<string>();
            using var vm = new ActionCenterViewModel(new ActionWorkflow(gate, new ActionCenterTests.Store(), () => ActionCenterTests.Session, [adapter], []),
                new WpfUiDispatcher(dispatcher), () => { Assert.True(dispatcher.CheckAccess()); rescan.TrySetResult(); return Task.CompletedTask; });
            vm.PropertyChanged += (_, e) => { if (!dispatcher.CheckAccess()) { lock (violations) { violations.Add(e.PropertyName ?? "unknown"); } } };
            vm.Results.CollectionChanged += (_, _) => { if (!dispatcher.CheckAccess()) { lock (violations) { violations.Add("results"); } } };
            await vm.PrepareAsync(ActionId.Power, ActionCenterTests.Target);
            var first = new ActionCenterWindow(vm);
            var execute = vm.ExecuteCommand.ExecuteAsync(null);
            await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            first.Close();
            Assert.True(gate.State.IsBusy);
            var second = new ActionCenterWindow(vm);
            try
            {
                Assert.Same(vm, second.DataContext);
                vm.CancelCommand.Execute(null);
                await execute.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(vm.Result!.IsDraining);
                adapter.Release.TrySetResult();
                await rescan.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(vm.IsWorking);
                Assert.Single(vm.Results);
                Assert.Contains("완료", vm.Result!.Title);
                lock (violations) { Assert.Empty(violations); }
            }
            finally { adapter.Release.TrySetResult(); second.Close(); }
        });
    }

    private static void Render(Window window, string name, double scale)
    {
        var root = (FrameworkElement)window.Content;
        var size = new Size(440, 720);
        root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
        var output = Environment.GetEnvironmentVariable("PCOPTIMIZER_UI_ARTIFACTS");
        if (string.IsNullOrEmpty(output)) { return; }
        Directory.CreateDirectory(output);
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) { yield return found; }
            foreach (var descendant in Descendants<T>(child)) { yield return descendant; }
        }
    }
    private static void RunOnSta(Func<Task> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF dispatcher timed out");
        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
