/**
 * @file    : WmiResultInterpreter.cs
 * @author  : rudals252
 * @brief   : WMI 조회 실패를 프로브 Issue로 바꾸고, WMI 원시 값(부호 없는 정수·문자열·정수 배열)을 형식 구분 값으로 읽는 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// WMI 조회 결과 해석 도우미입니다. 값이 없으면 null을 돌려주며 0·빈 문자열로 바꾸지 않습니다.
/// </summary>
internal static class WmiResultInterpreter
{
    /// <summary>
    /// 실패한 조회를 사유 코드와 요약을 가진 Issue로 바꿉니다.
    /// </summary>
    /// <param name="className">조회한 WMI 클래스 이름.</param>
    /// <param name="result">실패한 조회 결과.</param>
    /// <returns>Issue.</returns>
    public static Issue ToIssue(string className, WmiQueryResult result)
    {
        return result.Status switch
        {
            WmiQueryStatus.ClassUnavailable => new Issue(CannotVerifyReason.Unsupported, Format(ProbeStrings.Wmi_ClassUnavailable, className)),
            WmiQueryStatus.AccessDenied => new Issue(CannotVerifyReason.AccessDenied, Format(ProbeStrings.Wmi_AccessDenied, className)),
            WmiQueryStatus.Timeout => new Issue(CannotVerifyReason.Timeout, Format(ProbeStrings.Wmi_Timeout, className)),
            _ => new Issue(CannotVerifyReason.ProbeError, Format(ProbeStrings.Wmi_Error, className, result.ErrorCode ?? string.Empty)),
        };
    }

    /// <summary>
    /// 성공했지만 항목이 없는 조회의 Issue를 만듭니다(빈 성공으로 숨기지 않음).
    /// </summary>
    /// <param name="className">조회한 WMI 클래스 이름.</param>
    /// <returns>Issue.</returns>
    public static Issue EmptyIssue(string className)
    {
        return new Issue(CannotVerifyReason.Unsupported, Format(ProbeStrings.Wmi_Empty, className));
    }

    /// <summary>
    /// 행에서 정수 값을 읽습니다. 없거나 정수가 아니거나 범위를 넘으면 null.
    /// </summary>
    /// <param name="row">행.</param>
    /// <param name="property">속성 이름.</param>
    /// <returns>정수 값 또는 null.</returns>
    public static long? GetInt64(IReadOnlyDictionary<string, object?> row, string property)
    {
        if (!row.TryGetValue(property, out var raw))
        {
            return null;
        }

        return raw switch
        {
            byte value => value,
            ushort value => value,
            uint value => value,
            ulong value when value <= long.MaxValue => (long)value,
            short value => value,
            int value => value,
            long value => value,
            _ => null,
        };
    }

    /// <summary>
    /// 행에서 문자열 값을 읽습니다. 없거나 공백뿐이면 null, 있으면 앞뒤 공백을 제거합니다.
    /// </summary>
    /// <param name="row">행.</param>
    /// <param name="property">속성 이름.</param>
    /// <returns>문자열 또는 null.</returns>
    public static string? GetText(IReadOnlyDictionary<string, object?> row, string property)
    {
        return row.TryGetValue(property, out var raw) && raw is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
    }

    /// <summary>
    /// 행에서 부호 없는 16비트 정수 배열을 숫자 문자열 목록으로 읽습니다. 없으면 null.
    /// </summary>
    /// <param name="row">행.</param>
    /// <param name="property">속성 이름.</param>
    /// <returns>숫자 문자열 목록 또는 null.</returns>
    public static IReadOnlyList<string>? GetUInt16ArrayAsText(IReadOnlyDictionary<string, object?> row, string property)
    {
        return row.TryGetValue(property, out var raw) && raw is ushort[] values
            ? [.. values.Select(value => value.ToString(CultureInfo.InvariantCulture))]
            : null;
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채운다.
    /// </summary>
    private static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }
}
