/**
 * @file    : FileCleanupAdapter.cs
 * @author  : rudals252
 * @brief   : 완전한 미리보기의 파일 집합만 고정하고 매 파일 삭제 직전 ID/메타데이터/보호 관문 재검증
 */
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>기본 사용자 캐시 정리입니다. 소유권 변경/프로세스 종료/디렉터리 재귀 삭제는 하지 않습니다.</summary>
public sealed class FileCleanupAdapter : IActionAdapter
{
    private readonly Func<string, ActionSession, CleanupTarget> _resolve;
    private readonly Func<ActionSession> _session;
    private readonly IFileCleanupPlatform _platform;
    private readonly TimeProvider _time;
    private readonly TimeSpan _budget;
    private readonly int _maxEntries;
    private readonly ActionDefinition _definition;
    private readonly ConditionalWeakTable<ActionPreview, FileCleanupSnapshot> _snapshots = new();
    /// <summary>현재 세션 공급자는 앱의 Full/SystemOnly 범위 판정을 전달해야 합니다.</summary>
    public FileCleanupAdapter(Func<ActionSession> session) : this(UserTempTargets.Resolve, session, new NativeFileCleanupPlatform()) { }
    internal FileCleanupAdapter(Func<string, ActionSession, CleanupTarget> resolve, Func<ActionSession> session, IFileCleanupPlatform platform,
        TimeProvider? time = null, TimeSpan? budget = null, int maxEntries = 20000, ActionDefinition? definition = null)
    { _resolve = resolve; _session = session; _platform = platform; _time = time ?? TimeProvider.System; _budget = budget ?? TimeSpan.FromSeconds(15); _maxEntries = maxEntries; _definition = definition ?? new(ActionId.UserFiles, ActionScope.CurrentUser); }
    /// <summary>Windows 기본 Temp 전용입니다. Update/Installer/WinSxS는 포함하지 않습니다.</summary>
    public static FileCleanupAdapter ForSystemTemp(Func<ActionSession> session) => new(SystemTempTargets.Resolve, session, new NativeFileCleanupPlatform(), definition: new(ActionId.SystemFiles, ActionScope.System));
    /// <summary>Adobe 기본 경로의 오래된 cfa/pek만 확인합니다. 다른 앱·사용자 지정 위치는 포함하지 않습니다.</summary>
    public static FileCleanupAdapter ForAdobeCache(Func<ActionSession> session) => new(AdobeCacheTargets.Resolve, session, new NativeFileCleanupPlatform(), definition: new(ActionId.AppFiles, ActionScope.CurrentUser));
    /// <summary>기본 위치와 사용자가 명시한 세션 한정 Adobe 캐시를 같은 보호 엔진으로 처리합니다.</summary>
    public static FileCleanupAdapter ForAdobeCache(Func<ActionSession> session, AdobeCacheLocationCatalog locations)
        => new(locations.Resolve, session, new NativeFileCleanupPlatform(), definition: new(ActionId.AppFiles, ActionScope.CurrentUser));
    /// <summary>선택한 Steam 라이브러리를 설정과 다시 대조하는 전용 카탈로그입니다.</summary>
    public static FileCleanupAdapter ForSteamCache(Func<ActionSession> session, SteamCacheLocationCatalog locations)
        => new(locations.Resolve, session, new NativeFileCleanupPlatform(), definition: new(ActionId.SteamShaderCache, ActionScope.CurrentUser));
    /// <summary>현재 사용자 LocalAppData의 NVIDIA·Direct3D 셰이더 캐시에서 30일 이상 쓰이지 않은 파일만 처리합니다.</summary>
    public static FileCleanupAdapter ForGraphicsCache(Func<ActionSession> session)
        => new(GraphicsCacheTargets.Resolve, session, new NativeFileCleanupPlatform(), definition: new(ActionId.GraphicsShaderCache, ActionScope.CurrentUser));
    /// <inheritdoc />
    public ActionDefinition Definition => _definition;
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
    {
        if (restore || target is not ActionTarget.Files selected) { return Task.FromResult<ActionPreview?>(null); }
        var session = _session();
        if (!session.IsKnown || (Definition.Scope == ActionScope.CurrentUser && session.Scope != ActionUserScope.Full)) { return Task.FromResult<ActionPreview?>(null); }
        var spec = _resolve(selected.CatalogKey, session);
        if (spec.CheckIdle?.Invoke() is { } busy) { throw new ActionUnavailableException(busy); }
        var snapshot = Scan(spec, ct);
        if (spec.CheckIdle?.Invoke() is { } changed) { throw new ActionUnavailableException(changed); }
        var preview = new ActionPreview(target, $"확인한 파일 {snapshot.Files.Count:N0}개만 정리합니다.",
            spec.Impact ?? "휴지통을 거치지 않습니다. 앱에서 다시 만들거나 다운로드할 수 있으며, 사용 중이거나 바뀐 파일은 건너뜁니다.",
            new(spec.Label + "\n" + spec.Root, $"최근 파일·보호 대상 등 {snapshot.Excluded:N0}개 제외. 폴더는 남기고 파일별로 재확인합니다.", snapshot.Bytes, false));
        _snapshots.Add(preview, snapshot);
        return Task.FromResult<ActionPreview?>(preview);
    }
    private FileCleanupSnapshot Scan(CleanupTarget target, CancellationToken ct)
    {
        var started = _time.GetTimestamp();
        var cutoff = (_time.GetUtcNow() - target.MinimumAge).UtcDateTime.ToFileTimeUtc();
        var files = new List<FileStamp>();
        var excluded = 0;
        long bytes = 0;
        var count = 0;
        void Budget() { ct.ThrowIfCancellationRequested(); if (++count > _maxEntries || _time.GetElapsedTime(started) >= _budget) { throw new IOException("InspectionIncomplete"); } }
        using var root = _platform.Open(target.Root, true, false);
        var rootStamp = root.Read();
        var stack = new Stack<string>(); stack.Push(target.Root);
        while (stack.TryPop(out var directory))
        {
            Budget();
            using var pinned = _platform.Open(directory, true, false);
            if (!pinned.Read().IsPlain || target.Protected(directory)) { throw new UnauthorizedAccessException("ProtectedDirectory"); }
            foreach (var path in _platform.Enumerate(directory))
            {
                Budget();
                if (!Inside(path, target.Root) || target.Protected(path)) { excluded++; continue; }
                // 속성만 보고 링크/placeholder 하위로 들어가지 않는다. 실제 파일은 핸들 메타데이터로 다시 확인한다.
                var attributes = _platform.Attributes(path);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x40000 | (FileAttributes)0x400000)) != 0) { excluded++; continue; }
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                using var entry = _platform.Open(path, isDirectory, false);
                var observed = entry.Read();
                if (!observed.IsPlain || observed.Identity.VolumeSerialNumber != rootStamp.Identity.VolumeSerialNumber) { throw new IOException("TargetChanged"); }
                if (isDirectory) { if (target.Recursive) { stack.Push(path); } else { excluded++; } continue; }
                if (observed.Links != 1 || (observed.Attributes & FileAttributes.ReadOnly) != 0 || observed.Written > cutoff || observed.Created > cutoff
                    || !target.Patterns.Any(p => FileSystemName.MatchesSimpleExpression(p, Path.GetFileName(path), ignoreCase: true))) { excluded++; continue; }
                bytes = checked(bytes + observed.Length); files.Add(observed);
            }
        }
        Budget();
        return new(target, rootStamp, files, bytes, excluded);
    }
    internal static bool Inside(string path, string root) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    /// <inheritdoc />
    public Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
    {
        if (!_snapshots.TryGetValue(plan.Preview, out var snapshot)) { return Task.FromResult(new ActionResult(plan.Id, false, false, "PlanExpired")); }
        _snapshots.Remove(plan.Preview);
        if (_session() != plan.Session) { return Task.FromResult(new ActionResult(plan.Id, false, false, "SessionChanged")); }
        var current = _resolve(snapshot.Target.Key, _session());
        if (current.CheckIdle?.Invoke() is { } busy) { return Task.FromResult(new ActionResult(plan.Id, false, false, busy)); }
        if (!current.Root.Equals(snapshot.Target.Root, StringComparison.OrdinalIgnoreCase)) { return Task.FromResult(new ActionResult(plan.Id, false, false, "TargetChanged")); }
        using var root = _platform.Open(current.Root, true, false);
        if (root.Read().Identity != snapshot.Root.Identity || current.Protected(current.Root)) { return Task.FromResult(new ActionResult(plan.Id, false, false, "TargetChanged")); }
        // 변경 전 전체 원본을 재확인한다. 새 파일을 열거해 대상에 추가하지 않는다.
        foreach (var file in snapshot.Files)
        {
            ct.ThrowIfCancellationRequested();
            if (!Inside(file.Path, current.Root) || current.Protected(file.Path)) { return Task.FromResult(new ActionResult(plan.Id, false, false, "TargetChanged")); }
            try
            {
                using var item = _platform.Open(file.Path, false, false);
                if (!file.Matches(item.Read())) { return Task.FromResult(new ActionResult(plan.Id, false, false, "TargetChanged")); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return Task.FromResult(new ActionResult(plan.Id, false, false, "TargetChanged")); }
        }
        var before = _platform.FreeBytes(current.Root);
        var started = false; var removed = 0; var skipped = 0; var failed = 0;
        string? stopReason = null;
        foreach (var file in snapshot.Files)
        {
            if (ct.IsCancellationRequested || _session() != plan.Session) { break; }
            try
            {
                using var item = _platform.Open(file.Path, false, true); // 독점 핸들: 사용 중이면 거절.
                var actual = item.Read();
                if (!file.Matches(actual) || actual.Links != 1 || !actual.IsPlain || current.Protected(file.Path)) { skipped++; continue; }
                // 전체 확인 후 시작된 앱도 삭제 직전에 다시 조회한다. 이미 처리한 개수는 보존한다.
                if (current.CheckIdle?.Invoke() is { } reason) { stopReason = reason; break; }
                ct.ThrowIfCancellationRequested();
                if (!started) { execution.MarkStarted(); started = true; }
                item.MarkForDeletion();
                // 파일 핸들 닫기로 해당 파일만 삭제된다. 경로 기반 Delete/재귀 폴더 삭제는 없다.
                item.Dispose();
                removed++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed++; }
        }
        skipped += snapshot.Files.Count - removed - skipped - failed;
        var after = _platform.FreeBytes(current.Root);
        long? delta = before is not null && after is not null ? after - before : null;
        var code = stopReason ?? (!started ? snapshot.Files.Count == 0 ? "NoEligibleFiles" : ct.IsCancellationRequested ? "Cancelled" : "FilesUnavailable"
            : skipped + failed > 0 ? "Partial" : "Completed");
        return Task.FromResult(new ActionResult(plan.Id, started, started && skipped + failed == 0, code, new(delta, removed, skipped, failed)));
    }
}
