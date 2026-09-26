/**
 * @file    : IUiDispatcher.cs
 * @author  : rudals252
 * @brief   : 엔진 결과를 UI 스레드로 전달하는 마샬러 계약(테스트에서는 즉시 실행 구현으로 바꿈)
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 작업을 UI 스레드에서 실행하는 계약입니다. 뷰모델은 WPF Dispatcher를 직접 쓰지 않고 이 계약만 씁니다.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// 작업을 UI 스레드에서 실행하고 끝날 때 완료되는 작업을 돌려줍니다.
    /// </summary>
    /// <param name="action">실행할 작업.</param>
    /// <returns>완료 작업.</returns>
    Task InvokeAsync(Action action);
}
