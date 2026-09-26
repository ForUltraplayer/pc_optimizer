/**
 * @file    : DisplayModeMatcher.cs
 * @author  : rudals252
 * @brief   : 현재 모드와 해상도·방향·색 깊이·순차 주사가 같은 모드의 주사율만 고르고, 의미 있게 높은 주사율(1Hz 초과 차이)을 판단하는 순수 도우미
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 주사율 비교 조건 도우미입니다(스펙 §5 디스플레이 행). 해상도 변경이 필요한 모드, 인터레이스 모드,
/// 방향·색 깊이가 다른 모드, 하드웨어 기본값(0·1Hz)은 비교 대상에서 뺍니다.
/// </summary>
public static class DisplayModeMatcher
{
    /// <summary>같은 값으로 보는 주사율 차이 한도(Hz). 이 값을 넘게 높아야 후보로 본다(59/60, 59.94/60 억제).</summary>
    public const int REFRESH_TOLERANCE_HZ = 1;

    /// <summary>하드웨어 기본값을 뜻하는 주사율 최댓값(DEVMODE에서 0·1은 기본값).</summary>
    public const int HARDWARE_DEFAULT_MAX_HZ = 1;

    /// <summary>
    /// 현재 모드와 같은 해상도·방향·색 깊이이며 순차 주사인 모드의 주사율을 중복 없이 오름차순으로 돌려줍니다.
    /// </summary>
    /// <param name="current">현재 모드.</param>
    /// <param name="modes">드라이버가 보고한 모드 목록.</param>
    /// <returns>주사율 목록(Hz).</returns>
    public static IReadOnlyList<int> SameShapeRefreshRates(DisplayMode current, IEnumerable<DisplayMode> modes)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(modes);

        return [.. modes
            .Where(mode => IsSameShape(current, mode))
            .Select(mode => mode.RefreshHz)
            .Distinct()
            .Order()];
    }

    /// <summary>
    /// 후보 주사율이 현재 주사율보다 의미 있게(1Hz 초과) 높은지 판단합니다.
    /// </summary>
    /// <param name="candidateHz">후보 주사율.</param>
    /// <param name="currentHz">현재 주사율.</param>
    /// <returns>의미 있게 높으면 true.</returns>
    public static bool IsMeaningfullyHigher(double candidateHz, double currentHz)
    {
        return candidateHz - currentHz > REFRESH_TOLERANCE_HZ;
    }

    /// <summary>
    /// 두 모드가 주사율만 빼고 같은 조건(해상도·방향·색 깊이·순차 주사)인지 확인한다.
    /// </summary>
    private static bool IsSameShape(DisplayMode current, DisplayMode mode)
    {
        return mode.Width == current.Width
            && mode.Height == current.Height
            && mode.Orientation == current.Orientation
            && mode.BitsPerPixel == current.BitsPerPixel
            && !mode.Interlaced
            && mode.RefreshHz > HARDWARE_DEFAULT_MAX_HZ;
    }
}
