/**
 * @file    : GraphicsCacheCleanupTests.cs
 * @author  : rudals252
 * @brief   : 그래픽 셰이더 캐시 부분 정리의 키·기본 위치·30일 기준·사용 중 파일 건너뛰기·사용자 범위 회귀(메모리 파일 시스템만 사용)
 */
using System.IO;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Files;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 NVIDIA·D3DSCache 폴더를 열지 않습니다.</summary>
public sealed class GraphicsCacheCleanupTests
{
    private static ActionCoordinator Service(FileCleanupTests.Platform fs, string key = GraphicsCacheTargets.NvidiaDx, ActionSession? session = null)
    {
        var current = session ?? FileCleanupTests.Session;
        return new(new OperationCoordinator(), () => current, [new FileCleanupAdapter(
            (_, _) => GraphicsCacheTargets.Target(key, FileCleanupTests.Root, _ => false), () => current, fs,
            definition: new(ActionId.GraphicsShaderCache, ActionScope.CurrentUser))]);
    }

    /// <summary>네 키가 관측 프로브와 같은 LocalAppData 하위 위치를 가리키고, 그 밖의 키는 거절합니다.</summary>
    [Theory]
    [InlineData(GraphicsCacheTargets.NvidiaDx, @"NVIDIA\DXCache")]
    [InlineData(GraphicsCacheTargets.NvidiaGl, @"NVIDIA\GLCache")]
    [InlineData(GraphicsCacheTargets.NvidiaNv, @"NVIDIA Corporation\NV_Cache")]
    [InlineData(GraphicsCacheTargets.Direct3D, "D3DSCache")]
    public void DefaultRootFollowsObservedLocations(string key, string relative)
    {
        var root = GraphicsCacheTargets.DefaultRoot(key, @"C:\Users\fixture", @"C:\Users\fixture\AppData\Local", p => p);
        Assert.Equal(Path.Combine(@"C:\Users\fixture\AppData\Local", relative), root);
        Assert.Contains(key, GraphicsCacheTargets.Keys);
        Assert.Throws<ActionUnavailableException>(() => GraphicsCacheTargets.DefaultRoot("nvidia-other", @"C:\Users\fixture", @"C:\Users\fixture\AppData\Local", p => p));
    }

    /// <summary>LocalAppData가 프로필 기본 위치가 아니면(옮긴 프로필·다른 사용자) 위치를 정하지 않습니다.</summary>
    [Fact]
    public void RelocatedLocalAppDataIsUnsupported()
        => Assert.Throws<ActionUnavailableException>(() => GraphicsCacheTargets.DefaultRoot(GraphicsCacheTargets.NvidiaDx, @"C:\Users\fixture", @"D:\Moved\Local", p => p));

    /// <summary>30일 안에 만들어지거나 바뀐 캐시는 남기고, 오래된 파일만 개별 삭제합니다.</summary>
    [Fact]
    public async Task DeletesOnlyStaleCacheFiles()
    {
        var fs = new FileCleanupTests.Platform();
        foreach (var name in new[] { "abc123.bin", "abc123.toc", "nested.nvph" }) { fs.Add(name); }
        var created = fs.Add("fresh.bin"); fs.Nodes[created] = fs.Nodes[created] with { Created = DateTime.UtcNow.AddDays(-29).ToFileTimeUtc() };
        var written = fs.Add("active.toc"); fs.Nodes[written] = fs.Nodes[written] with { Written = DateTime.UtcNow.AddDays(-1).ToFileTimeUtc() };
        var service = Service(fs);
        var plan = (await service.PrepareAsync(ActionId.GraphicsShaderCache, new ActionTarget.Files(GraphicsCacheTargets.NvidiaDx), false, default)).Plan!;
        Assert.Equal(300, plan.Preview.Details!.EstimatedLogicalBytes);
        Assert.Contains("30일", plan.Preview.Details.TargetLabel); Assert.Contains("부분 정리", plan.Preview.Impact);
        Assert.Equal(PcOptimizer.Core.Models.SafetyLevel.Irreversible, plan.Definition.Safety);
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Succeeded); Assert.Equal(3, fs.Deletes); Assert.Equal(3, result.Effect!.ChangedFiles);
    }

    /// <summary>드라이버가 열어 둔 파일은 독점 열기에 실패해 건너뛰고 나머지는 처리합니다.</summary>
    [Fact]
    public async Task InUseFilesAreSkippedNotFailed()
    {
        var fs = new FileCleanupTests.Platform();
        fs.Add("stale1.bin"); var locked = fs.Add("stale2.bin"); fs.Locked.Add(locked);
        var service = Service(fs, GraphicsCacheTargets.Direct3D);
        var plan = (await service.PrepareAsync(ActionId.GraphicsShaderCache, new ActionTarget.Files(GraphicsCacheTargets.Direct3D), false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Started); Assert.Equal(1, fs.Deletes); Assert.Equal("Partial", result.Code);
    }

    /// <summary>다른 관리자로 승격한 시스템 범위에서는 사용자 캐시 정리를 준비하지 않습니다.</summary>
    [Fact]
    public async Task SystemOnlyScopeIsRejected()
    {
        var fs = new FileCleanupTests.Platform(); fs.Add("stale.bin");
        var service = Service(fs, session: FileCleanupTests.Session with { Scope = ActionUserScope.SystemOnly });
        Assert.Equal("ScopeExcluded", (await service.PrepareAsync(ActionId.GraphicsShaderCache, new ActionTarget.Files(GraphicsCacheTargets.NvidiaDx), false, default)).Code);
        Assert.Equal(0, fs.Deletes);
    }
}
