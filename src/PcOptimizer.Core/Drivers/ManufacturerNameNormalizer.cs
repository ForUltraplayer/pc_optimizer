/**
 * @file    : ManufacturerNameNormalizer.cs
 * @author  : rudals252
 * @brief   : 제조사 이름을 비교용으로 정규화(소문자, 문자·숫자 외 문장 부호를 공백으로, 끝의 Inc./Corp./Co., Ltd./Corporation 등 회사 형태 제거)하는 순수 도우미
 */

// 기본 패키지
using System.Text;

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 제조사 이름 정규화 도우미입니다. 예: "Micro-Star International Co., Ltd." → "micro star international", "Dell Inc." → "dell".
/// 접두사·부분 일치 추측을 하지 않으며 비교는 정규화한 문자열 전체 일치로만 합니다.
/// </summary>
public static class ManufacturerNameNormalizer
{
    private const char SPACE = ' ';

    /// <summary>끝에서 제거하는 회사 형태 낱말.</summary>
    private static readonly HashSet<string> CORPORATE_SUFFIXES = new(StringComparer.Ordinal)
    {
        "inc",
        "incorporated",
        "corp",
        "corporation",
        "co",
        "ltd",
        "limited",
        "llc",
    };

    /// <summary>
    /// 제조사 이름을 정규화합니다.
    /// </summary>
    /// <param name="raw">원래 이름(없으면 null).</param>
    /// <returns>정규화한 이름(값이 없으면 빈 문자열).</returns>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw.Trim())
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : SPACE);
        }

        var tokens = builder.ToString().Split(SPACE, StringSplitOptions.RemoveEmptyEntries).ToList();
        while (tokens.Count > 1 && CORPORATE_SUFFIXES.Contains(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        return string.Join(SPACE, tokens);
    }
}
