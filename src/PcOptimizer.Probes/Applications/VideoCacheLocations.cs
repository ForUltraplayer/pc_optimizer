/**
 * @file    : VideoCacheLocations.cs
 * @author  : rudals252
 * @brief   : 사용자가 선택한 Resolve·CapCut 캐시 위치를 앱 수명 동안 조회용으로 보관
 */
namespace PcOptimizer.Probes.Applications;

/// <summary>프로젝트나 사용자 설정 파일을 추측하지 않고 명시적으로 지정된 폴더만 검사에 전달합니다.</summary>
public sealed class VideoCacheLocations
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    /// <summary>앱별 캐시 폴더 하나만 지정합니다. 선택한 경로는 아직 읽거나 삭제하지 않습니다.</summary>
    public bool TrySet(string app, string path)
    {
        var leaf = app switch { "davinci" => "CacheClip", "capcut" => "Cache", _ => null };
        if (leaf is null || string.IsNullOrWhiteSpace(path) || path.Length is < 4 or > 1024 || path.Any(char.IsControl)
            || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path[2..].IndexOfAny([':', '%', '~', '/', '*', '?']) >= 0) { return false; }
        var clean = path.TrimEnd('\\');
        if (clean.Split('\\').Skip(1).Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.'))) { return false; }
        try
        {
            var full = Path.GetFullPath(clean);
            if (!Path.GetFileName(full).Equals(leaf, StringComparison.OrdinalIgnoreCase)) { return false; }
            lock (_sync) { _paths[app] = full; }
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    /// <summary>한 검사에서 변하지 않는 복사본을 반환합니다.</summary>
    public IReadOnlyDictionary<string, string> Snapshot() { lock (_sync) { return new Dictionary<string, string>(_paths, StringComparer.Ordinal); } }
    /// <summary>선택을 해제하면 다음 검사부터 기본 위치·설정 자동 탐지를 사용합니다.</summary>
    public void Clear() { lock (_sync) { _paths.Clear(); } }
}
