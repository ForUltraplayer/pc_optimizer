/**
 * @file    : TargetMeasurement.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 관측 대상 하나의 관측 결과(상태, 규칙 패턴에 맞는 파일의 논리 크기·수, 사유별 건너뜀, 중복 가능, 공유 스캔 합계 사용 여부) 레코드와 상태 열거형
 */

// 사용자 패키지
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 관측 대상의 상태입니다.
/// </summary>
public enum TargetState
{
    /// <summary>완전 관측.</summary>
    Observed,

    /// <summary>읽지 못한 항목이 있는 부분 관측.</summary>
    Partial,

    /// <summary>폴더가 없음(오류 아님).</summary>
    Absent,

    /// <summary>폴더 자체 접근 거부.</summary>
    AccessDenied,

    /// <summary>폴더가 reparse point라 따라가지 않음.</summary>
    ReparsePoint,

    /// <summary>보호 루트 안(재확인).</summary>
    Protected,

    /// <summary>시간 예산을 넘겨 관측하지 못함.</summary>
    TimedOut,

    /// <summary>그 밖의 오류.</summary>
    Error,
}

/// <summary>
/// 관측 대상 하나의 결과입니다. 크기는 관측한 파일의 논리 크기이며 비울 수 있는 용량이 아닙니다.
/// </summary>
/// <param name="Order">대상 처리 순서(<c>ObservationTarget.Order</c>).</param>
/// <param name="State">상태.</param>
/// <param name="Bytes">관측 논리 크기(이 대상이 처음 센 파일만).</param>
/// <param name="FileCount">관측 파일 수.</param>
/// <param name="Skips">사유별 건너뜀.</param>
/// <param name="DuplicatesPossible">경로 단위로만 중복을 걸러 하드링크 중복이 섞였을 수 있는지 여부.</param>
/// <param name="FromSharedScan">공유 파일 스캔의 폴더 합계를 썼는지 여부(아니면 대상 열거).</param>
public sealed record TargetMeasurement(
    int Order,
    TargetState State,
    long Bytes,
    long FileCount,
    SkipCounts Skips,
    bool DuplicatesPossible,
    bool FromSharedScan)
{
    /// <summary>파일을 관측했는지(완전·부분) 여부.</summary>
    public bool IsObserved => State is TargetState.Observed or TargetState.Partial;
}
