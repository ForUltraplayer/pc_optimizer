/**
 * @file    : CacheProcessGuard.cs
 * @author  : rudals252
 * @brief   : 종료 실패 프로세스의 재실행 차단·익명 로그·실제 종료 후 복구
 */
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Probes.Actions;

/// <summary>호출자는 직렬화해야 합니다. 종료를 증명할 때까지 새 도구 실행을 차단합니다.</summary>
internal sealed class CacheProcessGuard
{
    private Func<bool>? _hasExited;
    private Action? _dispose;

    /// <summary>실제 종료를 다시 확인하여 살아 있는 프로세스의 중복 실행만 막습니다.</summary>
    internal bool CanRun(IAppLogger log)
    {
        if (_hasExited is null) { return true; }
        try { if (!_hasExited()) { return false; } }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log.Warn(nameof(CacheProcessGuard), $"ExitObservation type={ex.GetType().Name}");
            return false;
        }
        _dispose?.Invoke();
        _hasExited = null;
        _dispose = null;
        return true;
    }

    /// <summary>강제 종료 실패도 유형만 기록하며 종료 대기까지 실패하면 핸들을 보존합니다.</summary>
    internal async Task<bool> StopAsync(Func<bool> hasExited, Action kill, Func<Task> wait, Action dispose, IAppLogger log)
    {
        _hasExited = hasExited;
        _dispose = dispose;
        try
        {
            if (!hasExited())
            {
                try { kill(); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                { log.Warn(nameof(CacheProcessGuard), $"KillFailed type={ex.GetType().Name}"); }
                await wait().ConfigureAwait(false);
            }
            if (!hasExited()) { return false; }
            _hasExited = null;
            _dispose = null;
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log.Warn(nameof(CacheProcessGuard), $"ProcessStillRunning type={ex.GetType().Name}");
            return false;
        }
    }
}
