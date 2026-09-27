/**
 * @file    : GpuActionTests.cs
 * @author  : rudals252
 * @brief   : GPU 쓰기 대역으로 원본·상속·드라이버 변경·실패·사용자 범위와 네이티브 ABI 검증
 */
using System.Runtime.InteropServices;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Advanced;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 GPU 쓰기·드라이버 설정 저장은 수행하지 않습니다.</summary>
public sealed class GpuActionTests
{
    private static readonly ActionSession Session = ActionCenterTests.Session;
    internal sealed class Platform(GpuFeature feature) : IGpuPlatform
    {
        internal GpuSettingState State = feature switch
        {
            GpuFeature.NvidiaRebar => new("fixture-driver", NvidiaRebarPlatform.Legacy, 1, true, Predefined: 1, PredefinedValid: 1, PredefinedValue: 1),
            GpuFeature.NvidiaVideo => new("fixture-driver", 0x1D, 5, true, PredefinedValue: 5),
            _ => new("fixture-driver", 0, 1, true),
        };
        internal int Writes;
        internal bool FailWrite, FailVerify, Missing;
        internal Action? OnWrite;
        public IReadOnlyList<GpuOptionTarget> Enumerate(CancellationToken ct) => [new(feature, "fixture", "시험 대상", "변경 영향", [new(0, "끄기"), new(1, "켜기")])];
        public GpuSettingState Read(string key) => Missing ? throw new ActionUnavailableException("GpuTargetChanged") : State;
        public GpuSettingState Desired(string key, GpuSettingState before, int choice) => feature switch
        {
            GpuFeature.NvidiaRebar => new NvidiaRebarPlatform().Desired(key, before, choice),
            GpuFeature.NvidiaVideo => new NvidiaVideoPlatform().Desired(key, before, choice),
            _ => new AmdVideoPlatform().Desired(key, before, choice),
        };
        public bool CompareExchange(string key, GpuSettingState expected, GpuSettingState desired, Action beforeCommit)
        {
            if (State != expected) { return false; }
            beforeCommit(); OnWrite?.Invoke();
            if (FailWrite) { throw new ActionUnavailableException("GpuApiRejected"); }
            Writes++; if (!FailVerify) { State = desired; } return true;
        }
    }
    private static (GpuActionAdapter, RestoreCoordinator, ActionCenterTests.Store) Setup(GpuFeature feature, Platform platform, Func<ActionSession>? session = null)
    {
        session ??= () => Session; var adapter = new GpuActionAdapter(feature, session, platform); adapter.Discover(default);
        var store = new ActionCenterTests.Store(); return (adapter, new(new OperationCoordinator(), store, session, [adapter]), store);
    }
    /// <summary>Pending 기록이 GPU 쓰기보다 먼저 저장되며 세 기능 모두 원본으로 복원합니다.</summary>
    [Theory]
    [InlineData(GpuFeature.NvidiaRebar)][InlineData(GpuFeature.NvidiaVideo)][InlineData(GpuFeature.AmdVideo)]
    public async Task ApplyAndRestorePreserveExactState(GpuFeature feature)
    {
        var platform = new Platform(feature); var original = platform.State;
        var (_, service, store) = Setup(feature, platform);
        platform.OnWrite = () => Assert.True(Assert.Single(store.Catalog.Records).State is RollbackState.Pending or RollbackState.Restoring);
        var plan = (await service.PrepareApplyAsync(GpuOptions.Action(feature), new ActionTarget.Gpu(feature, "fixture", 0), default)).Plan!;
        Assert.Equal(0, platform.Writes); Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        var record = Assert.Single(store.Catalog.Records);
        Assert.Equal(original, GpuActionAdapter.Decode(RollbackCodec.Decode(RollbackCodec.Encode(record, Session.Sid), Session.Sid).Before));
        var restore = (await service.PrepareRestoreAsync(record.ActionId, record.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(restore.Id, default)).Succeeded);
        Assert.Equal(original, platform.State); Assert.Equal(2, platform.Writes);
    }
    /// <summary>명시 값이 아니라 기본·상속 상태도 원본의 일부입니다.</summary>
    [Theory]
    [InlineData(0u, 1u)][InlineData(1u, 0u)][InlineData(2u, 0u)][InlineData(3u, 0u)]
    public async Task RebarRestoresSettingOrigin(uint location, uint predefined)
    {
        var platform = new Platform(GpuFeature.NvidiaRebar); platform.State = platform.State with { Location = location, Predefined = predefined };
        var original = platform.State; var (_, service, _) = Setup(GpuFeature.NvidiaRebar, platform);
        var plan = (await service.PrepareApplyAsync(ActionId.NvidiaRebar, new ActionTarget.Gpu(GpuFeature.NvidiaRebar, "fixture", 0), default)).Plan!;
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.Equal(0u, platform.State.Location); Assert.Equal(0u, platform.State.Predefined);
        var restore = (await service.PrepareRestoreAsync(ActionId.NvidiaRebar, plan.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(restore.Id, default)).Succeeded); Assert.Equal(original, platform.State);
    }
    /// <summary>확인 뒤 드라이버·대상·설정·세션 변화와 취소는 쓰기 전에 거절됩니다.</summary>
    [Theory]
    [InlineData("driver")][InlineData("value")][InlineData("missing")][InlineData("session")][InlineData("cancel")]
    public async Task ChangedConditionsRejectBeforeWrite(string change)
    {
        var platform = new Platform(GpuFeature.AmdVideo); var session = Session;
        var (_, service, store) = Setup(GpuFeature.AmdVideo, platform, () => session);
        var plan = (await service.PrepareApplyAsync(ActionId.AmdVideo, new ActionTarget.Gpu(GpuFeature.AmdVideo, "fixture", 0), default)).Plan!;
        if (change == "driver") { platform.State = platform.State with { Stamp = "new-driver" }; }
        if (change == "value") { platform.State = platform.State with { Value = 0, Enabled = false }; }
        if (change == "missing") { platform.Missing = true; }
        if (change == "session") { session = session with { Scope = ActionUserScope.SystemOnly }; }
        var result = await service.ExecuteAsync(plan.Id, new CancellationToken(change == "cancel"));
        Assert.False(result.Succeeded); Assert.False(result.Started); Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }
    /// <summary>복원도 외부 변경·드라이버 교체를 덮어쓰지 않습니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RestoreRejectsExternalChange(bool driver)
    {
        var platform = new Platform(GpuFeature.NvidiaVideo); var (_, service, _) = Setup(GpuFeature.NvidiaVideo, platform);
        var plan = (await service.PrepareApplyAsync(ActionId.NvidiaVideo, new ActionTarget.Gpu(GpuFeature.NvidiaVideo, "fixture", 0), default)).Plan!;
        await service.ExecuteAsync(plan.Id, default);
        platform.State = driver ? platform.State with { Stamp = "new-driver" } : platform.State with { Value = 2 };
        var restore = (await service.PrepareRestoreAsync(ActionId.NvidiaVideo, plan.Id, default)).Plan!;
        Assert.Equal("CurrentValueChanged", (await service.ExecuteAsync(restore.Id, default)).Code); Assert.Equal(1, platform.Writes);
    }
    /// <summary>실패 또는 성공 응답 후 읽기 불일치는 Pending을 보존합니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task FailureNeverClaimsApplied(bool verification)
    {
        var platform = new Platform(GpuFeature.AmdVideo) { FailWrite = !verification, FailVerify = verification };
        var (_, service, store) = Setup(GpuFeature.AmdVideo, platform);
        var plan = (await service.PrepareApplyAsync(ActionId.AmdVideo, new ActionTarget.Gpu(GpuFeature.AmdVideo, "fixture", 0), default)).Plan!;
        Assert.False((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.Equal(RollbackState.Pending, Assert.Single(store.Catalog.Records).State);
    }
    /// <summary>SystemOnly·목록 밖 대상/값·잘못된 ID를 거절합니다.</summary>
    [Theory]
    [InlineData("scope")][InlineData("key")][InlineData("value")][InlineData("id")]
    public async Task UnregisteredTargetsNeverWrite(string kind)
    {
        var platform = new Platform(GpuFeature.AmdVideo);
        var (_, service, _) = Setup(GpuFeature.AmdVideo, platform, () => kind == "scope" ? Session with { Scope = ActionUserScope.SystemOnly } : Session);
        var result = await service.PrepareApplyAsync(kind == "id" ? ActionId.NvidiaVideo : ActionId.AmdVideo,
            new ActionTarget.Gpu(GpuFeature.AmdVideo, kind == "key" ? "unknown" : "fixture", kind == "value" ? 99 : 0), default);
        Assert.Null(result.Plan); Assert.Equal(0, platform.Writes);
    }
    /// <summary>JSON 누락·중복·알 수 없는 필드·형식 주입을 거절합니다.</summary>
    [Theory]
    [InlineData("{}")][InlineData("null")][InlineData("{\"Stamp\":\"x\",\"Stamp\":\"y\"}")]
    public void MalformedOriginalIsRejected(string json) => Assert.Throws<ActionUnavailableException>(() => GpuActionAdapter.Decode(new(true, 0, System.Text.Encoding.UTF8.GetBytes(json))));
    /// <summary>끄기는 품질을 보존하며 최신 ReBAR의 Auto=1/On=2를 뒤바꾸지 않습니다.</summary>
    [Fact]
    public void VideoOffPreservesQualityAndRebarAutoIsDistinct()
    {
        var video = new NvidiaVideoPlatform(); var state = new GpuSettingState("fixture", 0x1D, 4, true, PredefinedValue: 5);
        var off = video.Desired("fixture", state, 0); Assert.False(off.Enabled); Assert.Equal(4, off.Value);
        Assert.Equal("자동", NvidiaRebarPlatform.Choices(NvidiaRebarPlatform.Modern).Single(c => c.Value == 1).Label);
        Assert.Equal("켜기", NvidiaRebarPlatform.Choices(NvidiaRebarPlatform.Modern).Single(c => c.Value == 2).Label);
    }
    /// <summary>잘못된 Enabled 오프셋은 실제 setter 없이 네이티브 메모리에서 재현합니다.</summary>
    [Fact]
    public void NativeLayoutsKeepEnableSeparateFromFlagsAndQuality()
    {
        Assert.Equal(12320, Marshal.SizeOf<NvidiaApi.Setting>()); Assert.Equal(4116, Marshal.SizeOf<NvidiaApi.Profile>());
        Assert.Equal(128, Marshal.SizeOf<NvidiaApi.VideoGet>()); Assert.Equal(64, Marshal.SizeOf<NvidiaApi.VideoSet>());
        Assert.Equal(44, Marshal.OffsetOf<NvidiaApi.VideoGet>(nameof(NvidiaApi.VideoGet.Value)).ToInt32());
        var ptr = Marshal.AllocHGlobal(64);
        try
        {
            var value = NvidiaVideoPlatform.WriteValue(new("fixture", 0x1D, 4, true, PredefinedValue: 5));
            Marshal.StructureToPtr(value, ptr, false);
            Assert.Equal(0, Marshal.ReadInt32(ptr, 12)); Assert.Equal(1, Marshal.ReadInt32(ptr, 16)); Assert.Equal(4, Marshal.ReadInt32(ptr, 20));
            value = NvidiaVideoPlatform.WriteValue(new("fixture", 0x1D, 4, false, PredefinedValue: 5));
            Marshal.StructureToPtr(value, ptr, false); Assert.Equal(0, Marshal.ReadInt32(ptr, 16)); Assert.Equal(4, Marshal.ReadInt32(ptr, 20));
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}
