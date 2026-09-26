/**
 * @file    : CacheProcessGuardTests.cs
 * @author  : rudals252
 * @brief   : 종료 실패 시 익명 로그·중복 차단·나중 종료 시 복구 검증, 트리 종료의 AggregateException 처리(REV-010)
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

    /// <summary>기존 REV-010: 트리 종료의 AggregateException도 실행 이력을 보존하는 경로로 처리해야 합니다.</summary>
    [Fact]
    public async Task AggregateKillFailureMustNotEscapeGuard()
    {
        var guard = new CacheProcessGuard();
        var log = new RecordingLogger();
        var exited = false;
        var stopped = await guard.StopAsync(() => exited,
            () => throw new AggregateException(new System.ComponentModel.Win32Exception("fixture")),
            () => { exited = true; return Task.CompletedTask; }, () => { }, log);
        Assert.True(stopped);
    }

    /// <summary>트리 종료 AggregateException 뒤 종료 대기도 실패하면 형식 이름만 남기고 실제 종료까지 차단을 유지합니다(REV-010).</summary>
    [Fact]
    public async Task AggregateKillAndWaitFailureKeepsBlockerWithTypeOnlyLogs()
    {
        var guard = new CacheProcessGuard();
        var log = new RecordingLogger();
        var exited = false;
        var disposed = false;
        Assert.False(await guard.StopAsync(() => exited,
            () => throw new AggregateException(new Win32Exception("private kill"), new AggregateException(new InvalidOperationException("private nested"))),
            () => Task.FromException(new AggregateException(new TimeoutException("private wait"))), () => disposed = true, log));
        Assert.False(guard.CanRun(log));
        Assert.False(disposed);
        Assert.Contains(log.Entries, e => e.Message.StartsWith("KillFailed type=AggregateException", StringComparison.Ordinal));
        Assert.Contains(log.Entries, e => e.Message.StartsWith("ProcessStillRunning type=AggregateException", StringComparison.Ordinal));
        Assert.All(log.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain("private", entry.Message, StringComparison.Ordinal);
        });
        exited = true;
        Assert.True(guard.CanRun(log));
        Assert.True(disposed);
    }

    /// <summary>예상하지 않은 내부 예외가 섞인 AggregateException은 삼키지 않고 전파합니다(REV-010).</summary>
    [Fact]
    public async Task AggregateWithUnexpectedInnerExceptionPropagates()
    {
        var guard = new CacheProcessGuard();
        var log = new RecordingLogger();
        await Assert.ThrowsAsync<AggregateException>(() => guard.StopAsync(() => false,
            () => throw new AggregateException(new Win32Exception(), new ArgumentException()),
            () => Task.CompletedTask, () => { }, log));
    }
}
