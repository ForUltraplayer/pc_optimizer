/**
 * @file    : SteamLibraryFoldersParser.cs
 * @author  : rudals252
 * @brief   : Steam libraryfolders.vdf(Valve KeyValues 텍스트)에서 "path" 키의 문자열 값만 뽑는 관대한 순수 파서(따옴표 문자열·역슬래시 이스케이프·중괄호 블록·// 주석 처리, 닫히지 않은 문자열·짝이 안 맞는 중괄호는 실패)
 */

// 기본 패키지
using System.Text;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// libraryfolders.vdf 파서입니다. 라이브러리 경로 외의 값(앱 ID·크기·레이블)은 보관하지 않습니다.
/// </summary>
public static class SteamLibraryFoldersParser
{
    /// <summary>읽을 라이브러리 경로 최대 수.</summary>
    public const int MAX_LIBRARIES = 64;

    private const string PATH_KEY = "path";
    private const string OPEN_BRACE = "{";
    private const string CLOSE_BRACE = "}";
    private const char OPEN_BRACE_CHAR = '{';
    private const char CLOSE_BRACE_CHAR = '}';
    private const char TAB = '\t';
    private const char QUOTE = '"';
    private const char ESCAPE = '\\';
    private const char SLASH = '/';
    private const char LINE_FEED = '\n';
    private const char ESCAPED_NEW_LINE = 'n';
    private const char ESCAPED_TAB = 't';
    private const int MAX_DEPTH = 32;

    /// <summary>
    /// "path" 키의 값을 순서대로 뽑습니다.
    /// </summary>
    /// <param name="text">VDF 텍스트.</param>
    /// <returns>경로 값 목록(원문, 중복 제거 전), 형식 오류면 null.</returns>
    public static IReadOnlyList<string>? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = Tokenize(text);
        if (tokens is null)
        {
            return null;
        }

        var paths = new List<string>();
        var index = 0;
        return ParseBlock(tokens, ref index, depth: 0, paths) && index == tokens.Count && paths.Count <= MAX_LIBRARIES ? paths.AsReadOnly() : null;
    }

    /// <summary>
    /// 키-값(또는 키-블록) 쌍을 닫는 중괄호나 끝까지 읽는다.
    /// </summary>
    private static bool ParseBlock(List<Token> tokens, ref int index, int depth, List<string> paths)
    {
        if (depth > MAX_DEPTH)
        {
            return false;
        }

        while (index < tokens.Count && !tokens[index].Is(CLOSE_BRACE))
        {
            var key = tokens[index];
            if (key.IsBrace || index + 1 >= tokens.Count)
            {
                return false;
            }

            var next = tokens[index + 1];
            index += 2;
            if (next.Is(OPEN_BRACE))
            {
                if (!ParseBlock(tokens, ref index, depth + 1, paths) || index >= tokens.Count || !tokens[index].Is(CLOSE_BRACE))
                {
                    return false;
                }

                index++;
            }
            else if (next.IsBrace)
            {
                return false;
            }
            else if (string.Equals(key.Text, PATH_KEY, StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(next.Text);
            }
        }

        return depth > 0 || index == tokens.Count;
    }

    /// <summary>
    /// 문자열·중괄호 토큰으로 나눈다(// 주석 건너뜀). 닫히지 않은 문자열이면 null.
    /// </summary>
    private static List<Token>? Tokenize(string text)
    {
        var tokens = new List<Token>();
        var index = 0;
        while (index < text.Length)
        {
            var c = text[index];
            if (char.IsWhiteSpace(c))
            {
                index++;
            }
            else if (c == SLASH && index + 1 < text.Length && text[index + 1] == SLASH)
            {
                var end = text.IndexOf(LINE_FEED, index);
                index = end < 0 ? text.Length : end + 1;
            }
            else if (c is OPEN_BRACE_CHAR or CLOSE_BRACE_CHAR)
            {
                tokens.Add(new Token(c.ToString(), IsBrace: true));
                index++;
            }
            else if (c == QUOTE)
            {
                if (ReadQuoted(text, ref index) is not { } value)
                {
                    return null;
                }

                tokens.Add(new Token(value, IsBrace: false));
            }
            else
            {
                var start = index;
                while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not (QUOTE or OPEN_BRACE_CHAR or CLOSE_BRACE_CHAR))
                {
                    index++;
                }

                tokens.Add(new Token(text[start..index], IsBrace: false));
            }
        }

        return tokens;
    }

    /// <summary>
    /// 따옴표 문자열 하나를 읽는다(이스케이프 해제). 줄이 끝나기 전에 닫히지 않으면 null.
    /// </summary>
    private static string? ReadQuoted(string text, ref int index)
    {
        var builder = new StringBuilder();
        index++;
        while (index < text.Length)
        {
            var c = text[index];
            if (c == LINE_FEED)
            {
                return null;
            }

            if (c == QUOTE)
            {
                index++;
                return builder.ToString();
            }

            if (c == ESCAPE && index + 1 < text.Length)
            {
                var escaped = text[index + 1];
                builder.Append(escaped switch
                {
                    ESCAPED_NEW_LINE => LINE_FEED,
                    ESCAPED_TAB => TAB,
                    _ => escaped,
                });
                index += 2;
                continue;
            }

            builder.Append(c);
            index++;
        }

        return null;
    }

    /// <summary>
    /// 토큰(문자열 또는 중괄호).
    /// </summary>
    private readonly record struct Token(string Text, bool IsBrace)
    {
        /// <summary>
        /// 지정한 중괄호인지 확인한다.
        /// </summary>
        public bool Is(string brace)
        {
            return IsBrace && Text == brace;
        }
    }
}
