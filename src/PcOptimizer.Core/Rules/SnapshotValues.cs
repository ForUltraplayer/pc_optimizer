/**
 * @file    : SnapshotValues.cs
 * @author  : rudals252
 * @brief   : 규칙이 스냅샷에서 형식별 측정값(문자열·정수·실수·불리언·목록)을 "값 없음(null)"과 구분해 읽는 공용 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 스냅샷 측정값 읽기 도우미입니다. 값이 없거나 형식이 다르면 null이며 0·false·빈 문자열로 바꾸지 않습니다.
/// </summary>
internal static class SnapshotValues
{
    /// <summary>
    /// 문자열 측정값을 읽습니다. 없거나 공백이면 null, 있으면 앞뒤 공백을 제거합니다.
    /// </summary>
    public static string? Text(ScanSnapshot snapshot, string probeId, string name)
    {
        return snapshot.GetMeasurement(probeId, name)?.Value is TextValue text && !string.IsNullOrWhiteSpace(text.Value)
            ? text.Value.Trim()
            : null;
    }

    /// <summary>
    /// 정수 측정값을 읽습니다. 없으면 null(0과 구분).
    /// </summary>
    public static long? Integer(ScanSnapshot snapshot, string probeId, string name)
    {
        return snapshot.GetMeasurement(probeId, name)?.Value is IntegerValue value ? value.Value : null;
    }

    /// <summary>
    /// 실수 측정값을 읽습니다. 없으면 null.
    /// </summary>
    public static double? Decimal(ScanSnapshot snapshot, string probeId, string name)
    {
        return snapshot.GetMeasurement(probeId, name)?.Value is DecimalValue value ? value.Value : null;
    }

    /// <summary>
    /// 불리언 측정값을 읽습니다. 없으면 null(false와 구분).
    /// </summary>
    public static bool? Boolean(ScanSnapshot snapshot, string probeId, string name)
    {
        return snapshot.GetMeasurement(probeId, name)?.Value is BooleanValue value ? value.Value : null;
    }

    /// <summary>
    /// 문자열 목록 측정값을 읽습니다. 없으면 null(빈 목록과 구분).
    /// </summary>
    public static IReadOnlyList<string>? TextList(ScanSnapshot snapshot, string probeId, string name)
    {
        return snapshot.GetMeasurement(probeId, name)?.Value is TextListValue list ? list.Values : null;
    }

    /// <summary>
    /// 프로브 결과에서 접두사로 시작하는 측정값을 모읍니다(장치 한 개의 측정값 묶음).
    /// </summary>
    public static Measurement[] WithPrefix(ProbeResult result, string prefix)
    {
        return [.. result.Measurements.Where(m => m.Name.StartsWith(prefix, StringComparison.Ordinal))];
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채웁니다.
    /// </summary>
    public static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
