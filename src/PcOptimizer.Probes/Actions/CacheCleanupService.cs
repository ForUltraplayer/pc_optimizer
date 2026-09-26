/**
 * @file    : CacheCleanupService.cs
 * @author  : rudals252
 * @brief   : 공식 캐시 도구의 일회성 미리보기와 실행 직전 재검증·실행 후 관측 계약
 */
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.Probes.Actions;

/// <summary>1차 자동 정리 허용 목록입니다.</summary>
public enum CacheTool { Npm, Pip, NuGetHttp }

/// <summary>공식 도구가 보고한 캐시 위치와 실행 파일 지문입니다.</summary>
public sealed record CacheToolLocation(CacheTool Tool, string Executable, string CachePath, string Fingerprint, string? Script = null);

/// <summary>변경하지 않는 사전 관측입니다. Bytes는 논리 크기이며 확보 용량 보장이 아닙니다.</summary>
public sealed record CacheInspection(bool Allowed, long Bytes, string? Reason);

/// <summary>서비스가 발급한 일회성 실행 계획입니다.</summary>
public sealed record CacheCleanupPlan(Guid Id, CacheToolLocation Location, long ObservedBytes, DateTimeOffset ExpiresAt);

/// <summary>미리보기 결과입니다.</summary>
public sealed record CachePreparation(CacheCleanupPlan? Plan, string? Reason);

/// <summary>실행 결과입니다. 성공 종료와 후속 관측을 구분합니다.</summary>
public sealed record CacheCleanupResult(bool ToolSucceeded, long? RemainingBytes, string Code)
{
    /// <summary>미리보기 시점이 아닌 실제 명령 실행 직전의 논리 크기입니다.</summary>
    public long? BeforeBytes { get; init; }
}

/// <summary>실제 OS 및 공식 도구 경계입니다. 테스트에서는 가짜 구현을 씁니다.</summary>
public interface ICacheToolBackend
{
    /// <summary>현재 사용자의 설치 도구와 설정 위치를 읽습니다.</summary>
    Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct);
    /// <summary>권한·경로·보호·링크 경계와 현재 논리 크기를 확인합니다.</summary>
    Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct);
    /// <summary>허용된 종류의 고정된 공식 정리 명령만 실행합니다.</summary>
    Task<bool> ClearAsync(CacheToolLocation location, CancellationToken ct);
}

/// <summary>미리보기 없이는 실행하지 않으며 실행 중복을 막는 공식 도구 조율기입니다.</summary>
public sealed class CacheCleanupService(ICacheToolBackend backend, TimeProvider? time = null, IAppLogger? logger = null)
{
    private static readonly TimeSpan PLAN_LIFETIME = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly IAppLogger _logger = logger ?? NullAppLogger.Instance;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (CacheCleanupPlan Plan, long Started)> _plans = new();
    private readonly SemaphoreSlim _gate = new(1);

    /// <summary>도구와 대상을 조회하여 짧은 수명의 일회성 계획을 발급합니다. 정리는 하지 않습니다.</summary>
    public async Task<CachePreparation> PrepareAsync(CacheTool tool, CancellationToken ct)
    {
        foreach (var pair in _plans.Where(pair => _time.GetElapsedTime(pair.Value.Started) >= PLAN_LIFETIME)) { _plans.TryRemove(pair.Key, out _); }
        var location = await backend.LocateAsync(tool, ct).ConfigureAwait(false);
        if (location is null) { return new(null, "ToolUnavailable"); }
        var inspection = await backend.InspectAsync(location, ct).ConfigureAwait(false);
        if (!inspection.Allowed) { return new(null, inspection.Reason); }
        var plan = new CacheCleanupPlan(Guid.NewGuid(), location, inspection.Bytes, _time.GetUtcNow() + PLAN_LIFETIME);
        _plans[plan.Id] = (plan, _time.GetTimestamp());
        _logger.Info(nameof(CacheCleanupService), $"CleanupPreview tool={tool} plan={plan.Id}");
        return new(plan, null);
    }

    /// <summary>확인된 계획 ID로만 실행합니다. 대상·도구가 바뀌면 재확인을 요구합니다.</summary>
    public async Task<CacheCleanupResult> ExecuteAsync(Guid planId, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false)) { return new(false, null, "Busy"); }
        try
        {
            if (!_plans.TryRemove(planId, out var entry) || _time.GetElapsedTime(entry.Started) >= PLAN_LIFETIME) { return new(false, null, "PlanExpired"); }
            var plan = entry.Plan;
            var current = await backend.LocateAsync(plan.Location.Tool, ct).ConfigureAwait(false);
            if (current != plan.Location) { return new(false, null, "TargetChanged"); }
            var inspection = await backend.InspectAsync(current, ct).ConfigureAwait(false);
            if (!inspection.Allowed) { return new(false, null, inspection.Reason ?? "Blocked"); }
            if (_time.GetElapsedTime(entry.Started) >= PLAN_LIFETIME) { return new(false, null, "PlanExpired"); }
            ct.ThrowIfCancellationRequested();
            _logger.Info(nameof(CacheCleanupService), $"CleanupStarted tool={current.Tool} plan={plan.Id}");
            var success = await backend.ClearAsync(current, ct).ConfigureAwait(false);
            var after = await backend.InspectAsync(current, ct).ConfigureAwait(false);
            _logger.Info(nameof(CacheCleanupService), $"CleanupFinished tool={current.Tool} success={success} verified={after.Allowed}");
            return new(success, after.Allowed ? after.Bytes : null, success ? "Completed" : "ToolFailed") { BeforeBytes = inspection.Bytes };
        }
        finally { _gate.Release(); }
    }
}
