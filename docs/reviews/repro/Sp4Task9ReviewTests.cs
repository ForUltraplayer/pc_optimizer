/**
 * @file    : Sp4Task9ReviewTests.cs
 * @author  : rudals252
 * @brief   : SP4 Task 9 시점의 사용자 조치 범위·사양 실행 경계·기존 종료 실패 재현
 */
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.App.Fakes;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>66cd7cb 스냅샷의 테스트 프로젝트에 복사하여 실행합니다. 개인 파일·설정은 변경하지 않습니다.</summary>
public sealed class Sp4Task9ReviewTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(3);

    /// <summary>시스템 범위만 허용한 창에서는 사용자 캐시 정리로 진입하지 않아야 합니다.</summary>
    [Fact]
    public void SystemOnlyMustNotOfferUserCacheActions()
    {
        using var vm = Create(UserScopeMode.SystemOnly, SpecTestFactory.Create());
        Assert.True(vm.HasScopeBanner);
        Assert.False(vm.CanOpenCacheTools);
    }

    /// <summary>사양 수집 중에는 조치 실행 창을 열 수 없어야 합니다.</summary>
    [Fact]
    public async Task SpecLoadingMustBlockCacheActions()
    {
        var probe = new GatedSystemDetailsProbe();
        var spec = SpecTestFactory.Create(probes: [probe]);
        using var vm = Create(UserScopeMode.Full, spec);
        vm.ToggleSpecCommand.Execute(null);
        await probe.Started.WaitAsync(Bound);
        try
        {
            Assert.True(spec.IsLoading);
            Assert.False(vm.StartScanCommand.CanExecute(null));
            Assert.False(vm.CanOpenCacheTools);
        }
        finally
        {
            probe.Release();
            await spec.RefreshCommand.ExecutionTask!.WaitAsync(Bound);
        }
    }

    /// <summary>사양 수집에서 타임아웃됐지만 아직 실행 중인 프로브를 재진입시키지 않아야 합니다.</summary>
    [Fact]
    public async Task TimedOutSpecMustNotReenterLiveSharedProbe()
    {
        var probe = new NonCooperativeSpecProbe();
        var service = new PcSpecService([probe], new FakeClock(), NullAppLogger.Instance, TestContexts.Normal);
        try
        {
            await service.CaptureAsync(default).WaitAsync(Bound);
            Assert.False(probe.Release.Task.IsCompleted);
            await service.CaptureAsync(default).WaitAsync(Bound);
            Assert.Equal(1, probe.Calls);
        }
        finally { probe.Release.TrySetResult(); }
    }

    /// <summary>기존 REV-010: 트리 종료의 AggregateException도 실행 이력을 보존하는 경로로 처리해야 합니다.</summary>
    [Fact]
    public async Task AggregateKillFailureMustNotEscapeGuard()
    {
        var guard = new CacheProcessGuard();
        var log = new RecordingLogger();
        var exited = false;
        var stopped = await guard.StopAsync(() => exited,
            () => throw new AggregateException(new System.ComponentModel.Win32Exception("fixture")),
            () => { exited = true; return Task.CompletedTask; }, () => { }, log);
        Assert.True(stopped);
    }

    private static MainViewModel Create(UserScopeMode scope, PcSpecViewModel spec)
    {
        var logger = NullAppLogger.Instance;
        var service = new ScanService([], [], new ScanOptions(), new ScanReportVersions("review", "review"),
            new FakeClock(), logger, () => true, scope == UserScopeMode.SystemOnly);
        return new MainViewModel(service, new ReportExporter(new PersonalDataScrubber(null, null, null)),
            new FixedExportPathPicker(null), new SettingsUriPolicy(logger, _ => { }), new LinkPolicy(null, logger, _ => { }),
            new ImmediateUiDispatcher(), logger, new FakeElevationState(true), scope, new FixedActionAvailability(true), spec);
    }

    private sealed class NonCooperativeSpecProbe : IProbe
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public string Id => SystemDetailsProbeContract.PROBE_ID;
        public FindingCategory Category => FindingCategory.Driver;
        public bool RequiresElevation => false;
        public bool RequiresNetwork => false;
        public ProbeScope Scope => ProbeScope.System;
        public TimeSpan DefaultTimeout => TimeSpan.FromMilliseconds(40);
        public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
        {
            Calls++;
            await Release.Task;
            return new(Id, ProbeStatus.Success, [], [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext);
        }
    }
}
