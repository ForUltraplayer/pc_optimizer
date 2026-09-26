/**
 * @file    : Recommendation.cs
 * @author  : rudals252
 * @brief   : Finding의 권고(권고 문장과 적용 조건) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 개선 후보에 대한 권고입니다.
/// </summary>
/// <param name="Text">권고 문장. 예: "설정에서 130Hz 후보 확인".</param>
/// <param name="Condition">권고를 따르기 전 확인할 조건. 예: "동일 해상도·HDR/색상 조건 확인 후 선택".</param>
public sealed record Recommendation(string Text, string Condition);
