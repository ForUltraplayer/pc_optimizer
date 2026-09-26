/**
 * @file    : Measurement.cs
 * @author  : rudals252
 * @brief   : 프로브가 수집한 단일 측정값(이름·값·단위·출처·UTC 관측 시각·관측 품질) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 프로브가 수집한 측정값 하나입니다. 판정은 포함하지 않습니다.
/// </summary>
/// <param name="Name">측정 항목 이름(프로브 안에서 고유).</param>
/// <param name="Value">형식이 구분된 측정값.</param>
/// <param name="Unit">단위(없으면 null). 예: "Hz", "MiB".</param>
/// <param name="Source">값의 출처. 예: "WMI Win32_VideoController".</param>
/// <param name="ObservedAtUtc">관측 시각(UTC).</param>
/// <param name="Quality">관측 품질.</param>
public sealed record Measurement(
    string Name,
    MeasurementValue Value,
    string? Unit,
    string Source,
    DateTimeOffset ObservedAtUtc,
    MeasurementQuality Quality);
