/**
 * @file    : ScanPlan.cs
 * @author  : rudals252
 * @brief   : 파일 스캔 계획(루트별 정규화 경로와 계획 상태, 표준 임시 위치와 파일 이름 패턴, 미분류 선정 루트·제외 경로) 레코드
 */

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 스캔 루트의 계획 상태입니다.
/// </summary>
public enum ScanRootPlanState
{
    /// <summary>순회 예정.</summary>
    Planned,

    /// <summary>경로를 해석하지 못함(환경 변수 없음 등).</summary>
    Unresolved,

    /// <summary>허용하지 않는 위치(다른 사용자 프로필·볼륨 루트·UNC).</summary>
    Rejected,

    /// <summary>다른 루트 안에 있어 따로 순회하지 않음.</summary>
    Nested,

    /// <summary>보호 루트 안에 있어 순회하지 않음.</summary>
    Protected,
}

/// <summary>
/// 스캔 루트 하나의 계획입니다.
/// </summary>
/// <param name="Id">루트 ID(<c>FileScanProbeContract.ROOT_*</c> 또는 <c>LOCATION_*</c>).</param>
/// <param name="Path">정규화 경로(해석하지 못하면 null).</param>
/// <param name="State">계획 상태.</param>
/// <param name="NestedIn">중첩이면 이 루트를 포함하는 루트 ID.</param>
public sealed record ScanRootPlan(string Id, string? Path, ScanRootPlanState State, string? NestedIn = null);

/// <summary>
/// 크기를 따로 보고할 표준 임시 위치입니다.
/// </summary>
/// <param name="Id">위치 ID(<c>FileScanProbeContract.LOCATION_*</c>).</param>
/// <param name="Path">정규화 경로(해석하지 못하면 null).</param>
/// <param name="IsSystem">시스템 위치인지 여부(일반 권한에서 접근 거부 시 관리자 권한 안내).</param>
/// <param name="FilePatterns">이 폴더의 직접 포함 파일 중 셀 이름 패턴(비어 있으면 하위 전체).</param>
public sealed record ScanLocationPlan(string Id, string? Path, bool IsSystem, IReadOnlyList<string> FilePatterns);

/// <summary>
/// 파일 스캔 계획입니다.
/// </summary>
/// <param name="Roots">루트 계획(정의 순서).</param>
/// <param name="Locations">표준 임시 위치(정의 순서).</param>
/// <param name="SelectionRoots">미분류 선정 루트(순회 예정인 프로필·ProgramData).</param>
/// <param name="ExcludedPaths">미분류에서 뺄 경로(보호 루트 + 임시 위치).</param>
public sealed record ScanPlan(
    IReadOnlyList<ScanRootPlan> Roots,
    IReadOnlyList<ScanLocationPlan> Locations,
    IReadOnlyList<string> SelectionRoots,
    IReadOnlyList<string> ExcludedPaths);
