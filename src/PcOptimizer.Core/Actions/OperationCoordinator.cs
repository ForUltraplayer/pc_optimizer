/**
 * @file    : OperationCoordinator.cs
 * @author  : rudals252
 * @brief   : 검사·사양·준비·적용·복구가 실제로 끝날 때까지 소유권을 유지하는 공통 관문
 */
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Core.Actions;

/// <summary>공통 관문을 사용하는 작업 종류입니다.</summary>
public enum OperationKind { Scan, Specification, Prepare, Apply, Restore }
/// <summary>호출 종료와 실제 자식 작업 종료를 구분합니다.</summary>
public enum OperationPhase { Idle, Running, Draining }
/// <summary>현재 소유 작업의 불변 상태입니다.</summary>
public sealed record OperationState(Guid? Id, OperationKind? Kind, OperationPhase Phase)
{
    /// <summary>새 작업을 시작할 수 없는지 여부입니다.</summary>
    public bool IsBusy => Phase != OperationPhase.Idle;
}

/// <summary>직접 호출에도 적용되는 실행 관문입니다. 대기열에 오래된 변경 요청을 쌓지 않습니다.</summary>
public interface IOperationCoordinator
{
    /// <summary>현재 상태입니다.</summary>
    OperationState State { get; }
    /// <summary>실행 스레드에서 상태 변경을 알립니다. UI는 디스패처로 전달합니다.</summary>
    event EventHandler? Changed;
    /// <summary>비어 있을 때만 소유권을 발급합니다.</summary>
    IOperationLease? TryAcquire(OperationKind kind);
}

/// <summary>호출이 반환돼도 추적한 실제 작업이 끝날 때까지 관문을 점유합니다.</summary>
public interface IOperationLease : IDisposable
{
    /// <summary>실제 비동기 작업을 등록합니다. 반환 뒤에는 아직 추적 중인 부모 작업만 자식을 추가할 수 있습니다.</summary>
    void Track(Task task);
}

/// <summary>프로세스 내에서 하나를 공유하는 실행 관문입니다.</summary>
public sealed class OperationCoordinator(IAppLogger? logger = null) : IOperationCoordinator
{
    private readonly object _sync = new();
    private readonly IAppLogger _logger = logger ?? NullAppLogger.Instance;
    private Lease? _owner;
    /// <inheritdoc />
    public event EventHandler? Changed;
    /// <inheritdoc />
    public OperationState State
    {
        get { lock (_sync) { return _owner is { } owner ? new(owner.Id, owner.Kind, owner.Returned ? OperationPhase.Draining : OperationPhase.Running) : new(null, null, OperationPhase.Idle); } }
    }
    /// <inheritdoc />
    public IOperationLease? TryAcquire(OperationKind kind)
    {
        if (!Enum.IsDefined(kind)) { throw new ArgumentOutOfRangeException(nameof(kind)); }
        Lease lease;
        lock (_sync)
        {
            if (_owner is not null) { return null; }
            _owner = lease = new(this, kind);
        }
        Notify();
        return lease;
    }
    private void Notify()
    {
        if (Changed is not { } handlers) { return; }
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception ex) { LogFailure("ObserverFailed", ex); }
        }
    }
    private void LogFailure(string stage, Exception ex)
    {
        try { _logger.Warn(nameof(OperationCoordinator), $"{stage} type={ex.GetType().Name}"); }
        catch { /* 로거 실패가 관문 해제를 막지 않는다. */ }
    }
    private sealed class Lease(OperationCoordinator coordinator, OperationKind kind) : IOperationLease
    {
        internal readonly Guid Id = Guid.NewGuid();
        internal readonly OperationKind Kind = kind;
        internal bool Returned;
        private int _pending;
        public void Track(Task task)
        {
            ArgumentNullException.ThrowIfNull(task);
            lock (coordinator._sync)
            {
                if (!ReferenceEquals(coordinator._owner, this)) { throw new ObjectDisposedException(nameof(IOperationLease)); }
                _pending++;
            }
            _ = task.ContinueWith(completed =>
            {
                // 반환 뒤 실패도 관측한다. 메시지·경로는 로그에 남기지 않는다.
                if (completed.Exception is { } error) { coordinator.LogFailure("TrackedFailure", error.GetBaseException()); }
                var released = false;
                lock (coordinator._sync)
                {
                    _pending--;
                    if (Returned && _pending == 0 && ReferenceEquals(coordinator._owner, this)) { coordinator._owner = null; released = true; }
                }
                if (released) { coordinator.Notify(); }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        public void Dispose()
        {
            lock (coordinator._sync)
            {
                if (Returned) { return; }
                Returned = true;
                if (_pending == 0 && ReferenceEquals(coordinator._owner, this)) { coordinator._owner = null; }
            }
            coordinator.Notify();
        }
    }
}
