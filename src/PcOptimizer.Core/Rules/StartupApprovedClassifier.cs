/**
 * @file    : StartupApprovedClassifier.cs
 * @author  : rudals252
 * @brief   : StartupApproved 조회 결과·값 형식·첫 바이트 원시 값을 활성/비활성/알 수 없음으로 해석하는 순수 분류기(0x02 활성, 0x03 비활성, 그 밖은 알 수 없음)
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// StartupApproved 원시 값을 해석합니다. StartupApproved는 비공식 형식이므로 알려진 값만 해석하고 나머지는 모두 알 수 없음입니다.
/// </summary>
public static class StartupApprovedClassifier
{
    /// <summary>
    /// 조회 결과와 원시 값으로 활성 상태를 정합니다.
    /// </summary>
    /// <param name="lookup">조회 결과 코드(<see cref="StartupItemsProbeContract"/>의 LOOKUP_*). 없으면 null.</param>
    /// <param name="kind">값 형식 이름. 없으면 null.</param>
    /// <param name="firstByte">이진 값의 첫 바이트. 없으면 null.</param>
    /// <returns>같은 이름의 이진 값이고 첫 바이트가 0x02면 활성, 0x03이면 비활성, 그 밖에는 알 수 없음.</returns>
    public static StartupApprovedState Classify(string? lookup, string? kind, long? firstByte)
    {
        if (!string.Equals(lookup, StartupItemsProbeContract.LOOKUP_FOUND, StringComparison.Ordinal)
            || !string.Equals(kind, StartupItemsProbeContract.KIND_BINARY, StringComparison.Ordinal))
        {
            return StartupApprovedState.Unknown;
        }

        return firstByte switch
        {
            StartupItemsProbeContract.APPROVED_ENABLED => StartupApprovedState.Enabled,
            StartupItemsProbeContract.APPROVED_DISABLED => StartupApprovedState.Disabled,
            _ => StartupApprovedState.Unknown,
        };
    }
}
