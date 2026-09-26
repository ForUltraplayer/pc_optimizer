/**
 * @file    : ProbeText.cs
 * @author  : rudals252
 * @brief   : 프로브가 Issue 요약 문장을 리소스 형식 문자열로 채우는 공용 도우미
 */

// 기본 패키지
using System.Globalization;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 프로브 문장 도우미입니다. 문장 자체는 리소스(ProbeStrings)에 있고 여기서는 채우기만 합니다.
/// </summary>
internal static class ProbeText
{
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
