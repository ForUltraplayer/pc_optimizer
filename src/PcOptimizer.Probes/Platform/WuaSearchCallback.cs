/**
 * @file    : WuaSearchCallback.cs
 * @author  : rudals252
 * @brief   : IUpdateSearcher.BeginSearch에 넘기는 완료 콜백(ISearchCompletedCallback, IID 88AEE058-…) COM 선언과 아무 일도 하지 않는 구현(완료는 ISearchJob.IsCompleted로 확인)
 */

// 기본 패키지
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// WUA 검색 완료 콜백 인터페이스(wuapi.idl ISearchCompletedCallback)입니다. BeginSearch는 null 콜백을 받지 않으므로(E_POINTER) 선언합니다.
/// </summary>
[ComImport]
[Guid("88AEE058-D4B0-4725-A2F1-814A67AE964C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISearchCompletedCallback
{
    /// <summary>
    /// 검색이 끝나면 WUA가 호출합니다.
    /// </summary>
    /// <param name="searchJob">검색 작업(ISearchJob).</param>
    /// <param name="callbackArgs">콜백 인자(ISearchCompletedCallbackArgs).</param>
    void Invoke([MarshalAs(UnmanagedType.IUnknown)] object searchJob, [MarshalAs(UnmanagedType.IUnknown)] object callbackArgs);
}

/// <summary>
/// 아무 일도 하지 않는 완료 콜백입니다. 게이트웨이는 작업의 IsCompleted를 주기적으로 확인하므로 콜백에서 상태를 바꾸지 않습니다.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class WuaSearchCallback : ISearchCompletedCallback
{
    /// <inheritdoc />
    public void Invoke(object searchJob, object callbackArgs)
    {
        // 완료 확인은 ISearchJob.IsCompleted 폴링으로 한다(콜백 스레드에서 COM 객체를 다루지 않음).
    }
}
