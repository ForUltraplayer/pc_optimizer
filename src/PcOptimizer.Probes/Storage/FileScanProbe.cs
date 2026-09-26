/**
 * @file    : FileScanProbe.cs
 * @author  : rudals252
 * @brief   : 공유 파일 스캔 서비스 결과로 보호 루트·루트별 관측 합계와 건너뜀·표준 임시 위치·미분류 대용량 폴더 후보를 측정값으로 내는 사용자 범위 프로브(보호 정책 무효면 Failed, 읽지 못한 항목이 있으면 Partial)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 파일 스캔 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="FileScanProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>보호 정책이 없거나 무효이면 순회하지 않고 Failed + ProbeError(Issue 요약은 리소스 문장)를 돌려줍니다. 다른 검사에는 영향이 없습니다.</item>
/// <item>접근 거부·사용 중·시간 초과로 읽지 못한 항목이 있으면 Partial과 사유별 개수 Issue를 돌려줍니다(경로는 Issue에 넣지 않음).</item>
/// <item>사용자 프로필을 읽으므로 사용자 범위이며, 관리자 권한은 필요 없습니다(시스템 임시 위치는 접근 거부로 남음).</item>
/// </list>
/// </remarks>
public sealed class FileScanProbe : IProbe
{
    /// <summary>기본 타임아웃 상한.</summary>
    public static readonly TimeSpan FILE_SCAN_TIMEOUT_CAP = TimeSpan.FromMinutes(10);

    /// <summary>볼륨 예산 외에 정책 해석·측정값 생성에 더 주는 여유 시간.</summary>
    public static readonly TimeSpan FILE_SCAN_TIMEOUT_MARGIN = ScanOptions.DEFAULT_LOCAL_TIMEOUT;

    private readonly IFileScanService _service;
    private readonly IClock _clock;

    /// <summary>
    /// 프로브를 만듭니다.
    /// </summary>
    /// <param name="service">공유 파일 스캔 서비스.</param>
    /// <param name="clock">UTC 시계.</param>
    public FileScanProbe(IFileScanService service, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(clock);
        _service = service;
        _clock = clock;

        var perVolume = service.BudgetPerVolume * service.CountPlannedVolumes();
        var total = perVolume + FILE_SCAN_TIMEOUT_MARGIN;
        DefaultTimeout = total > FILE_SCAN_TIMEOUT_CAP ? FILE_SCAN_TIMEOUT_CAP : total;
    }

    /// <inheritdoc />
    public string Id => FileScanProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Storage;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.User;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout { get; }

    /// <inheritdoc />
    public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var result = await _service.GetOrScanAsync(context, ct).ConfigureAwait(false);
        var observedAt = _clock.UtcNow;
        if (!result.IsPolicyValid)
        {
            return new ProbeResult(
                Id,
                ProbeStatus.Failed,
                FileScanMeasurements.ForInvalidPolicy(result.PolicyError, observedAt),
                [new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.FileScan_PolicyInvalid, result.PolicyError))],
                observedAt,
                TimeSpan.Zero,
                context.UserContext);
        }

        ct.ThrowIfCancellationRequested();
        var plan = result.Plan!;
        var selection = UnclassifiedFolderSelector.Select(
            result.Traversal!.GetDirectoryAggregates(plan.SelectionRoots), plan.SelectionRoots, plan.ExcludedPaths);
        var measurements = FileScanMeasurements.Build(result, selection, observedAt);
        var issues = CollectIssues(result.Traversal);
        var scanned = result.Traversal.Roots.Count(root => root.State == RootScanState.Scanned);
        var status = scanned == 0 && issues.Count > 0
            ? ProbeStatus.Failed
            : issues.Count > 0 ? ProbeStatus.Partial : ProbeStatus.Success;
        return new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext);
    }

    /// <summary>
    /// 읽지 못한 항목을 사유별 개수 Issue로 모은다(경로 없음).
    /// </summary>
    private static List<Issue> CollectIssues(TraversalResult traversal)
    {
        var skips = traversal.Roots
            .Where(root => root.Totals is not null)
            .Aggregate(default(SkipCounts), (sum, root) => sum.Add(root.Totals!.Skips));
        var issues = new List<Issue>();
        var deniedRoots = traversal.Roots.Count(root => root.State == RootScanState.AccessDenied);
        if (skips.AccessDenied + deniedRoots > 0)
        {
            issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.FileScan_AccessDenied, skips.AccessDenied + deniedRoots)));
        }

        if (skips.InUse > 0)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.FileScan_InUse, skips.InUse)));
        }

        foreach (var volume in traversal.Volumes.Where(volume => volume.TimedOut))
        {
            issues.Add(new Issue(CannotVerifyReason.Timeout, ProbeText.Format(ProbeStrings.FileScan_VolumeTimedOut, volume.VolumeRoot)));
        }

        var errorRoots = traversal.Roots.Count(root => root.State == RootScanState.Error);
        if (errorRoots > 0)
        {
            issues.Add(new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.FileScan_RootError, errorRoots)));
        }

        return issues;
    }
}
