/**
 * @file    : RepairCommandRunner.cs
 * @author  : rudals252
 * @brief   : 닫힌 목록의 Windows 내장 복구 명령을 셸 없이 System32 절대 경로·고정 인자로 실행하고, 출력을 상한 안에서 진행 콜백으로 전달하며 취소·시간 초과 시 프로세스 트리를 종료
 */
using System.Diagnostics;
using System.Text;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Troubleshooting;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.Probes.Troubleshooting;

/// <summary>명령 실행 결과입니다. 시작 여부와 종료 코드를 분리해 전달합니다.</summary>
/// <param name="CommandId">명령 ID.</param>
/// <param name="Started">프로세스가 실제로 시작됐는지.</param>
/// <param name="ExitCode">종료 코드(시작 못 했거나 강제 종료면 null).</param>
/// <param name="Code">결과 코드: Completed·Failed·Cancelled·TimedOut·StartFailed·ExecutableMissing·Busy.</param>
/// <param name="OutputTail">출력 마지막 부분(상한 적용).</param>
/// <param name="RebootRequired">완료 뒤 다시 시작이 필요한지.</param>
public sealed record RepairCommandResult(string CommandId, bool Started, int? ExitCode, string Code, string OutputTail, bool RebootRequired)
{
    /// <summary>성공 여부입니다.</summary>
    public bool Succeeded => Code == RepairCommandRunner.CODE_COMPLETED;
}

/// <summary>한 번에 하나의 명령만 실행합니다. 사용자 입력을 인자로 받지 않습니다.</summary>
public sealed class RepairCommandRunner
{
    /// <summary>정상 완료.</summary>
    public const string CODE_COMPLETED = "Completed";
    /// <summary>종료 코드가 성공 목록 밖.</summary>
    public const string CODE_FAILED = "Failed";
    /// <summary>사용자 취소.</summary>
    public const string CODE_CANCELLED = "Cancelled";
    /// <summary>시간 상한 초과.</summary>
    public const string CODE_TIMED_OUT = "TimedOut";
    /// <summary>프로세스 시작 실패.</summary>
    public const string CODE_START_FAILED = "StartFailed";
    /// <summary>System32에 실행 파일이 없음.</summary>
    public const string CODE_EXECUTABLE_MISSING = "ExecutableMissing";
    /// <summary>다른 명령이 실행 중.</summary>
    public const string CODE_BUSY = "Busy";
    /// <summary>출력 보관 상한(문자).</summary>
    public const int MAX_OUTPUT_CHARS = 32 * 1024;
    private const int READ_BUFFER_BYTES = 4096;
    private const int UNICODE_NULL_RATIO_PERCENT = 20;
    private static readonly TimeSpan KILL_WAIT = TimeSpan.FromSeconds(5);
    private static readonly SemaphoreSlim Gate = new(1);
    private readonly IAppLogger _logger;
    private readonly string _systemDirectory;
    private readonly string _systemDrive;

    /// <summary>실제 System32와 시스템 드라이브를 씁니다.</summary>
    public RepairCommandRunner(IAppLogger? logger = null)
        : this(logger, Environment.GetFolderPath(Environment.SpecialFolder.System), Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))?.TrimEnd('\\') ?? "C:") { }

    internal RepairCommandRunner(IAppLogger? logger, string systemDirectory, string systemDrive)
    {
        _logger = logger ?? NullAppLogger.Instance;
        _systemDirectory = systemDirectory;
        _systemDrive = systemDrive;
    }

    /// <summary>
    /// 셸 없이 실행할 시작 정보를 만듭니다(프로세스는 시작하지 않음). 실행 파일은 System32 절대 경로, 작업 폴더는 System32,
    /// 코드 주입이 가능한 환경 변수는 <see cref="CacheToolProcess"/>와 같은 규칙으로 지웁니다.
    /// </summary>
    internal ProcessStartInfo CreateStartInfo(RepairCommand command)
    {
        var start = new ProcessStartInfo(Path.Combine(_systemDirectory, command.Executable))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = _systemDirectory,
        };
        foreach (var key in start.Environment.Keys.Where(CacheToolProcess.IsStrippedVariable).ToArray()) { start.Environment.Remove(key); }
        foreach (var argument in command.Arguments) { start.ArgumentList.Add(argument.Replace(RepairCommandCatalog.SYSTEM_DRIVE_TOKEN, _systemDrive, StringComparison.Ordinal)); }
        return start;
    }

    /// <summary>명령을 실행하고 출력 줄을 <paramref name="progress"/>로 전달합니다. 예외를 던지지 않습니다.</summary>
    public async Task<RepairCommandResult> RunAsync(RepairCommand command, IProgress<string>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await Gate.WaitAsync(0, CancellationToken.None).ConfigureAwait(false)) { return new(command.Id, false, null, CODE_BUSY, string.Empty, command.RebootRequired); }
        Process? process = null;
        var started = false;
        var output = new OutputCollector(progress);
        try
        {
            var start = CreateStartInfo(command);
            if (!File.Exists(start.FileName) || !SystemCacheToolBackend.IsPlainPath(start.FileName, directory: false))
            { return new(command.Id, false, null, CODE_EXECUTABLE_MISSING, string.Empty, command.RebootRequired); }
            process = new Process { StartInfo = start };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(command.Timeout);
            if (!process.Start()) { return new(command.Id, false, null, CODE_START_FAILED, string.Empty, command.RebootRequired); }
            started = true;
            _logger.Info(nameof(RepairCommandRunner), $"CommandStarted id={command.Id}");
            var stdout = output.PumpAsync(process.StandardOutput.BaseStream, deadline.Token);
            var stderr = output.PumpAsync(process.StandardError.BaseStream, deadline.Token);
            try
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                try { await Task.WhenAll(stdout, stderr).WaitAsync(KILL_WAIT, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or IOException) { _logger.Warn(nameof(RepairCommandRunner), $"OutputObservation type={ex.GetType().Name}"); }
                var exit = process.ExitCode;
                var code = command.SuccessExitCodes.Contains(exit) ? CODE_COMPLETED : CODE_FAILED;
                _logger.Info(nameof(RepairCommandRunner), $"CommandExited id={command.Id} exit={exit}");
                return new(command.Id, true, exit, code, output.Tail, command.RebootRequired);
            }
            catch (OperationCanceledException)
            {
                var reason = ct.IsCancellationRequested ? CODE_CANCELLED : CODE_TIMED_OUT;
                await KillAsync(process).ConfigureAwait(false);
                _logger.Warn(nameof(RepairCommandRunner), $"CommandStopped id={command.Id} reason={reason}");
                return new(command.Id, true, null, reason, output.Tail, command.RebootRequired);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.Warn(nameof(RepairCommandRunner), $"CommandFailure id={command.Id} type={ex.GetType().Name}");
            return new(command.Id, started, null, started ? CODE_FAILED : CODE_START_FAILED, output.Tail, command.RebootRequired);
        }
        finally
        {
            process?.Dispose();
            Gate.Release();
        }
    }

    private async Task KillAsync(Process process)
    {
        try
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync().WaitAsync(KILL_WAIT).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException or AggregateException)
        { _logger.Warn(nameof(RepairCommandRunner), $"KillFailed type={ex.GetType().Name}"); }
    }

    /// <summary>
    /// 바이트 스트림을 읽어 줄 단위로 진행을 알립니다. sfc는 리디렉션 시 UTF-16으로, dism·chkdsk는 콘솔 코드 페이지로 출력하므로 널 바이트 비율로 인코딩을 고릅니다.
    /// </summary>
    private sealed class OutputCollector(IProgress<string>? progress)
    {
        private readonly StringBuilder _text = new();
        private readonly object _sync = new();
        private Encoding? _encoding;
        private string _pending = string.Empty;

        internal string Tail { get { lock (_sync) { return _text.ToString(); } } }

        internal async Task PumpAsync(Stream stream, CancellationToken ct)
        {
            var buffer = new byte[READ_BUFFER_BYTES];
            var decoder = (Decoder?)null;
            int count;
            while ((count = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                decoder ??= ChooseEncoding(buffer.AsSpan(0, count)).GetDecoder();
                var chars = new char[decoder.GetCharCount(buffer, 0, count)];
                var produced = decoder.GetChars(buffer, 0, count, chars, 0);
                Append(new string(chars, 0, produced));
            }
            Flush();
        }

        private Encoding ChooseEncoding(ReadOnlySpan<byte> sample)
        {
            if (_encoding is not null) { return _encoding; }
            var nulls = 0;
            foreach (var b in sample) { if (b == 0) { nulls++; } }
            var unicode = sample.Length > 0 && nulls * 100 / sample.Length >= UNICODE_NULL_RATIO_PERCENT;
            return _encoding = unicode ? Encoding.Unicode : Console.OutputEncoding;
        }

        private void Append(string chunk)
        {
            lock (_sync)
            {
                var remaining = MAX_OUTPUT_CHARS - _text.Length;
                if (remaining <= 0) { return; }
                _text.Append(chunk.Length <= remaining ? chunk : chunk[..remaining]);
                _pending += chunk;
                var separators = new[] { '\r', '\n' };
                int cut;
                while ((cut = _pending.IndexOfAny(separators)) >= 0)
                {
                    var line = _pending[..cut];
                    _pending = _pending[(cut + 1)..];
                    if (line.Trim().Length > 0) { progress?.Report(line.TrimEnd()); }
                }
            }
        }

        private void Flush()
        {
            lock (_sync)
            {
                if (_pending.Trim().Length > 0) { progress?.Report(_pending.TrimEnd()); }
                _pending = string.Empty;
            }
        }
    }
}
