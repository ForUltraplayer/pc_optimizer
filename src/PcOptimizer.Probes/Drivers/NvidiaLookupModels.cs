/**
 * @file    : NvidiaLookupModels.cs
 * @author  : rudals252
 * @brief   : NVIDIA 조회 어댑터의 값 모델(제품 목록 항목, 검증한 드라이버 목록 항목, 목록 종류, 실패 사유·코드, 성공/실패 결과)
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// NVIDIA 제품 목록(lookupValueSearch TypeID=3)의 항목 하나입니다.
/// </summary>
/// <param name="Name">제품 이름.</param>
/// <param name="Psid">제품 계열 ID(ParentID).</param>
/// <param name="Pfid">제품 ID(Value).</param>
public sealed record NvidiaProduct(string Name, int Psid, int Pfid);

/// <summary>
/// 검증을 마친 NVIDIA 드라이버 목록 항목 하나입니다.
/// </summary>
/// <param name="Version">버전(예: 617.14).</param>
/// <param name="ReleaseDate">배포일(UTC 날짜).</param>
/// <param name="Name">배포 이름(URL 디코딩).</param>
/// <param name="DetailsUrl">공식 배포 설명 URL(검증된 HTTPS NVIDIA 호스트).</param>
/// <param name="DownloadUrl">공식 다운로드 URL(검증된 HTTPS NVIDIA 호스트, 앱은 내려받지 않음).</param>
/// <param name="IsBeta">베타 여부.</param>
public sealed record NvidiaDriverEntry(string Version, DateOnly ReleaseDate, string Name, string DetailsUrl, string DownloadUrl, bool IsBeta);

/// <summary>
/// 드라이버 목록 종류입니다.
/// </summary>
public enum NvidiaDriverListKind
{
    /// <summary>Game Ready 목록(isWHQL=1, IsCRD=0).</summary>
    GameReady,

    /// <summary>Studio 목록(upCRD=1, isWHQL=0, IsCRD=1).</summary>
    Studio,
}

/// <summary>
/// 조회 실패 하나입니다.
/// </summary>
/// <param name="Reason">사유(NetworkFailed: 연결·상태 코드·리디렉션·JSON/XML 아님, ProbeError: 필수 항목 누락·값 오류·매핑 불일치).</param>
/// <param name="Code">요약 코드(영문, 예외 원문 아님).</param>
public sealed record NvidiaLookupFailure(CannotVerifyReason Reason, string Code);

/// <summary>
/// 조회 결과입니다. 값과 실패 중 하나만 있습니다.
/// </summary>
/// <typeparam name="T">값 형식.</typeparam>
/// <param name="Value">성공 값.</param>
/// <param name="Failure">실패.</param>
public sealed record NvidiaLookupResult<T>(T? Value, NvidiaLookupFailure? Failure)
    where T : class
{
    /// <summary>성공 여부.</summary>
    public bool IsSuccess => Failure is null && Value is not null;

    /// <summary>
    /// 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="value">값.</param>
    /// <returns>결과.</returns>
    public static NvidiaLookupResult<T> Success(T value) => new(value, null);

    /// <summary>
    /// 실패 결과를 만듭니다.
    /// </summary>
    /// <param name="reason">사유.</param>
    /// <param name="code">요약 코드.</param>
    /// <returns>결과.</returns>
    public static NvidiaLookupResult<T> Fail(CannotVerifyReason reason, string code) => new(null, new NvidiaLookupFailure(reason, code));

    /// <summary>
    /// 다른 형식의 실패를 그대로 옮깁니다.
    /// </summary>
    /// <param name="failure">실패.</param>
    /// <returns>결과.</returns>
    public static NvidiaLookupResult<T> From(NvidiaLookupFailure failure) => new(null, failure);
}
