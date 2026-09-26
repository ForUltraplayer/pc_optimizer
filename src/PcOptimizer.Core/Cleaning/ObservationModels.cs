/**
 * @file    : ObservationModels.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 관측 계획의 입력(규칙 FileKey를 펼친 관측 후보, 실제 경로로 펼친 제외 조건)과 출력(우선순위·병합·하위 제외·전체/필터 구분이 끝난 관측 대상, 보호 제외·규칙 제외·병합 기록) 레코드
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 관측 우선순위입니다(스펙 §5.1 "보호 정책 > 규칙의 제외 경로 > 검토된 supplement/앱 설정 경로 > 기본 커뮤니티 경로").
/// 보호 정책과 제외 경로는 모든 대상에 먼저 적용하고, 이 값은 겹치는 파일을 어느 규칙에 셀지 정합니다.
/// </summary>
public enum ObservationPrecedence
{
    /// <summary>검토한 보충 규칙 또는 앱 설정 리더가 준 경로.</summary>
    Reviewed = 0,

    /// <summary>커뮤니티 기본 경로.</summary>
    Community = 1,
}

/// <summary>
/// 관측 경로의 출처입니다.
/// </summary>
public enum TargetSource
{
    /// <summary>규칙에 적힌 기본 위치.</summary>
    Default,

    /// <summary>앱 설정(환경 변수·설정 파일·레지스트리)에서 읽은 사용자 설정 경로.</summary>
    UserConfig,
}

/// <summary>
/// 관측 후보 하나입니다. 환경 변수와 폴더 와일드카드를 펼친 실제 절대 경로입니다.
/// </summary>
/// <param name="RuleId">규칙 ID.</param>
/// <param name="Precedence">우선순위.</param>
/// <param name="Directory">폴더 절대 경로.</param>
/// <param name="Patterns">파일 이름 패턴(Win32 와일드카드).</param>
/// <param name="Recurse">하위 폴더 포함 여부.</param>
/// <param name="Source">경로 출처.</param>
public sealed record ObservationCandidate(
    string RuleId,
    ObservationPrecedence Precedence,
    string Directory,
    IReadOnlyList<string> Patterns,
    bool Recurse,
    TargetSource Source);

/// <summary>
/// 실제 경로로 펼친 제외 조건입니다. 폴더 구성 요소에는 와일드카드가 남아 있을 수 있습니다.
/// </summary>
/// <param name="RuleId">제외를 적은 규칙 ID.</param>
/// <param name="Kind">종류.</param>
/// <param name="DirectoryPattern">폴더 절대 경로 패턴.</param>
/// <param name="Patterns">파일 이름 패턴.</param>
public sealed record ExclusionSpec(string RuleId, ExcludeKind Kind, string DirectoryPattern, IReadOnlyList<string> Patterns);

/// <summary>
/// 계획이 끝난 관측 대상입니다. 앞선(우선순위가 높은) 대상이 센 파일은 뒤 대상이 다시 세지 않습니다.
/// </summary>
/// <param name="Order">처리 순서(0부터).</param>
/// <param name="Directory">폴더 정규화 경로.</param>
/// <param name="Patterns">파일 이름 패턴.</param>
/// <param name="Recurse">하위 폴더 포함 여부.</param>
/// <param name="OwnerRuleId">이 대상의 크기를 받는 규칙 ID.</param>
/// <param name="Precedence">우선순위.</param>
/// <param name="Source">경로 출처.</param>
/// <param name="ContributingRuleIds">같은 위치를 가리킨 규칙 ID(소유 규칙 포함, 중복 없음).</param>
/// <param name="IsWhole">모든 파일·하위 포함·적용할 제외 없음이면 true(폴더 하위 합계로 셀 수 있음).</param>
/// <param name="Exclusions">이 대상에 적용할 제외 조건.</param>
/// <param name="CarveOuts">앞선 전체 대상이 이미 센 하위 폴더(이 대상은 들어가지 않음).</param>
/// <param name="OverlapsEarlierFiltered">앞선 필터 대상과 파일이 겹칠 수 있는지 여부(파일 경로 단위로 중복을 걸러야 함).</param>
public sealed record ObservationTarget(
    int Order,
    string Directory,
    IReadOnlyList<string> Patterns,
    bool Recurse,
    string OwnerRuleId,
    ObservationPrecedence Precedence,
    TargetSource Source,
    IReadOnlyList<string> ContributingRuleIds,
    bool IsWhole,
    IReadOnlyList<ExclusionSpec> Exclusions,
    IReadOnlyList<string> CarveOuts,
    bool OverlapsEarlierFiltered);

/// <summary>
/// 관측 대상에서 빠진 후보의 사유입니다.
/// </summary>
public enum DroppedCandidateReason
{
    /// <summary>보호 루트 안(순회하지 않음).</summary>
    Protected,

    /// <summary>모든 파일을 빼는 PATH 제외 안.</summary>
    RuleExcluded,

    /// <summary>앞선 전체 대상 안이라 그 대상에서 한 번만 셈.</summary>
    Merged,
}

/// <summary>
/// 관측 대상에서 빠진 후보 하나입니다.
/// </summary>
/// <param name="RuleId">규칙 ID.</param>
/// <param name="Directory">폴더 정규화 경로.</param>
/// <param name="Source">경로 출처.</param>
/// <param name="Reason">사유.</param>
/// <param name="MergedIntoOrder">병합이면 받은 대상의 처리 순서.</param>
public sealed record DroppedCandidate(string RuleId, string Directory, TargetSource Source, DroppedCandidateReason Reason, int? MergedIntoOrder);

/// <summary>
/// 관측 계획입니다.
/// </summary>
/// <param name="Targets">관측 대상(처리 순서).</param>
/// <param name="Dropped">빠진 후보.</param>
public sealed record ObservationPlan(IReadOnlyList<ObservationTarget> Targets, IReadOnlyList<DroppedCandidate> Dropped);
