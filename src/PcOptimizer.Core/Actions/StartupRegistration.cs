/**
 * @file    : StartupRegistration.cs
 * @author  : rudals252
 * @brief   : Run·시작 폴더·작업 관리자 상태(StartupApproved) 출처의 제한된 이름·복구 키와 원문 값 검증
 */
using System.Text;

namespace PcOptimizer.Core.Actions;

/// <summary>실행 명령을 해석하지 않고 등록 이름과 원문 형식만 검증합니다.</summary>
public static class StartupRegistration
{
    /// <summary>64비트 현재 사용자 Run만 지원합니다.</summary>
    public const string Source = "hkcu.run";
    /// <summary>모든 사용자에게 적용되는 64비트 Run 출처입니다.</summary>
    public const string Machine64 = "hklm64.run";
    /// <summary>모든 사용자에게 적용되는 32비트 Run 출처입니다.</summary>
    public const string Machine32 = "hklm32.run";
    /// <summary>현재 사용자 시작 폴더의 바로가기입니다.</summary>
    public const string UserFolder = "folder.user";
    /// <summary>모든 사용자 시작 폴더의 바로가기입니다.</summary>
    public const string CommonFolder = "folder.common";
    /// <summary>현재 사용자 Run 항목의 작업 관리자 사용/사용 안 함 상태(StartupApproved\Run)입니다.</summary>
    public const string ApprovalUser = "hkcu.approved";
    /// <summary>모든 사용자 64비트 Run 항목의 작업 관리자 상태(HKLM StartupApproved\Run)입니다.</summary>
    public const string ApprovalMachine64 = "hklm64.approved";
    /// <summary>모든 사용자 32비트 Run 항목의 작업 관리자 상태(HKLM StartupApproved\Run32)입니다.</summary>
    public const string ApprovalMachine32 = "hklm32.approved";
    /// <summary>현재 사용자 시작 폴더 바로가기의 작업 관리자 상태(HKCU StartupApproved\StartupFolder)입니다.</summary>
    public const string ApprovalUserFolder = "hkcu.approvedFolder";
    /// <summary>모든 사용자 시작 폴더 바로가기의 작업 관리자 상태(HKLM StartupApproved\StartupFolder)입니다.</summary>
    public const string ApprovalCommonFolder = "hklm.approvedFolder";
    private const string Prefix = "hkcu-run-v1:";
    private static string? PrefixFor(string source) => source switch
    { Source => Prefix, Machine64 => "hklm64-run-v1:", Machine32 => "hklm32-run-v1:", UserFolder => "startup-user-file-v1:", CommonFolder => "startup-common-file-v1:",
      ApprovalUser => "hkcu-approved-v1:", ApprovalMachine64 => "hklm64-approved-v1:", ApprovalMachine32 => "hklm32-approved-v1:", ApprovalUserFolder => "hkcu-approvedfolder-v1:", ApprovalCommonFolder => "hklm-approvedfolder-v1:", _ => null };
    /// <summary>작업 관리자 상태(StartupApproved) 출처인지 판정합니다.</summary>
    public static bool IsApproval(string? source) => source is ApprovalUser or ApprovalMachine64 or ApprovalMachine32 or ApprovalUserFolder or ApprovalCommonFolder;
    /// <summary>모든 허용 출처입니다.</summary>
    public static readonly IReadOnlyList<string> Sources = [Source, Machine64, Machine32, UserFolder, CommonFolder, ApprovalUser, ApprovalMachine64, ApprovalMachine32, ApprovalUserFolder, ApprovalCommonFolder];
    /// <summary>기계 전체에 영향을 주는 허용 출처인지 판정합니다.</summary>
    public static bool IsMachine(string source) => source is Machine64 or Machine32 or CommonFolder or ApprovalMachine64 or ApprovalMachine32 or ApprovalCommonFolder;
    /// <summary>출처별 조치 ID를 구분합니다.</summary>
    public static ActionId ActionFor(string source) => source switch
    { UserFolder => ActionId.StartupFolder, CommonFolder => ActionId.CommonStartupFolder, Machine64 or Machine32 => ActionId.MachineStartup,
      ApprovalUser or ApprovalUserFolder => ActionId.StartupApproval, ApprovalMachine64 or ApprovalMachine32 or ApprovalCommonFolder => ActionId.MachineStartupApproval, _ => ActionId.Startup };
    /// <summary>동일 이름을 가진 출처를 구분하는 사용자 표시입니다.</summary>
    public static string Label(string source) => source switch
    { Source => "현재 사용자", Machine64 => "모든 사용자 · 64비트", Machine32 => "모든 사용자 · 32비트", UserFolder => "현재 사용자 시작 폴더", CommonFolder => "모든 사용자 시작 폴더",
      ApprovalUser => "현재 사용자 · 작업 관리자 상태", ApprovalMachine64 => "모든 사용자 · 64비트 · 작업 관리자 상태", ApprovalMachine32 => "모든 사용자 · 32비트 · 작업 관리자 상태",
      ApprovalUserFolder => "현재 사용자 시작 폴더 · 작업 관리자 상태", ApprovalCommonFolder => "모든 사용자 시작 폴더 · 작업 관리자 상태", _ => "지원하지 않는 출처" };
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding StrictUnicode = new UnicodeEncoding(false, false, true);
    /// <summary>복구 키 길이와 화면 표시를 제한하며 기본값/제어 문자는 허용하지 않습니다.</summary>
    public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 100
        && !name.Any(c => char.IsControl(c) || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format);
    /// <summary>등록 이름만 복구 키로 저장합니다. 경로나 명령으로 사용하지 않습니다.</summary>
    public static string Key(string name) => ValidName(name) ? Prefix + Convert.ToBase64String(StrictUtf8.GetBytes(name)) : throw new ArgumentException("Unsupported name");
    /// <summary>출처와 이름을 함께 저장하며 기존 HKCU 복구 키와 호환됩니다.</summary>
    public static string Key(string source, string name) => PrefixFor(source) is { } prefix && ValidName(name)
        ? prefix + Convert.ToBase64String(StrictUtf8.GetBytes(name)) : throw new ArgumentException("Unsupported target");
    /// <summary>허용된 버전의 키에서 출처를 얻습니다. 이름도 별도로 검증해야 합니다.</summary>
    public static string? SourceOfKey(string key) => Sources.FirstOrDefault(s => key.StartsWith(PrefixFor(s)!, StringComparison.Ordinal));
    /// <summary>정규 인코딩의 복구 키에서 이름을 얻습니다.</summary>
    public static string? Name(string key)
    {
        if (SourceOfKey(key) is not { } source || key.Length > 512) { return null; }
        try
        {
            var name = StrictUtf8.GetString(Convert.FromBase64String(key[PrefixFor(source)!.Length..]));
            return ValidName(name) && Key(source, name) == key ? name : null;
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
