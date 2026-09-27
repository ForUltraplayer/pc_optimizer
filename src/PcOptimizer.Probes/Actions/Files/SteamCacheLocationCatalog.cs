/**
 * @file    : SteamCacheLocationCatalog.cs
 * @author  : rudals252
 * @brief   : 명시적으로 선택하고 현재 라이브러리 설정과 대조한 shadercache만 실행 대상으로 등록
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>Steam 다운로드 캐시나 설치 게임과 구분한 세션 한정 셰이더 캐시 선택입니다.</summary>
public sealed class SteamCacheLocationCatalog
{
    private readonly Dictionary<string, (string Path, ActionSession Session)> _locations = new();
    private readonly object _sync = new();
    /// <summary>선택 경로 형식만 등록합니다. 파일 접근은 공통 준비 워커에서 수행합니다.</summary>
    public (string? Key, string? Code) TryRegister(string path, ActionSession session)
    {
        try
        {
            if (!session.IsKnown || session.Scope != ActionUserScope.Full) { return (null, "ScopeExcluded"); }
            var full = Normalize(path);
            lock (_sync)
            {
                var found = _locations.FirstOrDefault(p => p.Value.Session == session && p.Value.Path.Equals(full, StringComparison.OrdinalIgnoreCase));
                if (found.Key is not null) { return (found.Key, null); }
                if (_locations.Count >= 16) { return (null, "SteamLocationsFull"); }
                var key = "steam-selected-" + Guid.NewGuid().ToString("N");
                _locations.Add(key, (full, session));
                return (key, null);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return (null, "SteamLocationUnsupported"); }
    }
    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length is < 4 or > 1024 || path.Any(char.IsControl)
            || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\'
            || path[2..].IndexOfAny([':', '%', '~', '/', '*', '?']) >= 0) { throw new ArgumentException("InvalidPath"); }
        var clean = path.TrimEnd('\\');
        if (clean.Split('\\').Skip(1).Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.')))
        { throw new ArgumentException("InvalidPath"); }
        var full = Path.GetFullPath(clean);
        if (!Path.GetFileName(full).Equals("shadercache", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(Path.GetDirectoryName(full)), "steamapps", StringComparison.OrdinalIgnoreCase))
        { throw new ArgumentException("ShaderCacheRequired"); }
        return full;
    }
    internal CleanupTarget Resolve(string key, ActionSession session)
    {
        (string Path, ActionSession Session) location;
        lock (_sync)
        {
            if (!_locations.TryGetValue(key, out location)) { throw new ActionUnavailableException("TargetRejected"); }
        }
        if (location.Session != session || session.Scope != ActionUserScope.Full || SystemActionSession.Read() != session)
        { throw new ActionUnavailableException("SessionChanged"); }
        _ = RollbackStore.CurrentRoot(session.Sid);
        var env = SystemPathEnvironment.Instance;
        string Canonical(string p) => CachePathInspector.CanonicalPath(env, p);
        var root = Canonical(location.Path);
        if (!root.Equals(location.Path, StringComparison.OrdinalIgnoreCase)) { throw new ActionUnavailableException("TargetChanged"); }
        var drive = new DriveInfo(Path.GetPathRoot(root)!);
        if (!drive.IsReady || drive.DriveType != DriveType.Fixed) { throw new ActionUnavailableException("SteamLocationUnsupported"); }
        Func<string, bool> protect;
        try { protect = UserTempTargets.ProtectionFor(root, session); }
        catch (UnauthorizedAccessException) { throw new ActionUnavailableException("SteamProtectedLocation"); }
        // Program Files 예외는 설정 파일의 제한된 읽기에만 적용한다. 삭제 보호에는 적용하지 않는다.
        var policy = ProtectionPolicyParser.Parse(FileScanService.ReadBundledPolicy() ?? "").Policy
            ?? throw new ActionUnavailableException("TargetRejected");
        var protection = new ProtectionPolicyResolver(env, Win32RegistryReader.Instance).Resolve(policy);
        var others = UserTempTargets.OtherProfiles(Win32RegistryReader.Instance, env, session.Sid);
        var programFiles = new[] { "ProgramFiles", "ProgramFiles(x86)" }.Select(env.GetEnvironmentVariable)
            .OfType<string>().Select(Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool ConfigAllowed(string candidate)
        {
            var full = Canonical(candidate);
            return !others.Any(p => PathScope.IsSameOrUnder(full, p)) && !protection.SourceRoots.Any(p => PathScope.IsSameOrUnder(full, Canonical(p.Path))
                && !(p.Origin == ProtectedRootOrigin.SystemPath && p.Label is "%ProgramFiles%" or "%ProgramFiles(x86)%" && programFiles.Contains(Canonical(p.Path))));
        }
        var config = new SteamLibraryReader(env, Win32RegistryReader.Instance, FileSystemDirectoryEntrySource.Instance, ConfigAllowed).Read();
        if (config.State != AppConfigReadState.Configured || config.Paths.Count is < 1 or > 65)
        { throw new ActionUnavailableException("SteamLibrariesUnavailable"); }
        var caches = config.Paths.Select(Canonical).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!caches.Contains(root, StringComparer.OrdinalIgnoreCase)) { throw new ActionUnavailableException("SteamLibraryChanged"); }
        if (!Directory.Exists(root)) { throw new ActionUnavailableException("SteamCacheMissing"); }
        var libraries = caches.Select(p => Path.GetDirectoryName(Path.GetDirectoryName(p))!).ToArray();
        bool Protected(string path)
        {
            if (protect(path)) { return true; }
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase)) { return false; }
            if (!FileCleanupAdapter.Inside(path, root)) { return true; }
            var relative = Path.GetRelativePath(root, path);
            var first = relative.Split('\\')[0];
            // AppID 하위만 허용한다. shadercache 바로 아래의 파일·정체불명 폴더는 건너뛴다.
            return first.Length is < 1 or > 10 || first.Any(c => !char.IsAsciiDigit(c)) || !uint.TryParse(first, out var id) || id == 0
                || (!relative.Contains('\\') && !Directory.Exists(path));
        }
        return new(key, root, "선택한 Steam 라이브러리 · 30일 이상 된 셰이더 캐시", true, ["*"], TimeSpan.FromDays(30),
            Protected, () => SteamProcessGuard.Check(libraries),
            "휴지통 없이 삭제하며 되돌릴 수 없습니다. 같은 라이브러리를 쓰는 다른 사용자도 영향을 받을 수 있습니다. 다음 게임 실행 때 다시 다운로드·컴파일해 로딩이나 끊김이 늘 수 있습니다. 정리 중 Steam·게임을 실행하지 마세요. 설치 게임·워크숍·다운로드 캐시는 포함하지 않습니다.");
    }
}
