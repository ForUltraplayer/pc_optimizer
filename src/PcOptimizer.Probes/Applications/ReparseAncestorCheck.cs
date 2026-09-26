/**
 * @file    : ReparseAncestorCheck.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 경로의 중간 폴더(볼륨 루트와 대상 사이) 중 reparse point(정션·심볼릭 링크)가 있는지 폴더마다 속성으로 확인하는 검사기(예: "Documents and Settings" 정션을 거쳐 다른 폴더로 들어가지 않음, 검사 한 번 안에서 결과 캐시)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// reparse 중간 폴더 검사기입니다(스펙 §5.2 "정션·심볼릭 링크 등 reparse point는 따라가지 않는다").
/// OS가 경로를 풀 때 중간 정션을 따라가므로, 대상 폴더 자체뿐 아니라 그 위의 각 폴더도 확인합니다. 검사 한 번 동안만 씁니다.
/// </summary>
public sealed class ReparseAncestorCheck
{
    private readonly IDirectoryEntrySource _source;
    private readonly Dictionary<string, RootPresence> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 검사기를 만듭니다.
    /// </summary>
    /// <param name="source">속성 확인용 열거 공급자.</param>
    public ReparseAncestorCheck(IDirectoryEntrySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>
    /// 경로의 중간 폴더(볼륨 루트 제외, 경로 자신 제외) 중 reparse point가 있는지 확인합니다. 없는 폴더를 만나면 그 아래는 보지 않습니다.
    /// </summary>
    /// <param name="path">드라이브 절대 경로.</param>
    /// <returns>중간에 reparse point가 있으면 true.</returns>
    public bool HasReparseAncestor(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = Winapp2PathSyntax.Segments(PathScope.Normalize(path));
        var prefix = segments.Length > 0 ? segments[0] + PathScope.SEPARATOR : string.Empty;
        for (var index = 1; index < segments.Length - 1; index++)
        {
            prefix = Path.Join(prefix, segments[index]);
            switch (Presence(prefix))
            {
                case RootPresence.ReparsePoint:
                    return true;
                case RootPresence.Directory:
                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// 폴더 하나가 reparse point인지 확인합니다.
    /// </summary>
    /// <param name="path">폴더 경로.</param>
    /// <returns>reparse point이면 true.</returns>
    public bool IsReparsePoint(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Presence(PathScope.Normalize(path)) == RootPresence.ReparsePoint;
    }

    /// <summary>
    /// 캐시한 존재·형태.
    /// </summary>
    private RootPresence Presence(string path)
    {
        if (!_cache.TryGetValue(path, out var presence))
        {
            presence = _source.ProbeRoot(path);
            _cache[path] = presence;
        }

        return presence;
    }
}
