/**
 * @file    : ScanServiceTests.cs
 * @author  : rudals252
 * @brief   : 검사 서비스의 컨텍스트 구성(권한·온라인 요청·검사 단위 익명 ID)과 조율기 실행·분류 노출을 가짜 프로브로 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="ScanService"/>를 가짜 프로브·규칙으로 검증합니다. 실제 OS 수집은 하지 않습니다.
/// </summary>
public sealed class ScanServiceTests
{
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

    /// <summary>기본 구성은 P3a까지의 로컬 프로브를 등록하고, 네트워크 프로브는 없으며 관리자 권한 프로브는 TRIM 정책 하나뿐이다.</summary>
    [Fact]
    public void 기본_구성은_로컬_프로브만_등록한다()
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
            ],
            service.Categories);
        Assert.All(service.Probes, probe => Assert.False(probe.RequiresNetwork));
        var elevated = Assert.Single(service.Probes, probe => probe.RequiresElevation);
        Assert.Equal(TrimPolicyProbeContract.PROBE_ID, elevated.Id);
        Assert.Equal(service.Probes.Count, service.Probes.Select(probe => probe.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
