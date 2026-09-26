/**
 * @file    : CannotVerifyTexts.cs
 * @author  : rudals252
 * @brief   : 확인 불가 사유 코드를 한국어 근거 문장(CoreStrings 리소스)으로 바꾸는 조회 도우미
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 확인 불가 사유 코드에 맞는 사용자용 근거 문장을 리소스(<see cref="CoreStrings"/>)에서 찾습니다.
/// 문장 자체는 코드에 두지 않고 리소스 파일에서 관리합니다.
/// </summary>
public static class CannotVerifyTexts
{
    /// <summary>
    /// 사유 코드에 맞는 근거 문장을 돌려줍니다.
    /// </summary>
    /// <param name="reason">사유 코드.</param>
    /// <returns>근거 문장.</returns>
    public static string EvidenceFor(CannotVerifyReason reason)
    {
        return reason switch
        {
            CannotVerifyReason.ElevationRequired => CoreStrings.CannotVerify_Evidence_ElevationRequired,
            CannotVerifyReason.NetworkFailed => CoreStrings.CannotVerify_Evidence_NetworkFailed,
            CannotVerifyReason.NoRule => CoreStrings.CannotVerify_Evidence_NoRule,
            CannotVerifyReason.Unsupported => CoreStrings.CannotVerify_Evidence_Unsupported,
            CannotVerifyReason.ProbeError => CoreStrings.CannotVerify_Evidence_ProbeError,
            CannotVerifyReason.Timeout => CoreStrings.CannotVerify_Evidence_Timeout,
            CannotVerifyReason.Cancelled => CoreStrings.CannotVerify_Evidence_Cancelled,
            CannotVerifyReason.AccessDenied => CoreStrings.CannotVerify_Evidence_AccessDenied,
            CannotVerifyReason.PartialData => CoreStrings.CannotVerify_Evidence_PartialData,
            CannotVerifyReason.Ambiguous => CoreStrings.CannotVerify_Evidence_Ambiguous,
            CannotVerifyReason.NotRequested => CoreStrings.CannotVerify_Evidence_NotRequested,
            _ => CoreStrings.CannotVerify_Evidence_ProbeError,
        };
    }
}
