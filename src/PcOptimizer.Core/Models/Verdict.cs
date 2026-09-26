/**
 * @file    : Verdict.cs
 * @author  : rudals252
 * @brief   : Finding 판정 등급(정상 확인/개선 후보/확인 불가/정보) 열거형
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// Finding의 판정 등급입니다. "문제" 등급은 두지 않습니다.
/// </summary>
public enum Verdict
{
    /// <summary>정상임을 확인했습니다.</summary>
    Ok,

    /// <summary>개선 후보입니다. 권고와 근거가 반드시 함께 있어야 합니다.</summary>
    Candidate,

    /// <summary>확인하지 못했습니다. 사유가 반드시 함께 있어야 합니다.</summary>
    CannotVerify,

    /// <summary>판정 없이 사실만 전달하는 정보입니다.</summary>
    Info,
}
