/**
 * @file    : PowerSupplyClassifier.cs
 * @author  : rudals252
 * @brief   : 전원 프로브의 ACLineStatus 원시 값을 AC/배터리/알 수 없음으로 분류하고 표시 이름을 주는 규칙 공용 도우미
 */

// 사용자 패키지
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 전원 공급 상태 해석 도우미입니다. 전원·디스플레이 규칙이 같은 해석을 쓰도록 한 곳에 둡니다.
/// </summary>
public static class PowerSupplyClassifier
{
    /// <summary>
    /// ACLineStatus 원시 값을 분류합니다. 값이 없거나 알 수 없음(255 등)이면 Unknown입니다.
    /// </summary>
    /// <param name="acLineStatus">ACLineStatus 원시 값(없으면 null).</param>
    /// <returns>전원 공급 상태.</returns>
    public static PowerSupplyKind Classify(long? acLineStatus)
    {
        return acLineStatus switch
        {
            PowerProbeContract.AC_LINE_ONLINE => PowerSupplyKind.Ac,
            PowerProbeContract.AC_LINE_OFFLINE => PowerSupplyKind.Battery,
            _ => PowerSupplyKind.Unknown,
        };
    }

    /// <summary>
    /// 전원 공급 상태의 사용자 표시 이름(CoreStrings)을 돌려줍니다.
    /// </summary>
    /// <param name="kind">전원 공급 상태.</param>
    /// <returns>표시 이름.</returns>
    public static string DisplayName(PowerSupplyKind kind)
    {
        return kind switch
        {
            PowerSupplyKind.Ac => CoreStrings.Power_Supply_Ac,
            PowerSupplyKind.Battery => CoreStrings.Power_Supply_Battery,
            _ => CoreStrings.Power_Supply_Unknown,
        };
    }
}
