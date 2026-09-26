/**
 * @file    : CacheCleanupTests.cs
 * @author  : rudals252
 * @brief   : 자동 정리의 승인·만료·일회성·변경·보호·실패·후속 관측 경계 검증
 */
using System.IO;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>사용자 캐시를 변경하지 않는 실행 계약 회귀 테스트입니다.</summary>
public sealed class CacheCleanupTests
{
    /// <summary>실행 후 조회 실패는 0바이트로 바꾸지 않습니다.</summary>
    [Fact]
    public async Task UnverifiedAfterActionIsNotZero()
    {
        var backend = new Backend { BlockAfterClear = true };
        var service = new CacheCleanupService(backend);
        var plan = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.ToolSucceeded);
        Assert.Null(result.RemainingBytes);
        Assert.Equal(500, result.BeforeBytes);
    }

    /// <summary>도구가 없거나 처음부터 보호된 대상이면 계획을 발급하지 않습니다.</summary>
    [Fact]
    public async Task BlockedPreviewCannotExecute()
    {
        var backend = new Backend { Allowed = false };
        var service = new CacheCleanupService(backend);
        Assert.Null((await service.PrepareAsync(CacheTool.Pip, default)).Plan);
        Assert.Equal(0, backend.Clears);
    }

    /// <summary>하나가 실행 중이면 두 번째 정리는 실행하지 않습니다.</summary>
    [Fact]
    public async Task ConcurrentExecutionIsRejected()
    {
        var backend = new Backend { ClearGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = new CacheCleanupService(backend);
        var first = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        var second = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        var running = service.ExecuteAsync(first.Id, default);
        var refused = await service.ExecuteAsync(second.Id, default);
        Assert.Equal("Busy", refused.Code);
        Assert.False(refused.Started);
        Assert.Equal(1, backend.Clears);
        backend.ClearGate.SetResult();
        await running;
    }
    /// <summary>미리보기는 정리하지 않으며 발급한 ID만 한 번 실행할 수 있습니다.</summary>
    [Fact]
    public async Task RequiresSingleUsePreview()
    {
        var backend = new Backend();
        var service = new CacheCleanupService(backend);
        Assert.False((await service.ExecuteAsync(Guid.NewGuid(), default)).ToolSucceeded);
        var plan = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        Assert.Equal(0, backend.Clears);
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.ToolSucceeded);
        Assert.Equal(0, result.RemainingBytes);
        Assert.False((await service.ExecuteAsync(plan.Id, default)).ToolSucceeded);
        Assert.Equal(1, backend.Clears);
    }

    /// <summary>도구 지문이나 경로가 바뀌면 실행하지 않습니다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedTargetRequiresNewPreview(bool pathChanged)
    {
        var backend = new Backend();
        var service = new CacheCleanupService(backend);
        var plan = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        backend.Location = pathChanged ? backend.Location with { CachePath = @"C:\Users\tester\other" } : backend.Location with { Fingerprint = "different" };
        Assert.Equal("TargetChanged", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(0, backend.Clears);
    }

    /// <summary>확인 이후 보호 상태가 바뀌면 실행하지 않습니다.</summary>
    [Fact]
    public async Task RechecksProtection()
    {
        var backend = new Backend();
        var service = new CacheCleanupService(backend);
        var plan = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        backend.Allowed = false;
        Assert.False((await service.ExecuteAsync(plan.Id, default)).ToolSucceeded);
        Assert.Equal(0, backend.Clears);
    }

    /// <summary>유효 기간이 지나면 실행하지 않습니다.</summary>
    [Fact]
    public async Task Expires()
    {
        var backend = new Backend();
        var time = new ManualTimeProvider();
        var service = new CacheCleanupService(backend, time);
        var plan = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal("PlanExpired", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(0, backend.Clears);
    }

    /// <summary>실패 종료를 성공으로 표시하지 않고 후속 측정은 따로 돌려줍니다.</summary>
    [Fact]
    public async Task FailureIsNotSuccess()
    {
        var backend = new Backend { Succeeds = false };
        var service = new CacheCleanupService(backend);
        var plan = (await service.PrepareAsync(CacheTool.Npm, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.ToolSucceeded);
        Assert.Equal("ToolFailed", result.Code);
        Assert.NotNull(result.RemainingBytes);
    }

    /// <summary>확인창에서 취소하면 실행과 재검사 요청이 없습니다.</summary>
    [Fact]
    public async Task CancelledConfirmationDoesNotExecute()
    {
        var backend = new Backend();
        var vm = new CacheToolsViewModel(new CacheCleanupService(backend), _ => false);
        await vm.PrepareCommand.ExecuteAsync(null);
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.Equal(0, backend.Clears);
        Assert.False(vm.NeedsRescan);
        Assert.Null(vm.Outcome);
    }

    /// <summary>상태 문구가 바뀌어도 준비된 대상을 확인 창에 표시합니다.</summary>
    [Fact]
    public async Task ConfirmationUsesPreparedTargetEvenIfStatusMessageChanges()
    {
        var backend = new Backend();
        string? confirmation = null;
        var vm = new CacheToolsViewModel(new CacheCleanupService(backend), text => { confirmation = text; return false; });
        await vm.PrepareCommand.ExecuteAsync(null);
        vm.Message = "Windows 저장소 설정을 열었습니다.";
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.Contains(backend.Location.CachePath, confirmation, StringComparison.Ordinal);
        Assert.Contains(backend.Location.Executable, confirmation, StringComparison.Ordinal);
        Assert.Contains(PcOptimizer.App.Resources.Strings.Cleanup_Impact, confirmation, StringComparison.Ordinal);
        Assert.Equal(0, backend.Clears);
    }

    /// <summary>미리보기 대신 실행 직전 크기로 전후 차이를 계산합니다.</summary>
    [Fact]
    public async Task OutcomeComparesImmediatePreflightInsteadOfStalePreview()
    {
        var backend = new Backend { BeforeBytes = 500 };
        var vm = new CacheToolsViewModel(new CacheCleanupService(backend), _ => true);
        await vm.PrepareCommand.ExecuteAsync(null);
        backend.BeforeBytes = 900; // Cache grew between preview and confirmed execution.
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.Equal("900 B", vm.Outcome!.BeforeText);
        Assert.Equal("0 B", vm.Outcome.AfterText);
        Assert.Contains("900 B", vm.Outcome.ChangeText, StringComparison.Ordinal);
        Assert.True(vm.NeedsRescan);
        var outcome = vm.Outcome;
        vm.SelectedTool = 1;
        Assert.Same(outcome, vm.Outcome); // A new selection does not erase the last attempted action.
        Assert.Equal("npm", vm.Outcome.Tool);
    }

    /// <summary>실패·미확인·크기 증가를 성공적인 정리로 표시하지 않습니다.</summary>
    [Theory]
    [InlineData(true, null, "Cleanup_OutcomeUnverified")]
    [InlineData(true, 500L, "Cleanup_OutcomeUnchanged")]
    [InlineData(true, 700L, "Cleanup_OutcomeIncreased")]
    [InlineData(false, 0L, "Cleanup_OutcomeFailed")]
    public void OutcomeDoesNotInventSuccessfulSavings(bool success, long? after, string expectedKey)
    {
        var outcome = new CleanupOutcomeViewModel("npm", new(success, after, "fixture") { BeforeBytes = 500 });
        Assert.Equal(PcOptimizer.App.Resources.Strings.ResourceManager.GetString(expectedKey), outcome.Title);
        Assert.DoesNotContain("줄었습니다", outcome.ChangeText, StringComparison.Ordinal);
        if (after is null) { Assert.Equal(PcOptimizer.App.Resources.Strings.Cleanup_Unverified, outcome.AfterText); }
    }

    /// <summary>NuGet 명령은 HTTP 캐시만 지정하며 패키지 전체를 지우는 인자가 없습니다.</summary>
    [Fact]
    public void CommandsAreFixedAndDoNotUseShell()
    {
        var location = new Backend().Location;
        Assert.Equal(["nuget", "locals", "http-cache", "--clear"],
            CacheToolProcess.Arguments(location with { Tool = CacheTool.NuGetHttp }, true));
        Assert.Equal(["-I", "-m", "pip", "--cache-dir", location.CachePath, "cache", "purge"],
            CacheToolProcess.Arguments(location with { Tool = CacheTool.Pip }, true));
        Assert.Equal(["-I", "-m", "pip", "cache", "dir"], CacheToolProcess.Arguments(location with { Tool = CacheTool.Pip }, false));
        Assert.Equal(["nuget", "locals", "http-cache", "--list"], CacheToolProcess.Arguments(location with { Tool = CacheTool.NuGetHttp }, false));
        Assert.Contains("--offline", CacheToolProcess.Arguments(location, true));
    }

    /// <summary>계획 만료·대상 변경·보호·시작 실패는 실행 결과나 재검사 요청을 만들지 않습니다.</summary>
    [Theory]
    [InlineData("expired")]
    [InlineData("changed")]
    [InlineData("protected")]
    [InlineData("throw")]
    [InlineData("start")]
    [InlineData("running")]
    public async Task PreflightRefusalDoesNotClaimPartialCleanup(string refusal)
    {
        var backend = new Backend();
        var time = new ManualTimeProvider();
        var vm = new CacheToolsViewModel(new CacheCleanupService(backend, time), _ => true);
        await vm.PrepareCommand.ExecuteAsync(null);
        switch (refusal)
        {
            case "expired": time.Advance(TimeSpan.FromMinutes(6)); break;
            case "changed": backend.Location = backend.Location with { Fingerprint = "changed" }; break;
            case "protected": backend.Allowed = false; break;
            case "throw": backend.ThrowInspect = true; break;
            case "start": backend.Started = false; break;
            case "running": backend.Unavailable = "ProcessStillRunning"; break;
        }
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.False(vm.NeedsRescan);
        Assert.Null(vm.Outcome);
        Assert.Contains("실행하지 않았", vm.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("일부만", vm.Message, StringComparison.Ordinal);
        if (refusal == "running") { Assert.Contains("아직 종료되지", vm.Message, StringComparison.Ordinal); }
    }

    /// <summary>이전 실제 실행 결과는 다음 사전 검사 거절로 덮어쓰지 않습니다.</summary>
    [Fact]
    public async Task RefusalPreservesPreviousOutcome()
    {
        var backend = new Backend();
        var vm = new CacheToolsViewModel(new CacheCleanupService(backend), _ => true);
        await vm.PrepareCommand.ExecuteAsync(null);
        await vm.ClearCommand.ExecuteAsync(null);
        var previous = vm.Outcome;
        await vm.PrepareCommand.ExecuteAsync(null);
        backend.Allowed = false;
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.NotNull(previous);
        Assert.Same(previous, vm.Outcome);
    }

    /// <summary>시작된 프로세스 실패와 실행 뒤 관측 예외는 실행 이력을 보존합니다.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartedFailureOrAfterInspectionExceptionRequiresRescan(bool afterException)
    {
        var backend = new Backend { Succeeds = afterException, ThrowAfter = afterException };
        var vm = new CacheToolsViewModel(new CacheCleanupService(backend), _ => true);
        await vm.PrepareCommand.ExecuteAsync(null);
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.True(vm.NeedsRescan);
        Assert.NotNull(vm.Outcome);
        Assert.Equal(afterException ? PcOptimizer.App.Resources.Strings.Cleanup_OutcomeUnverified
            : PcOptimizer.App.Resources.Strings.Cleanup_OutcomeFailed, vm.Outcome.Title);
    }

    /// <summary>프로세스 종료 대기 차단은 도구 미설치로 변환하지 않습니다.</summary>
    [Fact]
    public async Task RunningToolIsNotMisdiagnosedAsUninstalled()
    {
        var service = new CacheCleanupService(new Backend { Unavailable = "ProcessStillRunning" });
        Assert.Equal("ProcessStillRunning", (await service.PrepareAsync(CacheTool.Npm, default)).Reason);
    }

    private sealed class Backend : ICacheToolBackend
    {
        public CacheToolLocation Location { get; set; } = new(CacheTool.Npm, @"C:\tools\node.exe", @"C:\Users\tester\cache", "hash", @"C:\tools\npm-cli.js");
        public bool Started { get; set; } = true;
        public bool ThrowInspect { get; set; }
        public bool ThrowAfter { get; set; }
        public string? Unavailable { get; set; }
        public bool Allowed { get; set; } = true;
        public bool Succeeds { get; set; } = true;
        public bool BlockAfterClear { get; set; }
        public TaskCompletionSource? ClearGate { get; set; }
        public int Clears { get; private set; }
        public long BeforeBytes { get; set; } = 500;
        public Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct) => Unavailable is { } code ? throw new CacheToolUnavailableException(code) : Task.FromResult<CacheToolLocation?>(Location);
        public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct) => ThrowInspect || (ThrowAfter && Clears > 0) ? throw new IOException("fixture") : Task.FromResult(new CacheInspection(Allowed, Clears == 0 ? BeforeBytes : 0, "Blocked"));
        public async Task<CacheToolExecution> ClearAsync(CacheToolLocation location, CancellationToken ct)
        {
            if (!Started) { return new(false, false, "StartFailed"); }
            Clears++;
            if (ClearGate is not null) { await ClearGate.Task; }
            if (BlockAfterClear) { Allowed = false; }
            return new(true, Succeeds, Succeeds ? "Completed" : "ToolFailed");
        }
    }
}
