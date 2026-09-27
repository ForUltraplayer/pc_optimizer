/**
 * @file    : PowerActionSmokeTests.cs
 * @author  : rudals252
 * @brief   : 설치 GUID와 활성 계획·전원 상태만 조회하며 실제 전원 설정 쓰기는 금지
 */
using PcOptimizer.Probes.Actions.Power;

namespace PcOptimizer.Tests.Smoke;

/// <summary>실제 Windows 전원 조회 ABI를 검증합니다. Set은 호출하지 않습니다.</summary>
[Trait("Category", "Smoke")]
public sealed class PowerActionSmokeTests
{
    /// <summary>설치 목록의 GUID가 유효하며 현재 계획이 목록에 존재해야 합니다.</summary>
    [Fact]
    public void InstalledAndCurrentSchemesCanBeReadWithoutWriting()
    {
        var platform = new PowerSettingsPlatform();
        var values = platform.Installed();
        Assert.NotEmpty(values); Assert.Contains(platform.Current(), values);
        Assert.DoesNotContain(Guid.Empty, values);
        Assert.Contains(platform.AcStatus(), new byte[] { 0, 1, 255 });
    }
}
