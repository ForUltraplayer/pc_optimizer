/**
 * @file    : FakeProbes.cs
 * @author  : rudals252
 * @brief   : 실행 조율 테스트용 가짜 프로브(성공·예외·멈춤·동기 차단·지연·취소 협조·부분·잘못된 ID·null 목록)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Engine.Fakes;

/// <summary>
/// 가짜 프로브의 공통 기반입니다. 호출 횟수와 시작 신호를 기록합니다.
/// </summary>
internal abstract class FakeProbe : IProbe
{
    /// <summary>완료되는 가짜 프로브의 기본 타임아웃입니다(테스트가 이 시간까지 기다리는 일은 없어야 함).</summary>
    public static readonly TimeSpan GENEROUS_TIMEOUT = TimeSpan.FromSeconds(10);

    /// <summary>멈추는 가짜 프로브에 쓰는 짧은 타임아웃입니다.</summary>
    public static readonly TimeSpan SHORT_TIMEOUT = TimeSpan.FromMilliseconds(200);

    /// <summary>가짜 측정값의 관측 시각입니다.</summary>
    public static readonly DateTimeOffset OBSERVED_AT = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

    private readonly object _sync = new();
    private readonly List<(int Count, TaskCompletionSource Signal)> _invocationWaiters = [];
    private int _invocationCount;

    /// <summary>
    /// 가짜 프로브를 만든다.
    /// </summary>
    protected FakeProbe(string id, TimeSpan timeout, bool requiresElevation = false, bool requiresNetwork = false)
    {
        Id = id;
        DefaultTimeout = timeout;
        RequiresElevation = requiresElevation;
        RequiresNetwork = requiresNetwork;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public FindingCategory Category { get; init; } = FindingCategory.Display;

    /// <inheritdoc />
    public bool RequiresElevation { get; }

    /// <inheritdoc />
    public bool RequiresNetwork { get; }

    /// <inheritdoc />
    public TimeSpan DefaultTimeout { get; }

    /// <summary>RunAsync가 호출된 횟수.</summary>
    public int InvocationCount
    {
        get
        {
            lock (_sync)
            {
                return _invocationCount;
            }
        }
    }

    /// <summary>RunAsync가 처음 호출되면 완료되는 작업.</summary>
    public Task Started => WhenInvokedAsync(1);

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        // RunCoreAsync가 작업을 돌려준 뒤에 신호를 보내, 테스트가 신호를 받은 시점에는 호출 준비가 끝나 있게 한다.
        var task = RunCoreAsync(context, ct);
        lock (_sync)
        {
            _invocationCount++;
            foreach (var waiter in _invocationWaiters.Where(w => w.Count <= _invocationCount).ToArray())
            {
                waiter.Signal.TrySetResult();
                _invocationWaiters.Remove(waiter);
            }
        }

        return task;
    }

    /// <summary>
    /// RunAsync 호출 횟수가 지정한 값에 도달하면 완료되는 작업을 돌려준다.
    /// </summary>
    public Task WhenInvokedAsync(int count)
    {
        lock (_sync)
        {
            if (_invocationCount >= count)
            {
                return Task.CompletedTask;
            }

            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _invocationWaiters.Add((count, signal));
            return signal.Task;
        }
    }

    /// <summary>
    /// 가짜 프로브별 동작.
    /// </summary>
    protected abstract Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct);

    /// <summary>
    /// 이 프로브 ID로 결과를 만든다.
    /// </summary>
    protected ProbeResult CreateResult(
        ScanContext context,
        ProbeStatus status,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<Issue> issues)
    {
        return new ProbeResult(Id, status, measurements, issues, OBSERVED_AT, TimeSpan.Zero, context.UserContext);
    }

    /// <summary>
    /// 이름이 주어진 가짜 정수 측정값을 만든다.
    /// </summary>
    public static Measurement CreateMeasurement(string name, long value = 1)
    {
        return new Measurement(name, new IntegerValue(value), null, "fake", OBSERVED_AT, MeasurementQuality.Observed);
    }
}

/// <summary>
/// 즉시 성공하는 프로브입니다.
/// </summary>
internal sealed class SuccessProbe : FakeProbe
{
    /// <summary>성공 결과에 담는 측정값 이름.</summary>
    public const string MEASUREMENT_NAME = "value";

    /// <summary>성공 프로브를 만든다.</summary>
    public SuccessProbe(string id, bool requiresElevation = false, bool requiresNetwork = false)
        : base(id, GENEROUS_TIMEOUT, requiresElevation, requiresNetwork)
    {
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        return Task.FromResult(CreateResult(context, ProbeStatus.Success, [CreateMeasurement(MEASUREMENT_NAME)], []));
    }
}

/// <summary>
/// 실행 중 예외를 던지는 프로브입니다.
/// </summary>
internal sealed class ThrowingProbe : FakeProbe
{
    /// <summary>던지는 예외의 메시지.</summary>
    public const string ERROR_MESSAGE = "의도한 실패";

    /// <summary>예외 프로브를 만든다.</summary>
    public ThrowingProbe(string id)
        : base(id, GENEROUS_TIMEOUT)
    {
    }

    /// <inheritdoc />
    protected override async Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        await Task.Yield();
        throw new InvalidOperationException(ERROR_MESSAGE);
    }
}

/// <summary>
/// 취소 토큰을 무시하고 영원히 끝나지 않는 프로브입니다.
/// </summary>
internal sealed class HangingProbe : FakeProbe
{
    private readonly TaskCompletionSource<ProbeResult> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>멈춤 프로브를 만든다.</summary>
    public HangingProbe(string id, TimeSpan? timeout = null)
        : base(id, timeout ?? SHORT_TIMEOUT)
    {
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        return _never.Task;
    }
}

/// <summary>
/// RunAsync 안에서 스레드를 동기적으로 막는 프로브입니다(동기 WMI/COM 호출 모사). 취소 토큰을 무시합니다.
/// </summary>
internal sealed class BlockingProbe : FakeProbe, IDisposable
{
    private readonly ManualResetEventSlim _release = new(false);

    /// <summary>동기 차단 프로브를 만든다.</summary>
    public BlockingProbe(string id)
        : base(id, SHORT_TIMEOUT)
    {
    }

    /// <summary>막힌 스레드를 풀어 준다.</summary>
    public void Release()
    {
        _release.Set();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _release.Set();
        _release.Dispose();
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        _release.Wait(CancellationToken.None);
        return Task.FromResult(CreateResult(context, ProbeStatus.Success, [], []));
    }
}

/// <summary>
/// 테스트가 Complete를 호출할 때에만 성공 결과를 반환하는 프로브입니다. 취소 토큰을 무시합니다.
/// </summary>
internal sealed class DelayedProbe : FakeProbe
{
    /// <summary>늦게 도착하는 결과에 담는 측정값 이름.</summary>
    public const string LATE_MEASUREMENT_NAME = "late-value";

    private readonly object _pendingSync = new();
    private TaskCompletionSource<ProbeResult> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ScanContext? _lastContext;

    /// <summary>지연 프로브를 만든다.</summary>
    public DelayedProbe(string id, TimeSpan? timeout = null)
        : base(id, timeout ?? SHORT_TIMEOUT)
    {
    }

    /// <summary>
    /// 대기 중인 호출을 성공 결과로 끝낸다. 다음 호출은 다시 대기한다.
    /// </summary>
    public void Complete()
    {
        TaskCompletionSource<ProbeResult> pending;
        ScanContext context;
        lock (_pendingSync)
        {
            context = _lastContext ?? throw new InvalidOperationException("아직 호출되지 않았습니다.");
            pending = _pending;
            _pending = new TaskCompletionSource<ProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        pending.TrySetResult(CreateResult(context, ProbeStatus.Success, [CreateMeasurement(LATE_MEASUREMENT_NAME)], []));
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        lock (_pendingSync)
        {
            _lastContext = context;
            return _pending.Task;
        }
    }
}

/// <summary>
/// 취소 토큰에 협조해 취소 시 OperationCanceledException으로 끝나는 프로브입니다.
/// </summary>
internal sealed class CancellableProbe : FakeProbe
{
    /// <summary>취소 협조 프로브를 만든다.</summary>
    public CancellableProbe(string id)
        : base(id, GENEROUS_TIMEOUT)
    {
    }

    /// <inheritdoc />
    protected override async Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        return CreateResult(context, ProbeStatus.Success, [], []);
    }
}

/// <summary>
/// 일부 측정만 성공한 Partial 결과를 반환하는 프로브입니다.
/// </summary>
internal sealed class PartialProbe : FakeProbe
{
    /// <summary>부분 결과에 담는 측정값 이름.</summary>
    public const string MEASUREMENT_NAME = "partial-value";

    /// <summary>부분 결과 프로브를 만든다.</summary>
    public PartialProbe(string id)
        : base(id, GENEROUS_TIMEOUT)
    {
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        return Task.FromResult(CreateResult(
            context,
            ProbeStatus.Partial,
            [CreateMeasurement(MEASUREMENT_NAME, value: 42)],
            [new Issue(CannotVerifyReason.AccessDenied, "일부 항목 접근 거부")]));
    }
}

/// <summary>
/// 자신의 Id와 다른 ProbeId를 담은 결과를 반환하는 잘못된 프로브입니다.
/// </summary>
internal sealed class WrongIdProbe : FakeProbe
{
    /// <summary>잘못된 ID 프로브를 만든다.</summary>
    public WrongIdProbe(string id)
        : base(id, GENEROUS_TIMEOUT)
    {
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        return Task.FromResult(new ProbeResult(
            Id + "-other", ProbeStatus.Success, [], [], OBSERVED_AT, TimeSpan.Zero, context.UserContext));
    }
}

/// <summary>
/// 측정값 또는 Issue 목록이 null이거나 null 항목을 담은 잘못된 결과를 반환하는 프로브입니다.
/// </summary>
internal sealed class NullCollectionsProbe : FakeProbe
{
    private readonly bool _nullMeasurements;
    private readonly bool _nullIssues;
    private readonly bool _nullIssueItem;

    /// <summary>null 목록 프로브를 만든다.</summary>
    public NullCollectionsProbe(string id, bool nullMeasurements, bool nullIssues, bool nullIssueItem = false)
        : base(id, GENEROUS_TIMEOUT)
    {
        _nullMeasurements = nullMeasurements;
        _nullIssues = nullIssues;
        _nullIssueItem = nullIssueItem;
    }

    /// <inheritdoc />
    protected override Task<ProbeResult> RunCoreAsync(ScanContext context, CancellationToken ct)
    {
        return Task.FromResult(new ProbeResult(
            Id,
            ProbeStatus.Success,
            _nullMeasurements ? null! : [],
            _nullIssues ? null! : _nullIssueItem ? [null!] : [],
            OBSERVED_AT,
            TimeSpan.Zero,
            context.UserContext));
    }
}
