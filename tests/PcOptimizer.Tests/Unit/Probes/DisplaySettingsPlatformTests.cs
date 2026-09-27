/**
 * @file    : DisplaySettingsPlatformTests.cs
 * @author  : rudals252
 * @brief   : 물리 경로·모드 bytes·외부 변경·네이티브 플래그 관문 대역 검증
 */
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Display;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>ChangeDisplaySettingsEx 실제 호출 없이 쓰기 직전 계약을 검사합니다.</summary>
public sealed class DisplaySettingsPlatformTests
{
    private sealed class Topology : IDisplayPlatform
    {
        internal DisplayPathReading Path = new(1, 1, 1, 5, 60, 60, "monitor", "fixture", 0, "gdi", 0, "adapter", 0);
        internal bool Remote, Clone;
        internal string Adapter = "fixture GPU";
        internal bool Supported = true;
        public DisplayTopologyReading QueryActivePaths() => new(0, Clone ? [Path, Path with { TargetId = 2, MonitorDevicePath = "other" }] : [Path]);
        public DisplayModeListReading EnumerateModes(string name) => new(DisplayTrialTests.Frame(60).Mode,
            Supported ? [DisplayTrialTests.Frame(60).Mode, DisplayTrialTests.Frame(120).Mode] : []);
        public string? GetAdapterName(string name) => Adapter;
        public bool IsRemoteSession() => Remote;
    }
    private sealed class Modes : IDisplayModeApi
    {
        internal DisplayFrame Current = DisplayTrialTests.Frame(60), Registered = DisplayTrialTests.Frame(60);
        internal readonly List<(int Hz, uint Flags)> Calls = [];
        public DisplayFrame Read(string device, uint index) => index == DisplaySettingsPlatform.CurrentSettings ? Current : Registered;
        public int Change(string device, DisplayFrame current, int hz, uint flags) { Calls.Add((hz, flags)); return 0; }
    }
    /// <summary>시험·임시·영구·프로필만 복구하는 플래그를 구분합니다.</summary>
    [Fact] public void NativeFlagsAreRestrictedAndSeparate()
    {
        var modes = new Modes(); var p = new DisplaySettingsPlatform(new Topology(), modes, () => true); var change = p.Capture("monitor", 120);
        var expected = p.Observe(change.Identity);
        p.Test(change.Identity, change.Desired);
        p.Apply(change.Identity, change.Desired, false, expected, () => { });
        p.Apply(change.Identity, change.Desired, true, expected, () => { });
        p.SaveProfileOnly(change.Identity, change.RegisteredBefore, expected, () => { });
        Assert.Equal(new[] { (120, 2u), (120, 0u), (120, 1u), (60, 0x10000001u) }, modes.Calls);
    }
    /// <summary>원격·복제·간접·이름 실패·가상 어댑터는 준비 단계에서 거절됩니다.</summary>
    [Theory] [InlineData("remote")] [InlineData("clone")] [InlineData("indirect")] [InlineData("name")] [InlineData("virtual")] [InlineData("noninteractive")]
    public void AmbiguousOutputNeverReachesChange(string kind)
    {
        var t = new Topology(); var modes = new Modes();
        if (kind == "remote") { t.Remote = true; }
        if (kind == "clone") { t.Clone = true; }
        if (kind == "indirect") { t.Path = t.Path with { OutputTechnology = 17 }; }
        if (kind == "name") { t.Path = t.Path with { TargetNameError = 5 }; }
        if (kind == "virtual") { t.Adapter = "Virtual Display"; }
        Assert.Throws<ActionUnavailableException>(() => new DisplaySettingsPlatform(t, modes, () => kind != "noninteractive").Capture("monitor", 120)); Assert.Empty(modes.Calls);
    }
    /// <summary>재식별·지원 목록·현재 값·형태·원문 손상은 쓰기 전에 다시 검사합니다.</summary>
    [Theory] [InlineData("route")] [InlineData("unsupported")] [InlineData("current")] [InlineData("shape")] [InlineData("bytes")]
    public void RevalidationRejectsChangesBeforeWriting(string kind)
    {
        var t = new Topology(); var modes = new Modes(); var p = new DisplaySettingsPlatform(t, modes, () => true); var change = p.Capture("monitor", 120);
        var expected = p.Observe(change.Identity); var desired = change.Desired;
        if (kind == "route") { t.Path = t.Path with { TargetId = 2 }; }
        if (kind == "unsupported") { t.Supported = false; }
        if (kind == "current") { modes.Current = DisplayTrialTests.Frame(144); }
        if (kind == "shape") { desired = desired with { Mode = desired.Mode with { Width = 2560 } }; }
        if (kind == "bytes") { desired = desired with { Native = new byte[220] }; }
        Assert.Throws<ActionUnavailableException>(() => p.Apply(change.Identity, desired, false, expected, () => Assert.Fail("쓰기 전 관문 누락"))); Assert.Empty(modes.Calls);
    }
}
