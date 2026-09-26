/**
 * @file    : CacheToolProcess.cs
 * @author  : rudals252
 * @brief   : 셸 없는 고정 도구 실행과 제한된 출력·시간·환경, 사용자 캐시 명령 조립
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
            var work = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!SystemCacheToolBackend.IsPlainPath(work, directory: true)) { return new(false, string.Empty, false, "UnsafeWorkDirectory"); }
            foreach (var segment in new[] { "PcOptimizer", "ToolWork" })
            {
                work = Path.Combine(work, segment);
                Directory.CreateDirectory(work);
                if (!SystemCacheToolBackend.IsPlainPath(work, directory: true)) { return new(false, string.Empty, false, "UnsafeWorkDirectory"); }
            }
            var start = new ProcessStartInfo(tool.Executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = work,
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
            process = new Process { StartInfo = start };
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
