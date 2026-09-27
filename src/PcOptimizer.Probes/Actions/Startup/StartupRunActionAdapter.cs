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
    private readonly Func<string, IStartupRunPlatform> _platform;
    private readonly bool _machine;
    private readonly bool _folder;
    private readonly Func<ActionSession> _session;
    private readonly ConditionalWeakTable<ActionPreview, Snapshot> _snapshots = new();
    private sealed record Snapshot(string Source, string Name, RollbackValue Before, ActionSession Session);
    /// <summary>공통 사용자 범위 판정과 실제 Windows 레지스트리를 사용합니다.</summary>
    public StartupRunActionAdapter(Func<ActionSession> session) : this(source => new StartupRunPlatform(source), session, false) { }
    /// <summary>모든 사용자 Run의 32/64비트 보기를 각각 식별하는 시스템 범위 실행기를 만듭니다.</summary>
    public static StartupRunActionAdapter ForMachine(Func<ActionSession> session) => new(source => new StartupRunPlatform(source), session, true);
    /// <summary>시작 폴더 바로가기를 원본 그대로 보관·복원하는 실행기입니다.</summary>
    public static StartupRunActionAdapter ForFolder(Func<ActionSession> session, bool common = false)
        => new(_ => new StartupFolderPlatform(common), session, common, true);
    internal StartupRunActionAdapter(IStartupRunPlatform platform, Func<ActionSession> session) : this(_ => platform, session, false) { }
    internal StartupRunActionAdapter(Func<string, IStartupRunPlatform> platform, Func<ActionSession> session, bool machine, bool folder = false)
    { _platform = platform; _session = session; _machine = machine; _folder = folder; }
    private bool Supports(string? source) => _folder ? source == (_machine ? StartupRegistration.CommonFolder : StartupRegistration.UserFolder)
        : _machine ? source is StartupRegistration.Machine32 or StartupRegistration.Machine64 : source == StartupRegistration.Source;
    private bool ValidValue(RollbackValue value) => _folder ? StartupFolderPlatform.ValidValue(value) : StartupRegistration.ValidValue(value);
    private bool ValidName(string name) => _folder ? StartupFolderPlatform.ValidName(name) : StartupRegistration.ValidName(name);
    /// <inheritdoc />
    public ActionDefinition Definition => new(_folder ? _machine ? ActionId.CommonStartupFolder : ActionId.StartupFolder : _machine ? ActionId.MachineStartup : ActionId.Startup, _machine ? ActionScope.System : ActionScope.CurrentUser, true);
    private ActionSession Session()
    {
        var session = _session();
        if (!session.IsKnown || (!_machine && session.Scope != ActionUserScope.Full)) { throw new ActionUnavailableException("ScopeExcluded"); }
        return session;
    }
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        if (target is not ActionTarget.Startup selected || !Supports(selected.SourceKey) || !ValidName(selected.ValueName)) { return Task.FromResult<ActionPreview?>(null); }
        var before = _platform(selected.SourceKey).Read(selected.ValueName, session);
        if (!ValidValue(before)) { throw new ActionUnavailableException("StartupUnsupported"); }
        var preview = new ActionPreview(target, $"'{selected.ValueName}'의 {StartupRegistration.Label(selected.SourceKey)} 자동 실행 등록을 해제합니다.",
            (_machine ? "이 PC의 모든 사용자에게 영향을 줍니다. " : "") + "다음 로그인부터 이 등록으로 앱을 시작하지 않습니다. 실행 중인 앱은 종료하지 않으며 프로그램을 삭제하지 않습니다. 알림·동기화 등 필요한 기능인지 확인하세요.",
            new(selected.ValueName + " · " + StartupRegistration.Label(selected.SourceKey), _folder
                ? "바로가기를 시작 폴더의 바로 위 Programs 폴더에 .PcOptimizer-disabled-… 파일로 보관합니다. 원본 파일과 복구 기록을 지우면 복원할 수 없습니다. 같은 이름의 새 항목을 덮어쓰지 않습니다. 작업 관리자 토글은 바꾸지 않습니다."
                : "등록 원문을 저장하고 선택한 출처의 항목 하나만 제거합니다. 시작 앱 목록에서 사라질 수 있으며 앱이 다시 등록할 수도 있습니다. 작업 관리자의 사용/사용 안 함 상태는 변경하지 않습니다.", RequiresRestart: false));
        _snapshots.Add(preview, new(selected.SourceKey, selected.ValueName, before with { Data = before.Data.ToArray() }, session));
        return Task.FromResult<ActionPreview?>(preview);
    }
    /// <inheritdoc />
    public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { throw new ActionUnavailableException("PlanExpired"); }
        _snapshots.Remove(plan.Preview);
        if (Session() != snapshot.Session || plan.Session != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
        if (!_platform(snapshot.Source).Read(snapshot.Name, snapshot.Session).SameAs(snapshot.Before)) { throw new ActionUnavailableException("CurrentValueChanged"); }
        return Task.FromResult(new RollbackChange(StartupRegistration.Key(snapshot.Source, snapshot.Name), RollbackPurpose.UserUndo, snapshot.Before, StartupRegistration.Absent));
    }
    /// <inheritdoc />
    public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        return Task.FromResult(record.ActionId == Definition.Id && record.Scope == Definition.Scope && record.Sid == session.Sid
            && Supports(StartupRegistration.SourceOfKey(record.TargetKey))
            && record.Purpose == RollbackPurpose.UserUndo && StartupRegistration.Name(record.TargetKey) is not null
            && ValidName(StartupRegistration.Name(record.TargetKey)!) && ValidValue(record.Before) && record.Applied.SameAs(StartupRegistration.Absent));
    }
    /// <inheritdoc />
    public async Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { throw new ActionUnavailableException("TargetRejected"); }
        return _platform(StartupRegistration.SourceOfKey(record.TargetKey)!).Read(StartupRegistration.Name(record.TargetKey)!, Session());
    }
    /// <inheritdoc />
    public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct) || (!desired.SameAs(record.Before) && !desired.SameAs(record.Applied))
            || (!expected.SameAs(record.Before) && !expected.SameAs(record.Applied))) { return false; }
        var session = Session();
        return _platform(StartupRegistration.SourceOfKey(record.TargetKey)!).CompareExchange(StartupRegistration.Name(record.TargetKey)!, expected, desired, session, () =>
        {
            ct.ThrowIfCancellationRequested();
            if (Session() != session) { throw new ActionUnavailableException("SessionChanged"); }
        });
    }
}
