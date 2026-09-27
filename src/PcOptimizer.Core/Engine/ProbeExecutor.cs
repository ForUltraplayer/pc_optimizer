/**
 * @file    : ProbeExecutor.cs
 * @author  : rudals252
 * @brief   : 프로브 한 번의 격리 실행(타임아웃·취소 즉시 반환·예외 흡수, 결과 검증)과 끝나지 않은 호출의 종료 중 추적·늦은 결과 폐기
 */

// 기본 패키지
using System.Collections.Concurrent;
using System.Diagnostics;
using PcOptimizer.Core.Actions;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Engine;

/// <summary>
/// 프로브 한 번을 격리해 실행합니다. 예외·타임아웃·취소를 ProbeResult로 흡수하며 예외를 던지지 않습니다.
/// 제한 시간 안에 끝나지 않았거나 사용자 취소 뒤 짧은 유예 시간 안에 끝나지 않은 호출은 기다림을 멈추고 "종료 중"으로 추적하며,
/// 나중에 끝나면 결과를 버리고 로그만 남깁니다.
/// </summary>
internal sealed class ProbeExecutor
{
    private const string LOG_CATEGORY = nameof(ScanCoordinator);

    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _cancellationGracePeriod;

    /// <summary>끝나지 않은 호출: 프로브 ID → 그 호출을 시작한 검사 ID.</summary>
    private readonly ConcurrentDictionary<string, Guid> _draining = new(StringComparer.Ordinal);

    /// <summary>
    /// 실행기를 만듭니다.
    /// </summary>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="cancellationGracePeriod">사용자 취소 뒤 협조하는 프로브가 끝나기를 기다리는 짧은 유예 시간.</param>
    public ProbeExecutor(IClock clock, IAppLogger logger, TimeSpan cancellationGracePeriod)
    {
        _clock = clock;
        _logger = logger;
        _cancellationGracePeriod = cancellationGracePeriod;
    }

    /// <summary>
    /// 아직 끝나지 않은(종료 중인) 프로브 ID 목록.
    /// </summary>
    public IReadOnlyCollection<string> DrainingProbeIds => [.. _draining.Keys];

    /// <summary>종료 중 목록이 바뀌면 실행 스레드에서 알립니다.</summary>
    public event EventHandler? DrainingChanged;

    /// <summary>
    /// 프로브의 이전 호출이 아직 종료 중인지 확인합니다.
    /// </summary>
    /// <param name="probeId">프로브 ID.</param>
    /// <param name="scanId">종료 중인 호출을 시작한 검사 ID.</param>
    /// <returns>종료 중이면 true.</returns>
    public bool TryGetDrainingScan(string probeId, out Guid scanId)
    {
        return _draining.TryGetValue(probeId, out scanId);
    }

    /// <summary>
    /// 어느 검사에서 시작했든 그 프로브의 호출이 아직 끝나지 않았는지(종료 중인지) 확인합니다.
    /// 재검사에서 건너뛴 프로브도 이전 호출이 끝날 때까지 "종료 중"으로 표시하기 위함입니다.
    /// </summary>
    /// <param name="probeId">프로브 ID.</param>
    /// <returns>종료 중이면 true.</returns>
    public bool IsDraining(string probeId)
    {
        return _draining.ContainsKey(probeId);
    }

    /// <summary>
    /// 프로브를 실행합니다. 이 작업은 예외를 던지지 않고 항상 결과를 돌려줍니다.
    /// </summary>
    /// <param name="probe">프로브.</param>
    /// <param name="context">검사 컨텍스트.</param>
    /// <param name="timeout">이 호출의 제한 시간.</param>
    /// <param name="userToken">사용자 취소 토큰.</param>
    /// <param name="lease">실제 프로브 작업을 추적할 호출 서비스의 소유권입니다.</param>
    /// <returns>수집 결과 또는 실패·취소·타임아웃 결과.</returns>
    public async Task<ProbeResult> RunAsync(
        IProbe probe,
        ScanContext context,
        TimeSpan timeout,
        CancellationToken userToken, IOperationLease? lease = null)
    {
        var startedAt = _clock.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var timeoutCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userToken, timeoutCts.Token);
        var tokenSourcesHandedOff = false;
        var userCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var userCancelRegistration = userToken.Register(() => userCancelled.TrySetResult());

        try
        {
            timeoutCts.CancelAfter(timeout);
            var probeToken = linkedCts.Token;

            // 스레드를 동기적으로 막는 프로브도 조율기를 멈추지 못하도록 스레드 풀에서 시작한다.
            var probeTask = Task.Run(() => probe.RunAsync(context, probeToken), CancellationToken.None);
            lease?.Track(probeTask);

            using var deadlineCts = new CancellationTokenSource();
            var deadline = Task.Delay(timeout, deadlineCts.Token);
            var first = await Task.WhenAny(probeTask, deadline, userCancelled.Task).ConfigureAwait(false);

            if (first == userCancelled.Task)
            {
                // 사용자 취소: 취소 토큰에 협조하는 프로브가 끝날 짧은 유예만 주고, 무시하는 프로브는 기다리지 않는다.
                var grace = Task.Delay(_cancellationGracePeriod, deadlineCts.Token);
                first = await Task.WhenAny(probeTask, deadline, grace).ConfigureAwait(false);
            }

            if (first != probeTask)
            {
                // 호출이 끝나지 않았다. 토큰 소스는 호출이 실제로 끝날 때 정리하도록 넘긴다.
                tokenSourcesHandedOff = true;
                StartDraining(probe.Id, context.ScanId, probeTask, timeoutCts, linkedCts);
                return CreateUnfinishedResult(probe, context, timeout, userToken, startedAt, stopwatch.Elapsed);
            }

            await deadlineCts.CancelAsync().ConfigureAwait(false);
            return await ClassifyCompletedAsync(
                probe, context, probeTask, timeoutCts.Token, userToken, startedAt, stopwatch).ConfigureAwait(false);
        }
        finally
        {
            if (!tokenSourcesHandedOff)
            {
                linkedCts.Dispose();
                timeoutCts.Dispose();
            }
        }
    }

    /// <summary>
    /// Issue 하나를 담은 측정값 없는 결과를 만듭니다.
    /// </summary>
    /// <param name="probeId">프로브 ID.</param>
    /// <param name="context">검사 컨텍스트.</param>
    /// <param name="status">상태.</param>
    /// <param name="reason">사유 코드.</param>
    /// <param name="summary">요약.</param>
    /// <param name="startedAt">시작 시각(UTC).</param>
    /// <param name="duration">소요 시간.</param>
    /// <returns>결과.</returns>
    public static ProbeResult CreateIssueResult(
        string probeId,
        ScanContext context,
        ProbeStatus status,
        CannotVerifyReason reason,
        string summary,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        return new ProbeResult(
            probeId,
            status,
            [],
            [new Issue(reason, summary)],
            startedAt,
            duration,
            context.UserContext);
    }

    /// <summary>
    /// 끝난 호출의 결과를 분류한다: 정상 결과는 검증 후 시각을 채우고, 예외·취소는 Failed/Cancelled로 바꾼다.
    /// </summary>
    private async Task<ProbeResult> ClassifyCompletedAsync(
        IProbe probe,
        ScanContext context,
        Task<ProbeResult> probeTask,
        CancellationToken timeoutToken,
        CancellationToken userToken,
        DateTimeOffset startedAt,
        Stopwatch stopwatch)
    {
        try
        {
            var result = await probeTask.ConfigureAwait(false);
            return NormalizeResult(probe, context, result, startedAt, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (userToken.IsCancellationRequested)
        {
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Cancelled, CannotVerifyReason.Cancelled, CoreStrings.ProbeIssue_Cancelled, startedAt, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
        {
            _logger.Warn(LOG_CATEGORY, $"{ScanLogEvents.PROBE_TIMED_OUT} probe={probe.Id} scan={context.ScanId} finished=true");
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Failed, CannotVerifyReason.Timeout, CoreStrings.ProbeIssue_Timeout, startedAt, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            // 예외 원문 메시지에는 개인 경로가 들어갈 수 있으므로 모델과 로그 어디에도 담지 않고 형식 이름만 남긴다.
            _logger.Warn(LOG_CATEGORY, $"{ScanLogEvents.PROBE_FAILED} probe={probe.Id} scan={context.ScanId} error={ex.GetType().Name}");
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Failed, CannotVerifyReason.ProbeError, ex.GetType().Name, startedAt, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 프로브가 돌려준 결과를 검증하고 실행기가 잰 시작 시각·소요 시간으로 덮어쓴다.
    /// null 결과, null 측정값/Issue 목록(또는 null 항목), 다른 ID의 결과는 성공으로 받지 않고 Failed/ProbeError로 바꾼다.
    /// 잘못된 결과 하나가 리포트 조립을 깨뜨려 다른 프로브 결과까지 잃지 않게 하기 위함이다.
    /// </summary>
    private ProbeResult NormalizeResult(
        IProbe probe,
        ScanContext context,
        ProbeResult? result,
        DateTimeOffset startedAt,
        TimeSpan elapsed)
    {
        if (result is null)
        {
            _logger.Warn(LOG_CATEGORY, $"{ScanLogEvents.PROBE_FAILED} probe={probe.Id} scan={context.ScanId} error=NullResult");
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Failed, CannotVerifyReason.ProbeError, CoreStrings.ProbeIssue_NullResult, startedAt, elapsed);
        }

        if (result.Measurements is null
            || result.Issues is null
            || result.Measurements.Any(measurement => measurement is null)
            || result.Issues.Any(issue => issue is null))
        {
            _logger.Warn(LOG_CATEGORY, $"{ScanLogEvents.PROBE_FAILED} probe={probe.Id} scan={context.ScanId} error=NullCollection");
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Failed, CannotVerifyReason.ProbeError, CoreStrings.ProbeIssue_NullCollection, startedAt, elapsed);
        }

        if (!string.Equals(result.ProbeId, probe.Id, StringComparison.Ordinal))
        {
            _logger.Warn(LOG_CATEGORY, $"{ScanLogEvents.PROBE_FAILED} probe={probe.Id} scan={context.ScanId} error=MismatchedProbeId");
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Failed, CannotVerifyReason.ProbeError, CoreStrings.ProbeIssue_MismatchedId, startedAt, elapsed);
        }

        return result with { StartedAtUtc = startedAt, Duration = elapsed };
    }

    /// <summary>
    /// 기다림을 멈춘 시점에 끝나지 않은 호출의 결과를 만든다. 사용자 취소 뒤라면 Cancelled(아직 종료 중), 아니면 Failed/Timeout.
    /// </summary>
    private ProbeResult CreateUnfinishedResult(
        IProbe probe,
        ScanContext context,
        TimeSpan timeout,
        CancellationToken userToken,
        DateTimeOffset startedAt,
        TimeSpan elapsed)
    {
        if (userToken.IsCancellationRequested)
        {
            _logger.Warn(LOG_CATEGORY, $"{ScanLogEvents.PROBE_STILL_RUNNING_AFTER_CANCEL} probe={probe.Id} scan={context.ScanId}");
            return CreateIssueResult(
                probe.Id, context, ProbeStatus.Cancelled, CannotVerifyReason.Cancelled, CoreStrings.ProbeIssue_CancelledStillRunning, startedAt, elapsed);
        }

        _logger.Warn(
            LOG_CATEGORY,
            $"{ScanLogEvents.PROBE_TIMED_OUT} probe={probe.Id} scan={context.ScanId} timeoutMs={timeout.TotalMilliseconds} finished=false");
        return CreateIssueResult(
            probe.Id, context, ProbeStatus.Failed, CannotVerifyReason.Timeout, CoreStrings.ProbeIssue_Timeout, startedAt, elapsed);
    }

    /// <summary>
    /// 끝나지 않은 호출을 "종료 중"으로 등록하고, 끝나면 결과를 버리고 로그를 남기도록 예약한다.
    /// </summary>
    private void StartDraining(
        string probeId,
        Guid scanId,
        Task<ProbeResult> probeTask,
        CancellationTokenSource timeoutCts,
        CancellationTokenSource linkedCts)
    {
        _draining[probeId] = scanId;
        NotifyDrainingChanged();
        _ = probeTask.ContinueWith(
            completed => DiscardLateResult(probeId, scanId, completed, timeoutCts, linkedCts),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 늦게 끝난 호출의 결과를 버리고(어느 검사에도 합치지 않음) 종료 중 목록에서 뺀 뒤 로그를 남긴다.
    /// </summary>
    private void DiscardLateResult(
        string probeId,
        Guid scanId,
        Task<ProbeResult> completed,
        CancellationTokenSource timeoutCts,
        CancellationTokenSource linkedCts)
    {
        // 실패한 늦은 호출의 예외를 관찰해 미관찰 예외로 남지 않게 한다(원문은 기록하지 않음).
        var errorType = completed.Exception?.InnerException?.GetType().Name ?? "none";
        _draining.TryRemove(new KeyValuePair<string, Guid>(probeId, scanId));
        NotifyDrainingChanged();
        _logger.Info(
            LOG_CATEGORY,
            $"{ScanLogEvents.LATE_RESULT_DISCARDED} probe={probeId} scan={scanId} taskStatus={completed.Status} error={errorType}");
        linkedCts.Dispose();
        timeoutCts.Dispose();
    }

    /// <summary>화면 구독자의 실패가 실행 정리를 방해하지 않도록 격리합니다.</summary>
    private void NotifyDrainingChanged()
    {
        foreach (EventHandler handler in DrainingChanged?.GetInvocationList() ?? [])
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception ex) { _logger.Warn(LOG_CATEGORY, $"DrainingNotificationFailed error={ex.GetType().Name}"); }
        }
    }
}
