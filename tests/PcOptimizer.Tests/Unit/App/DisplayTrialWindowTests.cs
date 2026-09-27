/**
 * @file    : DisplayTrialWindowTests.cs
 * @author  : rudals252
 * @brief   : 실제 WPF 디스패처·닫기 관문·확인 버튼 바인딩 대역 검증
 */
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions.Display;
using PcOptimizer.Tests.Unit.Probes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>창을 실제로 표시하거나 사용자 입력을 보내지 않고 흐름을 검증합니다.</summary>
public sealed class DisplayTrialWindowTests
{
    /// <summary>시험 중 닫기는 원복을 요청하며 완료 후 닫히고 UI 갱신은 Dispatcher로 돌아옵니다.</summary>
    [Fact] public void WindowCloseWaitsForRollbackAndBindingsUseUiThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var platform = new DisplayTrialTests.Platform(); var gate = new OperationCoordinator();
                    var service = new DisplayTrialCoordinator(platform, gate, new ActionCenterTests.Store(), () => DisplayTrialTests.Session);
                    using var vm = new DisplayTrialViewModel(service, gate, new WpfUiDispatcher(dispatcher), () => { Assert.True(dispatcher.CheckAccess()); return Task.CompletedTask; });
                    vm.PropertyChanged += (_, _) => Assert.True(dispatcher.CheckAccess());
                    vm.Records.CollectionChanged += (_, _) => Assert.True(dispatcher.CheckAccess());
                    await vm.RefreshCommand.ExecuteAsync(null); vm.Selected = Assert.Single(vm.Choices);
                    await vm.PrepareCommand.ExecuteAsync(null);
                    var window = new DisplayTrialWindow(vm); var closed = false; window.Closed += (_, _) => closed = true;
                    var root = (FrameworkElement)window.Content;
                    root.Measure(new Size(600, 680)); root.Arrange(new Rect(0, 0, 600, 680)); root.UpdateLayout();
                    var confirm = Descendants<Button>(root).Single(b => Equals(b.Content, "확인한 내용 실행"));
                    Assert.Same(vm.ExecuteCommand, confirm.Command); Assert.True(confirm.IsEnabled); Assert.False(confirm.IsDefault);
                    var folder = Environment.GetEnvironmentVariable("PCOPTIMIZER_UI_ARTIFACTS");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        Directory.CreateDirectory(folder); var bitmap = new RenderTargetBitmap(600, 680, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(folder, "display-trial-preview.png")); encoder.Save(file);
                    }
                    var running = vm.ExecuteCommand.ExecuteAsync(null); var end = DateTime.UtcNow.AddSeconds(5);
                    while (service.Status is null) { if (DateTime.UtcNow >= end) { throw new TimeoutException(); } await Task.Delay(1); }
                    vm.Tick(); window.Close(); Assert.False(closed);
                    await running; Assert.Equal(60, platform.Current.Mode.RefreshHz); Assert.False(vm.IsWorking);
                    window.Close(); Assert.True(closed);
                }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T value) { yield return value; }
            foreach (var descendant in Descendants<T>(child)) { yield return descendant; }
        }
    }
}
