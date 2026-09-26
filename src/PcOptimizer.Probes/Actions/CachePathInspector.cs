/**
 * @file    : CachePathInspector.cs
 * @author  : rudals252
 * @brief   : 실행 직전 캐시 보호 관문(도구 보호 위치·경로·링크·예산)과 주입 가능한 파일 시스템·Program Files 위치·시간 경계
 */
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions;

/// <summary>
/// 실제 실행기와 테스트가 같은 보호 관문을 사용합니다.
/// <paramref name="programRoot"/>는 <see cref="PROTECTED_PROGRAM_ROOTS"/>의 이름별 Program Files 계열 경로를 돌려줍니다(없으면 null).
/// </summary>
internal sealed class CachePathInspector(IPathEnvironment environment, IRegistryReader registry,
    IDirectoryEntrySource source, Func<string, string?> programRoot, Func<string?> currentSid,
    Func<string?> readPolicy, TimeProvider time, TimeSpan budget, int maxEntries = 100000)
{
    /// <summary>보호 위치 %ProgramFiles%의 이름입니다.</summary>
    internal const string PROGRAM_FILES = "ProgramFiles";

    /// <summary>보호 위치 %ProgramFiles(x86)%의 이름입니다.</summary>
    internal const string PROGRAM_FILES_X86 = "ProgramFiles(x86)";

    /// <summary>보호 위치 %ProgramW6432%의 이름입니다.</summary>
    internal const string PROGRAM_W6432 = "ProgramW6432";

    /// <summary>관리자 권한 앱이 도구를 실행해도 되는 보호 위치의 이름 목록입니다.</summary>
    internal static readonly IReadOnlyList<string> PROTECTED_PROGRAM_ROOTS = [PROGRAM_FILES, PROGRAM_FILES_X86, PROGRAM_W6432];

    /// <summary>없는 말단 경로도 기존 조상의 긴 이름을 먼저 해석합니다. 해석하지 못한 별칭은 거절합니다.</summary>
    internal static string CanonicalPath(IPathEnvironment environment, string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.IndexOf(':', 2) >= 0) { throw new ArgumentException("UnsafePath"); }
        var full = Path.GetFullPath(path);
        var result = Path.GetPathRoot(full)!;
        foreach (var segment in full[result.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            result = environment.NormalizePath(Path.Combine(result, segment));
            if (result.Contains('~', StringComparison.Ordinal)) { throw new ArgumentException("UnresolvedAlias"); }
        }
        return environment.NormalizePath(result);
    }

    /// <summary>
    /// 도구 파일이 보호 위치(Program Files 계열) 아래인지 확인합니다. 도구 경로와 각 보호 위치 모두 <see cref="CanonicalPath"/>로
    /// 정규화(8.3 별칭 해소·상대 구간 제거)한 뒤 폴더 경계 접두로 비교합니다(대소문자 무시). 비어 있거나 드라이브 루트·상대 경로인 값은 건너뜁니다.
    /// </summary>
    /// <param name="path">도구 실행 파일 또는 npm 진입 파일 경로.</param>
    /// <returns>보호 위치 아래이면 true. 정규화할 수 없는 경로는 false.</returns>
    internal bool IsProtectedProgramLocation(string path) => IsProtectedProgramLocation(environment, programRoot, path);

    /// <summary>
    /// <see cref="IsProtectedProgramLocation(string)"/>의 공용 판정입니다. 정리 창 노출 판정도 실행 규칙과 같은 이 함수를 씁니다.
    /// </summary>
    /// <param name="environment">경로 정규화 환경.</param>
    /// <param name="programRoot">보호 위치 이름별 경로(없으면 null).</param>
    /// <param name="path">도구 실행 파일 또는 npm 진입 파일 경로.</param>
    /// <returns>보호 위치 아래이면 true.</returns>
    internal static bool IsProtectedProgramLocation(IPathEnvironment environment, Func<string, string?> programRoot, string path)
    {
        string canonical;
        try { canonical = CanonicalPath(environment, path); }
        catch (ArgumentException) { return false; }
        foreach (var name in PROTECTED_PROGRAM_ROOTS)
        {
            var value = programRoot(name);
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) { continue; }
            string root;
            try { root = CanonicalPath(environment, value).TrimEnd('\\'); }
            catch (ArgumentException) { continue; }
            // 드라이브 루트가 보호 위치로 설정되면 모든 경로가 통과하므로 인정하지 않는다.
            if (string.Equals(Path.GetPathRoot(root)?.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (canonical.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) { return true; }
        }
        return false;
    }

    /// <summary>상위 폴더를 포함해 일반 항목인지 공급자로 확인합니다.</summary>
    private bool Plain(string path, bool directory) => SystemCacheToolBackend.IsPlainPath(path, directory, source);

    /// <summary>보호 경로·다른 사용자·보호 위치 밖 도구·링크·부분 관측은 실행 불가입니다. 사용자 데이터 본문은 읽지 않습니다.</summary>
    internal CacheInspection Inspect(CacheToolLocation location, CancellationToken ct)
    {
        try
        {
            var policy = ProtectionPolicyParser.Parse(readPolicy() ?? string.Empty);
            if (policy.Policy is null) { return new(false, 0, "ProtectionUnavailable"); }
            var resolved = new ProtectionPolicyResolver(environment, registry).Resolve(policy.Policy);
            var protection = new ResolvedProtection(resolved.Roots.Select(item => item with { Path = CanonicalPath(environment, item.Path) }), resolved.UnresolvedCount);
            var otherUsers = OtherUserLocationGuard.Create(registry, environment, currentSid());
            var root = CanonicalPath(environment, location.CachePath);
            if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal) || root.IndexOf(':', 2) >= 0
                || Path.GetPathRoot(root)?.TrimEnd('\\') == root || root.Length < 4) { return new(false, 0, "UnsafePath"); }
            var profile = environment.GetUserProfilePath() is { } value ? CanonicalPath(environment, value) : null;
            if (profile is null || string.Equals(root, profile, StringComparison.OrdinalIgnoreCase)
                || protection.IsProtected(root) || otherUsers.IsOtherUserLocation(root)
                || protection.Roots.Any(item => item.Path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))) { return new(false, 0, "ProtectedPath"); }
            // 기존 도구 실행은 공유·시스템 영역까지 확장하지 않는다. 1차는 현재 프로필의 캐시만 지원한다.
            if (!root.StartsWith(profile.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { return new(false, 0, "OutsideUserProfile"); }
            // 관리자 권한 앱은 사용자 쓰기 가능 위치의 도구를 실행하지 않는다(실행 직전 재확인 포함).
            if (!IsProtectedProgramLocation(location.Executable) || (location.Script is not null && !IsProtectedProgramLocation(location.Script)))
            { return new(false, 0, SystemCacheToolBackend.TOOL_NOT_IN_PROTECTED_LOCATION); }
            if (!Plain(location.Executable, false) || (location.Script is not null && !Plain(location.Script, false))) { return new(false, 0, "ToolChanged"); }
            if (source.ProbeRoot(root) == RootPresence.Missing)
            {
                var parent = Path.GetDirectoryName(root);
                return parent is not null && Plain(parent, true) && source.ProbeRoot(root) == RootPresence.Missing
                    ? new(true, 0, null) : new(false, 0, "UnreadablePath");
            }
            if (!Plain(root, true)) { return new(false, 0, "LinkOrPlaceholder"); }
            var started = time.GetTimestamp();
            long bytes = 0;
            var count = 0;
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.TryPop(out var directory))
            {
                if (!Plain(directory, true) || protection.IsProtected(directory)) { return new(false, 0, "ProtectedOrLinkedChild"); }
                if (time.GetElapsedTime(started) >= budget) { return new(false, 0, "InspectionIncomplete"); }
                foreach (var entry in source.Enumerate(directory))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++count > maxEntries || time.GetElapsedTime(started) >= budget) { return new(false, 0, "InspectionIncomplete"); }
                    if ((entry.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x40000 | (FileAttributes)0x400000)) != 0) { return new(false, 0, "LinkOrPlaceholder"); }
                    if (entry.IsDirectory) { stack.Push(Path.Combine(directory, entry.Name)); }
                    else
                    {
                        // HTTP 캐시를 전역 패키지로 잘못 설정한 경우에도 정리하지 않는다.
                        if (location.Tool == CacheTool.NuGetHttp && !entry.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) { return new(false, 0, "UnexpectedHttpCacheContent"); }
                        bytes = checked(bytes + entry.Length);
                    }
                }
            }
            ct.ThrowIfCancellationRequested();
            return time.GetElapsedTime(started) >= budget ? new(false, 0, "InspectionIncomplete") : new(true, bytes, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or OverflowException)
        { return new(false, 0, "InspectionFailed"); }
    }
}
