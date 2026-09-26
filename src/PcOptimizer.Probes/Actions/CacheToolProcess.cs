/**
 * @file    : CacheToolProcess.cs
 * @author  : rudals252
 * @brief   : 셸 없는 고정 도구 실행(작업 폴더 System32 고정)과 제한된 출력·시간·환경, 사용자 캐시 명령 조립
 */
using System.Diagnostics;
using System.Text;
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Probes.Actions;

/// <summary>프로세스 시작 여부와 종료 관측을 따로 전달합니다.</summary>
internal sealed record CacheProcessResult(bool Success, string Output, bool Started, string Code);

/// <summary>공식 캐시 도구 명령을 직접 실행합니다. 임의 셸 명령을 받지 않습니다.</summary>
internal static class CacheToolProcess
{
    private const int MAX_OUTPUT_CHARS = 16384;
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan QUERY_TIMEOUT = TimeSpan.FromSeconds(15);
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly CacheProcessGuard Guard = new();

    /// <summary>
    /// 도구 작업 폴더(System32)입니다. 관리자 권한으로 실행하므로 작업 폴더와 그 상위 폴더가 모두 관리자 전용이어야 한다.
    /// dotnet은 작업 폴더부터 global.json을, npm은 프로젝트 .npmrc를 찾으며 python은 작업 폴더를 모듈 경로로 쓸 수 있다.
    /// 사용자 쓰기 가능 폴더(예: %LOCALAPPDATA%)를 쓰면 사용자 폴더 코드·설정이 관리자 권한으로 로드될 수 있다.
    /// </summary>
    private static readonly string TOOL_WORKING_DIRECTORY = Environment.GetFolderPath(Environment.SpecialFolder.System);

    /// <summary>허용된 명령을 직렬 실행하고 실제 시작 여부를 전달합니다.</summary>
    public static async Task<CacheProcessResult> RunAsync(CacheToolLocation tool, bool clear, CancellationToken ct, IAppLogger? logger = null)
    {
        var log = logger ?? NullAppLogger.Instance;
        // 이미 검사한 대상이 대기열에서 오래된 상태가 되지 않도록 즉시 거절한다.
        if (!await Gate.WaitAsync(0, ct).ConfigureAwait(false)) { return new(false, string.Empty, false, "Busy"); }
        Process? process = null;
        var started = false;
        var retained = false;
        try
        {
            if (!Guard.CanRun(log)) { return new(false, string.Empty, false, "ProcessStillRunning"); }
            if (!Path.IsPathFullyQualified(TOOL_WORKING_DIRECTORY) || !SystemCacheToolBackend.IsPlainPath(TOOL_WORKING_DIRECTORY, directory: true))
            { return new(false, string.Empty, false, "UnsafeWorkDirectory"); }
            process = new Process { StartInfo = CreateStartInfo(tool, clear) };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(clear ? TIMEOUT : QUERY_TIMEOUT);
            if (!process.Start()) { return new(false, string.Empty, false, "StartFailed"); }
            started = true;
            var output = ReadBoundedAsync(process.StandardOutput, deadline.Token);
            var error = ReadBoundedAsync(process.StandardError, deadline.Token);
            try
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                var text = await output.ConfigureAwait(false);
                await error.ConfigureAwait(false);
                return new(process.ExitCode == 0, text, true, process.ExitCode == 0 ? "Completed" : "ToolFailed");
            }
            finally
            {
                retained = !await Guard.StopAsync(() => process.HasExited, () => process.Kill(entireProcessTree: true),
                    () => process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)), process.Dispose, log).ConfigureAwait(false);
                try { await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or TimeoutException or ObjectDisposedException)
                { log.Warn(nameof(CacheToolProcess), $"OutputObservation type={ex.GetType().Name}"); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            log.Warn(nameof(CacheToolProcess), $"ToolExecution type={ex.GetType().Name}");
            return new(false, string.Empty, started, retained ? "ProcessStillRunning" : started ? "ToolFailed" : "StartFailed");
        }
        finally
        {
            if (!retained) { process?.Dispose(); }
            Gate.Release();
        }
    }

    /// <summary>
    /// 셸 없이 실행할 시작 정보를 만듭니다(프로세스는 시작하지 않음). 작업 폴더는 System32로 고정하고, 런타임을 바꾸는 환경 변수는 지웁니다.
    /// </summary>
    /// <param name="tool">검사를 통과한 도구 위치.</param>
    /// <param name="clear">정리 명령이면 true, 위치 조회면 false.</param>
    /// <returns>시작 정보.</returns>
    internal static ProcessStartInfo CreateStartInfo(CacheToolLocation tool, bool clear)
    {
        var start = new ProcessStartInfo(tool.Executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = TOOL_WORKING_DIRECTORY,
        };
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("COR_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("NODE_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("PYTHON", StringComparison.OrdinalIgnoreCase)).ToArray())
        { start.Environment.Remove(key); }
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.Environment["NPM_CONFIG_UPDATE_NOTIFIER"] = "false";
        foreach (var arg in Arguments(tool, clear)) { start.ArgumentList.Add(arg); }
        if (clear && tool.Tool == CacheTool.NuGetHttp) { start.Environment["NUGET_HTTP_CACHE_PATH"] = tool.CachePath; }
        return start;
    }

    /// <summary>사용자 입력을 명령으로 해석하지 않는 인자 배열입니다.</summary>
    internal static IReadOnlyList<string> Arguments(CacheToolLocation tool, bool clear) => tool.Tool switch
    {
        CacheTool.Npm => clear
            ? [tool.Script!, "cache", "clean", "--force", "--offline", "--cache", tool.CachePath]
            : [tool.Script!, "config", "get", "cache"],
        CacheTool.Pip => clear
            ? ["-I", "-m", "pip", "--cache-dir", tool.CachePath, "cache", "purge"]
            : ["-I", "-m", "pip", "cache", "dir"],
        CacheTool.NuGetHttp => ["nuget", "locals", "http-cache", clear ? "--clear" : "--list"],
        _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };

    /// <summary>출력은 계속 소모하되 메모리에 남기는 크기는 제한합니다.</summary>
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken ct)
    {
        var builder = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        { builder.Append(buffer, 0, Math.Min(count, MAX_OUTPUT_CHARS - builder.Length)); }
        return builder.ToString();
    }
}
