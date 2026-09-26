/**
 * @file    : WmiQueryResult.cs
 * @author  : rudals252
 * @brief   : WMI 조회 결과(상태·행 목록·오류 코드 이름) 레코드와 조회 상태 열거형
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// WMI 조회 상태입니다. 실패를 빈 목록으로 바꾸지 않기 위해 행 목록과 따로 둡니다.
/// </summary>
public enum WmiQueryStatus
{
    /// <summary>조회에 성공했습니다(행이 0개일 수 있음).</summary>
    Success,

    /// <summary>클래스나 네임스페이스가 없습니다.</summary>
    ClassUnavailable,

    /// <summary>접근이 거부되었습니다.</summary>
    AccessDenied,

    /// <summary>제공자 타임아웃이 지났습니다.</summary>
    Timeout,

    /// <summary>그 밖의 오류입니다.</summary>
    Error,
}

/// <summary>
/// WMI 조회 한 번의 결과입니다.
/// </summary>
/// <param name="Status">조회 상태.</param>
/// <param name="Rows">행 목록(속성 이름 → 원시 값). 실패하면 비어 있다.</param>
/// <param name="ErrorCode">실패 원인 코드 이름(ManagementStatus 이름 또는 예외 형식 이름). 예외 메시지 원문은 담지 않는다.</param>
public sealed record WmiQueryResult(
    WmiQueryStatus Status,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string? ErrorCode)
{
    /// <summary>
    /// 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="rows">행 목록.</param>
    /// <returns>성공 결과.</returns>
    public static WmiQueryResult Succeeded(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        return new WmiQueryResult(WmiQueryStatus.Success, rows, null);
    }

    /// <summary>
    /// 실패 결과를 만듭니다.
    /// </summary>
    /// <param name="status">실패 상태.</param>
    /// <param name="errorCode">원인 코드 이름.</param>
    /// <returns>실패 결과.</returns>
    public static WmiQueryResult Failed(WmiQueryStatus status, string errorCode)
    {
        return new WmiQueryResult(status, [], errorCode);
    }
}
