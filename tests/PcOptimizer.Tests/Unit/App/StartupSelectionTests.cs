/**
 * @file    : StartupSelectionTests.cs
 * @author  : rudals252
 * @brief   : 지원 등록 선택·범위 변경 시 목록 제거·선택 후 별도 확인 검증
 */
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Startup;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 앱 선택 모델과 스냅샷을 사용합니다.</summary>
public sealed class StartupSelectionTests
{
    internal static ScanSnapshot Snapshot(string source = "hkcu.run", string view = "Registry64", string kind = "String", ProbeStatus status = ProbeStatus.Success)
    {
        var now = DateTimeOffset.UtcNow;
        Measurement Field(string field, string value) => new(StartupItemsProbeContract.ItemMeasurementName(0, field), new TextValue(value), null, "fixture", now, MeasurementQuality.Observed);
        return new(Guid.NewGuid(), [new(StartupItemsProbeContract.PROBE_ID, status,
            [new(StartupItemsProbeContract.ITEM_COUNT, new IntegerValue(1), null, "fixture", now, MeasurementQuality.Observed),
            Field("name", "Fixture App"), Field("source", source), Field("registryView", view), Field("valueKind", kind)], [], now, TimeSpan.Zero, new("fixture", true))]);
    }
    /// <summary>허용 출처·보기·형식과 성공/부분 관측만 제공합니다.</summary>
    [Theory]
    [InlineData("hkcu.run", "Registry64", "String", ProbeStatus.Success, 1)]
    [InlineData("hkcu.run", "Registry64", "ExpandString", ProbeStatus.Partial, 1)]
    [InlineData("hkcu.run", "Registry32", "String", ProbeStatus.Success, 0)]
    [InlineData("hklm64.run", "Registry64", "String", ProbeStatus.Success, 0)]
    [InlineData("hkcu.runOnce", "Registry64", "String", ProbeStatus.Success, 0)]
    [InlineData("folder.user", "Registry64", "String", ProbeStatus.Success, 0)]
    [InlineData("hkcu.run", "Registry64", "Binary", ProbeStatus.Success, 0)]
    [InlineData("hkcu.run", "Registry64", "String", ProbeStatus.Failed, 0)]
    public void SelectorFiltersSourceAndObservation(string source, string view, string kind, ProbeStatus status, int expected)
        => Assert.Equal(expected, StartupSelection.Names(Snapshot(source, view, kind, status)).Count);
    /// <summary>준비/실행 분리·재검사 목록 제거·복구 항목 이름을 확인합니다.</summary>
    [Fact]
    public async Task SelectionExecutesOnlyAfterConfirmAndHistoryRetainsName()
    {
        var platform = new StartupActionTests.Platform(); var store = new ActionCenterTests.Store();
        var workflow = new ActionWorkflow(new OperationCoordinator(), store, () => ActionCenterTests.Session, [], [new StartupRunActionAdapter(platform, () => ActionCenterTests.Session)]);
        using var vm = new ActionCenterViewModel(workflow, new ActionCenterTests.Dispatch(), () => Task.CompletedTask);
        Assert.Empty(vm.Choices); vm.UpdateStartupChoices(Snapshot(), true);
        var choice = Assert.Single(vm.Choices);
        await vm.PrepareChoiceCommand.ExecuteAsync(choice);
        Assert.Equal(0, platform.Writes); Assert.True(vm.HasPreview);
        await vm.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(1, platform.Writes); Assert.Contains("Fixture App", Assert.Single(vm.Records).Title);
        vm.UpdateStartupChoices(new(Guid.NewGuid(), []), true); Assert.Empty(vm.Choices);
        Assert.False(vm.PrepareChoiceCommand.CanExecute(choice));
        vm.UpdateStartupChoices(Snapshot(), false); Assert.Empty(vm.Choices);
    }
    /// <summary>SystemOnly는 항목이 있어도 자동 작업에 등록하지 않습니다.</summary>
    [Fact]
    public void WorkflowScopeOverridesUiArgument()
    {
        var workflow = new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(),
            () => ActionCenterTests.Session with { Scope = ActionUserScope.SystemOnly }, [],
            [new StartupRunActionAdapter(new StartupActionTests.Platform(), () => ActionCenterTests.Session)]);
        using var vm = new ActionCenterViewModel(workflow, new ActionCenterTests.Dispatch(), () => Task.CompletedTask);
        vm.UpdateStartupChoices(Snapshot(), true); Assert.Empty(vm.Choices);
    }
}
