/**
 * @file    : DriverVersionNumber.cs
 * @author  : rudals252
 * @brief   : NVIDIA 표기 드라이버 버전("주.부", 예: 617.14)을 두 정수로 해석해 수치로 비교하는 값 형식과 응답 형식(세·네 자리.두 자리) 검사
 */

// 기본 패키지
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// NVIDIA 표기 드라이버 버전입니다. "617.14"와 "617.9"는 소수가 아니라 부 버전 정수(14 &gt; 9)로 비교합니다.
/// </summary>
/// <param name="Major">주 버전.</param>
/// <param name="Minor">부 버전.</param>
public readonly partial record struct DriverVersionNumber(int Major, int Minor) : IComparable<DriverVersionNumber>
{
    private const string MAJOR_GROUP = "major";
    private const string MINOR_GROUP = "minor";
    private const int REGEX_TIMEOUT_MILLISECONDS = 100;

    /// <summary>
    /// "숫자.숫자"(ASCII 숫자, 주 1~4자리, 부 1~3자리) 문자열을 해석합니다. 앞뒤 공백·부호·다른 문자는 허용하지 않습니다.
    /// </summary>
    /// <param name="text">버전 문자열.</param>
    /// <param name="version">해석한 버전.</param>
    /// <returns>해석했으면 true.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out DriverVersionNumber version)
    {
        version = default;
        if (text is null)
        {
            return false;
        }

        var match = LenientPattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        version = new DriverVersionNumber(
            int.Parse(match.Groups[MAJOR_GROUP].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[MINOR_GROUP].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture));
        return true;
    }

    /// <summary>
    /// NVIDIA 조회 응답의 버전 형식(주 3~4자리, 부 정확히 2자리, 예: 617.14)인지 확인합니다.
    /// </summary>
    /// <param name="text">버전 문자열.</param>
    /// <returns>형식이 맞으면 true.</returns>
    public static bool IsNvidiaResponseFormat([NotNullWhen(true)] string? text)
    {
        return text is not null && StrictPattern().IsMatch(text);
    }

    /// <inheritdoc />
    public int CompareTo(DriverVersionNumber other)
    {
        var major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    /// <summary>
    /// 해석용 정규식(주 1~4자리, 부 1~3자리 ASCII 숫자).
    /// </summary>
    [GeneratedRegex(@"^(?<major>[0-9]{1,4})\.(?<minor>[0-9]{1,3})$", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex LenientPattern();

    /// <summary>
    /// NVIDIA 응답 형식 정규식(주 3~4자리, 부 2자리).
    /// </summary>
    [GeneratedRegex(@"^[0-9]{3,4}\.[0-9]{2}$", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex StrictPattern();
}
