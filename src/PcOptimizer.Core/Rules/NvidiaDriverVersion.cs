/**
 * @file    : NvidiaDriverVersion.cs
 * @author  : rudals252
 * @brief   : Windows 드라이버 버전(예: 32.0.16.1656)을 NVIDIA 표기 버전(예: 616.56)으로 바꾸는 순수 변환 도우미
 */

// 기본 패키지
using System.Globalization;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// NVIDIA 드라이버 버전 변환 도우미입니다. Windows 버전의 마지막 두 그룹을 이어 붙인 값(마지막 그룹은 4자리)의
/// 마지막 5자리를 "xxx.yy"로 표기합니다. NVIDIA 어댑터에만 적용하며 AMD/Intel은 변환하지 않습니다.
/// </summary>
public static class NvidiaDriverVersion
{
    private const int GROUP_COUNT = 4;
    private const int THIRD_GROUP_INDEX = 2;
    private const int FOURTH_GROUP_INDEX = 3;
    private const int FOURTH_GROUP_MAX = 9999;
    private const string FOURTH_GROUP_FORMAT = "D4";
    private const int SIGNIFICANT_DIGITS = 5;
    private const int MINOR_DIGITS = 2;
    private const char GROUP_SEPARATOR = '.';

    /// <summary>
    /// Windows 드라이버 버전을 NVIDIA 표기 버전으로 바꿉니다. 형식이 맞지 않으면 null입니다.
    /// </summary>
    /// <param name="windowsVersion">Windows 드라이버 버전(예: "32.0.16.1656").</param>
    /// <returns>NVIDIA 표기 버전(예: "616.56") 또는 null.</returns>
    public static string? FromWindowsVersion(string? windowsVersion)
    {
        if (string.IsNullOrWhiteSpace(windowsVersion))
        {
            return null;
        }

        var groups = windowsVersion.Trim().Split(GROUP_SEPARATOR);
        if (groups.Length != GROUP_COUNT)
        {
            return null;
        }

        var numbers = new int[GROUP_COUNT];
        for (var index = 0; index < GROUP_COUNT; index++)
        {
            if (!int.TryParse(groups[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return null;
            }
        }

        if (numbers[FOURTH_GROUP_INDEX] > FOURTH_GROUP_MAX)
        {
            return null;
        }

        var joined = numbers[THIRD_GROUP_INDEX].ToString(CultureInfo.InvariantCulture)
            + numbers[FOURTH_GROUP_INDEX].ToString(FOURTH_GROUP_FORMAT, CultureInfo.InvariantCulture);
        if (joined.Length < SIGNIFICANT_DIGITS)
        {
            return null;
        }

        var digits = joined[^SIGNIFICANT_DIGITS..];
        var major = int.Parse(digits[..^MINOR_DIGITS], NumberStyles.None, CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{major}{GROUP_SEPARATOR}{digits[^MINOR_DIGITS..]}");
    }
}
