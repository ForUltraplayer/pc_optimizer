/**
 * @file    : Impact.cs
 * @author  : rudals252
 * @brief   : Finding의 영향(기대 효과와 부작용) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 권고를 따랐을 때의 영향입니다.
/// </summary>
/// <param name="Benefit">기대 효과. 예: "스크롤이 부드러워짐".</param>
/// <param name="SideEffect">부작용. 예: "일부 캐시는 다음 실행 때 다시 생성".</param>
public sealed record Impact(string Benefit, string SideEffect);
