/**
 * @file    : DeliveryCacheSmokeTests.cs
 * @author  : rudals252
 * @brief   : 배달 최적화 실제 제공자 조회·메서드 형식만 확인하며 삭제는 호출하지 않음
 */
using PcOptimizer.Probes.Actions.SystemCleanup;

namespace PcOptimizer.Tests.Smoke;

/// <summary>실제 캐시를 읽기만 합니다.</summary>
[Trait("Category", "Smoke")]
public sealed class DeliveryCacheSmokeTests
{
    /// <summary>로컬 제공자 속성 형식과 pin 제외 인자, ID의 데이터 취급을 검증합니다.</summary>
    [Fact]
    public void ReadAndPrepareParametersWithoutInvokingDelete()
    {
        var files = new DeliveryCachePlatform().Read(default);
        Assert.All(files, f => { Assert.False(string.IsNullOrWhiteSpace(f.Id)); Assert.True(f.Bytes >= 0); });
        using var type = DeliveryCachePlatform.OpenClass();
        using var input = DeliveryCachePlatform.Parameters(type, "fixture;not-a-command");
        Assert.Equal("fixture;not-a-command", input["fileId"]);
        Assert.Equal(false, input["deletePinned"]);
    }
}
