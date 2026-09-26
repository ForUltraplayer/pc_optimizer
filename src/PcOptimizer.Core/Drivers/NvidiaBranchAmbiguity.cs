/**
 * @file    : NvidiaBranchAmbiguity.cs
 * @author  : rudals252
 * @brief   : NVIDIA 드라이버 계열을 정하지 못한 이유(양쪽 목록·어느 쪽도 아님·Studio 목록 없음·설치 버전 해석 불가) 열거형
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 계열을 정하지 못한 이유입니다.
/// </summary>
public enum NvidiaBranchAmbiguity
{
    /// <summary>계열을 정했음.</summary>
    None,

    /// <summary>설치 버전이 Game Ready·Studio 목록에 모두 있음.</summary>
    InBoth,

    /// <summary>설치 버전이 어느 목록에도 없음(조회한 최근 항목보다 오래됐거나 다른 배포).</summary>
    InNeither,

    /// <summary>Studio 목록을 받지 못해 한쪽에만 있는지 확인할 수 없음.</summary>
    StudioUnavailable,

    /// <summary>설치 버전을 NVIDIA 표기로 해석할 수 없음.</summary>
    InstalledUnparseable,
}
