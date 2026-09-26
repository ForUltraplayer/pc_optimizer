/**
 * @file    : Explanation.cs
 * @author  : rudals252
 * @brief   : 초보자용 설명 3줄(이게 뭔가요/효과/주의). 각 줄은 한 문장 60자 이내
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 카드에 표시하는 설명 3줄입니다. 근거 링크·난이도·용어 사전은 두지 않습니다.
/// </summary>
public sealed record Explanation
{
    /// <summary>한 줄 최대 길이(문자 수).</summary>
    public const int MAX_LINE_LENGTH = 60;

    /// <summary>설명 3줄을 만듭니다. 각 줄은 비어 있지 않고 60자 이내여야 합니다.</summary>
    /// <param name="what">"이게 뭔가요" 한 줄.</param>
    /// <param name="effect">효과 한 줄.</param>
    /// <param name="caution">주의 한 줄.</param>
    /// <exception cref="ArgumentException">비어 있거나 60자를 넘는 줄이 있는 경우.</exception>
    public Explanation(string what, string effect, string caution)
    {
        What = Validate(what, nameof(what));
        Effect = Validate(effect, nameof(effect));
        Caution = Validate(caution, nameof(caution));
    }

    /// <summary>"이게 뭔가요" 한 줄.</summary>
    public string What { get; }

    /// <summary>효과 한 줄.</summary>
    public string Effect { get; }

    /// <summary>주의 한 줄.</summary>
    public string Caution { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("설명 줄은 비어 있을 수 없습니다.", parameterName);
        }

        if (value.Length > MAX_LINE_LENGTH)
        {
            throw new ArgumentException($"설명 줄은 {MAX_LINE_LENGTH}자 이내여야 합니다.", parameterName);
        }

        return value;
    }
}
