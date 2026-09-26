/**
 * @file    : ScanServiceTests.cs
 * @author  : rudals252
 * @brief   : 검사 서비스의 컨텍스트 구성(권한·온라인 요청·검사 단위 익명 ID)과 조율기 실행·분류 노출, 기본 구성 프로브의 범위 명시, 프로세스 토큰 SID와 대화형 사용자 SID 비교에 따른 사용자 범위 제한을 가짜 프로브로 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="ScanService"/>를 가짜 프로브·규칙으로 검증합니다. 실제 OS 수집은 하지 않습니다.
/// </summary>
public sealed class ScanServiceTests
{
    private const string ORIGIN_SID = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string OTHER_ADMIN_SID = "S-1-5-21-1111111111-2222222222-3333333333-500";

    private static readonly ScanReportVersions VERSIONS = new("test-app", "test-rules");

    /// <summary>
    /// 받은 검사 컨텍스트를 기록하는 프로브입니다.
    /// </summary>
    private sealed class ContextCapturingProbe : IProbe
    {
        /// <summary>받은 컨텍스트 목록.</summary>
        public List<ScanContext> Contexts { get; } = [];

        /// <inheritdoc />
        public string Id => "capture";

        /// <inheritdoc />
        public FindingCategory Category => FindingCategory.Power;

        /// <inheritdoc />
        public bool RequiresElevation => false;

        /// <inheritdoc />
        public bool RequiresNetwork => false;

        /// <inheritdoc />
        public ProbeScope Scope => ProbeScope.System;

        /// <inheritdoc />
        public TimeSpan DefaultTimeout => FakeProbe.GENEROUS_TIMEOUT;

        /// <inheritdoc />
        public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
        {
            lock (Contexts)
            {
                Contexts.Add(context);
            }

            return Task.FromResult(new ProbeResult(Id, ProbeStatus.Success, [], [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext));
        }
    }

    /// <summary>
    /// 지정한 권한 판정 함수로 서비스를 만든다.
    /// </summary>
    private static ScanService CreateService(IEnumerable<IProbe> probes, bool elevated = false)
    {
        return new ScanService(probes, [], new ScanOptions(), VERSIONS, new FakeClock(), NullAppLogger.Instance, () => elevated);
    }

    /// <summary>컨텍스트에 권한 여부와 온라인 요청이 그대로 들어간다.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task 컨텍스트에_권한과_온라인_요청을_담는다(bool elevated, bool online)
    {
        var probe = new ContextCapturingProbe();
        var service = CreateService([probe], elevated);

        var result = await service.RunScanAsync(online, CancellationToken.None);

        var context = Assert.Single(probe.Contexts);
        Assert.Equal(elevated, context.IsElevated);
        Assert.Equal(online, context.OnlineCheckRequested);
        Assert.Equal(context.ScanId, result.Report.ScanId);
        Assert.Equal(VERSIONS.AppVersion, result.Report.AppVersion);
    }

    /// <summary>익명 사용자 ID는 검사마다 새로 만들고 실제 사용자명·PC명을 담지 않는다.</summary>
    [Fact]
    public async Task 익명_ID는_검사마다_다르고_실명이_아니다()
    {
        var probe = new ContextCapturingProbe();
        var service = CreateService([probe]);

        await service.RunScanAsync(false, CancellationToken.None);
        await service.RunScanAsync(false, CancellationToken.None);

        Assert.Equal(2, probe.Contexts.Count);
        var first = probe.Contexts[0].UserContext.AnonymizedUserId;
        var second = probe.Contexts[1].UserContext.AnonymizedUserId;
        Assert.NotEqual(first, second);
        Assert.NotEqual(probe.Contexts[0].ScanId, probe.Contexts[1].ScanId);
        foreach (var id in new[] { first, second })
        {
            Assert.DoesNotContain(Environment.UserName, id, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.MachineName, id, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>등록한 프로브의 분류를 등록 순서대로 중복 없이 노출한다.</summary>
    [Fact]
    public void 등록한_분류를_노출한다()
    {
        var service = CreateService(
        [
            new SuccessProbe("memory") { Category = FindingCategory.Memory },
            new SuccessProbe("power") { Category = FindingCategory.Power },
            new SuccessProbe("power2") { Category = FindingCategory.Power },
        ]);

        Assert.Equal([FindingCategory.Memory, FindingCategory.Power], service.Categories);
    }

    /// <summary>
    /// 기본 구성은 P5까지의 로컬 프로브와 P6 온라인 프로브를 등록한다. 네트워크 프로브는 NVIDIA 조회와 Windows Update 검색 둘뿐이며
    /// (온라인 확인을 켠 검사에서만 실행), 관리자 권한 프로브는 TRIM 정책 하나뿐이다.
    /// </summary>
    [Fact]
    public void 기본_구성의_네트워크_프로브는_NVIDIA와_WUA뿐이다()
    {
        var service = ScanService.CreateDefault(NullAppLogger.Instance);

        Assert.Equal(
            [
                FindingCategory.Memory,
                FindingCategory.Power,
                FindingCategory.Display,
                FindingCategory.Driver,
                FindingCategory.Graphics,
                FindingCategory.Security,
                FindingCategory.Storage,
                FindingCategory.Startup,
                FindingCategory.AppCache,
            ],
            service.Categories);
        Assert.Equal(
            [NvidiaLookupProbeContract.PROBE_ID, WindowsUpdateProbeContract.PROBE_ID],
            service.Probes.Where(probe => probe.RequiresNetwork).Select(probe => probe.Id));
        var elevated = Assert.Single(service.Probes, probe => probe.RequiresElevation);
        Assert.Equal(TrimPolicyProbeContract.PROBE_ID, elevated.Id);
        Assert.Equal(service.Probes.Count, service.Probes.Select(probe => probe.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 기본 구성의 모든 프로브는 범위를 명시하며, HKCU·사용자 프로필을 읽는 프로브(게임 모드·시작 프로그램·파일 스캔·앱 캐시)만 사용자 범위다.
    /// 새 프로브를 등록하면 이 목록에 범위를 추가해야 통과한다.
    /// </summary>
    [Fact]
    public void 기본_구성의_모든_프로브는_범위를_명시한다()
    {
        var expected = new Dictionary<string, ProbeScope>(StringComparer.Ordinal)
        {
            [MemoryProbeContract.PROBE_ID] = ProbeScope.System,
            [PowerProbeContract.PROBE_ID] = ProbeScope.System,
            [DisplayProbeContract.PROBE_ID] = ProbeScope.System,
            [SystemInfoProbeContract.PROBE_ID] = ProbeScope.System,
            [SystemDetailsProbeContract.PROBE_ID] = ProbeScope.System,
            [GraphicsSettingsProbeContract.PROBE_ID] = ProbeScope.System,
            [GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID] = ProbeScope.User,
            [SecurityStatusProbeContract.PROBE_ID] = ProbeScope.System,
            [GpuProbeContract.PROBE_ID] = ProbeScope.System,
            [NvidiaLookupProbeContract.PROBE_ID] = ProbeScope.System,
            [WindowsUpdateProbeContract.PROBE_ID] = ProbeScope.System,
            [VolumeProbeContract.PROBE_ID] = ProbeScope.System,
            [PhysicalDiskProbeContract.PROBE_ID] = ProbeScope.System,
            [TrimPolicyProbeContract.PROBE_ID] = ProbeScope.System,
            [StartupItemsProbeContract.PROBE_ID] = ProbeScope.User,
            [FileScanProbeContract.PROBE_ID] = ProbeScope.User,
            [AppCacheProbeContract.PROBE_ID] = ProbeScope.User,
        };

        var service = ScanService.CreateDefault(NullAppLogger.Instance);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), service.Probes.Select(probe => probe.Id).Order(StringComparer.Ordinal));
        Assert.All(service.Probes, probe =>
        {
            Assert.True(Enum.IsDefined(probe.Scope), probe.Id);
            Assert.Equal(expected[probe.Id], probe.Scope);
        });
    }

    /// <summary>
    /// 대화형 로그온 사용자(원래 계정) SID와 프로세스 토큰 SID로 사용자 범위를 정하고, 그에 맞는 범위 제한으로 서비스를 만든다.
    /// </summary>
    private static (ScanService Service, UserScopeMode Mode) CreateElevatedService(string currentSid, IEnumerable<IProbe> probes)
    {
        var mode = UserScopeResolver.Resolve(currentSid, ORIGIN_SID);
        var service = new ScanService(
            probes,
            [],
            new ScanOptions(),
            VERSIONS,
            new FakeClock(),
            NullAppLogger.Instance,
            () => true,
            limitToSystemScope: mode == UserScopeMode.SystemOnly);
        return (service, mode);
    }

    /// <summary>같은 SID(대소문자 무시)로 승격되면 사용자 범위 프로브까지 모두 실행한다(관리자 권한 컨텍스트).</summary>
    [Fact]
    public async Task 같은_SID면_모든_프로브를_실행한다()
    {
        var system = new SuccessProbe("system") { Scope = ProbeScope.System };
        var user = new SuccessProbe("user") { Scope = ProbeScope.User };
        var (service, mode) = CreateElevatedService(ORIGIN_SID.ToLowerInvariant(), [system, user]);

        var result = await service.RunScanAsync(false, CancellationToken.None);

        Assert.Equal(UserScopeMode.Full, mode);
        Assert.False(service.LimitsToSystemScope);
        Assert.Equal(1, system.InvocationCount);
        Assert.Equal(1, user.InvocationCount);
        Assert.All(result.Report.ProbeSummaries, summary => Assert.Equal(ProbeStatus.Success, summary.Status));
        Assert.True(result.Report.UserContext.IsElevated);
        Assert.DoesNotContain(result.Report.Findings, f => f.Verdict == Verdict.CannotVerify);
    }

    /// <summary>
    /// 다른 SID(다른 관리자 계정)로 승격되면 시스템 범위 프로브만 실행하고, 사용자 범위 프로브는 호출하지 않은 채
    /// Skipped(Unsupported)와 "관리자 계정으로 로그인해서 실행" 안내 상세로 남긴다(관리자 계정의 HKCU를 원래 사용자 결과로 보이지 않음).
    /// </summary>
    [Fact]
    public async Task 다른_SID면_시스템_범위만_실행한다()
    {
        var system = new SuccessProbe("system") { Scope = ProbeScope.System, Category = FindingCategory.Memory };
        var user = new SuccessProbe("user") { Scope = ProbeScope.User, Category = FindingCategory.Startup };
        var (service, mode) = CreateElevatedService(OTHER_ADMIN_SID, [system, user]);

        var result = await service.RunScanAsync(false, CancellationToken.None);

        Assert.Equal(UserScopeMode.SystemOnly, mode);
        Assert.True(service.LimitsToSystemScope);
        Assert.Equal(1, system.InvocationCount);
        Assert.Equal(0, user.InvocationCount);
        Assert.Equal(ProbeStatus.Success, Assert.Single(result.Report.ProbeSummaries, s => s.ProbeId == "system").Status);
        Assert.Equal(ProbeStatus.Skipped, Assert.Single(result.Report.ProbeSummaries, s => s.ProbeId == "user").Status);
        var skipped = Assert.Single(result.Report.Findings, f => f.Category == FindingCategory.Startup);
        Assert.Equal(Verdict.CannotVerify, skipped.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, skipped.CannotVerifyReason);
        Assert.Equal(CoreStrings.ProbeIssue_UserScopeExcluded, skipped.Detail);
        Assert.Contains("관리자 계정으로 로그인해서 실행하면 전부 검사할 수 있어요", skipped.Detail, StringComparison.Ordinal);
        Assert.Empty(skipped.Measured);
    }
}
