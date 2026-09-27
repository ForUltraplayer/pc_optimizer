/**
 * @file    : WindowsUpdateCleanupAdapter.cs
 * @author  : rudals252
 * @brief   : Download 파일 미리보기·서비스 원본 보존·정리·복구를 단일 작업으로 연결
 */
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions.Files;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

/// <summary>Windows Update Download의 고정 파일 집합만 서비스 중지 구간에서 처리합니다.</summary>
public sealed class WindowsUpdateCleanupAdapter : IActionAdapter
{
    /// <summary>사용자 지정 경로를 받지 않는 업데이트 다운로드 카탈로그 키입니다.</summary>
    public const string Download = "windows-update-download";
    private readonly Func<ActionSession> _session;
    private readonly UpdateCleanupGuard _guard;
    private readonly UpdateServiceMaintenance _maintenance;
    private readonly ConditionalWeakTable<ActionPreview, Prepared> _prepared = new();

    /// <summary>앱의 공통 원본 저장소와 실제 사용자 세션을 연결합니다.</summary>
    public WindowsUpdateCleanupAdapter(IRollbackStore store, Func<ActionSession> session)
    {
        _session = session;
        var services = new UpdateServicePlatform();
        _guard = new(services);
        _maintenance = new(store, services, session);
    }
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.WindowsUpdateCache, ActionScope.System);
    /// <inheritdoc />
    public async Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
    {
        if (restore || target is not ActionTarget.Files { CatalogKey: Download }) { return null; }
        var session = _session();
        if (!session.IsKnown || SystemActionSession.Read(session.Scope) != session) { throw new ActionUnavailableException("ScopeExcluded"); }
        _guard.BeforeStopping(ct);
        var phase = new Phase { Token = ct };
        var files = new FileCleanupAdapter((key, owner) => WindowsUpdateDownloadTargets.Resolve(key, owner, () => _guard.FileBoundary(phase.Stopped, phase.Token)),
            _session, new NativeFileCleanupPlatform(), definition: Definition);
        var inner = await files.PrepareAsync(target, false, ct).ConfigureAwait(false);
        if (inner is null) { return null; }
        _guard.BeforeStopping(ct);
        if (_session() != session) { throw new ActionUnavailableException("SessionChanged"); }
        var preview = inner with { Details = inner.Details! with
        {
            SafetyNote = inner.Details.SafetyNote + " 실행 직전에 다운로드·설치 활동을 다시 확인합니다. 서비스 복구 실패 시 별도 복구 기록을 확인해야 합니다.",
            RequiresRestart = null,
        } };
        _prepared.Add(preview, new(files, inner, phase));
        return preview;
    }
    /// <inheritdoc />
    public async Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
    {
        if (!_prepared.TryGetValue(plan.Preview, out var prepared)) { return new(plan.Id, false, false, "PlanExpired"); }
        _prepared.Remove(plan.Preview);
        ct.ThrowIfCancellationRequested();
        if (_session() != plan.Session) { return new(plan.Id, false, false, "SessionChanged"); }
        // 공간 확보 대상이 없으면 서비스도 건드리지 않는다. 0바이트 파일만 있어도 그대로 둔다.
        if (prepared.Preview.Details?.EstimatedLogicalBytes is not > 0) { return new(plan.Id, false, false, "NoEligibleFiles"); }
        _guard.BeforeStopping(ct);
        prepared.Phase.Token = ct;
        return await _maintenance.RunAsync(plan, execution, async (started, token) =>
        {
            prepared.Phase.Stopped = true;
            var result = await prepared.Files.ExecuteAsync(plan with { Preview = prepared.Preview }, started, token).ConfigureAwait(false);
            return !result.Started && result.Code == "NoEligibleFiles" ? result with { Code = "UpdateNoFilesChanged" } : result;
        }, UpdateCleanupGuard.CheckNonActivating, ct).ConfigureAwait(false);
    }
    private sealed class Phase
    {
        internal bool Stopped;
        internal CancellationToken Token;
    }
    private sealed record Prepared(FileCleanupAdapter Files, ActionPreview Preview, Phase Phase);
}
