/**
 * @file    : MeasurementItemViewModel.cs
 * @author  : rudals252
 * @brief   : 카드의 측정값 한 줄(요약)과 상세 패널 한 줄(출처·품질·관측 시각 포함) 표시 모델
 */

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 측정값 하나의 화면 표시 모델입니다.
/// </summary>
public sealed class MeasurementItemViewModel
{
    /// <summary>
    /// 측정값으로 표시 모델을 만듭니다.
    /// </summary>
    /// <param name="measurement">측정값.</param>
    public MeasurementItemViewModel(Measurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        Name = measurement.Name;
        ValueText = DisplayText.Value(measurement);
        Source = measurement.Source;
        QualityText = DisplayText.Quality(measurement.Quality);
        SummaryText = DisplayText.Format(Strings.Card_MeasurementFormat, Name, ValueText);
        DetailText = DisplayText.Format(
            Strings.Details_Format, Name, ValueText, Source, QualityText, DisplayText.LocalTime(measurement.ObservedAtUtc));
    }

    /// <summary>측정 이름.</summary>
    public string Name { get; }

    /// <summary>값(단위 포함).</summary>
    public string ValueText { get; }

    /// <summary>출처.</summary>
    public string Source { get; }

    /// <summary>관측 품질 이름.</summary>
    public string QualityText { get; }

    /// <summary>카드 요약 줄("이름: 값").</summary>
    public string SummaryText { get; }

    /// <summary>상세 패널 줄(출처·품질·관측 시각 포함).</summary>
    public string DetailText { get; }
}
