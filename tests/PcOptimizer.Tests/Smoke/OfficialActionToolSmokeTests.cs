/**
 * @file    : OfficialActionToolSmokeTests.cs
 * @author  : rudals252
 * @brief   : npm·pip·NuGet 명령을 소유 임시 경로에 고정하고 공통 실행기의 시작 기록까지 검증
 */
using System.IO;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Smoke;

/// <summary>명시한 도구 실행 파일만 사용하며 실제 사용자 캐시는 대상으로 삼지 않습니다.</summary>
[Collection("OfficialCacheTools")]
[Trait("Category", "ToolSmoke")]
public sealed class OfficialActionToolSmokeTests
{
    /// <summary>공통 계획→실제 프로세스→관측을 거치며 설치 패키지 fixture를 보존합니다.</summary>
    [Theory]
    [InlineData(CacheTool.Npm, "PCOPTIMIZER_TEST_NODE", "_cacache/content-v2/owned")]
    [InlineData(CacheTool.Pip, "PCOPTIMIZER_TEST_PYTHON", "http-v2/owned.body")]
    [InlineData(CacheTool.NuGetHttp, "PCOPTIMIZER_TEST_DOTNET", "source/owned.dat")]
    public async Task OfficialCommandsOnlyCleanOwnedCache(CacheTool tool, string variable, string entry)
    {
        var executable = Environment.GetEnvironmentVariable(variable);
        Assert.NotNull(executable); Assert.True(File.Exists(executable));
        using var tree = new TestDirectory();
        var cached = tree.File("cache/" + entry, 20); var keep = tree.File("installed/keep.dat", 20);
        var script = tool == CacheTool.Npm ? Path.Combine(Path.GetDirectoryName(executable)!, "node_modules", "npm", "bin", "npm-cli.js") : null;
        if (script is not null) { Assert.True(File.Exists(script)); }
        var backend = new FixtureBackend(new(tool, executable, tree.PathOf("cache"), "owned-fixture", script));
        var session = new ActionSession("owned-fixture", 1, ActionUserScope.Full);
        var gate = new OperationCoordinator();
        var service = new ActionCoordinator(gate, () => session, [new OfficialCacheActionAdapter(backend, () => session)]);
        var plan = (await service.PrepareAsync(ActionId.OfficialCache, new ActionTarget.OfficialTool((OfficialCacheTool)tool), false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Started); Assert.True(result.Succeeded, result.Code);
        Assert.False(File.Exists(cached)); Assert.True(File.Exists(keep)); Assert.NotNull(result.Effect!.AfterLogicalBytes);
        await CacheToolProcess.WaitForDrainAsync(NullAppLogger.Instance).WaitAsync(TimeSpan.FromSeconds(10));
    }
    private sealed class FixtureBackend(CacheToolLocation location) : ICacheToolActionBackend
    {
        public Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct) => Task.FromResult<CacheToolLocation?>(location);
        public Task<CacheInspection> InspectAsync(CacheToolLocation selected, CancellationToken ct)
        {
            Assert.Equal(location, selected);
            long bytes = Directory.Exists(location.CachePath) ? Directory.EnumerateFiles(location.CachePath, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length) : 0;
            return Task.FromResult(new CacheInspection(true, bytes, null));
        }
        public Task<CacheToolExecution> ClearAsync(CacheToolLocation selected, CancellationToken ct) => throw new NotSupportedException();
        public async Task<CacheToolExecution> ClearAsync(CacheToolLocation selected, IActionExecution execution, CancellationToken ct)
        {
            Assert.Equal(location, selected);
            var result = await CacheToolProcess.RunAsync(location, true, ct, execution: execution);
            return new(result.Started, result.Success, result.Code);
        }
        public Task WaitForDrainAsync() => CacheToolProcess.WaitForDrainAsync(NullAppLogger.Instance);
    }
}
