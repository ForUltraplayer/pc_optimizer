/**
 * @file    : AdvancedRegistrySmokeTests.cs
 * @author  : rudals252
 * @brief   : 실제 GPU 설정과 분리된 GUID 소유 HKCU 키에서 DWORD 변경·삭제·충돌 검증
 */
using Microsoft.Win32;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Advanced;

namespace PcOptimizer.Tests.Smoke;

/// <summary>제품 레지스트리·재부팅·GPU API를 호출하지 않습니다.</summary>
[Trait("Category", "Smoke")]
public sealed class AdvancedRegistrySmokeTests
{
    /// <summary>실제 API가 지정 값 하나만 변경하고 다른 값과 충돌 값은 보존합니다.</summary>
    [Fact]
    public void OwnedKeyRoundTripAndExternalChange()
    {
        var fixture = Guid.NewGuid(); var parent = @"Software\PcOptimizer.Tests\" + fixture.ToString("N");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using var key = root.CreateSubKey(parent + @"\Advanced");
            key.SetValue("Unrelated", "preserved");
            var platform = new AdvancedRegistryPlatform(fixture); var session = SystemActionSession.Read(ActionUserScope.Full);
            Assert.True(session.IsKnown);
            Assert.False(platform.Read(AdvancedOption.Mpo, session).Exists);
            Assert.True(platform.CompareExchange(AdvancedOption.Mpo, AdvancedOptions.Absent, AdvancedOptions.Dword(5), session, () => { }));
            Assert.Equal(5, key.GetValue("OverlayTestMode"));
            key.SetValue("OverlayTestMode", 7, RegistryValueKind.DWord);
            Assert.False(platform.CompareExchange(AdvancedOption.Mpo, AdvancedOptions.Dword(5), AdvancedOptions.Absent, session, () => { }));
            Assert.Equal(7, key.GetValue("OverlayTestMode"));
            key.SetValue("OverlayTestMode", 5, RegistryValueKind.DWord);
            Assert.True(platform.CompareExchange(AdvancedOption.Mpo, AdvancedOptions.Dword(5), AdvancedOptions.Absent, session, () => { }));
            Assert.Null(key.GetValue("OverlayTestMode")); Assert.Equal("preserved", key.GetValue("Unrelated"));
            key.SetValue("OverlayTestMode", "wrong-type");
            Assert.Throws<ActionUnavailableException>(() => platform.Read(AdvancedOption.Mpo, session));
        }
        finally { root.DeleteSubKeyTree(parent, false); }
    }
}
