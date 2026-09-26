/**
 * @file    : DisplayText.cs
 * @author  : rudals252
 * @brief   : 판정·사유·분류·상태·관측 품질·측정값을 화면용 한국어 문자열(Strings 리소스)로 바꾸는 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 모델 값을 화면 문자열로 바꿉니다. 문장은 리소스에서만 가져옵니다.
/// </summary>
public static class DisplayText
{
    private const string LIST_SEPARATOR = ", ";
    private const string UNIT_SEPARATOR = " ";

    /// <summary>
    /// 판정 배지 문자열(색 없이도 판정을 읽을 수 있게 하는 텍스트).
    /// </summary>
    /// <param name="verdict">판정.</param>
    /// <returns>배지 문자열.</returns>
    public static string Verdict(Verdict verdict)
    {
        return verdict switch
        {
            Core.Models.Verdict.Ok => Strings.Verdict_Ok,
            Core.Models.Verdict.Candidate => Strings.Verdict_Candidate,
            Core.Models.Verdict.CannotVerify => Strings.Verdict_CannotVerify,
            _ => Strings.Verdict_Info,
        };
    }

    /// <summary>
    /// 확인 불가 사유 문자열.
    /// </summary>
    /// <param name="reason">사유 코드.</param>
    /// <returns>"확인 불가 사유: …" 문자열.</returns>
    public static string Reason(CannotVerifyReason reason)
    {
        var name = reason switch
        {
            CannotVerifyReason.ElevationRequired => Strings.Reason_ElevationRequired,
            CannotVerifyReason.NetworkFailed => Strings.Reason_NetworkFailed,
            CannotVerifyReason.NoRule => Strings.Reason_NoRule,
            CannotVerifyReason.Unsupported => Strings.Reason_Unsupported,
            CannotVerifyReason.Timeout => Strings.Reason_Timeout,
            CannotVerifyReason.Cancelled => Strings.Reason_Cancelled,
            CannotVerifyReason.AccessDenied => Strings.Reason_AccessDenied,
            CannotVerifyReason.PartialData => Strings.Reason_PartialData,
            CannotVerifyReason.Ambiguous => Strings.Reason_Ambiguous,
            CannotVerifyReason.NotRequested => Strings.Reason_NotRequested,
            _ => Strings.Reason_ProbeError,
        };
        return Format(Strings.Reason_Format, name);
    }

    /// <summary>
    /// 분류 이름.
    /// </summary>
    /// <param name="category">분류.</param>
    /// <returns>분류 이름.</returns>
    public static string Category(FindingCategory category)
    {
        return category switch
        {
            FindingCategory.Display => Strings.Category_Display,
            FindingCategory.Memory => Strings.Category_Memory,
            FindingCategory.Driver => Strings.Category_Driver,
            FindingCategory.Power => Strings.Category_Power,
            FindingCategory.Graphics => Strings.Category_Graphics,
            FindingCategory.Security => Strings.Category_Security,
            FindingCategory.Storage => Strings.Category_Storage,
            FindingCategory.AppCache => Strings.Category_AppCache,
            FindingCategory.Startup => Strings.Category_Startup,
            _ => Strings.Category_Unclassified,
        };
    }

    /// <summary>
    /// 검사 상태 이름.
    /// </summary>
    /// <param name="state">검사 상태.</param>
    /// <returns>상태 이름.</returns>
    public static string State(ScanState state)
    {
        return state switch
        {
            ScanState.Scanning => Strings.State_Scanning,
            ScanState.Completed => Strings.State_Completed,
            ScanState.Partial => Strings.State_Partial,
            ScanState.Cancelled => Strings.State_Cancelled,
            _ => Strings.State_Idle,
        };
    }

    /// <summary>
    /// 관측 품질 이름.
    /// </summary>
    /// <param name="quality">관측 품질.</param>
    /// <returns>품질 이름.</returns>
    public static string Quality(MeasurementQuality quality)
    {
        return quality switch
        {
            MeasurementQuality.Observed => Strings.Quality_Observed,
            MeasurementQuality.Reported => Strings.Quality_Reported,
            MeasurementQuality.Estimated => Strings.Quality_Estimated,
            _ => Strings.Quality_Partial,
        };
    }

    /// <summary>
    /// 측정값을 단위와 함께 원시 값 그대로 문자열로 만듭니다(숫자는 문화권 구분 기호 없이).
    /// </summary>
    /// <param name="measurement">측정값.</param>
    /// <returns>값 문자열.</returns>
    public static string Value(Measurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        var value = measurement.Value switch
        {
            IntegerValue integer => integer.Value.ToString(CultureInfo.InvariantCulture),
            DecimalValue number => number.Value.ToString(CultureInfo.InvariantCulture),
            BooleanValue boolean => boolean.Value.ToString(CultureInfo.InvariantCulture),
            TextValue text => text.Value,
            TextListValue list => string.Join(LIST_SEPARATOR, list.Values),
            _ => string.Empty,
        };

        return string.IsNullOrWhiteSpace(measurement.Unit) ? value : value + UNIT_SEPARATOR + measurement.Unit;
    }

    /// <summary>
    /// UTC 시각을 현지 시각 문자열로 만듭니다.
    /// </summary>
    /// <param name="utc">UTC 시각.</param>
    /// <returns>현지 시각 문자열.</returns>
    public static string LocalTime(DateTimeOffset utc)
    {
        return utc.ToLocalTime().ToString(Strings.LastMeasured_TimeFormat, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채웁니다.
    /// </summary>
    /// <param name="template">형식 문자열.</param>
    /// <param name="args">인자.</param>
    /// <returns>채운 문자열.</returns>
    public static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
