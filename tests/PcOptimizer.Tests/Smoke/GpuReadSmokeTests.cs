/**
 * @file    : GpuReadSmokeTests.cs
 * @author  : rudals252
 * @brief   : 설치 GPU의 제어 인터페이스를 조회만 하며 설정 쓰기는 호출하지 않음
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Advanced;
using Xunit.Abstractions;
namespace PcOptimizer.Tests.Smoke;

/// <summary>미설치 벤더의 결과도 명시하며 변경 API를 호출하지 않습니다.</summary>
[Trait("Category", "Smoke")]
public sealed class GpuReadSmokeTests(ITestOutputHelper output)
{
    /// <summary>조회 ABI를 실제 드라이버에서 확인합니다.</summary>
    [Theory]
    [InlineData(GpuFeature.NvidiaRebar)][InlineData(GpuFeature.NvidiaVideo)][InlineData(GpuFeature.AmdVideo)]
    public void ReadOnlyInventory(GpuFeature feature)
    {
        var adapter = new GpuActionAdapter(feature, () => SystemActionSession.Read(ActionUserScope.Full));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var result = adapter.Discover(deadline.Token);
        output.WriteLine($"{feature}: targets={result.Targets.Count}; {result.Status}");
        Assert.NotEmpty(result.Status);
        if (result.Targets.FirstOrDefault() is { } target)
        {
            var state = adapter.Observe(target.Key);
            output.WriteLine($"state={state.Value}, enabled={state.Enabled}, setting={state.SettingId:X8}, location={state.Location}, predefined={state.Predefined}, max={state.PredefinedValue}, stamp={state.Stamp}");
            Assert.NotEmpty(state.Stamp);
        }
    }
}
