/**
 * @file    : MainViewModelTests.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델의 상태 전이·Finding 기준 건수·분류 필터·취소 후 재검사·종료 중 표시·내보내기·관리자 권한 재검사(버튼 활성, UAC 취소 시 결과 보존, 실패 안내, 배너)를 가짜 프로브와 즉시 실행 마샬러로 검증
 */

// 기본 패키지
using System.ComponentModel;
using System.IO;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.App.Fakes;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="MainViewModel"/>를 WPF Dispatcher 없이 검증합니다. 실제 OS 수집은 하지 않습니다.
/// </summary>
public sealed class MainViewModelTests
{
    private static readonly TimeSpan WAIT_BOUND = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SHORT_GRACE = TimeSpan.FromMilliseconds(50);
    private const string APP_PATH = @"C:\Program Files\PcOptimizer\PcOptimizer.App.exe";

    private readonly List<string> _launchedUris = [];

    /// <summary>
    /// 프로브·규칙으로 뷰모델을 만든다.
    /// </summary>
    private MainViewModel CreateViewModel(
        IEnumerable<IProbe> probes,
        IExportPathPicker? picker = null,
        IUiDispatcher? dispatcher = null,
        IElevationState? elevation = null,
        IProcessStarter? starter = null,
        ScanLaunchMode launchMode = ScanLaunchMode.Normal)
    {
        var elevationState = elevation ?? new FakeElevationState(isElevated: false);
        var options = new ScanOptions { CancellationGracePeriod = SHORT_GRACE };
        var service = new ScanService(
            probes,
            [new MemorySpeedRule(), new PowerPlanRule()],
            options,
            new ScanReportVersions("test-app", "test-rules"),
            new FakeClock(),
            NullAppLogger.Instance,
            () => false);
        var scrubber = new PersonalDataScrubber(@"C:\Users\Kimtester", "Kimtester", "DESKTOP-FAKE01");

        return new MainViewModel(
            service,
            new ReportExporter(scrubber),
            picker ?? new FixedExportPathPicker(null),
            new SettingsUriPolicy(NullAppLogger.Instance, _launchedUris.Add),
            dispatcher ?? new ImmediateUiDispatcher(),
            NullAppLogger.Instance,
            elevationState,
            new ElevationRelauncher(starter ?? new RecordingProcessStarter(), elevationState, () => APP_PATH, NullAppLogger.Instance),
            launchMode);
    }

    /// <summary>처음에는 대기 상태이고 시작만 가능하다.</summary>
    [Fact]
    public void 처음에는_대기_상태다()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()]);

        Assert.Equal(ScanState.Idle, vm.State);
        Assert.True(vm.StartScanCommand.CanExecute(null));
        Assert.False(vm.CancelScanCommand.CanExecute(null));
        Assert.False(vm.ExportCommand.CanExecute(null));
        Assert.Empty(vm.Cards);
        Assert.Equal(Strings.LastMeasured_None, vm.LastMeasuredText);
    }

    /// <summary>검사가 끝나면 Finding 기준 건수·분류·카드·마지막 측정 시각을 UI 마샬러로 반영한다.</summary>
    [Fact]
    public async Task 검사_결과를_건수와_카드로_반영한다()
    {
        var dispatcher = new ImmediateUiDispatcher();
        var vm = CreateViewModel([new FixtureMemoryProbe(), new ThrowingProbe("fixture.power-failing") { Category = FindingCategory.Power }], dispatcher: dispatcher);

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Equal(ScanState.Partial, vm.State);
        Assert.True(dispatcher.InvocationCount > 0);
        Assert.Equal(0, vm.OkCount);
        Assert.Equal(1, vm.CandidateCount);
        Assert.Equal(1, vm.InfoCount);
        Assert.Equal(2, vm.CannotVerifyCount);
        Assert.Equal(DisplayText.Format(Strings.Summary_Format, 0, 1, 2, 1), vm.SummaryText);
        Assert.NotNull(vm.LastMeasuredAtUtc);
        Assert.Equal(4, vm.Cards.Count);
        Assert.Equal([null, FindingCategory.Memory, FindingCategory.Power], vm.Categories.Select(c => c.Category));
        Assert.Equal([4, 3, 1], vm.Categories.Select(c => c.Count));
        Assert.True(vm.ExportCommand.CanExecute(null));

        var failed = Assert.Single(vm.Cards, card => card.Finding.Category == FindingCategory.Power);
        Assert.Equal(Strings.Verdict_CannotVerify, failed.VerdictText);
        Assert.True(failed.HasReason);
        Assert.Equal(DisplayText.Reason(CannotVerifyReason.ProbeError), failed.ReasonText);
    }

    /// <summary>분류를 고르면 그 분류 카드만 보이고, 전체를 고르면 모두 보인다.</summary>
    [Fact]
    public async Task 분류를_고르면_카드를_거른다()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe(), new ThrowingProbe("fixture.power-failing") { Category = FindingCategory.Power }]);
        await vm.StartScanCommand.ExecuteAsync(null);

        vm.SelectedCategory = vm.Categories.Single(c => c.Category == FindingCategory.Power);
        Assert.Single(vm.Cards);

        vm.SelectedCategory = vm.Categories.Single(c => c.Category is null);
        Assert.Equal(4, vm.Cards.Count);
    }

    /// <summary>취소하면 취소됨 상태와 취소 사유 카드를 남기고, 다시 검사하면 새 결과로 완료된다.</summary>
    [Fact]
    public async Task 취소_후_다시_검사할_수_있다()
    {
        var waiting = new FirstCallWaitsForCancelProbe();
        var vm = CreateViewModel([waiting]);

        var scan = vm.StartScanCommand.ExecuteAsync(null);
        await waiting.Started.WaitAsync(WAIT_BOUND);
        Assert.Equal(ScanState.Scanning, vm.State);
        Assert.True(vm.CancelScanCommand.CanExecute(null));
        Assert.False(vm.StartScanCommand.CanExecute(null));

        vm.CancelScanCommand.Execute(null);
        await scan.WaitAsync(WAIT_BOUND);

        Assert.Equal(ScanState.Cancelled, vm.State);
        var cancelled = Assert.Single(vm.Cards);
        Assert.Equal(CannotVerifyReason.Cancelled, cancelled.Finding.CannotVerifyReason);
        Assert.True(vm.StartScanCommand.CanExecute(null));

        await vm.StartScanCommand.ExecuteAsync(null).WaitAsync(WAIT_BOUND);

        Assert.Equal(ScanState.Completed, vm.State);
        Assert.Empty(vm.Cards);
    }

    /// <summary>취소를 무시하는 프로브는 '아직 종료 중'으로 표시한다.</summary>
    [Fact]
    public async Task 끝나지_않은_프로브는_종료_중으로_표시한다()
    {
        var hanging = new HangingProbe("fixture.hanging");
        var vm = CreateViewModel([hanging]);

        var scan = vm.StartScanCommand.ExecuteAsync(null);
        await hanging.Started.WaitAsync(WAIT_BOUND);
        vm.CancelScanCommand.Execute(null);
        await scan.WaitAsync(WAIT_BOUND);

        Assert.Equal(ScanState.Cancelled, vm.State);
        Assert.True(vm.HasDrainingNote);
        Assert.Contains("fixture.hanging", vm.DrainingNote, StringComparison.Ordinal);
    }

    /// <summary>[유지]는 현재 검사의 카드만 접고, 다시 검사하면 새 카드는 펼쳐져 있다.</summary>
    [Fact]
    public async Task 유지는_현재_검사에서만_접는다()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()]);
        await vm.StartScanCommand.ExecuteAsync(null);
        var candidate = Assert.Single(vm.Cards, card => card.Verdict == Verdict.Candidate);

        Assert.True(candidate.HasKeepAction);
        candidate.KeepCommand.Execute(null);
        Assert.True(candidate.IsKept);
        Assert.False(candidate.IsBodyVisible);
        candidate.UnkeepCommand.Execute(null);
        Assert.True(candidate.IsBodyVisible);
        candidate.KeepCommand.Execute(null);

        await vm.StartScanCommand.ExecuteAsync(null);

        var fresh = Assert.Single(vm.Cards, card => card.Verdict == Verdict.Candidate);
        Assert.NotSame(candidate, fresh);
        Assert.False(fresh.IsKept);
    }

    /// <summary>내보내기는 고른 경로에 익명화 JSON을 쓰고, 경로 선택을 취소하면 아무것도 쓰지 않는다.</summary>
    [Fact]
    public async Task 내보내기는_고른_경로에_익명화_JSON을_쓴다()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcoptimizer-vm-export-{Guid.NewGuid():N}.json");
        try
        {
            var picker = new FixedExportPathPicker(path);
            var vm = CreateViewModel([new FixtureMemoryProbe()], picker);
            await vm.StartScanCommand.ExecuteAsync(null);

            await vm.ExportCommand.ExecuteAsync(null);

            Assert.True(File.Exists(path));
            Assert.Contains(ReportExporter.EXPORT_KIND_ANONYMIZED, await File.ReadAllTextAsync(path), StringComparison.Ordinal);
            Assert.NotNull(vm.StatusMessage);
            Assert.DoesNotContain(Path.GetDirectoryName(path)!, vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("pcoptimizer-report-", picker.SuggestedFileName, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }

        var cancelledVm = CreateViewModel([new FixtureMemoryProbe()], new FixedExportPathPicker(null));
        await cancelledVm.StartScanCommand.ExecuteAsync(null);
        await cancelledVm.ExportCommand.ExecuteAsync(null);
        Assert.Null(cancelledVm.StatusMessage);
    }

    /// <summary>일반 권한 창에서만 관리자 권한 재검사를 요청할 수 있고, 관리자 권한 창에서는 버튼이 꺼진다.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void 관리자_권한_재검사는_일반_권한에서만_가능하다(bool isElevated, bool canRequest)
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()], elevation: new FakeElevationState(isElevated));

        Assert.Equal(isElevated, vm.IsElevated);
        Assert.Equal(canRequest, vm.RequestElevatedRescanCommand.CanExecute(null));
        Assert.Equal(isElevated ? Strings.Tooltip_ElevatedRescan_AlreadyElevated : Strings.Tooltip_ElevatedRescan, vm.ElevatedRescanTooltip);
        Assert.False(vm.HasElevatedBanner);
    }

    /// <summary>UAC를 취소하면 기존 창의 상태·결과·카드는 그대로이고 "관리자 권한 요청이 취소되었어요"만 표시한다.</summary>
    [Fact]
    public async Task UAC_취소는_기존_결과를_보존한다()
    {
        var starter = new RecordingProcessStarter { ThrowOnStart = new Win32Exception(ElevationRelauncher.ERROR_CANCELLED) };
        var vm = CreateViewModel([new FixtureMemoryProbe()], starter: starter);
        await vm.StartScanCommand.ExecuteAsync(null);
        var result = vm.LastResult;
        var state = vm.State;
        var cards = vm.Cards.ToList();
        var summary = vm.SummaryText;
        var measured = vm.LastMeasuredText;

        await vm.RequestElevatedRescanCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Elevation_Cancelled, vm.StatusMessage);
        Assert.Same(result, vm.LastResult);
        Assert.Equal(state, vm.State);
        Assert.Equal(cards, vm.Cards);
        Assert.Equal(summary, vm.SummaryText);
        Assert.Equal(measured, vm.LastMeasuredText);
        Assert.Equal(ElevationRelauncher.RUNAS_VERB, Assert.Single(starter.Started).Verb);
        Assert.False(vm.IsRelaunching);
        Assert.True(vm.RequestElevatedRescanCommand.CanExecute(null));
    }

    /// <summary>그 밖의 시작 실패는 예외 형식 이름만 안내하고 기존 결과를 유지한다.</summary>
    [Fact]
    public async Task 시작_실패는_형식_이름만_안내한다()
    {
        var starter = new RecordingProcessStarter { ThrowOnStart = new InvalidOperationException(@"C:\Users\Kimtester secret") };
        var vm = CreateViewModel([new FixtureMemoryProbe()], starter: starter);
        await vm.StartScanCommand.ExecuteAsync(null);
        var result = vm.LastResult;

        await vm.RequestElevatedRescanCommand.ExecuteAsync(null);

        Assert.Equal(DisplayText.Format(Strings.Elevation_Failed, nameof(InvalidOperationException)), vm.StatusMessage);
        Assert.DoesNotContain("Kimtester", vm.StatusMessage, StringComparison.Ordinal);
        Assert.Same(result, vm.LastResult);
    }

    /// <summary>새 창을 시작해도 이 창의 결과는 합치거나 바꾸지 않고 안내만 표시한다.</summary>
    [Fact]
    public async Task 시작_성공은_이_창_결과를_바꾸지_않는다()
    {
        var starter = new RecordingProcessStarter();
        var vm = CreateViewModel([new FixtureMemoryProbe()], starter: starter);
        await vm.StartScanCommand.ExecuteAsync(null);
        var result = vm.LastResult;

        await vm.RequestElevatedRescanCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Elevation_Started, vm.StatusMessage);
        Assert.Same(result, vm.LastResult);
        Assert.Single(starter.Started);
    }

    /// <summary>관리자 재검사 인스턴스는 별도 검사 배너를, 다른 계정으로 승격된 경우 원래 창 안내를 함께 보인다.</summary>
    [Theory]
    [InlineData(ScanLaunchMode.ElevatedSameUser)]
    [InlineData(ScanLaunchMode.ElevatedDifferentUser)]
    public void 관리자_재검사_인스턴스는_배너를_보인다(ScanLaunchMode mode)
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()], elevation: new FakeElevationState(isElevated: true), launchMode: mode);

        Assert.True(vm.HasElevatedBanner);
        Assert.Equal(mode == ScanLaunchMode.ElevatedSameUser ? Strings.Banner_ElevatedSameUser : Strings.Banner_ElevatedDifferentUser, vm.ElevatedBannerText);
        Assert.Contains("별도 검사", vm.ElevatedBannerText, StringComparison.Ordinal);
        Assert.False(vm.RequestElevatedRescanCommand.CanExecute(null));
    }
}
