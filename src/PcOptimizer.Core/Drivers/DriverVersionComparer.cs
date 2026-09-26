/**
 * @file    : DriverVersionComparer.cs
 * @author  : rudals252
 * @brief   : NVIDIA 표기 드라이버 버전 문자열의 수치 비교·목록 포함 여부·최고 버전 선택 순수 도우미(문자열 비교 없음)
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 드라이버 버전 문자열을 <see cref="DriverVersionNumber"/>로 해석해 수치로 비교합니다. 해석할 수 없는 값은 비교하지 않습니다.
/// </summary>
public static class DriverVersionComparer
{
    /// <summary>
    /// 두 버전을 수치로 비교합니다.
    /// </summary>
    /// <param name="left">왼쪽 버전.</param>
    /// <param name="right">오른쪽 버전.</param>
    /// <param name="result">왼쪽이 크면 양수, 같으면 0, 작으면 음수.</param>
    /// <returns>두 값 모두 해석했으면 true.</returns>
    public static bool TryCompare(string? left, string? right, out int result)
    {
        result = 0;
        if (!DriverVersionNumber.TryParse(left, out var leftVersion) || !DriverVersionNumber.TryParse(right, out var rightVersion))
        {
            return false;
        }

        result = leftVersion.CompareTo(rightVersion);
        return true;
    }

    /// <summary>
    /// 목록에 같은 수치의 버전이 있는지 확인합니다(해석할 수 없는 항목은 무시).
    /// </summary>
    /// <param name="versions">버전 목록.</param>
    /// <param name="version">찾을 버전.</param>
    /// <returns>있으면 true. 찾을 버전을 해석할 수 없으면 false.</returns>
    public static bool ContainsVersion(IEnumerable<string> versions, string? version)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (!DriverVersionNumber.TryParse(version, out var target))
        {
            return false;
        }

        return versions.Any(item => DriverVersionNumber.TryParse(item, out var parsed) && parsed == target);
    }

    /// <summary>
    /// 목록에서 수치로 가장 높은 버전 문자열을 고릅니다(해석할 수 없는 항목은 무시).
    /// </summary>
    /// <param name="versions">버전 목록.</param>
    /// <returns>가장 높은 버전 또는 null(해석 가능한 항목 없음).</returns>
    public static string? Highest(IEnumerable<string> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        string? best = null;
        DriverVersionNumber bestVersion = default;
        foreach (var item in versions)
        {
            if (DriverVersionNumber.TryParse(item, out var parsed) && (best is null || parsed.CompareTo(bestVersion) > 0))
            {
                best = item;
                bestVersion = parsed;
            }
        }

        return best;
    }
}
