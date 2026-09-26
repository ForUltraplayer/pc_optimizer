/**
 * @file    : DirectoryTotals.cs
 * @author  : rudals252
 * @brief   : 디렉터리 하위 전체의 관측 합계(논리 크기·파일·폴더 수, 압축·희소 파일의 논리/할당 크기, 사유별 건너뜀, 중복 가능, 부분 집계·완전 관측 여부) 레코드
 */

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 디렉터리 하위 전체(자신 포함)의 관측 합계입니다. 크기는 관측한 파일의 논리 크기 합이며 '확보 가능량'이 아닙니다.
/// </summary>
/// <param name="Bytes">논리 크기 합(ID로 확인한 하드링크 중복은 한 번만).</param>
/// <param name="FileCount">관측 파일 수.</param>
/// <param name="DirectoryCount">하위 디렉터리 수(자신 제외, 들어가지 않은 항목 제외).</param>
/// <param name="CompressedOrSparseFileCount">할당 크기를 읽은 압축·희소 파일 수.</param>
/// <param name="CompressedOrSparseLogicalBytes">그 파일들의 논리 크기 합.</param>
/// <param name="CompressedOrSparseAllocatedBytes">그 파일들의 실제 할당 크기 합.</param>
/// <param name="Skips">사유별 건너뛴 항목 수.</param>
/// <param name="DuplicatesPossible">파일 ID로 확인하지 않은 파일이 있어 하드링크 중복이 섞였을 수 있는지 여부(논리 크기 추정).</param>
public sealed record DirectoryTotals(
    long Bytes,
    long FileCount,
    long DirectoryCount,
    long CompressedOrSparseFileCount,
    long CompressedOrSparseLogicalBytes,
    long CompressedOrSparseAllocatedBytes,
    SkipCounts Skips,
    bool DuplicatesPossible)
{
    /// <summary>읽지 못한 항목(접근 거부·사용 중·시간 초과)이 있어 부분 집계인지 여부.</summary>
    public bool IsPartial => Skips.Incomplete > 0;

    /// <summary>건너뛴 항목이 하나도 없는 완전 관측인지 여부.</summary>
    public bool IsFullyObserved => Skips.Total == 0;
}
