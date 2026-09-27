/**
 * @file    : StartupRunActionAdapter.cs
 * @author  : rudals252
 * @brief   : 현재 사용자 Run 항목 한 개의 원문 보존·등록 해제·외부 변경 보호 복원
 */
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Startup;

/// <summary>StartupApproved 내부 형식을 쓰지 않고 Run 등록 자체를 해제합니다.</summary>
public sealed class StartupRunActionAdapter : IReversibleActionAdapter
{
    private readonly IStartupRunPlatform _platform;
    private readonly Func<ActionSession> _session;
    private readonly ConditionalWeakTable<ActionPreview, Snapshot> _snapshots = new();
    private sealed record Snapshot(string Name, RollbackValue Before, ActionSession Session);
    /// <summary>공통 사용자 범위 판정과 실제 Windows 레지스트리를 사용합니다.</summary>
    public StartupRunActionAdapter(Func<ActionSession> session) : this(new StartupRunPlatform(), session) { }
    internal StartupRunActionAdapter(IStartupRunPlatform platform, Func<ActionSession> session) { _platform = platform; _session = session; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.Startup, ActionScope.CurrentUser, true);
    private ActionSession Session()
    {
        var session = _session();
        if (!session.IsKnown || session.Scope != ActionUserScope.Full) { throw new ActionUnavailableException("ScopeExcluded"); }
        return session;
    }
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        if (target is not ActionTarget.Startup selected || selected.SourceKey != StartupRegistration.Source || !StartupRegistration.ValidName(selected.ValueName)) { return Task.FromResult<ActionPreview?>(null); }
        var before = _platform.Read(selected.ValueName, session);
        if (!StartupRegistration.ValidValue(before)) { throw new ActionUnavailableException("StartupUnsupported"); }
        var preview = new ActionPreview(target, $"'{selected.ValueName}'의 현재 사용자 자동 실행 등록을 해제합니다.",
            "다음 로그인부터 이 등록으로 앱을 시작하지 않습니다. 실행 중인 앱은 종료하지 않으며 프로그램을 삭제하지 않습니다. 알림·동기화 등 필요한 기능인지 확인하세요.",
            new(selected.ValueName, "등록 원문을 저장하고 항목 하나만 제거합니다. 시작 앱 목록에서 사라질 수 있으며 앱이 다시 등록할 수도 있습니다. 작업 관리자의 사용/사용 안 함 상태는 변경하지 않습니다.", RequiresRestart: false));
        _snapshots.Add(preview, new(selected.ValueName, before with { Data = before.Data.ToArray() }, session));
        return Task.FromResult<ActionPreview?>(preview);
    }
    /// <inheritdoc />
    public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { throw new ActionUnavailableException("PlanExpired"); }
        _snapshots.Remove(plan.Preview);
        if (Session() != snapshot.Session || plan.Session != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
        if (!_platform.Read(snapshot.Name, snapshot.Session).SameAs(snapshot.Before)) { throw new ActionUnavailableException("CurrentValueChanged"); }
        return Task.FromResult(new RollbackChange(StartupRegistration.Key(snapshot.Name), RollbackPurpose.UserUndo, snapshot.Before, StartupRegistration.Absent));
    }
    /// <inheritdoc />
    public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        return Task.FromResult(record.ActionId == ActionId.Startup && record.Scope == ActionScope.CurrentUser && record.Sid == session.Sid
            && record.Purpose == RollbackPurpose.UserUndo && StartupRegistration.Name(record.TargetKey) is not null
            && StartupRegistration.ValidValue(record.Before) && record.Applied.SameAs(StartupRegistration.Absent));
    }
    /// <inheritdoc />
    public async Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { throw new ActionUnavailableException("TargetRejected"); }
        return _platform.Read(StartupRegistration.Name(record.TargetKey)!, Session());
    }
    /// <inheritdoc />
    public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct) || (!desired.SameAs(record.Before) && !desired.SameAs(record.Applied))
            || (!expected.SameAs(record.Before) && !expected.SameAs(record.Applied))) { return false; }
        var session = Session();
        return _platform.CompareExchange(StartupRegistration.Name(record.TargetKey)!, expected, desired, session, () =>
        {
            ct.ThrowIfCancellationRequested();
            if (Session() != session) { throw new ActionUnavailableException("SessionChanged"); }
        });
    }
}
