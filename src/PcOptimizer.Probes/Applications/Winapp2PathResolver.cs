/**
 * @file    : Winapp2PathResolver.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 경로 템플릿의 %이름% 변수를 이 PC 값으로 펼치는 해석기(ProgramFiles·CommonProgramFiles는 64비트·x86 두 위치, Known Folder는 리디렉션 반영, 해석 실패·볼륨 루트·UNC·장치 경로는 미지원 사유로 알림, 와일드카드는 그대로 둠)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 경로 템플릿 해석 결과입니다.
/// </summary>
/// <param name="Paths">펼친 경로(정규화, 와일드카드 가능, 중복 없음). 실패면 비어 있음.</param>
/// <param name="Failure">실패 사유(성공이면 null).</param>
/// <param name="Detail">실패 보조 정보(변수 이름 등, 개인 경로 아님).</param>
public sealed record PathResolution(IReadOnlyList<string> Paths, UnsupportedRuleReason? Failure, string? Detail)
{
    /// <summary>성공 여부.</summary>
    public bool IsResolved => Failure is null;
}

/// <summary>
/// winapp2 형식 변수 해석기입니다. 표(<see cref="Winapp2Variables"/>)에 없는 변수는 해석하지 않습니다.
/// </summary>
public sealed class Winapp2PathResolver
{
    private const string ENV_APP_DATA = "APPDATA";
    private const string ENV_LOCAL_APP_DATA = "LOCALAPPDATA";
    private const string ENV_PROGRAM_FILES = "ProgramFiles";
    private const string ENV_PROGRAM_FILES_X86 = "ProgramFiles(x86)";
    private const string ENV_COMMON_PROGRAM_FILES = "CommonProgramFiles";
    private const string ENV_COMMON_PROGRAM_FILES_X86 = "CommonProgramFiles(x86)";
    private const string ENV_PROGRAM_DATA = "ProgramData";
    private const string ENV_SYSTEM_ROOT = "SystemRoot";
    private const string ENV_SYSTEM_DRIVE = "SystemDrive";
    private const string ENV_PUBLIC = "PUBLIC";
    private const string ENV_TEMP = "TEMP";
    private const string LOCAL_LOW_RELATIVE = @"AppData\LocalLow";
    private const string UNC_PREFIX = @"\\";
    private const string ALT_UNC_PREFIX = "//";

    private readonly IPathEnvironment _environment;

    /// <summary>
    /// 해석기를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경.</param>
    public Winapp2PathResolver(IPathEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>
    /// 템플릿을 펼칩니다.
    /// </summary>
    /// <param name="template">경로 템플릿.</param>
    /// <returns>해석 결과.</returns>
    public PathResolution Resolve(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!Winapp2Variables.TryGetNames(template, out var names))
        {
            return Fail(UnsupportedRuleReason.MalformedEntry, null);
        }

        IEnumerable<string> expanded = [template];
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var values = ValuesOf(name);
            if (values.Count == 0)
            {
                return Fail(UnsupportedRuleReason.UnresolvedVariable, name);
            }

            var token = Winapp2Variables.MARK + name + Winapp2Variables.MARK;
            expanded = expanded.SelectMany(path => values.Select(value => path.Replace(token, value.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))).ToList();
        }

        var paths = new List<string>();
        foreach (var path in expanded)
        {
            var trimmed = path.Trim();
            if (trimmed.StartsWith(UNC_PREFIX, StringComparison.Ordinal) || trimmed.StartsWith(ALT_UNC_PREFIX, StringComparison.Ordinal))
            {
                return Fail(UnsupportedRuleReason.UncOrDevicePath, null);
            }

            if (!PathScope.IsDriveAbsolute(trimmed + PathScope.SEPARATOR))
            {
                return Fail(UnsupportedRuleReason.MalformedEntry, null);
            }

            if (PathScope.IsDriveRoot(trimmed))
            {
                return Fail(UnsupportedRuleReason.VolumeRootPath, null);
            }

            var normalized = PathScope.Normalize(trimmed);
            if (!paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(normalized);
            }
        }

        return new PathResolution(paths.AsReadOnly(), null, null);
    }

    /// <summary>
    /// 변수 하나의 값(여러 위치일 수 있음). 없으면 빈 목록.
    /// </summary>
    private List<string> ValuesOf(string name)
    {
        string? Env(string variable) => _environment.GetEnvironmentVariable(variable);
        string? Known(ProtectedKnownFolder folder) => _environment.GetKnownFolderPath(folder);

        string?[] values = name switch
        {
            var n when Is(n, Winapp2Variables.APP_DATA) => [Env(ENV_APP_DATA)],
            var n when Is(n, Winapp2Variables.LOCAL_APP_DATA) => [Env(ENV_LOCAL_APP_DATA)],
            var n when Is(n, Winapp2Variables.LOCAL_LOW_APP_DATA) => [_environment.GetUserProfilePath() is { } profile ? Path.Join(profile, LOCAL_LOW_RELATIVE) : null],
            var n when Is(n, Winapp2Variables.USER_PROFILE) => [_environment.GetUserProfilePath()],
            var n when Is(n, Winapp2Variables.PROGRAM_FILES) => [Env(ENV_PROGRAM_FILES), Env(ENV_PROGRAM_FILES_X86)],
            var n when Is(n, Winapp2Variables.COMMON_PROGRAM_FILES) => [Env(ENV_COMMON_PROGRAM_FILES), Env(ENV_COMMON_PROGRAM_FILES_X86)],
            var n when Is(n, Winapp2Variables.COMMON_APP_DATA) || Is(n, Winapp2Variables.PROGRAM_DATA) => [Env(ENV_PROGRAM_DATA)],
            var n when Is(n, Winapp2Variables.WIN_DIR) => [Env(ENV_SYSTEM_ROOT)],
            var n when Is(n, Winapp2Variables.SYSTEM_DRIVE) => [Env(ENV_SYSTEM_DRIVE)],
            var n when Is(n, Winapp2Variables.DOCUMENTS) => [Known(ProtectedKnownFolder.Documents)],
            var n when Is(n, Winapp2Variables.PICTURES) => [Known(ProtectedKnownFolder.Pictures)],
            var n when Is(n, Winapp2Variables.MUSIC) => [Known(ProtectedKnownFolder.Music)],
            var n when Is(n, Winapp2Variables.VIDEO) => [Known(ProtectedKnownFolder.Videos)],
            var n when Is(n, Winapp2Variables.PUBLIC) => [Env(ENV_PUBLIC)],
            var n when Is(n, Winapp2Variables.TEMP) => [Env(ENV_TEMP)],
            _ => [],
        };

        return [.. values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// 변수 이름 비교(대소문자 무시).
    /// </summary>
    private static bool Is(string name, string expected)
    {
        return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 실패 결과.
    /// </summary>
    private static PathResolution Fail(UnsupportedRuleReason reason, string? detail)
    {
        return new PathResolution([], reason, detail);
    }
}
