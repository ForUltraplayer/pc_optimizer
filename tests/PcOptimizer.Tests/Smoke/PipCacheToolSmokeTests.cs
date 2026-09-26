/**
 * @file    : PipCacheToolSmokeTests.cs
 * @author  : rudals252
 * @brief   : 명시한 Python 설치의 pip purge를 소유한 임시 HTTP·wheel 캐시에만 실행하는 선택형 검증
 */
using System.IO;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Smoke;

/// <summary>PCOPTIMIZER_TEST_PYTHON에 테스트할 설치를 지정하여 별도로 실행합니다.</summary>
[Trait("Category", "ToolSmoke")]
public sealed class PipCacheToolSmokeTests
{
    /// <summary>pip의 HTTP·wheel 캐시만 정리하고 설치 패키지 fixture를 보존합니다.</summary>
    [Fact]
    public async Task PipPurgesPinnedFixtureCache()
    {
        var executable = Environment.GetEnvironmentVariable("PCOPTIMIZER_TEST_PYTHON");
        Assert.NotNull(executable);
        Assert.True(File.Exists(executable));
        using var tree = new TestDirectory();
        var http = tree.File(@"pip-cache\http-v2\server\entry.body", 12);
        var wheel = tree.File(@"pip-cache\wheels\entry\example-1.0-py3-none-any.whl", 12);
        var installed = tree.File(@"site-packages\example\module.py", 12);
        var result = await CacheToolProcess.RunAsync(new CacheToolLocation(CacheTool.Pip, executable, tree.PathOf("pip-cache"), "fixture"), true, default);
        Assert.True(result.Success);
        Assert.False(File.Exists(http));
        Assert.False(File.Exists(wheel));
        Assert.True(File.Exists(installed));
    }
}
