/**
 * @file    : CacheProcessGuardTests.cs
 * @author  : rudals252
 * @brief   : 종료 실패 시 익명 로그·중복 차단·나중 종료 시 복구 검증
 */
using System.ComponentModel;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>죽일 수 없는 실제 프로세스 없이 종료 실패 경로를 재현합니다.</summary>
public sealed class CacheProcessGuardTests
{
    /// <summary>Kill과 종료 대기 실패는 개인정보 없이 기록하고 실제 종료까지 차단합니다.</summary>
    [Fact]
    public async Task FailedKillBlocksUntilObservedExitAndLogsOnlyTypes()
    {
        var guard = new CacheProcessGuard();
        var log = new RecordingLogger();
        var exited = false;
        var disposed = false;
        Assert.False(await guard.StopAsync(() => exited,
            () => throw new Win32Exception("private path secret"),
            () => Task.FromException(new TimeoutException("private output")), () => disposed = true, log));
        Assert.False(guard.CanRun(log));
        Assert.False(disposed);
        Assert.Contains(log.Entries, e => e.Message == "KillFailed type=Win32Exception");
        Assert.Contains(log.Entries, e => e.Message == "ProcessStillRunning type=TimeoutException");
        Assert.All(log.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain("private", entry.Message, StringComparison.Ordinal);
        });
        exited = true;
        Assert.True(guard.CanRun(log));
        Assert.True(disposed);
    }

    /// <summary>Kill 예외가 있어도 종료를 관측했으면 영구 비활성화하지 않습니다.</summary>
    [Fact]
    public async Task ExitAfterFailedKillDoesNotLatchBlocker()
    {
        var guard = new CacheProcessGuard();
        var log = new RecordingLogger();
        var exited = false;
        Assert.True(await guard.StopAsync(() => exited, () => throw new InvalidOperationException(),
            () => { exited = true; return Task.CompletedTask; }, () => { }, log));
        Assert.True(guard.CanRun(log));
    }
}
