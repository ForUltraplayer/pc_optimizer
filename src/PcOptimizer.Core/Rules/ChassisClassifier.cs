/**
 * @file    : ChassisClassifier.cs
 * @author  : rudals252
 * @brief   : Win32_SystemEnclosure.ChassisTypes 코드 목록을 노트북형/데스크톱형/기타/알 수 없음으로 분류하는 규칙 공용 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 섀시 코드 해석 도우미입니다. 전원·디스플레이·시스템 정보 규칙이 같은 매핑을 쓰도록 한 곳에 둡니다.
/// 노트북형과 데스크톱형이 섞이거나 불명 코드만 있으면 <see cref="ChassisKind.Unknown"/>입니다.
/// </summary>
public static class ChassisClassifier
{
    /// <summary>노트북형 섀시 코드(SMBIOS: Portable, Laptop, Notebook, Hand Held, Sub Notebook, Tablet, Convertible, Detachable).</summary>
    private static readonly HashSet<int> LAPTOP_CHASSIS_CODES = [8, 9, 10, 11, 14, 30, 31, 32];

    /// <summary>데스크톱형 섀시 코드(SMBIOS: Desktop, Low Profile, Pizza Box, Mini Tower, Tower, All in One, Space-saving, Lunch Box, Mini PC, Stick PC).</summary>
    private static readonly HashSet<int> DESKTOP_CHASSIS_CODES = [3, 4, 5, 6, 7, 13, 15, 16, 35, 36];

    /// <summary>불명 섀시 코드(SMBIOS: Other, Unknown).</summary>
    private static readonly HashSet<int> UNKNOWN_CHASSIS_CODES = [1, 2];

    /// <summary>
    /// 섀시 코드 측정값(숫자 문자열 목록)을 분류합니다. 측정값이 없거나 목록이 아니면 Unknown입니다.
    /// </summary>
    /// <param name="measurement">섀시 코드 측정값(없으면 null).</param>
    /// <returns>섀시 분류.</returns>
    public static ChassisKind Classify(Measurement? measurement)
    {
        return measurement?.Value is TextListValue list ? Classify(list.Values) : ChassisKind.Unknown;
    }

    /// <summary>
    /// 섀시 코드 문자열 목록을 분류합니다. 숫자가 아닌 코드가 하나라도 있으면 Unknown입니다.
    /// </summary>
    /// <param name="rawCodes">숫자 문자열 목록.</param>
    /// <returns>섀시 분류.</returns>
    public static ChassisKind Classify(IReadOnlyList<string> rawCodes)
    {
        ArgumentNullException.ThrowIfNull(rawCodes);

        var codes = new List<int>();
        foreach (var raw in rawCodes)
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                return ChassisKind.Unknown;
            }

            codes.Add(code);
        }

        var hasLaptop = codes.Exists(LAPTOP_CHASSIS_CODES.Contains);
        var hasDesktop = codes.Exists(DESKTOP_CHASSIS_CODES.Contains);
        var hasKnown = codes.Exists(code => !UNKNOWN_CHASSIS_CODES.Contains(code));

        return (hasLaptop, hasDesktop) switch
        {
            (true, false) => ChassisKind.Laptop,
            (false, true) => ChassisKind.Desktop,
            (true, true) => ChassisKind.Unknown,
            _ => hasKnown ? ChassisKind.Other : ChassisKind.Unknown,
        };
    }

    /// <summary>
    /// 섀시 분류의 사용자 표시 이름(CoreStrings)을 돌려줍니다.
    /// </summary>
    /// <param name="kind">섀시 분류.</param>
    /// <returns>표시 이름.</returns>
    public static string DisplayName(ChassisKind kind)
    {
        return kind switch
        {
            ChassisKind.Laptop => CoreStrings.Power_Chassis_Laptop,
            ChassisKind.Desktop => CoreStrings.Power_Chassis_Desktop,
            ChassisKind.Other => CoreStrings.Power_Chassis_Other,
            _ => CoreStrings.Power_Chassis_Unknown,
        };
    }
}
