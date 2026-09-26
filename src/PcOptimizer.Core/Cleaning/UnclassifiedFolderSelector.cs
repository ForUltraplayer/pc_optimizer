/**
 * @file    : UnclassifiedFolderSelector.cs
 * @author  : rudals252
 * @brief   : 디렉터리별 관측 집계에서 미분류 대용량 폴더를 고르는 순수 함수(완전 관측·1,000,000,000바이트 이상, bottom-up 부모/자식 중복 제거와 부모 잔여 크기 규칙, 크기 내림차순·경로 서수 오름차순, 상위 20개, 부분 관측 폴더 별도 목록)
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 미분류 대용량 폴더 선정기입니다(스펙 §5 미분류 행, §5.2 마지막 항목).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>대상: 선정 루트(사용자 프로필·ProgramData)의 하위 폴더. 루트 자체와 루트 밖 폴더는 후보가 아닙니다.</item>
/// <item>제외: 보호 루트·표준 임시 위치(P5부터 규칙 경로 추가)와 그 하위. 제외 위치의 크기는 조상에 합치지 않고, 그 안의 건너뜀도 조상의 관측 완전성에 영향을 주지 않습니다.</item>
/// <item>완전 관측: 자신과 제외되지 않은 모든 하위 폴더에 건너뛴 항목이 없어야 합니다.</item>
/// <item>bottom-up: 가장 깊은 폴더부터 "보고된 하위 폴더를 뺀 잔여" 크기를 계산해 임계값 이상이면 보고하고, 보고된 폴더의 크기는 조상에 더하지 않습니다.
/// 따라서 부모는 자기 잔여 크기가 임계값 이상일 때만 보고됩니다.</item>
/// <item>부분 관측인데 잔여 크기가 임계값 이상인 폴더는 후보가 아니라 부분 관측 목록으로 알리고 같은 방식으로 조상 크기에서 뺍니다.</item>
/// <item>정렬: 크기 내림차순, 같으면 경로 서수(ordinal) 오름차순. 상위 <see cref="UNCLASSIFIED_TOP_COUNT"/>개.</item>
/// </list>
/// 경로는 대소문자 무시·디렉터리 경계로 비교합니다.
/// </remarks>
public static class UnclassifiedFolderSelector
{
    /// <summary>후보 최소 크기(10진 1GB = 1,000,000,000바이트, 스펙 값).</summary>
    public const long UNCLASSIFIED_MIN_BYTES = 1_000_000_000;

    /// <summary>후보 상위 개수.</summary>
    public const int UNCLASSIFIED_TOP_COUNT = 20;

    /// <summary>후보마다 보여 줄 확장자 개수.</summary>
    public const int TOP_EXTENSION_COUNT = 5;

    /// <summary>
    /// 미분류 대용량 폴더를 고릅니다.
    /// </summary>
    /// <param name="directories">디렉터리별 직접 포함 파일 집계(순서 무관).</param>
    /// <param name="selectionRoots">선정 루트(사용자 프로필·ProgramData).</param>
    /// <param name="excludedPaths">제외 위치(보호 루트·임시 위치 등, 하위 포함).</param>
    /// <returns>선정 결과.</returns>
    public static UnclassifiedSelection Select(
        IEnumerable<DirectoryAggregate> directories,
        IReadOnlyList<string> selectionRoots,
        IReadOnlyList<string> excludedPaths)
    {
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(selectionRoots);
        ArgumentNullException.ThrowIfNull(excludedPaths);

        var roots = selectionRoots.Select(PathScope.Normalize).ToArray();
        var excluded = excludedPaths.Select(PathScope.Normalize).ToArray();
        var nodes = BuildNodes(directories, roots, excluded);

        var candidates = new List<UnclassifiedFolderCandidate>();
        var partials = new List<PartialFolder>();
        foreach (var node in nodes.OrderByDescending(n => n.Depth))
        {
            Evaluate(node, candidates, partials);
        }

        return new UnclassifiedSelection(
            [.. candidates.OrderByDescending(c => c.Bytes).ThenBy(c => c.Path, StringComparer.Ordinal).Take(UNCLASSIFIED_TOP_COUNT)],
            candidates.Count,
            [.. partials.OrderByDescending(p => p.ObservedBytes).ThenBy(p => p.Path, StringComparer.Ordinal).Take(UNCLASSIFIED_TOP_COUNT)],
            partials.Count);
    }

    /// <summary>
    /// 선정 루트 아래 디렉터리로 트리 노드를 만들고 부모에 연결한다.
    /// </summary>
    private static List<Node> BuildNodes(IEnumerable<DirectoryAggregate> directories, string[] roots, string[] excluded)
    {
        var byPath = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
        {
            var path = PathScope.Normalize(directory.Path);
            if (!roots.Any(root => PathScope.IsSameOrUnder(path, root)))
            {
                continue;
            }

            if (!byPath.TryGetValue(path, out var node))
            {
                node = new Node(
                    path,
                    roots.Any(root => string.Equals(root, path, StringComparison.OrdinalIgnoreCase)),
                    excluded.Any(item => PathScope.IsSameOrUnder(path, item)));
                byPath.Add(path, node);
            }

            node.AddOwn(directory);
        }

        foreach (var node in byPath.Values.Where(n => !n.IsRoot))
        {
            if (PathScope.GetParent(node.Path) is { } parent && byPath.TryGetValue(parent, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
        }

        return [.. byPath.Values];
    }

    /// <summary>
    /// 자식이 모두 평가된 노드 하나를 평가한다: 잔여 집계를 만들고 임계값 이상이면 후보 또는 부분 관측으로 보고해 조상 크기에서 뺀다.
    /// </summary>
    private static void Evaluate(Node node, List<UnclassifiedFolderCandidate> candidates, List<PartialFolder> partials)
    {
        if (node.IsExcluded)
        {
            return;
        }

        var fullyObserved = node.OwnSkipped == 0;
        var reportedBelow = false;
        foreach (var child in node.Children.Where(c => !c.IsExcluded))
        {
            fullyObserved &= child.FullyObserved;
            reportedBelow |= child.IsReported || child.ReportedBelow;
            if (!child.IsReported)
            {
                node.Remaining.Merge(child.Remaining);
            }

            child.Release();
        }

        node.FullyObserved = fullyObserved;
        node.ReportedBelow = reportedBelow;
        if (node.IsRoot || node.Remaining.Bytes < UNCLASSIFIED_MIN_BYTES)
        {
            return;
        }

        node.IsReported = true;
        if (fullyObserved)
        {
            candidates.Add(new UnclassifiedFolderCandidate(
                node.Path,
                node.Remaining.Bytes,
                node.Remaining.FileCount,
                node.Remaining.TopExtensions(),
                node.Remaining.NewestWriteUtc,
                node.Remaining.DuplicatesPossible,
                reportedBelow));
        }
        else
        {
            partials.Add(new PartialFolder(node.Path, node.Remaining.Bytes));
        }
    }

    /// <summary>
    /// 선정용 디렉터리 노드입니다.
    /// </summary>
    private sealed class Node(string path, bool isRoot, bool isExcluded)
    {
        /// <summary>정규화 경로.</summary>
        public string Path { get; } = path;

        /// <summary>선정 루트 자체인지 여부.</summary>
        public bool IsRoot { get; } = isRoot;

        /// <summary>제외 위치(또는 그 하위)인지 여부.</summary>
        public bool IsExcluded { get; } = isExcluded;

        /// <summary>경로 깊이(구분자 수).</summary>
        public int Depth { get; } = path.Count(c => c == PathScope.SEPARATOR);

        /// <summary>자식 노드.</summary>
        public List<Node> Children { get; } = [];

        /// <summary>이 디렉터리에서 직접 건너뛴 항목 수.</summary>
        public long OwnSkipped { get; private set; }

        /// <summary>보고되지 않은 잔여 집계(자신 + 보고되지 않은 하위).</summary>
        public Stats Remaining { get; private set; } = new();

        /// <summary>하위 전체가 완전 관측인지 여부(평가 후).</summary>
        public bool FullyObserved { get; set; }

        /// <summary>이 노드가 후보 또는 부분 관측으로 보고되었는지 여부.</summary>
        public bool IsReported { get; set; }

        /// <summary>하위에 보고된 노드가 있는지 여부.</summary>
        public bool ReportedBelow { get; set; }

        /// <summary>
        /// 직접 포함 파일 집계를 더한다.
        /// </summary>
        public void AddOwn(DirectoryAggregate directory)
        {
            OwnSkipped += directory.SkippedEntryCount;
            Remaining.AddOwn(directory);
        }

        /// <summary>
        /// 부모에 합친 뒤 잔여 집계를 놓아 메모리를 줄인다.
        /// </summary>
        public void Release()
        {
            Remaining = Stats.EMPTY;
        }
    }

    /// <summary>
    /// 잔여 파일 집계입니다. 확장자 사전은 큰 쪽에 작은 쪽을 합쳐 복사 비용을 줄입니다.
    /// </summary>
    private sealed class Stats
    {
        /// <summary>합쳐도 영향이 없는 빈 집계(해제 표시용).</summary>
        public static readonly Stats EMPTY = new();

        private Dictionary<string, long>? _extensions;

        /// <summary>논리 크기 합.</summary>
        public long Bytes { get; private set; }

        /// <summary>파일 수.</summary>
        public long FileCount { get; private set; }

        /// <summary>가장 최근 수정 시각.</summary>
        public DateTimeOffset? NewestWriteUtc { get; private set; }

        /// <summary>중복 가능 여부.</summary>
        public bool DuplicatesPossible { get; private set; }

        /// <summary>
        /// 디렉터리 하나의 직접 포함 파일 집계를 더한다.
        /// </summary>
        public void AddOwn(DirectoryAggregate directory)
        {
            Bytes += directory.Bytes;
            FileCount += directory.FileCount;
            DuplicatesPossible |= directory.DuplicatesPossible;
            NewestWriteUtc = Max(NewestWriteUtc, directory.NewestWriteUtc);
            foreach (var (extension, bytes) in directory.ExtensionBytes)
            {
                _extensions ??= new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                _extensions[extension] = _extensions.GetValueOrDefault(extension) + bytes;
            }
        }

        /// <summary>
        /// 다른 잔여 집계를 합친다(다른 집계는 이후 쓰지 않는다).
        /// </summary>
        public void Merge(Stats other)
        {
            if (ReferenceEquals(other, EMPTY))
            {
                return;
            }

            Bytes += other.Bytes;
            FileCount += other.FileCount;
            DuplicatesPossible |= other.DuplicatesPossible;
            NewestWriteUtc = Max(NewestWriteUtc, other.NewestWriteUtc);
            if (other._extensions is null)
            {
                return;
            }

            if (_extensions is null || other._extensions.Count > _extensions.Count)
            {
                (_extensions, other._extensions) = (other._extensions, _extensions);
            }

            if (other._extensions is null)
            {
                return;
            }

            foreach (var (extension, bytes) in other._extensions)
            {
                _extensions[extension] = _extensions.GetValueOrDefault(extension) + bytes;
            }
        }

        /// <summary>
        /// 크기순 상위 확장자(동률은 확장자 서수 오름차순).
        /// </summary>
        public IReadOnlyList<ExtensionShare> TopExtensions()
        {
            return _extensions is null
                ? []
                : [.. _extensions
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(TOP_EXTENSION_COUNT)
                    .Select(pair => new ExtensionShare(pair.Key, pair.Value))];
        }

        /// <summary>
        /// 두 시각 중 늦은 값(null은 없는 값).
        /// </summary>
        private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
        {
            if (left is null)
            {
                return right;
            }

            return right is null || left >= right ? left : right;
        }
    }
}
