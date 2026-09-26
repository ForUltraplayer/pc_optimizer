/**
 * @file    : CacheCleanupTests.cs
 * @author  : rudals252
 * @brief   : 자동 정리의 승인·만료·일회성·변경·보호·실패·후속 관측 경계 검증
 */
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
        Assert.Equal("Busy", (await service.ExecuteAsync(second.Id, default)).Code);
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
    }

    /// <summary>NuGet 명령은 HTTP 캐시만 지정하며 패키지 전체를 지우는 인자가 없습니다.</summary>
    [Fact]
    public void CommandsAreFixedAndDoNotUseShell()
    {
        var location = new Backend().Location;
        Assert.Equal(["nuget", "locals", "http-cache", "--clear", "--force-english-output"],
            CacheToolProcess.Arguments(location with { Tool = CacheTool.NuGetHttp }, true));
        Assert.Contains("--cache-dir", CacheToolProcess.Arguments(location with { Tool = CacheTool.Pip }, true));
        Assert.Contains("--offline", CacheToolProcess.Arguments(location, true));
    }

    private sealed class Backend : ICacheToolBackend
    {
        public CacheToolLocation Location { get; set; } = new(CacheTool.Npm, @"C:\tools\node.exe", @"C:\Users\tester\cache", "hash", @"C:\tools\npm-cli.js");
        public bool Allowed { get; set; } = true;
        public bool Succeeds { get; set; } = true;
        public bool BlockAfterClear { get; set; }
        public TaskCompletionSource? ClearGate { get; set; }
        public int Clears { get; private set; }
        public Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct) => Task.FromResult<CacheToolLocation?>(Location);
        public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct) => Task.FromResult(new CacheInspection(Allowed, Clears == 0 ? 500 : 0, "Blocked"));
        public async Task<bool> ClearAsync(CacheToolLocation location, CancellationToken ct)
        {
            Clears++;
            if (ClearGate is not null) { await ClearGate.Task; }
            if (BlockAfterClear) { Allowed = false; }
            return Succeeds;
        }
    }
}
