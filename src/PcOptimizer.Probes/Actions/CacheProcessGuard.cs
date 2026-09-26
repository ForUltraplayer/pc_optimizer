/**
 * @file    : CacheProcessGuard.cs
 * @author  : rudals252
 * @brief   : 종료 실패 프로세스의 재실행 차단·익명 로그·실제 종료 후 복구(트리 종료의 AggregateException 포함)
 */

// 기본 패키지
using System.ComponentModel;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Probes.Actions;

/// <summary>호출자는 직렬화해야 합니다. 종료를 증명할 때까지 새 도구 실행을 차단합니다.</summary>
internal sealed class CacheProcessGuard
{
    private const string INNER_TYPE_SEPARATOR = ",";

    private Func<bool>? _hasExited;
    private Action? _dispose;

    /// <summary>실제 종료를 다시 확인하여 살아 있는 프로세스의 중복 실행만 막습니다.</summary>
    internal bool CanRun(IAppLogger log)
    {
        if (_hasExited is null) { return true; }
        try { if (!_hasExited()) { return false; } }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            log.Warn(nameof(CacheProcessGuard), $"ExitObservation type={ex.GetType().Name}");
            return false;
        }
        _dispose?.Invoke();
        _hasExited = null;
        _dispose = null;
        return true;
    }

    /// <summary>
    /// 강제 종료 실패도 유형만 기록하며 종료 대기까지 실패하면 핸들을 보존합니다.
    /// 트리 종료(Kill(entireProcessTree))가 던지는 AggregateException은 펼친 내부 예외가 모두 예상 유형일 때만 같은 경로로 처리합니다.
    /// </summary>
    internal async Task<bool> StopAsync(Func<bool> hasExited, Action kill, Func<Task> wait, Action dispose, IAppLogger log)
    {
        _hasExited = hasExited;
        _dispose = dispose;
        try
        {
            if (!hasExited())
            {
                try { kill(); }
                catch (Exception ex) when (IsExpected(ex, IsKillFailure))
                { log.Warn(nameof(CacheProcessGuard), $"KillFailed type={TypeNames(ex)}"); }
                await wait().ConfigureAwait(false);
            }
            if (!hasExited()) { return false; }
            _hasExited = null;
            _dispose = null;
            return true;
        }
        catch (Exception ex) when (IsExpected(ex, IsStopFailure))
        {
            log.Warn(nameof(CacheProcessGuard), $"ProcessStillRunning type={TypeNames(ex)}");
            return false;
        }
    }

    /// <summary>강제 종료 호출이 실패로 보고하는 예외 유형인지 여부.</summary>
    private static bool IsKillFailure(Exception ex) => ex is InvalidOperationException or Win32Exception;

    /// <summary>종료 대기·종료 관측이 실패로 보고하는 예외 유형인지 여부.</summary>
    private static bool IsStopFailure(Exception ex) => ex is TimeoutException || IsKillFailure(ex);

    /// <summary>
    /// 예외가 예상 유형인지 판단합니다. AggregateException은 펼친 내부 예외가 하나 이상이고 모두 예상 유형일 때만 true입니다.
    /// </summary>
    private static bool IsExpected(Exception ex, Func<Exception, bool> expected)
    {
        if (ex is not AggregateException aggregate)
        {
            return expected(ex);
        }

        var inner = aggregate.Flatten().InnerExceptions;
        return inner.Count > 0 && inner.All(expected);
    }

    /// <summary>
    /// 로그용 형식 이름(메시지 제외). AggregateException은 펼친 내부 형식 이름을 함께 적습니다.
    /// </summary>
    private static string TypeNames(Exception ex)
    {
        if (ex is not AggregateException aggregate)
        {
            return ex.GetType().Name;
        }

        var inner = aggregate.Flatten().InnerExceptions.Select(e => e.GetType().Name).Distinct(StringComparer.Ordinal);
        return $"{ex.GetType().Name} inner={string.Join(INNER_TYPE_SEPARATOR, inner)}";
    }
}
