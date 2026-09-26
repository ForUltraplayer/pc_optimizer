/**
 * @file    : WpfUiDispatcher.cs
 * @author  : rudals252
 * @brief   : WPF Dispatcher.InvokeAsync로 작업을 UI 스레드에 전달하는 IUiDispatcher 구현
 */

// 기본 패키지
using System.Windows.Threading;

namespace PcOptimizer.App.Services;

/// <summary>
/// WPF Dispatcher를 쓰는 UI 마샬러입니다. 이미 UI 스레드이면 바로 실행합니다.
/// </summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// UI 스레드의 Dispatcher로 마샬러를 만듭니다.
    /// </summary>
    /// <param name="dispatcher">UI 스레드 Dispatcher.</param>
    public WpfUiDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(action).Task;
    }
}
