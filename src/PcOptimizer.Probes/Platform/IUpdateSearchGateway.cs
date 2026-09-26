/**
 * @file    : IUpdateSearchGateway.cs
 * @author  : rudals252
 * @brief   : Windows Update Agent 검색 전용 계약(검색·취소 시 중단 요청만, 다운로드·설치 없음)과 검색 결과·드라이버 업데이트 값 레코드
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 드라이버 업데이트 하나의 조회 값입니다(IUpdate/IWindowsDriverUpdate에서 읽음). 읽지 못한 값은 null입니다.
/// </summary>
/// <param name="Title">제목.</param>
/// <param name="DriverModel">드라이버 모델.</param>
/// <param name="DriverManufacturer">드라이버 제조사.</param>
/// <param name="DriverVerDate">드라이버 날짜.</param>
/// <param name="DriverClass">드라이버 클래스.</param>
/// <param name="KbArticleIds">KB 문서 ID(읽지 못했으면 빈 목록).</param>
/// <param name="IsDownloaded">이미 내려받았는지.</param>
/// <param name="DetailsAvailable">드라이버 세부 정보(IWindowsDriverUpdate 속성)를 모두 읽었는지.</param>
public sealed record WuaDriverUpdate(
    string? Title,
    string? DriverModel,
    string? DriverManufacturer,
    DateTime? DriverVerDate,
    string? DriverClass,
    IReadOnlyList<string> KbArticleIds,
    bool? IsDownloaded,
    bool DetailsAvailable);

/// <summary>
/// Windows Update 검색 한 번의 결과입니다. <see cref="ErrorHResult"/>나 <see cref="ErrorCode"/>가 있으면 검색 결과 코드는 없습니다.
/// </summary>
/// <param name="ResultCode">ISearchResult.ResultCode(OperationResultCode).</param>
/// <param name="Updates">찾은 업데이트.</param>
/// <param name="RebootRequired">Microsoft.Update.SystemInfo.RebootRequired(읽지 못하면 null).</param>
/// <param name="ErrorHResult">COM 호출 실패 HRESULT(없으면 null).</param>
/// <param name="ErrorCode">HRESULT가 아닌 실패 요약 코드(예: 구성 요소 없음·멤버 없음).</param>
public sealed record WuaSearchOutcome(
    int? ResultCode,
    IReadOnlyList<WuaDriverUpdate> Updates,
    bool? RebootRequired,
    int? ErrorHResult,
    string? ErrorCode);

/// <summary>
/// Windows Update Agent 검색 계약입니다. 검색만 하며, 취소 토큰이 신호되면 진행 중인 검색에 중단을 요청합니다.
/// 업데이트 서비스 선택·정책(ServerSelection/ServiceID/Online)을 바꾸지 않고, 다운로드·설치 API는 이 계약에 없습니다.
/// </summary>
public interface IUpdateSearchGateway
{
    /// <summary>
    /// 검색을 비동기로 한 번 실행합니다. 취소되면 중단을 요청한 뒤 <see cref="OperationCanceledException"/>으로 끝납니다.
    /// </summary>
    /// <param name="criteria">검색 조건.</param>
    /// <param name="ct">취소 토큰(검사 취소·프로브 타임아웃).</param>
    /// <returns>검색 결과(COM 실패도 결과로 돌려줌).</returns>
    Task<WuaSearchOutcome> SearchAsync(string criteria, CancellationToken ct);
}
