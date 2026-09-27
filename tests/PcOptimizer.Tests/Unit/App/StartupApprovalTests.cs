/**
 * @file    : StartupApprovalTests.cs
 * @author  : rudals252
 * @brief   : 작업 관리자 시작 상태(StartupApproved) 값 해석·토글·복원·선택 대상·복구 기록 코덱 검증(대역 레지스트리만 사용)
 */
using System.Buffers.Binary;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Startup;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 StartupApproved 키는 읽거나 쓰지 않습니다.</summary>
public sealed class StartupApprovalTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActionSession Session = ActionCenterTests.Session;
    private static RollbackValue Bytes(params byte[] data) => new(true, StartupApproval.REG_BINARY, data);
    private static RollbackValue Enabled() => StartupApproval.Enabled();
    private static RollbackValue Disabled() => StartupApproval.Disabled(FixedNow);
    private static RestoreCoordinator Service(StartupActionTests.Platform platform, ActionCenterTests.Store store, bool machine = false, Func<ActionSession>? session = null)
    {
        var current = session ?? (() => Session);
        return new(new OperationCoordinator(), store, current, [new StartupRunActionAdapter(_ => platform, current, machine, false, true, new FixedTime())]);
    }

    /// <summary>이 PC(Windows 11 26200)에서 관측한 두 형식만 지원하고 다른 길이·형식·표식은 거절합니다.</summary>
    [Fact]
    public void ValidValueAcceptsObservedFormatsOnly()
    {
        Assert.True(StartupApproval.ValidValue(Bytes(0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.True(StartupApproval.ValidValue(Bytes(0x03, 0, 0, 0, 0xAD, 0x65, 0x57, 0xAB, 0xE4, 0x76, 0xDA, 0x01)));
        Assert.False(StartupApproval.ValidValue(Bytes(0x02, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0)));
        Assert.False(StartupApproval.ValidValue(Bytes(0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.False(StartupApproval.ValidValue(Bytes(0x02, 0, 0, 0)));
        Assert.False(StartupApproval.ValidValue(new(true, 1, new byte[12])));
        Assert.False(StartupApproval.ValidValue(StartupRegistration.Absent));
    }

    /// <summary>값 없음은 활성, 0x02는 활성, 0x03은 비활성이며 토글은 반대 상태의 값을 만듭니다.</summary>
    [Fact]
    public void StateAndToggleFollowTaskManagerSemantics()
    {
        Assert.Equal(StartupApprovedState.Enabled, StartupApproval.StateOf(StartupRegistration.Absent));
        Assert.Equal(StartupApprovedState.Enabled, StartupApproval.StateOf(Enabled()));
        Assert.Equal(StartupApprovedState.Disabled, StartupApproval.StateOf(Disabled()));
        Assert.Equal(StartupApprovedState.Unknown, StartupApproval.StateOf(Bytes(0x09, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        var disabled = StartupApproval.Toggled(StartupRegistration.Absent, FixedNow)!;
        Assert.Equal(StartupApproval.DISABLED_MARKER, BinaryPrimitives.ReadUInt32LittleEndian(disabled.Data));
        Assert.Equal(FixedNow.UtcDateTime.ToFileTimeUtc(), BinaryPrimitives.ReadInt64LittleEndian(disabled.Data.AsSpan(sizeof(uint))));
        Assert.True(StartupApproval.Toggled(disabled, FixedNow)!.SameAs(Enabled()));
        Assert.Null(StartupApproval.Toggled(Bytes(0x09, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), FixedNow));
    }

    /// <summary>값 없음(활성)을 "사용 안 함"으로 바꾸고, 되돌리기는 값을 다시 없앱니다. 등록 해제 실행기와 키·조치 ID가 다릅니다.</summary>
    [Fact]
    public async Task ToggleDisablesAndRestoreRemovesValue()
    {
        var platform = new StartupActionTests.Platform { Current = StartupRegistration.Absent }; var store = new ActionCenterTests.Store();
        var service = Service(platform, store);
        var target = new ActionTarget.Startup(StartupRegistration.ApprovalUser, "Fixture App");
        var plan = (await service.PrepareApplyAsync(ActionId.StartupApproval, target, default)).Plan!;
        Assert.Contains("사용 안 함", plan.Preview.Summary); Assert.Equal(0, platform.Writes);
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.Equal(StartupApprovedState.Disabled, StartupApproval.StateOf(platform.Current));
        var record = Assert.Single(store.Catalog.Records);
        Assert.Equal(ActionId.StartupApproval, record.ActionId); Assert.Equal(StartupRegistration.ApprovalUser, StartupRegistration.SourceOfKey(record.TargetKey));
        Assert.Equal("Fixture App", StartupRegistration.Name(record.TargetKey)); Assert.False(record.Before.Exists);
        var decoded = RollbackCodec.Decode(RollbackCodec.Encode(record, Session.Sid), Session.Sid);
        Assert.True(decoded.Applied.SameAs(record.Applied));
        var undo = (await service.PrepareRestoreAsync(ActionId.StartupApproval, record.Id, default)).Plan!;
        Assert.Contains("작업 관리자 시작 상태 되돌리기", undo.Preview.Summary);
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded);
        Assert.False(platform.Current.Exists); Assert.Equal(2, platform.Writes);
    }

    /// <summary>"사용 안 함"이던 항목은 "사용"으로 바꾸고 원래 바이트(비활성화 시각 포함)를 그대로 복원합니다.</summary>
    [Fact]
    public async Task ToggleEnablesAndRestorePreservesOriginalBytes()
    {
        var original = Bytes(0x03, 0, 0, 0, 0x16, 0x55, 0xE5, 0x81, 0xB7, 0x24, 0xDD, 0x01);
        var platform = new StartupActionTests.Platform { Current = original }; var store = new ActionCenterTests.Store();
        var service = Service(platform, store, machine: true, session: () => Session with { Scope = ActionUserScope.SystemOnly });
        var target = new ActionTarget.Startup(StartupRegistration.ApprovalMachine32, "Fixture App");
        var plan = (await service.PrepareApplyAsync(ActionId.MachineStartupApproval, target, default)).Plan!;
        Assert.Contains("'사용'", plan.Preview.Summary); Assert.Contains("모든 사용자", plan.Preview.Impact);
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.True(platform.Current.SameAs(Enabled()));
        var record = Assert.Single(store.Catalog.Records);
        Assert.Equal(ActionScope.System, record.Scope);
        var undo = (await service.PrepareRestoreAsync(ActionId.MachineStartupApproval, record.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded);
        Assert.True(platform.Current.SameAs(original));
    }

    /// <summary>지원하지 않는 형식·다른 출처·시스템 범위의 사용자 항목은 계획을 발급하지 않고 쓰지 않습니다.</summary>
    [Fact]
    public async Task UnsupportedValueSourceOrScopeIsRejected()
    {
        var platform = new StartupActionTests.Platform { Current = Bytes(0x02, 0, 0, 0, 0, 0) }; var store = new ActionCenterTests.Store();
        var service = Service(platform, store);
        Assert.Null((await service.PrepareApplyAsync(ActionId.StartupApproval, new ActionTarget.Startup(StartupRegistration.ApprovalUser, "Fixture App"), default)).Plan);
        platform.Current = Enabled();
        Assert.Null((await service.PrepareApplyAsync(ActionId.StartupApproval, new ActionTarget.Startup(StartupRegistration.Source, "Fixture App"), default)).Plan);
        var systemOnly = Service(platform, store, session: () => Session with { Scope = ActionUserScope.SystemOnly });
        Assert.Null((await systemOnly.PrepareApplyAsync(ActionId.StartupApproval, new ActionTarget.Startup(StartupRegistration.ApprovalUser, "Fixture App"), default)).Plan);
        Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }

    /// <summary>미리보기 뒤 작업 관리자가 값을 바꾸면 쓰지 않습니다.</summary>
    [Fact]
    public async Task ExternalChangeAfterPreviewNeverWrites()
    {
        var platform = new StartupActionTests.Platform { Current = Enabled() }; var store = new ActionCenterTests.Store();
        var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.StartupApproval, new ActionTarget.Startup(StartupRegistration.ApprovalUser, "Fixture App"), default)).Plan!;
        platform.Current = Disabled();
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Succeeded); Assert.False(result.Started); Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }

    /// <summary>StartupApproved 조회가 "찾음(이진)" 또는 "없음"인 항목만 작업 관리자 상태 대상으로 추가합니다.</summary>
    [Theory]
    [InlineData("hkcu.run", "found", "Binary", "hkcu.approved")]
    [InlineData("hkcu.run", "missing", null, "hkcu.approved")]
    [InlineData("hklm32.run", "found", "Binary", "hklm32.approved")]
    [InlineData("folder.user", "missing", null, "hkcu.approvedFolder")]
    [InlineData("hkcu.run", "unreadable", null, null)]
    [InlineData("hkcu.run", "found", "String", null)]
    [InlineData("hkcu.run", "notTracked", null, null)]
    public void SelectionAddsApprovalTargetsOnlyForSupportedLookups(string source, string lookup, string? kind, string? expectedApproval)
    {
        var now = DateTimeOffset.UtcNow;
        Measurement Field(string field, string value) => new(StartupItemsProbeContract.ItemMeasurementName(0, field), new TextValue(value), null, "fixture", now, MeasurementQuality.Observed);
        var folder = source.StartsWith("folder", StringComparison.Ordinal);
        var fields = new List<Measurement>
        {
            new(StartupItemsProbeContract.ITEM_COUNT, new IntegerValue(1), null, "fixture", now, MeasurementQuality.Observed),
            Field(StartupItemsProbeContract.FIELD_NAME, folder ? "Fixture App.lnk" : "Fixture App"), Field(StartupItemsProbeContract.FIELD_SOURCE, source),
            Field(StartupItemsProbeContract.FIELD_REGISTRY_VIEW, source == "hklm32.run" ? "Registry32" : "Registry64"), Field(StartupItemsProbeContract.FIELD_VALUE_KIND, "String"),
            Field(StartupItemsProbeContract.FIELD_APPROVED_LOOKUP, lookup),
        };
        if (kind is not null) { fields.Add(Field(StartupItemsProbeContract.FIELD_APPROVED_KIND, kind)); }
        var snapshot = new ScanSnapshot(Guid.NewGuid(), [new(StartupItemsProbeContract.PROBE_ID, ProbeStatus.Success, fields, [], now, TimeSpan.Zero, new("fixture", true))]);
        var targets = StartupSelection.Targets(snapshot);
        var approval = targets.Where(t => StartupRegistration.IsApproval(t.SourceKey)).ToArray();
        Assert.Single(targets, t => !StartupRegistration.IsApproval(t.SourceKey));
        if (expectedApproval is null) { Assert.Empty(approval); }
        else { Assert.Equal(expectedApproval, Assert.Single(approval).SourceKey); Assert.Equal(StartupRegistration.ActionFor(expectedApproval), expectedApproval.StartsWith("hklm", StringComparison.Ordinal) ? ActionId.MachineStartupApproval : ActionId.StartupApproval); }
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }
}
