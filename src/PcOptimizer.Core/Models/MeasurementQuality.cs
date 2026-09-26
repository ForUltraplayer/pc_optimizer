/**
 * @file    : MeasurementQuality.cs
 * @author  : rudals252
 * @brief   : 측정값의 관측 품질(직접 관측/보고값/추정/부분) 열거형
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 측정값을 어떻게 얻었는지 나타내는 관측 품질입니다.
/// </summary>
public enum MeasurementQuality
{
    /// <summary>직접 관측한 값입니다.</summary>
    Observed,

    /// <summary>장치·OS가 보고한 값을 그대로 옮긴 값입니다.</summary>
    Reported,

    /// <summary>다른 값으로부터 추정한 값입니다.</summary>
    Estimated,

    /// <summary>일부만 얻은 값입니다.</summary>
    Partial,
}
