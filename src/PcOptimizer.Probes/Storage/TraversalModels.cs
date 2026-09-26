/**
 * @file    : TraversalModels.cs
 * @author  : rudals252
 * @brief   : 파일 순회 입력(루트 대상·파일 이름 패턴 위치)과 결과 값(루트별 순회 상태·합계, 볼륨별 소요 시간·시간 초과, 패턴 위치 집계) 레코드
 */

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 순회할 루트입니다(중첩 제거·검증을 마친 정규화 경로).
/// </summary>
/// <param name="Id">루트 ID.</param>
/// <param name="Path">정규화 경로.</param>
public sealed record ScanTarget(string Id, string Path);

/// <summary>
/// 직접 포함 파일 중 이름 패턴에 맞는 파일만 따로 셀 위치입니다(예: 탐색기 thumbcache_*.db).
/// </summary>
/// <param name="Id">위치 ID.</param>
/// <param name="Path">정규화 경로.</param>
/// <param name="Patterns">파일 이름 패턴(* ? 와일드카드, 대소문자 무시).</param>
public sealed record FilePatternLocation(string Id, string Path, IReadOnlyList<string> Patterns);

/// <summary>
/// 루트 순회 결과 상태입니다.
/// </summary>
public enum RootScanState
{
    /// <summary>순회함(부분 집계일 수 있음).</summary>
    Scanned,

    /// <summary>없음(오류 아님).</summary>
    Absent,

    /// <summary>루트 자체 접근 거부.</summary>
    AccessDenied,

    /// <summary>reparse point라 따라가지 않음.</summary>
    ReparsePoint,

    /// <summary>보호 루트 안이라 순회하지 않음.</summary>
    Protected,

    /// <summary>볼륨 시간 예산을 넘겨 시작하지 못함.</summary>
    TimedOut,

    /// <summary>디렉터리가 아니거나 그 밖의 오류.</summary>
    Error,
}

/// <summary>
/// 루트 하나의 순회 결과입니다.
/// </summary>
/// <param name="Id">루트 ID.</param>
/// <param name="Path">정규화 경로.</param>
/// <param name="VolumeRoot">볼륨 루트(예: "C:\").</param>
/// <param name="State">순회 상태.</param>
/// <param name="Totals">하위 전체 합계(순회하지 않았으면 null — 0바이트로 보고하지 않음).</param>
/// <param name="TimedOut">순회 도중 시간 예산을 넘겨 멈췄는지 여부.</param>
public sealed record RootTraversal(string Id, string Path, string VolumeRoot, RootScanState State, DirectoryTotals? Totals, bool TimedOut);

/// <summary>
/// 볼륨 하나의 순회 결과입니다(볼륨당 순회는 한 번에 하나).
/// </summary>
/// <param name="VolumeRoot">볼륨 루트.</param>
/// <param name="Elapsed">게이트 대기를 뺀 순회 소요 시간.</param>
/// <param name="TimedOut">시간 예산을 넘겼는지 여부.</param>
public sealed record VolumeTraversal(string VolumeRoot, TimeSpan Elapsed, bool TimedOut);

/// <summary>
/// 파일 이름 패턴 위치의 집계입니다.
/// </summary>
/// <param name="Id">위치 ID.</param>
/// <param name="Enumerated">해당 폴더를 열거했는지 여부(false면 크기를 모름).</param>
/// <param name="Bytes">패턴에 맞는 파일의 논리 크기.</param>
/// <param name="FileCount">패턴에 맞는 파일 수.</param>
/// <param name="Failure">폴더 열거 실패 사유(없으면 null).</param>
/// <param name="DuplicatesPossible">하드링크 중복 가능 여부.</param>
public sealed record PatternTally(string Id, bool Enumerated, long Bytes, long FileCount, ScanSkipReason? Failure, bool DuplicatesPossible);
