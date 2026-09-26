/**
 * @file    : ScanContext.cs
 * @author  : rudals252
 * @brief   : 검사 한 번의 실행 컨텍스트(검사 ID, 사용자 컨텍스트, 온라인 확인 요청 여부, UTC 시작 시각, 시스템 범위 제한 여부) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 한 번의 실행 컨텍스트입니다. App이 만들어 실행 조율기와 프로브에 전달합니다.
/// </summary>
/// <param name="ScanId">검사 ID. 이 ID와 다른 늦은 결과는 버린다.</param>
/// <param name="UserContext">검사를 실행한 사용자 컨텍스트.</param>
/// <param name="OnlineCheckRequested">사용자가 "온라인 업데이트 확인"을 요청했는지 여부.</param>
/// <param name="StartedAtUtc">검사 시작 시각(UTC).</param>
public sealed record ScanContext(
    Guid ScanId,
    UserContext UserContext,
    bool OnlineCheckRequested,
    DateTimeOffset StartedAtUtc)
{
    /// <summary>
    /// 관리자 권한으로 실행 중인지 여부. 사용자 컨텍스트 값과 항상 같다(두 값이 어긋나지 않도록 파생).
    /// </summary>
    public bool IsElevated => UserContext.IsElevated;

    /// <summary>
    /// 시스템 범위 프로브만 실행하는지 여부(기본 false). 관리자 권한 재검사에서 원래 사용자와 다른 계정으로 승격된 경우 true이며,
    /// 이때 사용자별(<c>ProbeScope.User</c>) 프로브는 호출하지 않고 건너뜁니다(다른 계정의 HKCU·프로필을 원래 사용자 결과로 보이지 않기 위함).
    /// </summary>
    public bool LimitToSystemScope { get; init; }
}
