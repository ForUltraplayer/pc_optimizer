/**
 * @file    : ScanCoordinator.cs
 * @author  : rudals252
 * @brief   : 검사 한 번의 조율(중복 검사 억제, 범위/권한/온라인/종료 중 정책, 제한 병렬도, 사용자 취소)과 규칙 평가·리포트 조립
 */

// 기본 패키지
using System.Collections.Frozen;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 검사 한 번을 조율합니다: 프로브를 제한된 병렬도로 실행(Collect)하고, 규칙을 평가(Evaluate)한 뒤 리포트를 만듭니다(Report).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>권한·온라인·범위 정책에 맞지 않는 프로브는 호출하지 않고 Skipped로 남깁니다(시스템 범위 제한 검사에서 사용자별 프로브 포함).</item>
/// <item>프로브 예외·타임아웃·취소는 ProbeResult(Failed/Cancelled)로 흡수하고 다른 프로브에 전파하지 않습니다.</item>
/// <item>타임아웃된 호출은 끝날 때까지 "종료 중"으로 추적하고, 나중에 끝나도 결과를 합치지 않고 로그만 남깁니다.
/// 종료 중인 프로브는 다음 검사에서 다시 실행하지 않습니다.</item>
/// <item>사용자 취소 시 새 프로브를 시작하지 않고 곧바로 돌아옵니다. 취소에 협조하는 호출은 짧은 유예 시간 안에 끝나 Cancelled가 되고,
/// 취소를 무시하는 호출은 기다리지 않고 취소 완료가 아니라 "종료 중"(<see cref="ProbeSummary.IsStillRunning"/>, <see cref="DrainingProbeIds"/>)으로
/// 표시합니다. 이미 끝난 결과는 유지합니다.</item>
/// <item>동시에 두 검사를 실행하지 않습니다.</item>
/// </list>
/// 실제 스케줄러·OS 작업은 Core 밖(App/Probes)에 있습니다.
/// </remarks>
public sealed class ScanCoordinator
{
    private const string LOG_CATEGORY = nameof(ScanCoordinator);
    private const int SCAN_IDLE = 0;
    private const int SCAN_RUNNING = 1;

    private readonly IReadOnlyList<IProbe> _probes;
    private readonly FrozenDictionary<string, FindingCategory> _probeCategories;
    private readonly FrozenDictionary<string, TimeSpan> _probeTimeouts;
    private readonly RuleEvaluator _ruleEvaluator;
    private readonly ProbeExecutor _executor;
    private readonly ScanOptions _options;
    private readonly ScanReportVersions _versions;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;

    private int _scanState = SCAN_IDLE;

    /// <summary>
    /// 실행 조율기를 만듭니다.
    /// </summary>
    /// <param name="probes">프로브 목록(Id 중복 불가, 등록 순서가 실행·리포트 순서).</param>
    /// <param name="rules">판정 규칙 목록.</param>
    /// <param name="options">실행 설정.</param>
    /// <param name="versions">리포트에 기록할 버전.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="logger">공용 로거.</param>
    /// <exception cref="ArgumentException">프로브 Id가 중복되었거나 병렬도·타임아웃이 올바르지 않은 경우.</exception>
    public ScanCoordinator(
        IEnumerable<IProbe> probes,
        IEnumerable<IRule> rules,
        ScanOptions options,
        ScanReportVersions versions,
        IClock clock,
        IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxParallelism, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.CancellationGracePeriod, TimeSpan.Zero, nameof(options));

        IProbe[] probeList = [.. probes];
        var categories = new Dictionary<string, FindingCategory>(StringComparer.Ordinal);
        var timeouts = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        foreach (var probe in probeList)
        {
            if (!categories.TryAdd(probe.Id, probe.Category))
            {
                throw new ArgumentException($"프로브 Id '{probe.Id}'가 중복되었습니다.", nameof(probes));
            }

            var timeout = options.ProbeTimeoutOverrides.TryGetValue(probe.Id, out var overridden)
                ? overridden
                : probe.DefaultTimeout;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, nameof(probes));
            timeouts.Add(probe.Id, timeout);
        }

        _probes = probeList;
        _probeCategories = categories.ToFrozenDictionary(StringComparer.Ordinal);
        _probeTimeouts = timeouts.ToFrozenDictionary(StringComparer.Ordinal);
        _ruleEvaluator = new RuleEvaluator(rules, logger);
        _executor = new ProbeExecutor(clock, logger, options.CancellationGracePeriod);
        _options = options;
        _versions = versions;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 타임아웃·취소 뒤 아직 끝나지 않은(종료 중인) 프로브 ID 목록. UI가 "아직 종료 중"을 표시하는 데 씁니다.
    /// </summary>
    public IReadOnlyCollection<string> DrainingProbeIds => _executor.DrainingProbeIds;

    /// <summary>
    /// 검사를 한 번 실행합니다. 이미 검사가 진행 중이면 즉시 실패합니다.
    /// </summary>
    /// <param name="context">검사 컨텍스트.</param>
    /// <param name="cancellationToken">사용자 취소 토큰.</param>
    /// <returns>리포트와 스냅샷.</returns>
    /// <exception cref="InvalidOperationException">다른 검사가 진행 중인 경우(호출 즉시 던짐).</exception>
    public Task<ScanResult> RunScanAsync(ScanContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Interlocked.CompareExchange(ref _scanState, SCAN_RUNNING, SCAN_IDLE) != SCAN_IDLE)
        {
            throw new InvalidOperationException("이미 검사가 진행 중입니다. 두 검사를 동시에 실행할 수 없습니다.");
        }

        return RunExclusiveScanAsync(context, cancellationToken);
    }

    /// <summary>
    /// 실행 권한을 얻은 뒤 수집·평가·리포트를 수행하고, 끝나면 권한을 돌려준다.
    /// </summary>
    private async Task<ScanResult> RunExclusiveScanAsync(ScanContext context, CancellationToken cancellationToken)
    {
        try
        {
            var results = await CollectAsync(context, cancellationToken).ConfigureAwait(false);
            return BuildResult(context, results, cancellationToken.IsCancellationRequested);
        }
        finally
        {
            Volatile.Write(ref _scanState, SCAN_IDLE);
        }
    }

    /// <summary>
    /// 정책 검사 후 프로브를 제한된 병렬도로 실행하고 등록 순서대로 결과를 모은다.
    /// 사용자가 취소하면 아직 시작하지 않은 프로브는 시작하지 않고 Cancelled로 남긴다.
    /// </summary>
    private async Task<ProbeResult[]> CollectAsync(ScanContext context, CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(_options.MaxParallelism, _options.MaxParallelism);
        var runs = new Task<ProbeResult>[_probes.Count];

        for (var index = 0; index < _probes.Count; index++)
        {
            var probe = _probes[index];
            var skipped = TryCreatePolicySkip(probe, context);
            if (skipped is not null)
            {
                runs[index] = Task.FromResult(skipped);
                continue;
            }

            if (!await TryEnterGateAsync(gate, cancellationToken).ConfigureAwait(false))
            {
                runs[index] = Task.FromResult(ProbeExecutor.CreateIssueResult(
                    probe.Id, context, ProbeStatus.Cancelled, CannotVerifyReason.Cancelled, CoreStrings.ProbeIssue_CancelledBeforeStart, _clock.UtcNow, TimeSpan.Zero));
                continue;
            }

            runs[index] = RunInGateAsync(probe, context, gate, cancellationToken);
        }

        return await Task.WhenAll(runs).ConfigureAwait(false);
    }

    /// <summary>
    /// 범위·권한·온라인·종료 중 정책에 걸리면 호출하지 않고 Skipped 결과를 만든다. 걸리지 않으면 null.
    /// </summary>
    private ProbeResult? TryCreatePolicySkip(IProbe probe, ScanContext context)
    {
        if (probe.Scope == ProbeScope.User && context.LimitToSystemScope)
        {
            // 다른 계정으로 승격된 재검사: 그 계정의 HKCU·프로필을 원래 사용자 결과로 보이지 않도록 호출하지 않는다.
            return ProbeExecutor.CreateIssueResult(
                probe.Id, context, ProbeStatus.Skipped, CannotVerifyReason.Unsupported, CoreStrings.ProbeIssue_UserScopeExcluded, _clock.UtcNow, TimeSpan.Zero);
        }

        if (probe.RequiresElevation && !context.IsElevated)
        {
            return ProbeExecutor.CreateIssueResult(
                probe.Id, context, ProbeStatus.Skipped, CannotVerifyReason.ElevationRequired, CoreStrings.ProbeIssue_ElevationRequired, _clock.UtcNow, TimeSpan.Zero);
        }

        if (probe.RequiresNetwork && !context.OnlineCheckRequested)
        {
            return ProbeExecutor.CreateIssueResult(
                probe.Id, context, ProbeStatus.Skipped, CannotVerifyReason.NotRequested, CoreStrings.ProbeIssue_NotRequested, _clock.UtcNow, TimeSpan.Zero);
        }

        if (_executor.TryGetDrainingScan(probe.Id, out var drainingScanId))
        {
            _logger.Warn(
                LOG_CATEGORY,
                $"{ScanLogEvents.PROBE_DRAINING_SKIPPED} probe={probe.Id} scan={context.ScanId} drainingFromScan={drainingScanId}");
            return ProbeExecutor.CreateIssueResult(
                probe.Id, context, ProbeStatus.Skipped, CannotVerifyReason.ProbeError, CoreStrings.ProbeIssue_DrainingSkip, _clock.UtcNow, TimeSpan.Zero);
        }

        return null;
    }

    /// <summary>
    /// 병렬도 제한 슬롯을 얻는다. 사용자가 취소하면 false.
    /// 취소와 동시에 다른 프로브가 슬롯을 돌려줘 대기가 성공한 경우에도, 취소가 요청됐으면 슬롯을 돌려주고 false를 반환한다.
    /// </summary>
    private static async Task<bool> TryEnterGateAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            gate.Release();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 슬롯을 얻은 프로브를 실행하고, 조율기가 기다림을 멈추는 즉시(완료·타임아웃) 슬롯을 돌려준다.
    /// </summary>
    private async Task<ProbeResult> RunInGateAsync(
        IProbe probe,
        ScanContext context,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _executor.RunAsync(probe, context, _probeTimeouts[probe.Id], cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 스냅샷을 만들고 규칙 평가·상태 변환 Finding을 모아 리포트를 조립한다.
    /// </summary>
    private ScanResult BuildResult(ScanContext context, ProbeResult[] results, bool cancellationRequested)
    {
        var snapshot = new ScanSnapshot(context.ScanId, results);

        var ruleEvaluation = _ruleEvaluator.Evaluate(snapshot);
        var findings = new List<Finding>(ruleEvaluation.Findings);
        findings.AddRange(ProbeResultConverter.Convert(snapshot, _probeCategories));

        // 종료 중 표시는 어느 검사에서 시작한 호출이든 아직 끝나지 않았으면 유지한다(재검사에서 건너뛴 경우 포함).
        ProbeSummary[] summaries = [.. results.Select(result => new ProbeSummary(
            result.ProbeId,
            result.Status,
            result.Duration,
            result.Issues.Count,
            _executor.IsDraining(result.ProbeId)))];

        var report = new ScanReport
        {
            ScanId = context.ScanId,
            StartedAtUtc = context.StartedAtUtc,
            CompletedAtUtc = _clock.UtcNow,
            Outcome = ScanOutcomeResolver.Resolve(cancellationRequested, results, ruleEvaluation.HasFailures),
            AppVersion = _versions.AppVersion,
            RulesVersion = _versions.RulesVersion,
            UserContext = context.UserContext,
            ProbeSummaries = Array.AsReadOnly(summaries),
            Findings = findings.AsReadOnly(),
        };

        return new ScanResult(report, snapshot);
    }
}
