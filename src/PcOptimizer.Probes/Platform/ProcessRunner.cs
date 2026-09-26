/**
 * @file    : ProcessRunner.cs
 * @author  : rudals252
 * @brief   : 전체 경로 실행 파일을 셸 없이(UseShellExecute=false, 창 없음) 실행하고 표준 출력을 바이트 보존 디코딩으로 모으며 제한 시간·취소 시 프로세스 트리를 종료하는 구현
 */

// 기본 패키지
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 자식 프로세스 실행 구현입니다. 출력은 Latin-1로 디코딩해 바이트를 보존합니다(지역화 코드 페이지와 무관하게 ASCII 키·숫자가 유지됨).
/// 표준 오류는 읽어서 버리며 로그·리포트에 남기지 않습니다.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>공유 인스턴스.</summary>
    public static ProcessRunner Instance { get; } = new();

    /// <inheritdoc />
    public ProcessRunResult Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
        {
            return new ProcessRunResult(ProcessRunStatus.NotFound, null, string.Empty, null);
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.Latin1,
            StandardErrorEncoding = Encoding.Latin1,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return new ProcessRunResult(ProcessRunStatus.StartFailed, null, string.Empty, null);
            }
        }
        catch (Win32Exception ex)
        {
            return new ProcessRunResult(ProcessRunStatus.StartFailed, null, string.Empty, ex.GetType().Name);
        }
        catch (InvalidOperationException ex)
        {
            return new ProcessRunResult(ProcessRunStatus.StartFailed, null, string.Empty, ex.GetType().Name);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var registration = ct.Register(() => Kill(process));

        if (!process.WaitForExit(timeout))
        {
            Kill(process);
            process.WaitForExit();
            return new ProcessRunResult(ProcessRunStatus.TimedOut, null, string.Empty, null);
        }

        process.WaitForExit();
        ct.ThrowIfCancellationRequested();
        var output = outputTask.GetAwaiter().GetResult();
        _ = errorTask.GetAwaiter().GetResult();
        return new ProcessRunResult(ProcessRunStatus.Completed, process.ExitCode, output, null);
    }

    /// <summary>
    /// 프로세스 트리를 종료한다. 이미 끝났으면 무시한다.
    /// </summary>
    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 이미 끝난 프로세스: 종료할 것이 없다.
        }
        catch (Win32Exception)
        {
            // 종료 중 접근 오류: 호출자는 제한 시간/취소 결과로 처리한다.
        }
    }
}
