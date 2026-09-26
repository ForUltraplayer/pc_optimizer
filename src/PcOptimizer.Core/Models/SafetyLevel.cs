/**
 * @file    : SafetyLevel.cs
 * @author  : rudals252
 * @brief   : 조치·후보의 안전 수준(안전/주의/되돌릴 수 없음). 배지이자 실행 정책의 기준
 */

namespace PcOptimizer.Core.Models;

/// <summary>안전 수준입니다. 배지에 텍스트로 표시하며 SP1 실행기의 확인 정책 기준이 됩니다.</summary>
public enum SafetyLevel
{
    /// <summary>데이터 손실 없음. 되돌리기 가능하거나 지워도 자동 재생성되는 항목.</summary>
    Safe,

    /// <summary>되돌릴 수는 있으나 체감 비용(재생성 시간, 재로그인, 재부팅)이 있는 항목.</summary>
    Caution,

    /// <summary>되돌릴 수 없는 항목(영구 삭제 등). 별도 확인 문구가 붙는다.</summary>
    Irreversible,
}
