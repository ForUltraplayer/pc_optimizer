/**
 * @file    : AdobeCacheLocationCatalog.cs
 * @author  : rudals252
 * @brief   : 사용자가 명시한 Adobe 캐시 폴더를 세션 한정 키로 등록하고 실행 시 보호 경계를 재확인
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>앱 실행 동안만 유지되는 사용자 선택입니다. 환경설정 자동 탐지 결과로 표현하지 않습니다.</summary>
public sealed class AdobeCacheLocationCatalog
{
    private sealed record Location(string Path, bool Peaks, ActionSession Session);
    private readonly Dictionary<string, Location> _locations = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    /// <summary>캐시 하위 폴더 하나를 등록합니다. 파일 접근과 삭제는 하지 않습니다.</summary>
    public (string? Key, string? Code) TryRegister(string path, ActionSession session)
    {
        try { return (Register(path, session), null); }
        catch (ActionUnavailableException ex) { return (null, ex.Code); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return (null, "AdobeLocationUnsupported"); }
    }
    private string Register(string path, ActionSession session)
    {
        if (!session.IsKnown || session.Scope != ActionUserScope.Full) { throw new ActionUnavailableException("ScopeExcluded"); }
        var normalized = Normalize(path);
        var peaks = Path.GetFileName(normalized).Equals("Peak Files", StringComparison.OrdinalIgnoreCase);
        lock (_sync)
        {
            var existing = _locations.FirstOrDefault(p => p.Value.Path.Equals(normalized, StringComparison.OrdinalIgnoreCase) && p.Value.Session == session);
            if (existing.Key is not null) { return existing.Key; }
            if (_locations.Count >= 8) { throw new ActionUnavailableException("AdobeLocationsFull"); }
            var key = "adobe-selected-" + Guid.NewGuid().ToString("N");
            _locations.Add(key, new(normalized, peaks, session));
            return key;
        }
    }
    internal static string Normalize(string path)
    {
        // 상대 경로·UNC·장치 경로·ADS·환경 변수·8.3 추정 이름은 선택 단계부터 받지 않는다.
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.Any(char.IsControl)
            || path.Length < 4 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\'
            || path[2..].IndexOfAny([':', '%', '~', '/', '*', '?']) >= 0) { throw new ActionUnavailableException("AdobeLocationUnsupported"); }
        var clean = path.TrimEnd('\\');
        if (clean.Split('\\').Skip(1).Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.')))
        { throw new ActionUnavailableException("AdobeLocationUnsupported"); }
        var full = Path.GetFullPath(clean);
        if (Path.GetFileName(full) is not { } leaf || !(leaf.Equals("Media Cache Files", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Peak Files", StringComparison.OrdinalIgnoreCase)))
        { throw new ActionUnavailableException("AdobeCacheFolderRequired"); }
        return full;
    }
    internal CleanupTarget Resolve(string key, ActionSession session)
    {
        if (key is AdobeCacheTargets.Media or AdobeCacheTargets.Peaks) { return AdobeCacheTargets.Resolve(key, session); }
        Location location;
        lock (_sync)
        {
            if (!_locations.TryGetValue(key, out var selected)) { throw new ActionUnavailableException("TargetRejected"); }
            location = selected;
        }
        if (session != location.Session || session.Scope != ActionUserScope.Full || SystemActionSession.Read() != session)
        { throw new ActionUnavailableException("SessionChanged"); }
        _ = RollbackStore.CurrentRoot(session.Sid);
        var root = CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, Normalize(location.Path));
        if (!root.Equals(location.Path, StringComparison.OrdinalIgnoreCase)) { throw new ActionUnavailableException("TargetChanged"); }
        var drive = new DriveInfo(Path.GetPathRoot(root)!);
        if (!drive.IsReady || drive.DriveType != DriveType.Fixed) { throw new ActionUnavailableException("AdobeLocationUnsupported"); }
        var protect = UserTempTargets.ProtectionFor(root, session);
        if (!Directory.Exists(root)) { throw new ActionUnavailableException("AdobeCacheMissing"); }
        return new(key, root, "사용자 지정 Adobe 캐시 · 90일 이상 된 " + (location.Peaks ? ".pek" : ".cfa/.pek"), true,
            location.Peaks ? ["*.pek"] : ["*.cfa", "*.pek"], TimeSpan.FromDays(90), protect, AdobeProcessGuard.Check,
            "직접 선택한 캐시 폴더의 오래된 오디오 변환·파형 파일만 정리합니다. 휴지통을 거치지 않으며 되돌릴 수 없습니다. 다음 편집 때 다시 만드는 시간이 필요합니다. 정리 중에는 Adobe 앱을 실행하지 마세요. 프로젝트·원본·DB·렌더 파일은 대상이 아닙니다.");
    }
}
