/**
 * @file    : FakeProcessRunner.cs
 * @author  : rudals252
 * @brief   : 실제 프로세스를 띄우지 않고 고정 실행 결과(fixture 출력·시간 초과·시작 실패)를 돌려주며 호출 인수를 기록하는 테스트용 실행기
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 프로세스 실행기입니다.
/// </summary>
internal sealed class FakeProcessRunner(ProcessRunResult result) : IProcessRunner
{
    /// <summary>실행 요청 기록(실행 파일 경로, 인수, 제한 시간).</summary>
    public List<(string Path, IReadOnlyList<string> Arguments, TimeSpan Timeout)> Calls { get; } = [];

    /// <inheritdoc />
    public ProcessRunResult Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        Calls.Add((executablePath, [.. arguments], timeout));
        return result;
    }
}
