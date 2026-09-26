/**
 * @file    : DirectoryScanResult.cs
 * @author  : rudals252
 * @brief   : 검사 ID 하나의 공유 파일 스캔 결과(보호 정책 상태, 해석된 보호 루트, 스캔 계획, 순회 결과, 표준 임시 위치 관측, 소요 시간)와 임의 디렉터리 합계 조회 API
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 표준 임시 위치의 관측 상태입니다.
/// </summary>
public enum LocationState
{
    /// <summary>완전 관측.</summary>
    Observed,

    /// <summary>일부 항목을 읽지 못한 부분 관측.</summary>
    Partial,

    /// <summary>없음(오류 아님).</summary>
    Absent,

    /// <summary>위치 자체 접근 거부.</summary>
    AccessDenied,

    /// <summary>상위 폴더를 읽지 못했거나 시간 초과로 관측하지 못함.</summary>
    NotObserved,

    /// <summary>경로를 해석하지 못함.</summary>
    Unresolved,

    /// <summary>보호 루트 안이라 순회하지 않음.</summary>
    Protected,
}

/// <summary>
/// 표준 임시 위치 하나의 관측 결과입니다. 크기는 관측했을 때만 있습니다(관측하지 못한 위치를 0바이트로 보고하지 않음).
/// </summary>
/// <param name="Id">위치 ID.</param>
/// <param name="Path">정규화 경로(해석하지 못하면 null).</param>
/// <param name="IsSystem">시스템 위치 여부.</param>
/// <param name="State">관측 상태.</param>
/// <param name="Bytes">관측 논리 크기(Observed·Partial일 때만).</param>
/// <param name="FileCount">관측 파일 수(Observed·Partial일 때만).</param>
/// <param name="DuplicatesPossible">하드링크 중복 가능 여부.</param>
public sealed record LocationObservation(
    string Id,
    string? Path,
    bool IsSystem,
    LocationState State,
    long? Bytes,
    long? FileCount,
    bool DuplicatesPossible);

/// <summary>
/// 검사 ID 하나의 공유 파일 스캔 결과입니다. 저장소·앱 캐시·미분류 검사가 같은 결과를 재사용합니다.
/// 보호 정책이 무효이면 순회하지 않았으므로 계획·순회 결과가 없습니다.
/// </summary>
public sealed class DirectoryScanResult
{
    /// <summary>
    /// 결과를 만듭니다.
    /// </summary>
    /// <param name="scanId">검사 ID.</param>
    /// <param name="policyError">보호 정책 검증 결과(None이면 유효).</param>
    /// <param name="protection">해석된 보호 루트(무효 정책이면 null).</param>
    /// <param name="plan">스캔 계획(무효 정책이면 null).</param>
    /// <param name="traversal">순회 결과(무효 정책이면 null).</param>
    /// <param name="locations">표준 임시 위치 관측.</param>
    /// <param name="elapsed">전체 소요 시간.</param>
    public DirectoryScanResult(
        Guid scanId,
        ProtectionPolicyError policyError,
        ResolvedProtection? protection,
        ScanPlan? plan,
        TraversalResult? traversal,
        IReadOnlyList<LocationObservation> locations,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ScanId = scanId;
        PolicyError = policyError;
        Protection = protection;
        Plan = plan;
        Traversal = traversal;
        Locations = locations;
        Elapsed = elapsed;
    }

    /// <summary>검사 ID.</summary>
    public Guid ScanId { get; }

    /// <summary>보호 정책 검증 결과.</summary>
    public ProtectionPolicyError PolicyError { get; }

    /// <summary>보호 정책이 유효해 순회했는지 여부.</summary>
    public bool IsPolicyValid => PolicyError == ProtectionPolicyError.None && Traversal is not null;

    /// <summary>해석된 보호 루트.</summary>
    public ResolvedProtection? Protection { get; }

    /// <summary>스캔 계획.</summary>
    public ScanPlan? Plan { get; }

    /// <summary>순회 결과.</summary>
    public TraversalResult? Traversal { get; }

    /// <summary>표준 임시 위치 관측(계획 순서).</summary>
    public IReadOnlyList<LocationObservation> Locations { get; }

    /// <summary>전체 소요 시간.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// 무효 정책 결과를 만듭니다(순회하지 않음).
    /// </summary>
    /// <param name="scanId">검사 ID.</param>
    /// <param name="error">정책 오류.</param>
    /// <param name="elapsed">소요 시간.</param>
    /// <returns>결과.</returns>
    public static DirectoryScanResult InvalidPolicy(Guid scanId, ProtectionPolicyError error, TimeSpan elapsed)
    {
        return new DirectoryScanResult(scanId, error, null, null, null, [], elapsed);
    }

    /// <summary>
    /// 스캔 루트 안의 임의 디렉터리 하위 합계를 찾습니다(앱 캐시 등 후속 검사용). 순회하지 않은 경로는 false입니다.
    /// </summary>
    /// <param name="path">디렉터리 경로.</param>
    /// <param name="totals">합계.</param>
    /// <returns>순회한 디렉터리이면 true.</returns>
    public bool TryGetDirectoryTotals(string path, out DirectoryTotals totals)
    {
        if (Traversal is not null)
        {
            return Traversal.TryGetDirectoryTotals(path, out totals);
        }

        totals = null!;
        return false;
    }
}
