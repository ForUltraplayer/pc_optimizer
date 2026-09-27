/**
 * @file    : MainViewModelTests.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델의 상태 전이·Finding 기준 건수·바로 할 수 있는 것/직접 해야 하는 것 요약·정리 창 노출 조건·온라인 비교 완료 판정·분류 필터·취소 후 재검사·종료 중 표시·내보내기·다른 관리자 계정 실행·사용자 확인 불가 시 시스템 범위 안내 배너(문구 구분)와 사용자 범위 프로브 건너뜀·내 PC 사양 전환(첫 진입 새로 고침을 기다리지 않는 전환, 검사 중 새로 고침 막기와 검사 후 자동 읽기, 사양 읽는 중 검사 시작 막기, 사양·검사 프로브 종료 대기 중 상호 차단)을 가짜 프로브와 즉시 실행 마샬러로 검증
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

    private readonly List<string> _launchedUris = [];

    /// <summary>서비스 관문만 점유해도 UI의 검사·사양·정리와 안내가 갱신되며 창 해제는 실제 작업을 풀지 않습니다.</summary>
    [Fact]
    public async Task SharedOperationNotifiesUiAndOutlivesViewModel()
    {
        var vm = CreateViewModel([], availability: new FixedActionAvailability(true));
        var changes = new List<bool>();
        vm.StartScanCommand.CanExecuteChanged += (_, _) => changes.Add(vm.StartScanCommand.CanExecute(null));
        var pending = OperationLifetimeTests.Source();
        var lease = vm.Operations.TryAcquire(PcOptimizer.Core.Actions.OperationKind.Restore)!;
        lease.Track(pending.Task);
        lease.Dispose();
        Assert.False(vm.StartScanCommand.CanExecute(null));
        Assert.False(vm.CanOpenCacheTools);
        Assert.False(vm.Spec.RefreshCommand.CanExecute(null));
        Assert.True(vm.HasOperationNote);
        Assert.Contains(false, changes);
        vm.Dispose();
        Assert.True(vm.Operations.State.IsBusy);
        pending.SetResult();
        await OperationLifetimeTests.Idle(vm.Operations);
        Assert.False(vm.HasOperationNote);
    }

    /// <summary>
    /// 프로브·규칙으로 뷰모델을 만든다.
    /// </summary>
    private MainViewModel CreateViewModel(
        IEnumerable<IProbe> probes,
        IExportPathPicker? picker = null,
        IUiDispatcher? dispatcher = null,
        IElevationState? elevation = null,
        UserScopeMode userScope = UserScopeMode.Full,
        bool limitToSystemScope = false,
        IEnumerable<IRule>? rules = null,
        IActionAvailability? availability = null,
        PcSpecViewModel? spec = null,
        bool userScopeUnresolved = false)
    {
        var elevationState = elevation ?? new FakeElevationState(isElevated: false);
        var options = new ScanOptions { CancellationGracePeriod = SHORT_GRACE };
        var service = new ScanService(
            probes,
            rules ?? [new MemorySpeedRule(), new PowerPlanRule()],
            options,
            new ScanReportVersions("test-app", "test-rules"),
            new FakeClock(),
            NullAppLogger.Instance,
            () => false,
            limitToSystemScope);
        var scrubber = new PersonalDataScrubber(@"C:\Users\Kimtester", "Kimtester", "DESKTOP-FAKE01");

        return new MainViewModel(
            service,
            new ReportExporter(scrubber),
            picker ?? new FixedExportPathPicker(null),
            new SettingsUriPolicy(NullAppLogger.Instance, _launchedUris.Add),
            new LinkPolicy(null, NullAppLogger.Instance, _launchedUris.Add),
            dispatcher ?? new ImmediateUiDispatcher(),
            NullAppLogger.Instance,
            elevationState,
            userScope,
            availability ?? new FixedActionAvailability(false),
            spec ?? SpecTestFactory.Create(),
            userScopeUnresolved);
    }

    /// <summary>"내 PC 사양" 버튼은 본문을 사양 화면으로 바꾸고 첫 진입에서만 사양을 읽으며, 다시 누르면 결과로 돌아간다.</summary>
    [Fact]
    public async Task SpecToggleSwapsContentAndLoadsOnce()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()]);
        Assert.False(vm.IsSpecVisible);
        Assert.True(vm.IsResultsVisible);
        Assert.Equal(Strings.Spec_NavOpen, vm.SpecToggleText);

        vm.ToggleSpecCommand.Execute(null);
        await vm.Spec.RefreshCommand.ExecutionTask!.WaitAsync(WAIT_BOUND);

        Assert.True(vm.IsSpecVisible);
        Assert.False(vm.IsResultsVisible);
        Assert.Equal(Strings.Spec_NavBack, vm.SpecToggleText);
        Assert.Equal(9, vm.Spec.Sections.Count);
        var loadedAt = vm.Spec.Snapshot;
        Assert.NotNull(loadedAt);

        vm.ToggleSpecCommand.Execute(null);
        Assert.False(vm.IsSpecVisible);
        vm.ToggleSpecCommand.Execute(null);
        Assert.Same(loadedAt, vm.Spec.Snapshot);
    }

    /// <summary>첫 사양 읽기가 오래 걸려도 전환 버튼은 막히지 않아 바로 결과로 돌아갈 수 있고, 읽기는 뒤에서 끝난다. 읽는 동안에는 검사를 시작할 수 없다.</summary>
    [Fact]
    public async Task SpecToggleReturnsImmediatelyWhileSpecLoads()
    {
        var gate = new GatedSystemDetailsProbe();
        var vm = CreateViewModel([new FixtureMemoryProbe()], spec: SpecTestFactory.Create(probes: [gate]));

        vm.ToggleSpecCommand.Execute(null);
        await gate.Started.WaitAsync(WAIT_BOUND);

        Assert.True(vm.IsSpecVisible);
        Assert.True(vm.Spec.IsLoading);
        Assert.True(vm.ToggleSpecCommand.CanExecute(null));
        Assert.False(vm.StartScanCommand.CanExecute(null));
        vm.ToggleSpecCommand.Execute(null);
        Assert.False(vm.IsSpecVisible);

        gate.Release();
        await vm.Spec.RefreshCommand.ExecutionTask!.WaitAsync(WAIT_BOUND);
        Assert.False(vm.Spec.IsLoading);
        Assert.NotNull(vm.Spec.Snapshot);
        Assert.True(vm.StartScanCommand.CanExecute(null));
    }

    /// <summary>검사 중에 사양 화면을 열면 그때는 읽지 않고, 검사가 끝나면(화면이 열려 있고 아직 읽은 사양이 없으면) 자동으로 읽는다.</summary>
    [Fact]
    public async Task SpecOpenedDuringScanLoadsAfterScanEnds()
    {
        var waiting = new FirstCallWaitsForCancelProbe();
        var vm = CreateViewModel([waiting]);

        var scan = vm.StartScanCommand.ExecuteAsync(null);
        await waiting.Started.WaitAsync(WAIT_BOUND);
        vm.ToggleSpecCommand.Execute(null);
        Assert.True(vm.IsSpecVisible);
        Assert.Null(vm.Spec.RefreshCommand.ExecutionTask);
        Assert.Null(vm.Spec.Snapshot);

        vm.CancelScanCommand.Execute(null);
        await scan.WaitAsync(WAIT_BOUND);
        Assert.NotNull(vm.Spec.RefreshCommand.ExecutionTask);
        await vm.Spec.RefreshCommand.ExecutionTask!.WaitAsync(WAIT_BOUND);

        Assert.NotNull(vm.Spec.Snapshot);
        Assert.Equal(9, vm.Spec.Sections.Count);
    }

    /// <summary>사양 화면을 닫은 채로 검사가 끝나면 사양을 읽지 않는다.</summary>
    [Fact]
    public async Task SpecIsNotLoadedAfterScanWhenHidden()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()]);

        await vm.StartScanCommand.ExecuteAsync(null).WaitAsync(WAIT_BOUND);

        Assert.Null(vm.Spec.RefreshCommand.ExecutionTask);
        Assert.Null(vm.Spec.Snapshot);
    }

    /// <summary>검사 중에는 사양 새로 고침이 꺼지고(프로브 공유), 검사가 끝나면 다시 켜진다.</summary>
    [Fact]
    public async Task SpecRefreshIsDisabledWhileScanning()
    {
        var waiting = new FirstCallWaitsForCancelProbe();
        var vm = CreateViewModel([waiting]);
        Assert.True(vm.Spec.RefreshCommand.CanExecute(null));

        var scan = vm.StartScanCommand.ExecuteAsync(null);
        await waiting.Started.WaitAsync(WAIT_BOUND);
        Assert.False(vm.Spec.RefreshCommand.CanExecute(null));

        vm.CancelScanCommand.Execute(null);
        await scan.WaitAsync(WAIT_BOUND);
        Assert.True(vm.Spec.RefreshCommand.CanExecute(null));
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
        Assert.True(vm.IsOverview);
        Assert.False(vm.HasDoNow);
        Assert.Equal(DisplayText.Format(Strings.Overview_DoManuallyCount, 0), vm.DoManuallyText);
        Assert.Equal(Strings.Overview_Welcome, vm.OverviewTitle);
        Assert.Equal(Strings.LastMeasured_None, vm.LastMeasuredText);
    }

    /// <summary>캐시 관측과 커뮤니티 결과를 개선 후보로 승격하지 않습니다.</summary>
    [Fact]
    public async Task OverviewDoesNotPromoteCacheObservationsOrCommunityRules()
    {
        using var vm = CreateViewModel([new FixtureMemoryProbe()], rules: [new OverviewFixtureRule()]);
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal("fixture:action", Assert.Single(vm.VisibleCards).Finding.Id);
        Assert.Equal(3, vm.LastResult!.Report.Findings.Count);
        vm.ShowAllCommand.Execute(null);
        Assert.Equal(2, vm.VisibleCards.Count());
        Assert.Contains(vm.VisibleCards, c => c.Finding.Id == "fixture:cache");
        vm.ShowCommunityDetails = true;
        Assert.Equal(3, vm.VisibleCards.Count());
        vm.ShowRecommendationsCommand.Execute(null);
        Assert.Single(vm.VisibleCards);
        Assert.Equal(3, vm.LastResult.Report.Findings.Count);
    }

    private sealed class OverviewFixtureRule : IRule
    {
        public string Id => "fixture.overview";
        public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot) =>
        [
            new Finding("fixture:cache", FindingCategory.AppCache, "관측한 캐시 1 GB", [], "논리 크기", Verdict.Info,
                null, null, null, new Impact("용량 확인", "다시 내려받음"), []),
            new Finding("fixture:action", FindingCategory.Display, "주사율 후보", [], "동일 모드 비교", Verdict.Candidate,
                null, null, new Recommendation("설정 확인", "동일 해상도"), new Impact("화면 움직임", "전력 소비"),
                [new OpenSettingsAction(SettingsUriPolicy.DISPLAY_SETTINGS_URI)],
                explanation: new Explanation("테스트 설명", "테스트 효과", "테스트 주의"), safety: SafetyLevel.Safe),
            new Finding("fixture:community", FindingCategory.AppCache, "커뮤니티 후보", [
                new Measurement("cache.origin", new TextValue(AppCacheProbeContract.ORIGIN_COMMUNITY), null, "fixture",
                    DateTimeOffset.UnixEpoch, MeasurementQuality.Reported)], "커뮤니티 규칙", Verdict.Candidate,
                null, null, new Recommendation("수동 확인", "검증 전"), null, [],
                explanation: new Explanation("테스트 설명", "테스트 효과", "테스트 주의"), safety: SafetyLevel.Safe),
        ];
    }

    /// <summary>목적별 내비게이션은 원본 리포트를 바꾸지 않습니다.</summary>
    [Fact]
    public async Task GoalNavigationFiltersResultsWithoutChangingTheReport()
    {
        using var vm = CreateViewModel([new FixtureMemoryProbe()]);
        await vm.StartScanCommand.ExecuteAsync(null);
        var report = vm.LastResult;
        vm.ShowAllCommand.Execute(null);
        vm.Categories.Add(new CategoryItemViewModel(FindingCategory.Driver, 0));
        vm.SelectedCategory = vm.Categories.Single(c => c.Category == FindingCategory.Driver);
        Assert.True(vm.ShowAllResults);
        Assert.Equal(FindingCategory.Driver, vm.SelectedCategory!.Category);
        Assert.Empty(vm.VisibleCards); // Never falls back to unrelated findings.
        vm.ShowRecommendationsCommand.Execute(null);
        Assert.Single(vm.VisibleCards);
        Assert.Same(report, vm.LastResult);
    }

    /// <summary>온라인 요청(체크박스)만으로는 비교 완료로 보지 않고, 재검사해도 이전 정리 결과를 보존합니다.</summary>
    [Fact]
    public async Task OfflineComparisonAndLastCleanupRemainExplicitAfterRescan()
    {
        using var vm = CreateViewModel([new FixtureMemoryProbe()]);
        vm.LastCleanupOutcome = new CleanupOutcomeViewModel("npm", new(true, 0, "Completed") { BeforeBytes = 500 });
        var outcome = vm.LastCleanupOutcome;
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.False(vm.HasLastOnlineCheck);
        vm.IsOnlineCheckRequested = true;
        Assert.False(vm.HasLastOnlineCheck); // Toggling is not a completed comparison.
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.False(vm.HasLastOnlineCheck); // Requested, but no online provider results.
        vm.IsOnlineCheckRequested = false;
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Null(vm.LastOnlineCheckText);
        Assert.Same(outcome, vm.LastCleanupOutcome);
    }

    /// <summary>온라인 공급자의 성공·실패·부분·취소 결과 중 실제 성공만 옵션 영역의 마지막 온라인 확인 시각을 갱신합니다.</summary>
    [Theory]
    [InlineData(ProbeStatus.Success, true)]
    [InlineData(ProbeStatus.Failed, false)]
    [InlineData(ProbeStatus.Partial, false)]
    [InlineData(ProbeStatus.Cancelled, false)]
    [InlineData(ProbeStatus.Skipped, false)]
    public async Task LastOnlineCheckRequiresActualSuccessfulOnlineResults(ProbeStatus status, bool complete)
    {
        using var vm = CreateViewModel([
            new OnlineFixtureProbe(NvidiaLookupProbeContract.PROBE_ID, ProbeStatus.Success),
            new OnlineFixtureProbe(WindowsUpdateProbeContract.PROBE_ID, status)]);
        vm.IsOnlineCheckRequested = true;
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(complete, vm.HasLastOnlineCheck);
        Assert.Equal(complete, vm.LastOnlineCheckText is not null);
    }

    /// <summary>후보가 있어도 다른 공급자가 부분 결과이면 온라인 확인 시각을 갱신하지 않습니다.</summary>
    [Fact]
    public async Task PartialOnlineResultWithCandidateDoesNotRecordOnlineCheck()
    {
        using var vm = CreateViewModel([
            new OnlineFixtureProbe(NvidiaLookupProbeContract.PROBE_ID, ProbeStatus.Partial),
            new OnlineFixtureProbe(WindowsUpdateProbeContract.PROBE_ID, ProbeStatus.Success)], rules: [new DriverFixtureRule()]);
        vm.IsOnlineCheckRequested = true;
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Single(vm.RecommendedCards);
        Assert.False(vm.HasLastOnlineCheck);
    }

    /// <summary>
    /// REV-014: 로컬 드라이버 분류의 확인 불가(AMD/Intel 링크·OEM 미확인 등)는 온라인 비교 완료 판정에 섞이지 않고,
    /// 온라인 규칙(NVIDIA 비교)의 확인 불가만 완료를 막습니다.
    /// </summary>
    [Theory]
    [InlineData("gpu-vendor-link:intel-igpu", true)]
    [InlineData(DriverUpdateRule.FINDING_ID_PREFIX + "adapter-0", false)]
    public async Task OnlyOnlineRuleCannotVerifyBlocksOnlineCompletion(string cannotVerifyId, bool complete)
    {
        using var vm = CreateViewModel([
            new OnlineFixtureProbe(NvidiaLookupProbeContract.PROBE_ID, ProbeStatus.Success),
            new OnlineFixtureProbe(WindowsUpdateProbeContract.PROBE_ID, ProbeStatus.Success)],
            rules: [new DriverCannotVerifyFixtureRule(cannotVerifyId)]);
        vm.IsOnlineCheckRequested = true;
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(CannotVerifyReason.Unsupported, Assert.Single(vm.LastResult!.Report.Findings).CannotVerifyReason);
        Assert.Equal(complete, vm.HasLastOnlineCheck);
    }

    /// <summary>요약 타일은 바로 할 수 있는 후보와 직접 해야 하는 후보를 나눠 세고, 바로 할 수 있는 것이 0이면 그 타일을 숨긴다.</summary>
    [Fact]
    public async Task OverviewCountsDoNowAndDoManually()
    {
        using var vm = CreateViewModel([new FixtureMemoryProbe()], availability: new FixedActionAvailability(false));
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.RecommendedCards);
        Assert.Equal(0, vm.DoNowCount);
        Assert.False(vm.HasDoNow);
        Assert.Equal(vm.RecommendedCards.Count, vm.DoManuallyCount);
        Assert.Equal(DisplayText.Format(Strings.Overview_DoManuallyCount, vm.RecommendedCards.Count), vm.DoManuallyText);

        using var vm2 = CreateViewModel([new FixtureMemoryProbe()], availability: new FixedActionAvailability(true));
        await vm2.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(vm2.RecommendedCards.Count, vm2.DoNowCount);
        Assert.True(vm2.HasDoNow);
        Assert.Equal(0, vm2.DoManuallyCount);
        Assert.Equal(DisplayText.Format(Strings.Overview_DoNowCount, vm2.RecommendedCards.Count), vm2.DoNowText);
    }

    /// <summary>요약 건수는 개선 후보만 센다(정상·참고·확인 불가·커뮤니티 후보는 제외).</summary>
    [Fact]
    public async Task OverviewCountsExcludeNonCandidatesAndCommunityCards()
    {
        using var vm = CreateViewModel([new FixtureMemoryProbe()], rules: [new OverviewFixtureRule()], availability: new FixedActionAvailability(true));
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.LastResult!.Report.Findings.Count);
        Assert.Equal(1, vm.DoNowCount);
        Assert.Equal(0, vm.DoManuallyCount);
    }

    /// <summary>검사 결과가 반영되면 요약 타일 속성 변경을 알린다.</summary>
    [Fact]
    public async Task OverviewTilesRaisePropertyChangedAfterScan()
    {
        using var vm = CreateViewModel([new FixtureMemoryProbe()]);
        var changed = new HashSet<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Superset(new HashSet<string?> { nameof(vm.DoNowCount), nameof(vm.DoManuallyCount), nameof(vm.HasDoNow), nameof(vm.DoNowText), nameof(vm.DoManuallyText) }, changed);
    }

    /// <summary>보호 위치에 도구가 없으면 정리 창을 열 수 없고, 도구 위치 판정은 생성 시 한 번만 한다.</summary>
    [Fact]
    public async Task CacheToolsRequireToolInProtectedLocation()
    {
        using var without = CreateViewModel([new FixtureMemoryProbe()], availability: new FixedActionAvailability(false, cacheToolsAvailable: false));
        await without.StartScanCommand.ExecuteAsync(null);
        Assert.False(without.CacheToolsAvailable);
        Assert.False(without.CanOpenCacheTools);

        using var with = CreateViewModel([new FixtureMemoryProbe()], availability: new FixedActionAvailability(false, cacheToolsAvailable: true));
        Assert.True(with.CacheToolsAvailable);
        Assert.True(with.CanOpenCacheTools);

        var calls = 0;
        using var counted = CreateViewModel([new FixtureMemoryProbe()],
            availability: new CacheToolActionAvailability(_ => { calls++; return true; }, CacheToolActionAvailability.DEFAULT_REVIEWED_APP_IDS));
        await counted.StartScanCommand.ExecuteAsync(null);
        Assert.True(counted.CanOpenCacheTools);
        Assert.True(counted.CanOpenCacheTools);
        Assert.Equal(6, calls); // 시작 시 도구 3종 + 검사 때 3종, getter는 조회하지 않음.
    }

    private sealed class DriverCannotVerifyFixtureRule(string id) : IRule
    {
        public string Id => "fixture.driver-cannot-verify";
        public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot) => [new Finding(id, FindingCategory.Driver,
            "드라이버 확인 불가", [], "fixture", Verdict.CannotVerify, CannotVerifyReason.Unsupported, null, null, null, [])];
    }

    private sealed class DriverFixtureRule : IRule
    {
        public string Id => "fixture.driver";
        public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot) => [new Finding("fixture:candidate", FindingCategory.Driver,
            "드라이버 후보", [], "fixture", Verdict.Candidate, null, null, new Recommendation("공식 도구 확인", "사용자 선택"), null, [],
            explanation: new Explanation("테스트 설명", "테스트 효과", "테스트 주의"), safety: SafetyLevel.Safe)];
    }

    private sealed class OnlineFixtureProbe(string id, ProbeStatus status) : IProbe
    {
        public string Id => id;
        public FindingCategory Category => FindingCategory.Driver;
        public bool RequiresElevation => false;
        public bool RequiresNetwork => true;
        public ProbeScope Scope => ProbeScope.System;
        public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);
        public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct) => Task.FromResult(
            new ProbeResult(Id, status, [], status == ProbeStatus.Success ? [] : [new Issue(CannotVerifyReason.NetworkFailed, "fixture")],
                DateTimeOffset.UnixEpoch, TimeSpan.Zero, context.UserContext));
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
        Assert.Equal(Verdict.Candidate, Assert.Single(vm.VisibleCards).Verdict);
        Assert.Single(vm.RecommendedCards);
        var originalReport = vm.LastResult;
        vm.ShowAllCommand.Execute(null);
        Assert.Equal(4, vm.VisibleCards.Count());
        Assert.Same(originalReport, vm.LastResult);
        vm.ShowRecommendationsCommand.Execute(null);
        Assert.Single(vm.VisibleCards);
        Assert.Equal([null, FindingCategory.Memory, FindingCategory.Power], vm.Categories.Select(c => c.Category));
        Assert.Equal([4, 3, 1], vm.Categories.Select(c => c.Count));
        Assert.True(vm.ExportCommand.CanExecute(null));

        var failed = Assert.Single(vm.Cards, card => card.Finding.Category == FindingCategory.Power);
        Assert.Equal(Strings.Verdict_CannotVerify, failed.VerdictText);
        Assert.True(failed.HasReason);
        Assert.Equal(DisplayText.Reason(CannotVerifyReason.ProbeError), failed.ReasonText);
    }

    /// <summary>온라인 확인을 켠 검사가 끝나야 '마지막 온라인 확인' 시각을 보여 주며, 로컬 검사만 한 동안에는 보이지 않는다.</summary>
    [Fact]
    public async Task 온라인_확인_검사_후에만_마지막_온라인_확인을_보여_준다()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()]);

        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.False(vm.HasLastOnlineCheck);
        Assert.Null(vm.LastOnlineCheckText);

        vm.IsOnlineCheckRequested = true;
        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.False(vm.HasLastOnlineCheck); // 요청만 했고 온라인 공급자 결과가 없으면 확인 시각도 갱신하지 않는다.
        Assert.Contains("NVIDIA", Strings.Toggle_OnlineCheck, StringComparison.Ordinal);
        Assert.Contains("Windows Update", Strings.Toggle_OnlineCheck, StringComparison.Ordinal);
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
        Assert.True(vm.IsResultListEmpty);
        Assert.Equal(Strings.Overview_NoCandidates, vm.OverviewTitle);
        Assert.Equal(1, vm.CannotVerifyCount);
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

    /// <summary>늦은 작업이 끝나면 재검사 없이 안내가 사라지고 과거 리포트는 유지됩니다.</summary>
    [Fact]
    public async Task LateCompletionClearsLiveNote()
    {
        var delayed = new DelayedProbe("fixture.delayed");
        using var vm = CreateViewModel([delayed]);
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.True(vm.HasDrainingNote);
        var report = vm.LastResult;
        Assert.False(vm.CanOpenCacheTools);
        var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.DrainingNote) && !vm.HasDrainingNote) { cleared.TrySetResult(); } };
        delayed.Complete();
        await cleared.Task.WaitAsync(WAIT_BOUND);
        Assert.Same(report, vm.LastResult);
        Assert.True(vm.CanOpenCacheTools);
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

    /// <summary>시스템 범위 모드에서는 배너가 보이고 검사 서비스가 시스템 프로브만 실행한다.</summary>
    [Fact]
    public async Task SystemOnlyModeShowsBannerAndLimitsScope()
    {
        var userProbe = new SuccessProbe("fixture.user") { Scope = ProbeScope.User, Category = FindingCategory.Startup };
        var vm = CreateViewModel([new FixtureMemoryProbe(), userProbe], userScope: UserScopeMode.SystemOnly, limitToSystemScope: true);
        Assert.True(vm.IsSystemOnly);
        Assert.True(vm.HasScopeBanner);
        Assert.Equal(Strings.Banner_SystemOnly, vm.ScopeBannerText);
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Contains(vm.LastResult!.Report.ProbeSummaries, s => s.Status == ProbeStatus.Skipped);
        Assert.Equal(0, userProbe.InvocationCount);
    }

    /// <summary>
    /// 대화형 세션 사용자를 확인하지 못해 시스템 범위만 검사하면 "다른 관리자 계정" 대신 확인 불가 배너를 보이고,
    /// 범위 제한(시스템만·정리 창 막기)은 같게 유지한다(최종 리뷰 이월 4).
    /// </summary>
    [Fact]
    public void UnresolvedSystemOnlyShowsScopeUnknownBanner()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()], elevation: new FakeElevationState(true), userScope: UserScopeMode.SystemOnly,
            limitToSystemScope: true, availability: new FixedActionAvailability(true), userScopeUnresolved: true);

        Assert.True(vm.IsSystemOnly);
        Assert.True(vm.HasScopeBanner);
        Assert.Equal(Strings.Banner_ScopeUnknown, vm.ScopeBannerText);
        Assert.NotEqual(Strings.Banner_SystemOnly, vm.ScopeBannerText);
        Assert.False(vm.CanOpenCacheTools);
    }

    /// <summary>전체 범위에서는 확인 불가 표시가 넘어와도 배너를 보이지 않는다(판정 불가면 항상 시스템만이라 실제로는 생기지 않는 조합).</summary>
    [Fact]
    public void FullScopeIgnoresUnresolvedFlag()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()], userScopeUnresolved: true);

        Assert.False(vm.HasScopeBanner);
        Assert.Null(vm.ScopeBannerText);
    }

    /// <summary>시스템 범위만 허용한 창에서는 사용자 캐시 정리로 진입하지 않아야 합니다(REV-016 재현 편입).</summary>
    [Fact]
    public void SystemOnlyMustNotOfferUserCacheActions()
    {
        using var vm = CreateReviewViewModel(UserScopeMode.SystemOnly, SpecTestFactory.Create());
        Assert.True(vm.HasScopeBanner);
        Assert.False(vm.CanOpenCacheTools);
    }

    /// <summary>사양 수집 중에는 조치 실행 창을 열 수 없어야 합니다(REV-018 재현 편입).</summary>
    [Fact]
    public async Task SpecLoadingMustBlockCacheActions()
    {
        var probe = new GatedSystemDetailsProbe();
        var spec = SpecTestFactory.Create(probes: [probe]);
        using var vm = CreateReviewViewModel(UserScopeMode.Full, spec);
        vm.ToggleSpecCommand.Execute(null);
        await probe.Started.WaitAsync(WAIT_BOUND);
        try
        {
            Assert.True(spec.IsLoading);
            Assert.False(vm.StartScanCommand.CanExecute(null));
            Assert.False(vm.CanOpenCacheTools);
        }
        finally
        {
            probe.Release();
            await spec.RefreshCommand.ExecutionTask!.WaitAsync(WAIT_BOUND);
        }
    }

    /// <summary>사양 읽기 시작·종료 시 정리 창 진입 가능 여부 변경을 알립니다(REV-018).</summary>
    [Fact]
    public async Task SpecLoadingNotifiesCacheToolsGate()
    {
        var probe = new GatedSystemDetailsProbe();
        var spec = SpecTestFactory.Create(probes: [probe]);
        using var vm = CreateReviewViewModel(UserScopeMode.Full, spec);
        var changes = new List<bool>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.CanOpenCacheTools)) { changes.Add(vm.CanOpenCacheTools); } };
        vm.ToggleSpecCommand.Execute(null);
        await probe.Started.WaitAsync(WAIT_BOUND);
        probe.Release();
        await spec.RefreshCommand.ExecutionTask!.WaitAsync(WAIT_BOUND);
        Assert.Equal([false, true], changes);
        Assert.True(vm.CanOpenCacheTools);
    }

    /// <summary>사양 프로브가 타임아웃 뒤에도 종료 중이면(Spec.IsDraining) 검사 시작과 정리 창을 막고, 끝나면 다시 허용한다(REV-017·REV-018).</summary>
    [Fact]
    public async Task 사양_종료_대기_중에는_검사와_정리를_시작하지_않는다()
    {
        var probe = new NonCooperativeSpecProbe();
        var spec = SpecTestFactory.Create(probes: [probe]);
        using var vm = CreateReviewViewModel(UserScopeMode.Full, spec);
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.CanOpenCacheTools) && vm.CanOpenCacheTools) { drained.TrySetResult(); } };
        try
        {
            vm.ToggleSpecCommand.Execute(null);
            await spec.RefreshCommand.ExecutionTask!.WaitAsync(WAIT_BOUND);

            Assert.False(spec.IsLoading);
            Assert.True(spec.IsDraining);
            Assert.False(vm.StartScanCommand.CanExecute(null));
            Assert.False(vm.CanOpenCacheTools);
            Assert.True(vm.HasSpecDrainingNote);

            probe.Release.TrySetResult();
            await drained.Task.WaitAsync(WAIT_BOUND);
            Assert.False(spec.IsDraining);
            Assert.True(vm.StartScanCommand.CanExecute(null));
            Assert.True(vm.CanOpenCacheTools);
            Assert.False(vm.HasSpecDrainingNote);
        }
        finally { probe.Release.TrySetResult(); }
    }

    /// <summary>
    /// Spec.IsDraining이 바뀔 때마다 검사 시작 명령의 CanExecuteChanged와 결과 화면 안내(HasSpecDrainingNote)·정리 창 관문 변경을 알린다(REV-017).
    /// </summary>
    [Fact]
    public void SpecDrainingNotifiesScanGateAndMainNote()
    {
        var spec = SpecTestFactory.Create();
        using var vm = CreateReviewViewModel(UserScopeMode.Full, spec);
        var canExecuteChanges = new List<bool>();
        var properties = new List<string?>();
        vm.StartScanCommand.CanExecuteChanged += (_, _) => canExecuteChanges.Add(vm.StartScanCommand.CanExecute(null));
        vm.PropertyChanged += (_, e) => properties.Add(e.PropertyName);

        spec.IsDraining = true;
        Assert.Equal([false], canExecuteChanges);
        Assert.Contains(nameof(vm.HasSpecDrainingNote), properties);
        Assert.Contains(nameof(vm.CanOpenCacheTools), properties);
        Assert.True(vm.HasSpecDrainingNote);

        properties.Clear();
        spec.IsDraining = false;
        Assert.Equal([false, true], canExecuteChanges);
        Assert.Contains(nameof(vm.HasSpecDrainingNote), properties);
        Assert.False(vm.HasSpecDrainingNote);
    }

    /// <summary>검사 프로브가 아직 종료 중이면(HasDrainingNote) 사양 새로 고침을 막고, 늦게 끝나면 다시 허용한다(REV-017 반대 방향).</summary>
    [Fact]
    public async Task 검사_종료_대기_중에는_사양_새로_고침을_하지_않는다()
    {
        var delayed = new DelayedProbe("fixture.delayed");
        using var vm = CreateViewModel([delayed]);
        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.True(vm.HasDrainingNote);
        Assert.False(vm.IsScanning);
        Assert.False(vm.Spec.RefreshCommand.CanExecute(null));

        var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.DrainingNote) && !vm.HasDrainingNote) { cleared.TrySetResult(); } };
        delayed.Complete();
        await cleared.Task.WaitAsync(WAIT_BOUND);
        Assert.True(vm.Spec.RefreshCommand.CanExecute(null));
    }

    /// <summary>독립 리뷰 재현과 같은 조건(관리자 권한·보호 위치 도구 있음)으로 뷰모델을 만든다.</summary>
    private MainViewModel CreateReviewViewModel(UserScopeMode scope, PcSpecViewModel spec)
        => CreateViewModel([], elevation: new FakeElevationState(true), userScope: scope,
            limitToSystemScope: scope == UserScopeMode.SystemOnly, availability: new FixedActionAvailability(true), spec: spec);

    /// <summary>전체 범위(대화형 사용자와 같은 계정)에서는 배너가 없다.</summary>
    [Fact]
    public void FullScopeHasNoBanner()
    {
        var vm = CreateViewModel([new FixtureMemoryProbe()]);
        Assert.Equal(UserScopeMode.Full, vm.UserScope);
        Assert.False(vm.IsSystemOnly);
        Assert.False(vm.HasScopeBanner);
        Assert.Null(vm.ScopeBannerText);
    }
}
