/**
 * @file    : FileSystemScanner.cs
 * @author  : rudals252
 * @brief   : 스캔 루트를 볼륨별로 묶어 볼륨당 순회 1개(볼륨 루트별 세마포어), 볼륨 간 제한된 병렬, 볼륨별 시간 예산·취소로 메타데이터 순회를 조율하고 결과를 합치는 파일 순회기(조회 전용)
 */

// 기본 패키지
using System.Collections.Concurrent;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 파일 순회기입니다(스펙 §4 "디스크 순회는 볼륨당 1개", §5.2). 파일을 삭제·이동·수정하지 않고 내용을 읽지 않습니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>같은 볼륨의 루트는 한 작업에서 차례로 순회하고, 볼륨 루트별 세마포어로 겹친 검사(예: 시간 초과 뒤 아직 끝나지 않은 이전 순회)와도 동시에 돌지 않습니다.</item>
/// <item>다른 볼륨은 최대 병렬도까지 동시에 순회합니다.</item>
/// <item>볼륨마다 검사 시작 시각부터 시간 예산을 적용하며, 넘기면 그 볼륨의 남은 폴더를 시간 초과로 세고 부분 집계로 끝냅니다.</item>
/// </list>
/// </remarks>
public sealed class FileSystemScanner
{
    /// <summary>파일 ID로 하드링크 중복을 확인하는 최소 크기(64 MiB). 더 작은 파일은 확인하지 않고 '중복 가능'으로 표시합니다.</summary>
    public const long HARD_LINK_CHECK_MIN_BYTES = 64L * 1024 * 1024;

    private readonly IDirectoryEntrySource _source;
    private readonly IFileIdentityReader _identities;
    private readonly TimeProvider _time;
    private readonly int _maxParallelism;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _volumeGates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 순회기를 만듭니다.
    /// </summary>
    /// <param name="source">디렉터리 항목 열거.</param>
    /// <param name="identities">파일 ID·할당 크기 조회.</param>
    /// <param name="time">시간 예산 측정용 시간 공급자.</param>
    /// <param name="maxParallelism">동시에 순회할 최대 볼륨 수(1 이상).</param>
    public FileSystemScanner(
        IDirectoryEntrySource source,
        IFileIdentityReader identities,
        TimeProvider time,
        int maxParallelism = ScanOptions.DEFAULT_MAX_PARALLELISM)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParallelism, 1);
        _source = source;
        _identities = identities;
        _time = time;
        _maxParallelism = maxParallelism;
    }

    /// <summary>
    /// 실제 파일 시스템 순회기를 만듭니다.
    /// </summary>
    /// <returns>순회기.</returns>
    public static FileSystemScanner CreateDefault()
    {
        return new FileSystemScanner(FileSystemDirectoryEntrySource.Instance, Win32FileIdentityReader.Instance, TimeProvider.System);
    }

    /// <summary>
    /// 루트들을 순회합니다.
    /// </summary>
    /// <param name="targets">순회할 루트(중첩 제거된 정규화 경로).</param>
    /// <param name="protection">보호 루트(항목마다 재확인).</param>
    /// <param name="patterns">이름 패턴 위치.</param>
    /// <param name="budgetPerVolume">볼륨당 시간 예산.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>순회 결과.</returns>
    /// <exception cref="OperationCanceledException">취소된 경우.</exception>
    public async Task<TraversalResult> ScanAsync(
        IReadOnlyList<ScanTarget> targets,
        ResolvedProtection protection,
        IReadOnlyList<FilePatternLocation> patterns,
        TimeSpan budgetPerVolume,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(patterns);
        ct.ThrowIfCancellationRequested();

        var start = _time.GetTimestamp();
        var accumulators = patterns.Select(pattern => new PatternAccumulator(pattern)).ToList();
        using var parallel = new SemaphoreSlim(_maxParallelism, _maxParallelism);
        var volumes = targets
            .GroupBy(target => VolumeOf(target.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => RunVolumeAsync(
                group.Key,
                [.. group],
                protection,
                accumulators.Where(pattern => string.Equals(VolumeOf(pattern.Path), group.Key, StringComparison.OrdinalIgnoreCase)),
                start,
                budgetPerVolume,
                parallel,
                ct))
            .ToList();
        var outputs = await Task.WhenAll(volumes).ConfigureAwait(false);

        var nodes = new Dictionary<string, DirectoryNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var output in outputs)
        {
            foreach (var (path, node) in output.Nodes)
            {
                nodes[path] = node;
            }
        }

        var pairs = outputs.SelectMany(output => output.Roots).ToList();
        var ordered = targets.Select(target => pairs.First(pair => ReferenceEquals(pair.Target, target)).Result).ToList();
        return new TraversalResult(
            ordered,
            [.. outputs.Select(output => output.Volume)],
            [.. accumulators.Select(pattern => pattern.ToTally())],
            nodes,
            outputs.Sum(output => output.DuplicateCount),
            outputs.Sum(output => output.DuplicateBytes));
    }

    /// <summary>
    /// 경로의 볼륨 루트(예: "C:\").
    /// </summary>
    private static string VolumeOf(string path)
    {
        return (Path.GetPathRoot(path) is { Length: > 0 } root ? root : path).ToUpperInvariant();
    }

    /// <summary>
    /// 볼륨 하나를 병렬 제한·볼륨 게이트 안에서 순회한다. 게이트를 예산 안에 얻지 못하면 루트를 시간 초과로 남긴다.
    /// </summary>
    private async Task<VolumeOutput> RunVolumeAsync(
        string volumeRoot,
        List<ScanTarget> targets,
        ResolvedProtection protection,
        IEnumerable<PatternAccumulator> patterns,
        long start,
        TimeSpan budget,
        SemaphoreSlim parallel,
        CancellationToken ct)
    {
        await parallel.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var gate = _volumeGates.GetOrAdd(volumeRoot, _ => new SemaphoreSlim(1, 1));
            var remaining = budget - _time.GetElapsedTime(start);
            if (remaining <= TimeSpan.Zero || !await gate.WaitAsync(remaining, ct).ConfigureAwait(false))
            {
                return VolumeOutput.TimedOutBeforeStart(volumeRoot, targets);
            }

            try
            {
                var gateAcquired = _time.GetTimestamp();
                var run = new VolumeTraversalRun(_source, _identities, protection, patterns, _time, start, budget, ct);
                var results = await Task.Run(() => run.Run(targets, volumeRoot), ct).ConfigureAwait(false);
                return new VolumeOutput(
                    [.. targets.Zip(results, (target, result) => new TargetResult(target, result))],
                    new VolumeTraversal(volumeRoot, _time.GetElapsedTime(gateAcquired), run.TimedOut),
                    run.Nodes,
                    run.DuplicateCount,
                    run.DuplicateBytes);
            }
            finally
            {
                gate.Release();
            }
        }
        finally
        {
            parallel.Release();
        }
    }

    /// <summary>
    /// 입력 루트와 결과의 짝입니다(같은 경로의 루트가 두 번 들어와도 구분).
    /// </summary>
    private sealed record TargetResult(ScanTarget Target, RootTraversal Result);

    /// <summary>
    /// 볼륨 하나의 순회 출력입니다.
    /// </summary>
    private sealed record VolumeOutput(
        IReadOnlyList<TargetResult> Roots,
        VolumeTraversal Volume,
        IReadOnlyDictionary<string, DirectoryNode> Nodes,
        long DuplicateCount,
        long DuplicateBytes)
    {
        /// <summary>
        /// 게이트를 예산 안에 얻지 못한 볼륨의 출력을 만든다.
        /// </summary>
        public static VolumeOutput TimedOutBeforeStart(string volumeRoot, List<ScanTarget> targets)
        {
            return new VolumeOutput(
                [.. targets.Select(target => new TargetResult(
                    target,
                    new RootTraversal(target.Id, PathScope.Normalize(target.Path), volumeRoot, RootScanState.TimedOut, null, TimedOut: true)))],
                new VolumeTraversal(volumeRoot, TimeSpan.Zero, TimedOut: true),
                new Dictionary<string, DirectoryNode>(),
                0,
                0);
        }
    }
}
