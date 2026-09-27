/**
 * @file    : UpdateServiceRecoveryAdapter.cs
 * @author  : rudals252
 * @brief   : 앱이 중지 전에 기록한 서비스만 원래 Running 상태로 되돌리는 복구 전용 어댑터
 */
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

/// <summary>사용자에게 임의 서비스 중지/시작을 제공하지 않고 검증된 복구 기록만 처리합니다.</summary>
public sealed class UpdateServiceRecoveryAdapter : IReversibleActionAdapter
{
    private readonly IUpdateServicePlatform _platform;
    private readonly Func<ActionSession> _session;
    /// <summary>기존 복구 저장소와 공통 관문의 복구 목록에 연결합니다.</summary>
    public UpdateServiceRecoveryAdapter(Func<ActionSession> session) : this(new UpdateServicePlatform(), session) { }
    internal UpdateServiceRecoveryAdapter(IUpdateServicePlatform platform, Func<ActionSession> session) { _platform = platform; _session = session; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.UpdateServices, ActionScope.System, true);
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct) => Task.FromResult<ActionPreview?>(null);
    /// <inheritdoc />
    public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct) => Task.FromException<RollbackChange>(new ActionUnavailableException("Unsupported"));
    internal static string Key(UpdateService service) => "update-service-v1:" + UpdateServicePlatform.Name(service);
    internal static UpdateService? Service(string key) => key switch
    { "update-service-v1:wuauserv" => UpdateService.WindowsUpdate, "update-service-v1:BITS" => UpdateService.Bits, _ => null };
    internal static RollbackValue Value(UpdateServiceState state) => new(true, 1, [(byte)state]);
    private ActionSession Session()
    {
        var session = _session();
        if (!session.IsKnown || SystemActionSession.Read(session.Scope) != session) { throw new ActionUnavailableException("SessionChanged"); }
        return session;
    }
    /// <inheritdoc />
    public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        return Task.FromResult(record.ActionId == ActionId.UpdateServices && record.Scope == ActionScope.System && record.Sid == session.Sid
            && record.Purpose == RollbackPurpose.ServiceRecovery && Service(record.TargetKey) is not null
            && record.Before.SameAs(Value(UpdateServiceState.Running)) && record.Applied.SameAs(Value(UpdateServiceState.Stopped)));
    }
    /// <inheritdoc />
    public async Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { throw new ActionUnavailableException("TargetRejected"); }
        var status = await _platform.ReadStableAsync(Service(record.TargetKey)!.Value, ct).ConfigureAwait(false);
        return Value(status.State);
    }
    /// <inheritdoc />
    public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
    {
        // 이 어댑터는 정방향 중지를 제공하지 않고 기록에 따른 복구만 허용한다.
        if (!await ValidateAsync(record, ct) || !expected.SameAs(record.Applied) || !desired.SameAs(record.Before)) { return false; }
        var session = Session();
        return await _platform.ChangeAsync(Service(record.TargetKey)!.Value, UpdateServiceState.Stopped, UpdateServiceState.Running, () =>
        { ct.ThrowIfCancellationRequested(); if (Session() != session) { throw new ActionUnavailableException("SessionChanged"); } }, ct).ConfigureAwait(false);
    }
}
