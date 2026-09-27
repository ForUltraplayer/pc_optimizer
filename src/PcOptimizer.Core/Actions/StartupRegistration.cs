/**
 * @file    : StartupRegistration.cs
 * @author  : rudals252
 * @brief   : 현재 사용자 Run 등록의 제한된 이름·복구 키와 원문 값 검증
 */
using System.Text;

namespace PcOptimizer.Core.Actions;

/// <summary>실행 명령을 해석하지 않고 등록 이름과 원문 형식만 검증합니다.</summary>
public static class StartupRegistration
{
    /// <summary>64비트 현재 사용자 Run만 지원합니다.</summary>
    public const string Source = "hkcu.run";
    private const string Prefix = "hkcu-run-v1:";
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding StrictUnicode = new UnicodeEncoding(false, false, true);
    /// <summary>복구 키 길이와 화면 표시를 제한하며 기본값/제어 문자는 허용하지 않습니다.</summary>
    public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 100
        && !name.Any(c => char.IsControl(c) || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format);
    /// <summary>등록 이름만 복구 키로 저장합니다. 경로나 명령으로 사용하지 않습니다.</summary>
    public static string Key(string name) => ValidName(name) ? Prefix + Convert.ToBase64String(StrictUtf8.GetBytes(name)) : throw new ArgumentException("Unsupported name");
    /// <summary>정규 인코딩의 복구 키에서 이름을 얻습니다.</summary>
    public static string? Name(string key)
    {
        if (!key.StartsWith(Prefix, StringComparison.Ordinal) || key.Length > 512) { return null; }
        try
        {
            var name = StrictUtf8.GetString(Convert.FromBase64String(key[Prefix.Length..]));
            return ValidName(name) && Key(name) == key ? name : null;
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { return null; }
    }
    /// <summary>유효한 종료 문자를 가진 문자열만 지원하고 원본 바이트는 그대로 보관합니다.</summary>
    public static bool ValidValue(RollbackValue value)
    {
        if (!value.Exists || value.NativeType is not (1 or 2) || value.Data.Length is < 4 or > 32768 || value.Data.Length % 2 != 0) { return false; }
        try
        {
            var text = StrictUnicode.GetString(value.Data);
            return text[^1] == '\0' && !string.IsNullOrWhiteSpace(text[..^1]) && !text[..^1].Contains('\0');
        }
        catch (DecoderFallbackException) { return false; }
    }
    /// <summary>등록 해제 뒤의 정확한 상태입니다.</summary>
    public static RollbackValue Absent => new(false, 0, []);
}
