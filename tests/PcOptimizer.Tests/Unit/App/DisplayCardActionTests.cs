/**
 * @file    : DisplayCardActionTests.cs
 * @author  : rudals252
 * @brief   : 실제 카드 측정값 → 정확한 모니터/주사율 사전 확인 → 별도 실행 회귀
 */
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions.Display;
using PcOptimizer.Tests.Unit.Probes;
using PcOptimizer.Tests.Unit.Rules;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>화면 설정을 실제로 바꾸지 않는 카드 연결 검증입니다.</summary>
public sealed class DisplayCardActionTests
{
    internal static Finding Finding(string path = "monitor", int hz = 120, bool mismatched = false) => new(
        DisplayRefreshRule.FINDING_ID_PREFIX + path, FindingCategory.Display, "fixture display", [
            new(DisplayProbeContract.TargetMeasurementName(0, DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH), new TextValue(path), null, "fixture", DateTimeOffset.UtcNow, MeasurementQuality.Reported),
            new(DisplayProbeContract.TargetMeasurementName(mismatched ? 1 : 0, DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ), new IntegerValue(hz), "Hz", "fixture", DateTimeOffset.UtcNow, MeasurementQuality.Reported)],
        "fixture evidence", Verdict.Candidate, null, null, new Recommendation("fixture", "fixture"), null,
        [new OpenSettingsAction(DisplayRefreshRule.DISPLAY_SETTINGS_URI)], new Explanation("fixture", "fixture", "fixture"), SafetyLevel.Caution);
    private static FindingCardViewModel Card(Finding finding, bool available = true) => new(finding,
        new SettingsUriPolicy(NullAppLogger.Instance, _ => { }), new LinkPolicy(null, NullAppLogger.Instance, _ => { }), available);
    /// <summary>실제 판정 규칙 결과도 표시 문자열 파싱 없이 실행 후보로 연결합니다.</summary>
    [Fact] public void RealRuleFindingOffersTypedTrialButton()
    {
        var finding = Assert.Single(new DisplayRefreshRule().Evaluate(DisplayRefreshRuleTests.CandidateSnapshot()));
        var card = Card(finding); Assert.True(card.CanTrialDisplay); Assert.Contains("Hz 시험 적용", card.DisplayTrialButtonText);
        Assert.False(Card(finding, false).CanTrialDisplay);
    }
    /// <summary>다른 모니터의 Hz를 잘못 연결하거나 기본값/오버플로 값을 실행에 쓰지 않습니다.</summary>
    [Theory] [InlineData(true, 120)] [InlineData(false, 0)] [InlineData(false, 1)]
    public void MissingOrMismatchedMeasurementsDoNotOfferAction(bool mismatch, int hz)
    { Assert.False(Card(Finding(hz: hz, mismatched: mismatch)).CanTrialDisplay); }
    /// <summary>카드 클릭은 대상 선택과 사전 확인까지이며 별도 실행·유지 뒤에만 변경이 남습니다.</summary>
    [Fact] public async Task CardSelectsExactMonitorAndRequiresExplicitExecution()
    {
        var platform = new DisplayTrialTests.Platform(); var gate = new OperationCoordinator();
        var service = new DisplayTrialCoordinator(platform, gate, new ActionCenterTests.Store(), () => DisplayTrialTests.Session);
        using var vm = new DisplayTrialViewModel(service, gate, new ActionCenterTests.Dispatch(), () => Task.CompletedTask);
        await vm.PrepareFindingAsync(Finding());
        Assert.Equal("monitor", vm.Selected!.DeviceKey); Assert.Equal(120, vm.Selected.DesiredHz);
        Assert.True(vm.HasPreview); Assert.True(vm.ExecuteCommand.CanExecute(null)); Assert.Equal(0, platform.Writes);
        var run = vm.ExecuteCommand.ExecuteAsync(null); var end = DateTime.UtcNow.AddSeconds(5);
        while (service.Status is null) { if (DateTime.UtcNow >= end) { throw new TimeoutException(); } await Task.Delay(1); }
        vm.KeepCommand.Execute(null); await run; Assert.Equal(120, platform.Current.Mode.RefreshHz); Assert.Equal(120, platform.Registered.Mode.RefreshHz);
    }
    /// <summary>카드 대상이 사라지면 다른 화면이나 첫 후보를 자동 선택하지 않습니다.</summary>
    [Theory] [InlineData("other", 120)] [InlineData("monitor", 130)]
    public async Task StaleCardClearsOldPreviewAndNeverFallsBack(string path, int hz)
    {
        var platform = new DisplayTrialTests.Platform(); var gate = new OperationCoordinator();
        var service = new DisplayTrialCoordinator(platform, gate, new ActionCenterTests.Store(), () => DisplayTrialTests.Session);
        using var vm = new DisplayTrialViewModel(service, gate, new ActionCenterTests.Dispatch(), () => Task.CompletedTask);
        await vm.PrepareFindingAsync(Finding()); Assert.True(vm.HasPreview);
        await vm.PrepareFindingAsync(Finding(path, hz)); Assert.False(vm.HasPreview); Assert.Null(vm.Selected);
        Assert.False(vm.ExecuteCommand.CanExecute(null)); Assert.Equal(0, platform.Writes);
    }
}
