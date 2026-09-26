/**
 * @file    : TraversalResult.cs
 * @author  : rudals252
 * @brief   : 한 번의 파일 순회 결과(루트·볼륨·패턴 위치 결과, 하드링크 중복 통계)와 순회한 임의 디렉터리의 하위 합계·미분류 선정용 직접 포함 집계 조회 API
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 파일 순회 결과입니다. 순회가 끝난 뒤에는 바뀌지 않으며 여러 스레드에서 읽어도 됩니다.
/// 저장소·앱 캐시·미분류 검사가 같은 결과를 재사용합니다(스펙 §4 "공유 스캔 결과").
/// </summary>
public sealed class TraversalResult
{
    private readonly IReadOnlyDictionary<string, DirectoryNode> _nodes;

    /// <summary>
    /// 결과를 만든다(순회기 전용).
    /// </summary>
    internal TraversalResult(
        IReadOnlyList<RootTraversal> roots,
        IReadOnlyList<VolumeTraversal> volumes,
        IReadOnlyList<PatternTally> patterns,
        IReadOnlyDictionary<string, DirectoryNode> nodes,
        long hardLinkDuplicateCount,
        long hardLinkDuplicateBytes)
    {
        Roots = roots;
        Volumes = volumes;
        Patterns = patterns;
        _nodes = nodes;
        HardLinkDuplicateCount = hardLinkDuplicateCount;
        HardLinkDuplicateBytes = hardLinkDuplicateBytes;
    }

    /// <summary>루트별 결과(입력 순서).</summary>
    public IReadOnlyList<RootTraversal> Roots { get; }

    /// <summary>볼륨별 결과.</summary>
    public IReadOnlyList<VolumeTraversal> Volumes { get; }

    /// <summary>패턴 위치 집계(입력 순서).</summary>
    public IReadOnlyList<PatternTally> Patterns { get; }

    /// <summary>파일 ID로 확인해 한 번만 센 하드링크 중복 파일 수.</summary>
    public long HardLinkDuplicateCount { get; }

    /// <summary>하드링크 중복으로 합계에서 뺀 논리 크기.</summary>
    public long HardLinkDuplicateBytes { get; }

    /// <summary>순회한 디렉터리 수.</summary>
    public int DirectoryCount => _nodes.Count;

    /// <summary>
    /// 순회한 디렉터리의 하위 전체 합계를 찾습니다(대소문자·끝 구분자 무시). 순회하지 않은 경로는 false입니다(0바이트로 바꾸지 않음).
    /// </summary>
    /// <param name="path">디렉터리 경로(스캔 루트 안).</param>
    /// <param name="totals">합계.</param>
    /// <returns>순회한 디렉터리이면 true.</returns>
    public bool TryGetDirectoryTotals(string path, out DirectoryTotals totals)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_nodes.TryGetValue(PathScope.Normalize(path), out var node) && node.Totals is { } found)
        {
            totals = found;
            return true;
        }

        totals = null!;
        return false;
    }

    /// <summary>
    /// 순회한 디렉터리 자체의 열거 실패 사유를 찾습니다.
    /// </summary>
    /// <param name="path">디렉터리 경로.</param>
    /// <returns>실패 사유(순회하지 않았거나 실패가 없으면 null).</returns>
    public ScanSkipReason? GetEnumerationFailure(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _nodes.TryGetValue(PathScope.Normalize(path), out var node) ? node.EnumerationFailure : null;
    }

    /// <summary>
    /// 지정한 루트들과 같거나 그 아래인 디렉터리의 직접 포함 집계를 돌려줍니다(미분류 선정 입력).
    /// </summary>
    /// <param name="roots">루트 경로.</param>
    /// <returns>디렉터리별 집계.</returns>
    public IEnumerable<DirectoryAggregate> GetDirectoryAggregates(IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return _nodes.Values
            .Where(node => roots.Any(root => PathScope.IsSameOrUnder(node.Path, root)))
            .Select(node => node.ToAggregate());
    }
}
