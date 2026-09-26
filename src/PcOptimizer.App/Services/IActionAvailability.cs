/**
 * @file    : IActionAvailability.cs
 * @author  : rudals252
 * @brief   : 후보(Finding)를 앱 안에서 바로 실행할 수 있는지, 도구 캐시 정리 창을 열 수 있는지 판정하는 계약. SP1 실행기 등록으로 확장된다
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.Services;

/// <summary>후보를 앱 안에서 바로 실행할 수 있는지 판정합니다.</summary>
public interface IActionAvailability
{
    /// <summary>도구 캐시 정리 창을 열 수 있는지(보호 위치에 npm·pip·dotnet 중 하나라도 있는지). 호출할 때마다 확인할 수 있으므로 호출자가 한 번만 읽어 보관합니다.</summary>
    bool CacheToolsAvailable { get; }

    /// <summary>앱 안에서 바로 실행할 수 있으면 true, 사용자가 다른 곳에서 직접 해야 하면 false.</summary>
    /// <param name="finding">후보 Finding.</param>
    bool CanExecuteInApp(Finding finding);
}
