/**
 * @file    : VolumeTraversalRun.cs
 * @author  : rudals252
 * @brief   : 볼륨 하나의 루트들을 깊이 우선으로 메타데이터만 순회(보호·placeholder·reparse를 항목마다 재확인, 디렉터리별 접근 거부·사용 중 부분 집계, 64MiB 이상 파일 ID 하드링크 중복 제거, 압축·희소 할당 크기, 항목마다 취소 확인, 예산 초과 시 열거 중단·받은 항목의 논리 크기는 보존하되 새 OS 조회 중단·디렉터리당 실패 사유 하나)하고 하위 합계를 bottom-up으로 계산하는 내부 실행기
 */

// 기본 패키지
using System.Security;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 볼륨 하나의 순회 실행기입니다. 한 스레드에서 동기적으로 실행하며 파일 내용은 읽지 않습니다.
/// </summary>
/// <remarks>
/// 항목 처리 순서: 디렉터리는 보호 → placeholder → reparse 순으로 확인해 들어가지 않을 항목을 세고, 파일은 placeholder → reparse 순으로 확인합니다
/// (클라우드 placeholder는 reparse 속성도 함께 가지므로 placeholder로 셉니다). 숨김·시스템 항목은 일반 항목과 같이 셉니다.
/// </remarks>
internal sealed class VolumeTraversalRun
{
    private const FileAttributes RECALL_ON_OPEN = (FileAttributes)0x00040000;
    private const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes)0x00400000;
    private const FileAttributes PLACEHOLDER_ATTRIBUTES = FileAttributes.Offline | RECALL_ON_OPEN | RECALL_ON_DATA_ACCESS;
    private const FileAttributes COMPRESSED_OR_SPARSE = FileAttributes.Compressed | FileAttributes.SparseFile;

    private readonly IDirectoryEntrySource _source;
    private readonly IFileIdentityReader _identities;
    private readonly ResolvedProtection _protection;
    private readonly Dictionary<string, PatternAccumulator> _patterns;
    private readonly TimeProvider _time;
    private readonly long _scanStartTimestamp;
    private readonly TimeSpan _budget;
    private readonly CancellationToken _ct;
    private readonly HashSet<FileIdentity> _seenIdentities = [];
    private readonly Dictionary<string, string> _extensionPool = new(StringComparer.Ordinal);

    /// <summary>
    /// 실행기를 만든다.
    /// </summary>
    public VolumeTraversalRun(
        IDirectoryEntrySource source,
        IFileIdentityReader identities,
        ResolvedProtection protection,
        IEnumerable<PatternAccumulator> patterns,
        TimeProvider time,
        long scanStartTimestamp,
        TimeSpan budget,
        CancellationToken ct)
    {
        _source = source;
        _identities = identities;
        _protection = protection;
        _patterns = patterns.ToDictionary(pattern => pattern.Path, StringComparer.OrdinalIgnoreCase);
        _time = time;
        _scanStartTimestamp = scanStartTimestamp;
        _budget = budget;
        _ct = ct;
    }

    /// <summary>순회한 디렉터리 노드(경로 → 노드).</summary>
    public Dictionary<string, DirectoryNode> Nodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>하드링크 중복으로 한 번만 센 파일 수.</summary>
    public long DuplicateCount { get; private set; }

    /// <summary>하드링크 중복으로 뺀 크기.</summary>
    public long DuplicateBytes { get; private set; }

    /// <summary>시간 예산을 넘겼는지 여부.</summary>
    public bool TimedOut { get; private set; }

    /// <summary>
    /// 루트들을 차례로 순회한다. 시간 예산을 넘기면 남은 루트는 시작하지 않는다.
    /// </summary>
    /// <param name="targets">같은 볼륨의 루트.</param>
    /// <param name="volumeRoot">볼륨 루트.</param>
    /// <returns>루트별 결과(입력 순서).</returns>
    public List<RootTraversal> Run(IReadOnlyList<ScanTarget> targets, string volumeRoot)
    {
        var results = new List<RootTraversal>();
        foreach (var target in targets)
        {
            _ct.ThrowIfCancellationRequested();
            if (IsOverBudget())
            {
                TimedOut = true;
                results.Add(new RootTraversal(target.Id, target.Path, volumeRoot, RootScanState.TimedOut, null, TimedOut: true));
                continue;
            }

            results.Add(TraverseRoot(target, volumeRoot));
        }

        return results;
    }

    /// <summary>
    /// 시간 예산을 넘겼는지 확인한다(예산은 검사 시작부터 볼륨마다 적용).
    /// </summary>
    private bool IsOverBudget()
    {
        return _time.GetElapsedTime(_scanStartTimestamp) > _budget;
    }

    /// <summary>
    /// 루트 하나를 순회한다.
    /// </summary>
    private RootTraversal TraverseRoot(ScanTarget target, string volumeRoot)
    {
        var path = PathScope.Normalize(target.Path);
        if (_protection.IsProtected(path))
        {
            return new RootTraversal(target.Id, path, volumeRoot, RootScanState.Protected, null, false);
        }

        var presence = _source.ProbeRoot(path);
        if (presence != RootPresence.Directory)
        {
            var state = presence switch
            {
                RootPresence.Missing => RootScanState.Absent,
                RootPresence.AccessDenied => RootScanState.AccessDenied,
                RootPresence.ReparsePoint => RootScanState.ReparsePoint,
                _ => RootScanState.Error,
            };
            return new RootTraversal(target.Id, path, volumeRoot, state, null, false);
        }

        var root = new DirectoryNode(path, null);
        var created = new List<DirectoryNode> { root };
        Nodes[path] = root;
        var stack = new Stack<DirectoryNode>();
        stack.Push(root);
        var timedOut = false;
        while (stack.Count > 0)
        {
            _ct.ThrowIfCancellationRequested();
            if (IsOverBudget())
            {
                timedOut = true;
                TimedOut = true;
                while (stack.Count > 0)
                {
                    stack.Pop().Fail(ScanSkipReason.Timeout);
                }

                break;
            }

            if (ListDirectory(stack.Pop(), stack, created))
            {
                timedOut = true;
            }
        }

        ComputeTotals(created);
        if (root.EnumerationFailure is { } failure && root.FileCount == 0 && created.Count == 1 && root.OwnSkips.Total == 1)
        {
            // 루트 자체에서 아무 것도 관측하지 못했다: 합계를 0바이트로 보고하지 않는다(합계 없음).
            var state = failure switch
            {
                ScanSkipReason.AccessDenied => RootScanState.AccessDenied,
                ScanSkipReason.Timeout => RootScanState.TimedOut,
                _ => RootScanState.Error,
            };
            return new RootTraversal(target.Id, path, volumeRoot, state, null, failure == ScanSkipReason.Timeout);
        }

        // 시간 초과로 멈췄어도 관측한 부분이 있으면 부분 합계(Timeout 건너뜀 포함, IsPartial)와 TimedOut=true로 보고한다.
        return new RootTraversal(target.Id, path, volumeRoot, RootScanState.Scanned, root.Totals, timedOut);
    }

    /// <summary>
    /// 디렉터리 하나를 열거해 파일을 집계하고 들어갈 하위 디렉터리를 쌓는다.
    /// <list type="bullet">
    /// <item>열거 중에는 항목을 받을 때마다 취소를 확인하고, 받은 항목을 목록에 넣은 뒤 시간 예산을 확인한다.
    /// 예산을 넘기면 다음 항목을 더 요청하지 않는다(OS 열거 호출 한 번 자체는 중단할 수 없음).</item>
    /// <item>이미 받은 항목은 예산과 관계없이 모두 논리 크기·확장자·파일 수를 합계에 넣는다(메모리에 있는 관측 결과를 버리지 않음).
    /// 처리 중에는 항목마다 취소를 확인하고, 파일마다 예산을 확인해 넘겼으면 그 뒤로는 새 OS 조회(파일 ID·할당 크기)를 하지 않는다.
    /// 생략한 파일은 중복 가능·조회 생략 수로 품질을 낮춘다. 따라서 예산 초과분은 진행 중이던 OS 조회 한 번으로 제한된다.</item>
    /// <item>열거가 예외로 끝나면 그때까지 받은 항목을 처리하고 그 사유(접근 거부·사용 중)를, 예산으로 멈췄으면 시간 초과를 이 디렉터리의
    /// 실패 사유로 한 번만 기록한다(디렉터리당 사유 하나).</item>
    /// </list>
    /// </summary>
    /// <returns>이 디렉터리 안에서 시간 예산을 넘겼으면 true.</returns>
    private bool ListDirectory(DirectoryNode directory, Stack<DirectoryNode> stack, List<DirectoryNode> created)
    {
        _patterns.TryGetValue(directory.Path, out var pattern);
        pattern?.MarkEnumerated();

        var entries = new List<DirectoryEntry>();
        var timedOut = false;
        ScanSkipReason? failure = null;
        try
        {
            foreach (var entry in _source.Enumerate(directory.Path))
            {
                _ct.ThrowIfCancellationRequested();
                entries.Add(entry);
                if (IsOverBudget())
                {
                    timedOut = true;
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            failure = ScanSkipReason.AccessDenied;
        }
        catch (IOException)
        {
            // 사용 중·열거 중 삭제(DirectoryNotFound 포함) 등 변경 중인 항목.
            failure = ScanSkipReason.InUse;
        }

        var lookupsAllowed = !timedOut;
        foreach (var entry in entries)
        {
            _ct.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                HandleDirectory(directory, entry, stack, created);
                continue;
            }

            if (lookupsAllowed && IsOverBudget())
            {
                // 처리 단계에서 예산을 넘겼다: 논리 크기는 계속 세되 새 OS 조회는 하지 않는다.
                lookupsAllowed = false;
                timedOut = true;
            }

            HandleFile(directory, entry, pattern, lookupsAllowed);
        }

        if (timedOut)
        {
            failure ??= ScanSkipReason.Timeout;
            TimedOut = true;
        }

        if (failure is { } reason)
        {
            directory.Fail(reason);
            pattern?.Fail(reason);
        }

        return timedOut;
    }

    /// <summary>
    /// 하위 디렉터리 항목: 보호·placeholder·reparse면 들어가지 않고 세며, 아니면 노드를 만들어 쌓는다.
    /// </summary>
    private void HandleDirectory(DirectoryNode parent, DirectoryEntry entry, Stack<DirectoryNode> stack, List<DirectoryNode> created)
    {
        var childPath = Path.Join(parent.Path, entry.Name);
        if (_protection.IsProtected(childPath))
        {
            parent.Skip(ScanSkipReason.ProtectedExcluded);
            return;
        }

        if ((entry.Attributes & PLACEHOLDER_ATTRIBUTES) != 0)
        {
            parent.Skip(ScanSkipReason.Placeholder);
            return;
        }

        if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            parent.Skip(ScanSkipReason.Reparse);
            return;
        }

        var child = new DirectoryNode(childPath, parent);
        Nodes[childPath] = child;
        created.Add(child);
        stack.Push(child);
    }

    /// <summary>
    /// 파일 항목: placeholder·reparse는 건드리지 않고 세고, 64MiB 이상은 파일 ID로 하드링크 중복을 거르며, 압축·희소 파일은 할당 크기도 기록한다.
    /// OS 조회가 허용되지 않으면(예산 초과 뒤) 조회 없이 논리 크기만 세고, 조회가 필요했던 파일은 중복 가능·조회 생략 수로 표시한다.
    /// </summary>
    private void HandleFile(DirectoryNode directory, DirectoryEntry entry, PatternAccumulator? pattern, bool lookupsAllowed)
    {
        if ((entry.Attributes & PLACEHOLDER_ATTRIBUTES) != 0)
        {
            directory.Skip(ScanSkipReason.Placeholder);
            return;
        }

        if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            directory.Skip(ScanSkipReason.Reparse);
            return;
        }

        var length = entry.Length;
        var needsLookup = length >= FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES || (entry.Attributes & COMPRESSED_OR_SPARSE) != 0;
        if (needsLookup && !lookupsAllowed)
        {
            directory.LookupsSkipped++;
        }

        var fullPath = needsLookup && lookupsAllowed ? Path.Join(directory.Path, entry.Name) : null;
        var unverified = true;
        if (fullPath is not null && length >= FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES && _identities.TryGetIdentity(fullPath) is { } identity)
        {
            if (!_seenIdentities.Add(identity))
            {
                DuplicateCount++;
                DuplicateBytes += length;
                return;
            }

            unverified = false;
        }

        directory.DuplicatesPossible |= unverified;
        if (fullPath is not null && (entry.Attributes & COMPRESSED_OR_SPARSE) != 0 && _identities.TryGetAllocatedSize(fullPath) is { } allocated)
        {
            directory.CompressedOrSparseCount++;
            directory.CompressedOrSparseLogical += length;
            directory.CompressedOrSparseAllocated += allocated;
        }

        directory.Bytes += length;
        directory.FileCount++;
        var extension = Intern(Path.GetExtension(entry.Name).ToLowerInvariant());
        directory.Extensions ??= new Dictionary<string, long>(StringComparer.Ordinal);
        directory.Extensions[extension] = directory.Extensions.GetValueOrDefault(extension) + length;
        if (directory.NewestWriteUtc is null || entry.LastWriteTimeUtc > directory.NewestWriteUtc)
        {
            directory.NewestWriteUtc = entry.LastWriteTimeUtc;
        }

        if (pattern is not null && pattern.Matches(entry.Name))
        {
            pattern.Add(length, unverified);
        }
    }

    /// <summary>
    /// 같은 확장자 문자열을 하나만 보관한다.
    /// </summary>
    private string Intern(string extension)
    {
        if (_extensionPool.TryGetValue(extension, out var pooled))
        {
            return pooled;
        }

        _extensionPool[extension] = extension;
        return extension;
    }

    /// <summary>
    /// 만든 순서의 역순(자식이 부모보다 먼저)으로 하위 합계를 부모에 더한다.
    /// </summary>
    private static void ComputeTotals(List<DirectoryNode> created)
    {
        foreach (var node in created)
        {
            node.Totals = node.OwnTotals();
        }

        for (var index = created.Count - 1; index >= 0; index--)
        {
            var node = created[index];
            if (node.Parent is { Totals: { } parentTotals } parent)
            {
                var child = node.Totals!;
                parent.Totals = parentTotals with
                {
                    Bytes = parentTotals.Bytes + child.Bytes,
                    FileCount = parentTotals.FileCount + child.FileCount,
                    DirectoryCount = parentTotals.DirectoryCount + child.DirectoryCount + 1,
                    CompressedOrSparseFileCount = parentTotals.CompressedOrSparseFileCount + child.CompressedOrSparseFileCount,
                    CompressedOrSparseLogicalBytes = parentTotals.CompressedOrSparseLogicalBytes + child.CompressedOrSparseLogicalBytes,
                    CompressedOrSparseAllocatedBytes = parentTotals.CompressedOrSparseAllocatedBytes + child.CompressedOrSparseAllocatedBytes,
                    Skips = parentTotals.Skips.Add(child.Skips),
                    DuplicatesPossible = parentTotals.DuplicatesPossible || child.DuplicatesPossible,
                    LookupsSkipped = parentTotals.LookupsSkipped + child.LookupsSkipped,
                };
            }
        }
    }
}
