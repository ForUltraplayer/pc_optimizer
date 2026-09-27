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
    /// <summary>마지막 갱신에서 보호 위치의 npm·pip·dotnet 중 하나라도 확인됐는지. 속성 읽기는 디스크를 조회하지 않습니다.</summary>
    bool CacheToolsAvailable { get; }

    /// <summary>도구 위치 스냅샷을 갱신합니다. 고정 공급자는 변경이 없습니다.</summary>
    void Refresh() { }

    /// <summary>앱 안에서 바로 실행할 수 있으면 true, 사용자가 다른 곳에서 직접 해야 하면 false.</summary>
    /// <param name="finding">후보 Finding.</param>
    bool CanExecuteInApp(Finding finding);
}
