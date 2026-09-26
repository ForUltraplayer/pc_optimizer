/**
 * @file    : MainViewModelTests.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델의 상태 전이·Finding 기준 건수·분류 필터·취소 후 재검사·종료 중 표시·내보내기를 가짜 프로브와 즉시 실행 마샬러로 검증
 */

// 기본 패키지
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

    private readonly List<string> _launchedUris = [];

    /// <summary>
    /// 프로브·규칙으로 뷰모델을 만든다.
    /// </summary>
    private MainViewModel CreateViewModel(IEnumerable<IProbe> probes, IExportPathPicker? picker = null, IUiDispatcher? dispatcher = null)
    {
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
            NullAppLogger.Instance);
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
}
