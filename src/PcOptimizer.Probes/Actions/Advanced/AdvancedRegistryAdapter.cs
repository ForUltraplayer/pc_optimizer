/**
 * @file    : AdvancedRegistryAdapter.cs
 * @author  : rudals252
 * @brief   : 고급 설정의 미리보기·현재 값 비교·내구 원본 저장·복원을 기존 조율기에 연결
 */
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Advanced;

/// <summary>고정 옵션 하나만 처리하는 복구 가능 어댑터입니다.</summary>
public sealed class AdvancedRegistryAdapter : IReversibleActionAdapter
{
    private readonly AdvancedOption _option;
    private readonly Func<ActionSession> _session;
    private readonly IAdvancedRegistryPlatform _platform;
    private readonly ConditionalWeakTable<ActionPreview, Snapshot> _snapshots = new();
    private sealed record Snapshot(RollbackValue Before, RollbackValue Applied, ActionSession Session, string Key);
    /// <summary>실제 앱과 동일한 세션 공급자를 연결합니다.</summary>
    public AdvancedRegistryAdapter(AdvancedOption option, Func<ActionSession> session) : this(option, session, new AdvancedRegistryPlatform()) { }
    internal AdvancedRegistryAdapter(AdvancedOption option, Func<ActionSession> session, IAdvancedRegistryPlatform platform)
    { _ = AdvancedOptions.Action(option); _option = option; _session = session; _platform = platform; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(AdvancedOptions.Action(_option), _option == AdvancedOption.GameMode ? ActionScope.CurrentUser : ActionScope.System, true);
    private string Key(ActionSession session) => "advanced-registry-v1:" + _option + ":" + _platform.Identity(_option, session);
    private ActionSession Session()
    {
        var current = _session();
        if (!current.IsKnown || (_option == AdvancedOption.GameMode && current.Scope != ActionUserScope.Full)) { throw new ActionUnavailableException("ScopeExcluded"); }
        return current;
    }
    /// <summary>명시된 설정값만 읽습니다. HAGS 값 부재로 하드웨어 지원을 추측하지 않습니다.</summary>
    public AdvancedObservation Observe()
    {
        try
        {
            var value = _platform.Read(_option, Session());
            var state = AdvancedOptions.Interpret(_option, value);
            var canChange = state is not null && (_option != AdvancedOption.Hags || value.Exists);
            return new(_option, state, canChange, state is null ? "알려지지 않은 설정값이므로 직접 변경하지 않습니다."
                : !canChange ? "HAGS 설정값이 없습니다. GPU 지원 여부를 Windows 그래픽 설정에서 먼저 확인하세요."
                : _option == AdvancedOption.GameMode ? "현재 사용자 설정값입니다. 실행 중인 게임의 처리 상태를 뜻하지 않습니다."
                : "저장된 설정값입니다. 실제 적용에는 재부팅이 필요하며 현재 GPU 동작을 증명하지 않습니다.");
        }
        catch (ActionUnavailableException ex) { return new(_option, null, false, ex.Code == "ScopeExcluded" ? "현재 로그인 사용자 범위를 확인할 수 없습니다." : "설정 위치·형식 또는 접근 권한을 확인하지 못했습니다."); }
    }
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        if (target is not ActionTarget.Advanced selected || selected.Option != _option || !Enum.IsDefined(selected.Setting)
            || (_option == AdvancedOption.Mpo && selected.Setting == AdvancedSetting.On)) { return Task.FromResult<ActionPreview?>(null); }
        var key = Key(session);
        var before = _platform.Read(_option, session);
        if (AdvancedOptions.Interpret(_option, before) is null) { throw new ActionUnavailableException("AdvancedValueUnsupported"); }
        if (_option == AdvancedOption.Hags && !before.Exists) { throw new ActionUnavailableException("HagsSupportUnknown"); }
        var label = selected.Setting switch { AdvancedSetting.Default => "Windows 기본 동작", AdvancedSetting.On => "켜기", _ => "끄기" };
        var preview = new ActionPreview(target, $"{AdvancedOptions.Name(_option)}: {label}",
            _option == AdvancedOption.Mpo ? "화면 깜박임 문제를 비교하는 설정입니다. 끄면 전력·영상 처리 효율이 달라질 수 있습니다. RTX Video가 MPO에 영향을 줄 수도 있습니다."
                : _option == AdvancedOption.Hags ? "게임·드라이버에 따라 효과가 달라집니다. 레지스트리 설정만 변경하며 하드웨어 지원이나 성능 향상을 보장하지 않습니다."
                : "게임 실행 시 Windows의 자원 배분 동작이 달라질 수 있습니다. 모든 게임의 성능 향상을 보장하지 않습니다.",
            new(AdvancedOptions.Name(_option), "원래 값(값 없음 포함)을 먼저 보관합니다. 다른 프로그램이 바꾼 값은 복원으로 덮어쓰지 않습니다.", RequiresRestart: _option != AdvancedOption.GameMode));
        if (Key(session) != key) { throw new ActionUnavailableException("CurrentValueChanged"); }
        _snapshots.Add(preview, new(before, AdvancedOptions.Desired(_option, selected.Setting), session, key));
        return Task.FromResult<ActionPreview?>(preview);
    }
    /// <inheritdoc />
    public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { throw new ActionUnavailableException("PlanExpired"); }
        _snapshots.Remove(plan.Preview);
        if (Session() != snapshot.Session || plan.Session != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
        if (Key(snapshot.Session) != snapshot.Key || !_platform.Read(_option, snapshot.Session).SameAs(snapshot.Before)) { throw new ActionUnavailableException("CurrentValueChanged"); }
        return Task.FromResult(new RollbackChange(snapshot.Key, RollbackPurpose.UserUndo, snapshot.Before, snapshot.Applied));
    }
    /// <inheritdoc />
    public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        return Task.FromResult(record.ActionId == Definition.Id && record.Scope == Definition.Scope && record.Sid == session.Sid
            && record.TargetKey == Key(session) && record.Purpose == RollbackPurpose.UserUndo
            && AdvancedOptions.Interpret(_option, record.Before) is not null && AdvancedOptions.Interpret(_option, record.Applied) is not null);
    }
    /// <inheritdoc />
    public async Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { throw new ActionUnavailableException("TargetRejected"); }
        return _platform.Read(_option, Session());
    }
    /// <inheritdoc />
    public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct) || (!desired.SameAs(record.Before) && !desired.SameAs(record.Applied))
            || (!expected.SameAs(record.Before) && !expected.SameAs(record.Applied))) { return false; }
        var session = Session();
        return _platform.CompareExchange(_option, expected, desired, session, () =>
        { ct.ThrowIfCancellationRequested(); if (Session() != session || record.TargetKey != Key(session)) { throw new ActionUnavailableException("SessionChanged"); } });
    }
}
