/**
 * @file    : ScanLogEvents.cs
 * @author  : rudals252
 * @brief   : 실행 조율 로그 메시지 앞에 붙는 이벤트 이름 상수(로그 검색·테스트 검증용)
 */

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 실행 조율 로그 메시지 앞에 붙는 이벤트 이름입니다. 메시지는 "이벤트 key=value ..." 형식입니다.
/// </summary>
public static class ScanLogEvents
{
    /// <summary>타임아웃·취소 뒤 늦게 끝난 호출의 결과를 버림.</summary>
    public const string LATE_RESULT_DISCARDED = "LateResultDiscarded";

    /// <summary>프로브가 제한 시간 안에 끝나지 않음.</summary>
    public const string PROBE_TIMED_OUT = "ProbeTimedOut";

    /// <summary>프로브가 예외·잘못된 결과로 실패함.</summary>
    public const string PROBE_FAILED = "ProbeFailed";

    /// <summary>취소를 요청했지만 호출이 아직 끝나지 않음.</summary>
    public const string PROBE_STILL_RUNNING_AFTER_CANCEL = "ProbeStillRunningAfterCancel";

    /// <summary>이전 호출이 아직 종료 중이라 프로브를 건너뜀.</summary>
    public const string PROBE_DRAINING_SKIPPED = "ProbeDrainingSkipped";
}
