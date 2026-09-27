/**
 * @file    : AllTrueToVisibilityConverter.cs
 * @author  : rudals252
 * @brief   : 여러 bool 바인딩이 모두 true일 때만 Visible을 돌려주는 다중 값 변환기(매개변수 "invert"면 반대)
 */
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PcOptimizer.App.Services;

/// <summary>XAML의 MultiBinding에서 조건 두 개 이상을 AND로 묶어 표시 여부를 정합니다.</summary>
public sealed class AllTrueToVisibilityConverter : IMultiValueConverter
{
    /// <summary>"invert"를 넘기면 모두 true일 때 숨깁니다.</summary>
    public const string INVERT = "invert";
    /// <summary>공유 인스턴스입니다.</summary>
    public static readonly AllTrueToVisibilityConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var all = values.All(v => v is true);
        var invert = string.Equals(parameter as string, INVERT, StringComparison.OrdinalIgnoreCase);
        return all != invert ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
