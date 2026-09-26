/**
 * @file    : IndexedMeasurementName.cs
 * @author  : rudals252
 * @brief   : 장치별 측정 이름("접두사[인덱스].필드")과 접두사를 만드는 프로브 계약 공용 도우미
 */

// 기본 패키지
using System.Globalization;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 여러 장치를 한 프로브 결과에 담을 때 쓰는 측정 이름 형식 도우미입니다.
/// 인덱스는 같은 검사 안에서 한 장치의 측정값을 묶는 용도로만 쓰며, Finding ID에는 장치의 내부 식별자를 씁니다.
/// </summary>
public static class IndexedMeasurementName
{
    /// <summary>
    /// 장치 필드의 측정 이름을 만듭니다(예: "display.target[0].refreshHz").
    /// </summary>
    /// <param name="prefix">장치 접두사(예: "display.target").</param>
    /// <param name="index">같은 검사 안의 장치 인덱스(0부터).</param>
    /// <param name="field">필드 이름.</param>
    /// <returns>측정 이름.</returns>
    public static string Create(string prefix, int index, string field)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}[{index}].{field}");
    }

    /// <summary>
    /// 장치 하나의 측정 이름 접두사를 만듭니다(예: "display.target[0].").
    /// </summary>
    /// <param name="prefix">장치 접두사.</param>
    /// <param name="index">장치 인덱스.</param>
    /// <returns>접두사.</returns>
    public static string Prefix(string prefix, int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}[{index}].");
    }
}
