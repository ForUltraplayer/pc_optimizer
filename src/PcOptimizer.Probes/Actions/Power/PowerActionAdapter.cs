/**
 * @file    : PowerActionAdapter.cs
 * @author  : rudals252
 * @brief   : 설치된 기본 전원 계획만 제안하고 이전 GUID 저장·현재 값 비교·배터리 재검증 후 적용
 */
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Power;

/// <summary>현재 사용자의 설치된 전원 계획을 선택합니다. 계획 생성/세부 옵션 변경은 없습니다.</summary>
public sealed class PowerActionAdapter : IReversibleActionAdapter
{
    /// <summary>Windows 기본 균형 조정 GUID입니다. 설치 여부는 매번 확인합니다.</summary>
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    /// <summary>Windows 기본 고성능 GUID입니다. AC 전원이 확인된 경우에만 선택합니다.</summary>
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    /// <summary>Windows 기본 절전 GUID입니다.</summary>
    public static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    private const string Key = "current-user-active-scheme-v1";
    private readonly IPowerSettingsPlatform _platform;
    private readonly Func<ActionSession> _session;
    private readonly ConditionalWeakTable<ActionPreview, Snapshot> _snapshots = new();
    private sealed record Snapshot(Guid Before, Guid Applied, byte Ac, ActionSession Session);
    /// <summary>앱과 동일한 세션 판정 공급자를 사용합니다.</summary>
    public PowerActionAdapter(Func<ActionSession> session) : this(new PowerSettingsPlatform(), session) { }
    internal PowerActionAdapter(IPowerSettingsPlatform platform, Func<ActionSession> session) { _platform = platform; _session = session; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.Power, ActionScope.CurrentUser, true);
    private ActionSession Session()
    {
        var session = _session();
        if (!session.IsKnown || session.Scope != ActionUserScope.Full) { throw new ActionUnavailableException("ScopeExcluded"); }
        return session;
    }
    private static bool Offered(Guid scheme) => scheme == Balanced || scheme == HighPerformance || scheme == PowerSaver;
    private void CheckInstalled(params Guid[] schemes)
    {
        var installed = _platform.Installed();
        if (schemes.Any(s => !installed.Contains(s))) { throw new ActionUnavailableException("PowerSchemeMissing"); }
    }
    private void CheckPower(Guid desired)
    {
        if (desired != Balanced && desired != PowerSaver && _platform.AcStatus() != 1) { throw new ActionUnavailableException("PowerNeedsAc"); }
    }
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        if (target is not ActionTarget.Power selected || !Offered(selected.SchemeId)) { return Task.FromResult<ActionPreview?>(null); }
        var before = _platform.Current(); CheckInstalled(before, selected.SchemeId); CheckPower(selected.SchemeId);
        var name = selected.SchemeId == Balanced ? "균형 조정" : selected.SchemeId == HighPerformance ? "고성능" : "절전";
        var preview = new ActionPreview(target, $"설치된 '{name}' 전원 계획을 선택합니다.",
            selected.SchemeId == HighPerformance ? "소비 전력·발열·팬 소음이 늘 수 있습니다. 성능 향상을 보장하지 않습니다." : "전력 소비와 응답성이 달라질 수 있습니다. 개별 전원 옵션은 바꾸지 않습니다.",
            new(name, "변경 전 현재 계획을 저장합니다. 현재 값이 이 앱의 적용 값일 때만 되돌립니다.", RequiresRestart: false));
        _snapshots.Add(preview, new(before, selected.SchemeId, _platform.AcStatus(), session));
        return Task.FromResult<ActionPreview?>(preview);
    }
    /// <inheritdoc />
    public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { throw new ActionUnavailableException("PlanExpired"); }
        _snapshots.Remove(plan.Preview);
        if (Session() != snapshot.Session || plan.Session != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
        CheckInstalled(snapshot.Before, snapshot.Applied); CheckPower(snapshot.Applied);
        if (_platform.Current() != snapshot.Before) { throw new ActionUnavailableException("CurrentValueChanged"); }
        if (_platform.AcStatus() != snapshot.Ac) { throw new ActionUnavailableException("PowerStateChanged"); }
        return Task.FromResult(new RollbackChange(Key, RollbackPurpose.UserUndo, Value(snapshot.Before), Value(snapshot.Applied)));
    }
    private static RollbackValue Value(Guid value) => new(true, 0, value.ToByteArray());
    private static Guid Read(RollbackValue value) => value.Exists && value.NativeType == 0 && value.Data.Length == 16 && new Guid(value.Data) != Guid.Empty
        ? new(value.Data) : throw new ActionUnavailableException("TargetRejected");
    /// <inheritdoc />
    public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        if (record.ActionId != ActionId.Power || record.Scope != ActionScope.CurrentUser || record.Sid != session.Sid
            || record.TargetKey != Key || record.Purpose != RollbackPurpose.UserUndo || !Offered(Read(record.Applied))) { return Task.FromResult(false); }
        CheckInstalled(Read(record.Before), Read(record.Applied));
        return Task.FromResult(true);
    }
    /// <inheritdoc />
    public async Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { throw new ActionUnavailableException("TargetRejected"); }
        return Value(_platform.Current());
    }
    /// <inheritdoc />
    public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { return false; }
        var destination = Read(desired);
        if (!desired.SameAs(record.Before) && !desired.SameAs(record.Applied)) { return false; }
        if (_platform.Current() != Read(expected)) { return false; }
        CheckPower(destination); ct.ThrowIfCancellationRequested(); Session();
        _platform.Set(destination); // API에 CAS는 없다. 외부 프로세스와의 원자성을 주장하지 않는다.
        return true; // 실제 활성 GUID 검증은 RestoreCoordinator가 다시 읽어 수행한다.
    }
}
