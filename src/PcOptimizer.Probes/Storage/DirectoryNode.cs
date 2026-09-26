/**
 * @file    : DirectoryNode.cs
 * @author  : rudals252
 * @brief   : 순회 중 디렉터리 하나의 가변 집계(직접 포함 파일 크기·수·확장자·최근 수정 시각·건너뜀·열거 실패 사유)와 하위 합계 누적을 담는 내부 노드
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 순회 중 디렉터리 하나의 집계 노드입니다. 한 볼륨 순회 스레드만 수정하며, 순회가 끝난 뒤에는 읽기만 합니다.
/// </summary>
internal sealed class DirectoryNode(string path, DirectoryNode? parent)
{
    /// <summary>파일이 없는 디렉터리가 함께 쓰는 빈 확장자 사전(읽기 전용).</summary>
    private static readonly IReadOnlyDictionary<string, long> NO_EXTENSIONS = new Dictionary<string, long>();

    /// <summary>전체 경로.</summary>
    public string Path { get; } = path;

    /// <summary>부모 노드(루트는 null).</summary>
    public DirectoryNode? Parent { get; } = parent;

    /// <summary>직접 포함 파일 논리 크기.</summary>
    public long Bytes { get; set; }

    /// <summary>직접 포함 파일 수.</summary>
    public long FileCount { get; set; }

    /// <summary>할당 크기를 읽은 압축·희소 파일 수.</summary>
    public long CompressedOrSparseCount { get; set; }

    /// <summary>그 파일들의 논리 크기.</summary>
    public long CompressedOrSparseLogical { get; set; }

    /// <summary>그 파일들의 할당 크기.</summary>
    public long CompressedOrSparseAllocated { get; set; }

    /// <summary>확장자별 크기(파일이 있을 때만 만든다).</summary>
    public Dictionary<string, long>? Extensions { get; set; }

    /// <summary>가장 최근 수정 시각(UTC).</summary>
    public DateTimeOffset? NewestWriteUtc { get; set; }

    /// <summary>파일 ID로 확인하지 않은 파일이 있는지 여부.</summary>
    public bool DuplicatesPossible { get; set; }

    /// <summary>이 디렉터리에서 직접 건너뛴 항목(자신을 열거하지 못한 경우 포함).</summary>
    public SkipCounts OwnSkips { get; set; }

    /// <summary>이 디렉터리 자체를 열거하지 못했거나 도중에 실패한 사유(없으면 null).</summary>
    public ScanSkipReason? EnumerationFailure { get; set; }

    /// <summary>하위 전체 합계(순회 뒤 계산).</summary>
    public DirectoryTotals? Totals { get; set; }

    /// <summary>
    /// 사유 하나를 기록한다.
    /// </summary>
    public void Skip(ScanSkipReason reason)
    {
        OwnSkips = OwnSkips.Increment(reason);
    }

    /// <summary>
    /// 이 디렉터리를 열거하지 못했음을 기록한다(첫 사유만 보존).
    /// </summary>
    public void Fail(ScanSkipReason reason)
    {
        EnumerationFailure ??= reason;
        Skip(reason);
    }

    /// <summary>
    /// 직접 포함 파일만의 합계(하위 합계 계산의 시작값).
    /// </summary>
    public DirectoryTotals OwnTotals()
    {
        return new DirectoryTotals(
            Bytes, FileCount, 0, CompressedOrSparseCount, CompressedOrSparseLogical, CompressedOrSparseAllocated, OwnSkips, DuplicatesPossible);
    }

    /// <summary>
    /// 미분류 선정용 직접 포함 집계로 바꾼다.
    /// </summary>
    public DirectoryAggregate ToAggregate()
    {
        return new DirectoryAggregate(
            Path,
            Bytes,
            FileCount,
            OwnSkips.Total,
            Extensions ?? NO_EXTENSIONS,
            NewestWriteUtc,
            DuplicatesPossible);
    }
}
