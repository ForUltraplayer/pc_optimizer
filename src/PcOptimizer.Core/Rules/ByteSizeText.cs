/**
 * @file    : ByteSizeText.cs
 * @author  : rudals252
 * @brief   : 바이트 수를 10진 단위(B·KB·MB·GB·TB, 소수 첫째 자리) 문자열로 바꾸는 규칙 공용 도우미(미분류 기준 1GB = 1,000,000,000바이트와 같은 단위)
 */

// 기본 패키지
using System.Globalization;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 바이트 크기 표시 도우미입니다. 스펙의 미분류 기준(1GB = 1,000,000,000바이트)과 맞추려고 10진 단위를 씁니다.
/// 숫자는 문화권과 무관한 형식(소수점 '.')으로 만들어 같은 입력에 같은 문장이 나오게 합니다.
/// </summary>
internal static class ByteSizeText
{
    private const double DECIMAL_STEP = 1000d;
    private const string BYTES_FORMAT = "{0} B";
    private const string UNIT_FORMAT = "{0:0.0} {1}";
    private static readonly string[] UNITS = ["KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// 바이트 수를 문자열로 바꿉니다.
    /// </summary>
    /// <param name="bytes">바이트 수(0 이상).</param>
    /// <returns>예: "512 B", "1.5 GB".</returns>
    public static string Format(long bytes)
    {
        if (bytes < DECIMAL_STEP)
        {
            return string.Format(CultureInfo.InvariantCulture, BYTES_FORMAT, bytes);
        }

        var value = (double)bytes;
        var unit = -1;
        while (value >= DECIMAL_STEP && unit < UNITS.Length - 1)
        {
            value /= DECIMAL_STEP;
            unit++;
        }

        return string.Format(CultureInfo.InvariantCulture, UNIT_FORMAT, value, UNITS[unit]);
    }
}
