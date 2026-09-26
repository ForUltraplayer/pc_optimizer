/**
 * @file    : IProcessRunner.cs
 * @author  : rudals252
 * @brief   : 읽기 전용 시스템 도구 하나를 셸 없이 실행해 표준 출력을 받는 계약과 실행 결과 레코드. 테스트에서 fixture 출력으로 바꿔 끼운다
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 자식 프로세스 실행 상태입니다.
/// </summary>
public enum ProcessRunStatus
{
    /// <summary>제한 시간 안에 끝났습니다(종료 코드는 따로 확인).</summary>
    Completed,

    /// <summary>실행 파일이 없습니다.</summary>
    NotFound,

    /// <summary>시작하지 못했습니다.</summary>
    StartFailed,

    /// <summary>제한 시간이 지나 종료시켰습니다.</summary>
    TimedOut,
}

/// <summary>
/// 자식 프로세스 실행 결과입니다.
/// </summary>
/// <param name="Status">실행 상태.</param>
/// <param name="ExitCode">종료 코드(끝나지 않았으면 null).</param>
/// <param name="StandardOutput">표준 출력(바이트 보존 디코딩, 끝나지 않았으면 빈 문자열).</param>
/// <param name="ErrorCode">실패 원인(예외 형식 이름, 메시지 원문 아님).</param>
public sealed record ProcessRunResult(ProcessRunStatus Status, int? ExitCode, string StandardOutput, string? ErrorCode);

/// <summary>
/// 읽기 전용 시스템 도구 실행 계약입니다. 셸을 거치지 않고 전체 경로의 실행 파일만 실행합니다.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// 실행 파일을 인수 목록과 함께 실행하고 표준 출력을 모읍니다. 제한 시간이 지나면 프로세스 트리를 종료합니다.
    /// </summary>
    /// <param name="executablePath">실행 파일 전체 경로.</param>
    /// <param name="arguments">인수 목록(셸 해석 없음).</param>
    /// <param name="timeout">제한 시간.</param>
    /// <param name="ct">취소 토큰(취소되면 프로세스를 종료하고 예외를 던짐).</param>
    /// <returns>실행 결과.</returns>
    ProcessRunResult Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct);
}
