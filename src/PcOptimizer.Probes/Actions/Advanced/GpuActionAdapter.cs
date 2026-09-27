/**
 * @file    : GpuActionAdapter.cs
 * @author  : rudals252
 * @brief   : GPU 원본·출처·드라이버 스냅샷과 기존 내구 복구 기록의 연결
 */
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Advanced;

/// <summary>실제 GPU 변경은 확인된 계획과 기존 복원 조율기를 통해서만 실행합니다.</summary>
public sealed class GpuActionAdapter : IReversibleActionAdapter
{
    private readonly GpuFeature _feature;
    private readonly IGpuPlatform _platform;
    private readonly Func<ActionSession> _session;
    private readonly ConditionalWeakTable<ActionPreview, Snapshot> _snapshots = new();
    private readonly Dictionary<string, GpuOptionTarget> _targets = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 3 };
    private sealed record Snapshot(string Key, GpuSettingState Before, GpuSettingState Applied, ActionSession Session);
    /// <summary>기능별 닫힌 네이티브 실행기를 생성합니다.</summary>
    public GpuActionAdapter(GpuFeature feature, Func<ActionSession> session) : this(feature, session, feature switch
    { GpuFeature.NvidiaRebar => new NvidiaRebarPlatform(), GpuFeature.NvidiaVideo => new NvidiaVideoPlatform(), GpuFeature.AmdVideo => new AmdVideoPlatform(), _ => throw new ArgumentOutOfRangeException(nameof(feature)) }) { }
    internal GpuActionAdapter(GpuFeature feature, Func<ActionSession> session, IGpuPlatform platform)
    { _feature = feature; _session = session; _platform = platform; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(GpuOptions.Action(_feature), ActionScope.CurrentUser, true);
    private ActionSession Session()
    {
        var current = _session();
        if (!current.IsKnown || current.Scope != ActionUserScope.Full) { throw new ActionUnavailableException("ScopeExcluded"); }
        return current;
    }
    /// <summary>코드에서 관측한 대상만 실행 목록에 등록합니다. 오류를 빈 정상으로 표시하지 않습니다.</summary>
    public GpuOptionsResult Discover(CancellationToken ct)
    {
        _targets.Clear();
        try
        {
            Session(); var targets = _platform.Enumerate(ct).Where(t => ValidKey(t.Key)).ToArray();
            foreach (var target in targets) { _targets.TryAdd(target.Key, target); }
            return new(targets, targets.Length == 0 ? "지원되는 대상이 없습니다. GPU·드라이버·화면 연결 조건을 확인하세요." : $"지원 대상 {targets.Length:N0}개. 선택 후 현재 설정을 확인하세요.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is ActionUnavailableException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or IOException or UnauthorizedAccessException)
        { return new([], "이 PC에서 " + GpuOptions.Name(_feature) + " 제어 인터페이스를 확인하지 못했습니다. 미설치·미지원·사용자 범위 제한일 수 있습니다."); }
    }
    /// <summary>현재 드라이버 값만 관측합니다. 영상 처리 중이나 BIOS 활성 판정은 아닙니다.</summary>
    public GpuSettingState Observe(string key)
    { Session(); if (!_targets.ContainsKey(key)) { throw new ActionUnavailableException("TargetRejected"); } return _platform.Read(key); }
    private static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 120 && Encoding.UTF8.GetByteCount(key) <= 320 && !key.Any(char.IsControl);
    private string RecordKey(string key) => "gpu-v1:" + _feature + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
    /// <summary>원본 값이나 장치 식별자 전체를 노출하지 않고 복구할 프로필·출력을 표시합니다.</summary>
    public static string TargetLabel(RollbackRecord record)
    {
        if (record.ActionId == ActionId.AmdVideo) { return "선택한 AMD GPU의 동영상 업스케일링"; }
        var encoded = record.TargetKey[(record.TargetKey.LastIndexOf(':') + 1)..];
        try { var text = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(encoded)); return ValidKey(text) ? text : "지원하지 않는 GPU 대상"; }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { return "지원하지 않는 GPU 대상"; }
    }
    private string DecodeKey(string stored)
    {
        var prefix = "gpu-v1:" + _feature + ":";
        if (!stored.StartsWith(prefix, StringComparison.Ordinal)) { throw new ActionUnavailableException("TargetRejected"); }
        try
        {
            var key = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(stored[prefix.Length..]));
            if (!ValidKey(key) || RecordKey(key) != stored) { throw new ActionUnavailableException("TargetRejected"); }
            return key;
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { throw new ActionUnavailableException("TargetRejected"); }
    }
    internal static RollbackValue Encode(GpuSettingState state) => new(true, 0, JsonSerializer.SerializeToUtf8Bytes(state));
    internal static GpuSettingState Decode(RollbackValue value)
    {
        if (!value.Exists || value.NativeType != 0 || value.Data.Length > 4096) { throw new ActionUnavailableException("TargetRejected"); }
        try
        {
            var state = JsonSerializer.Deserialize<GpuSettingState>(value.Data, Json) ?? throw new ActionUnavailableException("TargetRejected");
            if (string.IsNullOrWhiteSpace(state.Stamp) || state.Stamp.Length > 256) { throw new ActionUnavailableException("TargetRejected"); }
            // 중복 키·형식 차이를 포함한 비정규 직렬화는 복구 입력으로 받지 않는다.
            if (!Encode(state).SameAs(value)) { throw new ActionUnavailableException("TargetRejected"); }
            return state;
        }
        catch (JsonException) { throw new ActionUnavailableException("TargetRejected"); }
    }
    private bool ValidState(GpuSettingState state) => _feature switch
    {
        GpuFeature.NvidiaRebar => state.SettingId is NvidiaRebarPlatform.Modern or NvidiaRebarPlatform.Legacy && state.Value >= 0
            && state.Value <= (state.SettingId == NvidiaRebarPlatform.Modern ? 2 : 1) && state.Enabled && state.Location <= 3 && state.Predefined <= 1 && state.PredefinedValid <= 1,
        GpuFeature.NvidiaVideo => state.SettingId == 0x1D && state.Value is >= 0 and <= 5 && state.PredefinedValue is >= 4 and <= 5 && state.Value <= state.PredefinedValue && state.Location == 0 && state.Predefined == 0 && state.PredefinedValid == 0,
        GpuFeature.AmdVideo => state.SettingId == 0 && state.Value == (state.Enabled ? 1 : 0) && state.Location == 0 && state.Predefined == 0 && state.PredefinedValid == 0 && state.PredefinedValue == 0,
        _ => false,
    };
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session();
        if (target is not ActionTarget.Gpu selected || selected.Feature != _feature || !_targets.TryGetValue(selected.Key, out var known)
            || !known.Choices.Any(c => c.Value == selected.Value)) { return Task.FromResult<ActionPreview?>(null); }
        var before = _platform.Read(selected.Key); var desired = _platform.Desired(selected.Key, before, selected.Value);
        if (!ValidState(before) || !ValidState(desired)) { throw new ActionUnavailableException("GpuValueUnsupported"); }
        var label = known.Choices.Single(c => c.Value == selected.Value).Label;
        var preview = new ActionPreview(target, GpuOptions.Name(_feature) + " · " + label, known.Detail,
            new(known.Label, "변경 전 상태와 드라이버 정보를 보관합니다. 외부 변경·드라이버 교체·대상 소실 시 덮어쓰지 않습니다. 사용 중인 게임/재생기는 다시 실행해 효과를 확인하세요.", RequiresRestart: false));
        _snapshots.Add(preview, new(selected.Key, before, desired, session)); return Task.FromResult<ActionPreview?>(preview);
    }
    /// <inheritdoc />
    public Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { throw new ActionUnavailableException("PlanExpired"); }
        _snapshots.Remove(plan.Preview);
        if (Session() != snapshot.Session || plan.Session != snapshot.Session) { throw new ActionUnavailableException("SessionChanged"); }
        if (_platform.Read(snapshot.Key) != snapshot.Before) { throw new ActionUnavailableException("CurrentValueChanged"); }
        return Task.FromResult(new RollbackChange(RecordKey(snapshot.Key), RollbackPurpose.UserUndo, Encode(snapshot.Before), Encode(snapshot.Applied)));
    }
    /// <inheritdoc />
    public Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var session = Session(); _ = DecodeKey(record.TargetKey);
        var before = Decode(record.Before); var applied = Decode(record.Applied);
        return Task.FromResult(record.ActionId == Definition.Id && record.Scope == Definition.Scope && record.Sid == session.Sid && record.Purpose == RollbackPurpose.UserUndo
            && ValidState(before) && ValidState(applied) && before.Stamp == applied.Stamp && before.SettingId == applied.SettingId);
    }
    /// <inheritdoc />
    public async Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct)) { throw new ActionUnavailableException("TargetRejected"); }
        return Encode(_platform.Read(DecodeKey(record.TargetKey)));
    }
    /// <inheritdoc />
    public async Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct)
    {
        if (!await ValidateAsync(record, ct) || (!desired.SameAs(record.Before) && !desired.SameAs(record.Applied))
            || (!expected.SameAs(record.Before) && !expected.SameAs(record.Applied))) { return false; }
        var session = Session();
        return _platform.CompareExchange(DecodeKey(record.TargetKey), Decode(expected), Decode(desired), () =>
        { ct.ThrowIfCancellationRequested(); if (Session() != session) { throw new ActionUnavailableException("SessionChanged"); } });
    }
}
