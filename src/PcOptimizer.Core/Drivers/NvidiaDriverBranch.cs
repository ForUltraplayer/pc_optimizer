/**
 * @file    : NvidiaDriverBranch.cs
 * @author  : rudals252
 * @brief   : NVIDIA 드라이버 계열(Game Ready/Studio/모호) 열거형
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 설치 버전이 속한 NVIDIA 드라이버 계열입니다. 계열은 공개 목록 포함 여부로만 정하며, 한쪽 목록에만 있을 때만 확정합니다.
/// </summary>
public enum NvidiaDriverBranch
{
    /// <summary>계열을 정할 수 없음(양쪽 목록에 있음, 어느 쪽에도 없음, Studio 목록 없음, 설치 버전 해석 불가).</summary>
    Ambiguous,

    /// <summary>Game Ready 목록에만 있음.</summary>
    GameReady,

    /// <summary>Studio 목록에만 있음.</summary>
    Studio,
}
