/**
 * @file    : RecordingLogger.cs
 * @author  : rudals252
 * @brief   : 기록된 로그를 보관하고 특정 로그가 남을 때까지 기다릴 수 있는 테스트용 로거
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Tests.Unit.Engine.Fakes;

/// <summary>
/// 기록된 로그 한 건입니다.
/// </summary>
internal sealed record LogEntry(string Level, string Category, string Message, Exception? Exception);

/// <summary>
/// 로그를 메모리에 기록하는 테스트용 로거입니다.
/// </summary>
internal sealed class RecordingLogger : IAppLogger
{
    private readonly object _sync = new();
    private readonly List<LogEntry> _entries = [];
    private readonly List<(Predicate<LogEntry> Match, TaskCompletionSource<LogEntry> Signal)> _waiters = [];

    /// <summary>지금까지 기록된 로그의 복사본.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_sync)
            {
                return [.. _entries];
            }
        }
    }

    /// <inheritdoc />
    public void Debug(string category, string message, Exception? ex = null) => Record("Debug", category, message, ex);

    /// <inheritdoc />
    public void Info(string category, string message, Exception? ex = null) => Record("Info", category, message, ex);

    /// <inheritdoc />
    public void Warn(string category, string message, Exception? ex = null) => Record("Warn", category, message, ex);

    /// <inheritdoc />
    public void Error(string category, string message, Exception? ex = null) => Record("Error", category, message, ex);

    /// <summary>
    /// 조건에 맞는 로그가 이미 있거나 새로 기록되면 완료되는 작업을 돌려준다.
    /// </summary>
    public Task<LogEntry> WaitForEntryAsync(Predicate<LogEntry> match)
    {
        lock (_sync)
        {
            var existing = _entries.Find(match);
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var signal = new TaskCompletionSource<LogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((match, signal));
            return signal.Task;
        }
    }

    /// <summary>
    /// 로그를 기록하고 조건이 맞는 대기자를 깨운다.
    /// </summary>
    private void Record(string level, string category, string message, Exception? ex)
    {
        var entry = new LogEntry(level, category, message, ex);
        lock (_sync)
        {
            _entries.Add(entry);
            foreach (var waiter in _waiters.Where(w => w.Match(entry)).ToArray())
            {
                waiter.Signal.TrySetResult(entry);
                _waiters.Remove(waiter);
            }
        }
    }
}
