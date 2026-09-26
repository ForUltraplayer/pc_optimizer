/**
 * @file    : FindingCategory.cs
 * @author  : rudals252
 * @brief   : Finding과 프로브가 속하는 검사 분류 열거형
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 분류입니다. Finding과 프로브가 같은 분류 체계를 사용합니다.
/// </summary>
public enum FindingCategory
{
    /// <summary>디스플레이(해상도·주사율 등).</summary>
    Display,

    /// <summary>메모리.</summary>
    Memory,

    /// <summary>드라이버.</summary>
    Driver,

    /// <summary>전원.</summary>
    Power,

    /// <summary>그래픽.</summary>
    Graphics,

    /// <summary>보안.</summary>
    Security,

    /// <summary>저장소.</summary>
    Storage,

    /// <summary>앱 캐시.</summary>
    AppCache,

    /// <summary>시작 프로그램.</summary>
    Startup,

    /// <summary>미분류.</summary>
    Unclassified,
}
