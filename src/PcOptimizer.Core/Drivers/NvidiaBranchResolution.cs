/**
 * @file    : NvidiaBranchResolution.cs
 * @author  : rudals252
 * @brief   : NVIDIA 드라이버 계열 판정 결과(계열과 모호한 이유) 레코드
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 계열 판정 결과입니다.
/// </summary>
/// <param name="Branch">계열.</param>
/// <param name="Ambiguity">모호한 이유(계열을 정했으면 <see cref="NvidiaBranchAmbiguity.None"/>).</param>
public sealed record NvidiaBranchResolution(NvidiaDriverBranch Branch, NvidiaBranchAmbiguity Ambiguity);
