/**
 * @file    : VideoEditorCacheInspector.cs
 * @author  : rudals252
 * @brief   : Resolve CacheClip·CapCut Cache를 한정 시간 안에 읽기 전용으로 관측하고 경로 범위와 불완전 상태를 보존
 */
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

internal sealed class VideoEditorCacheInspector(IPathEnvironment environment, IDirectoryEntrySource source, TimeProvider time)
{
    internal List<Measurement> Inspect(ResolvedProtection protection, Func<string, bool> otherUser, bool profileVerified,
        DateTimeOffset now, Func<bool> expired, CancellationToken ct)
    {
        var output = new List<Measurement>();
        var profile = environment.GetUserProfilePath();
        if (profile is null || !profileVerified) { return output; }
        var configPath = Path.Combine(profile, DaVinciCacheConfigReader.RelativeConfig);
        var config = protection.IsProtected(configPath) || otherUser(configPath)
            ? new AppConfigReading("davinci", AppConfigReadState.Unreadable, AppConfigValueOrigin.UserFile, [])
            : new DaVinciCacheConfigReader(environment, source).Read();
        var fallback = Path.Combine(environment.GetKnownFolderPath(ProtectedKnownFolder.Videos) ?? Path.Combine(profile, "Videos"), "CacheClip");
        var resolve = config.Paths.SingleOrDefault() ?? fallback;
        var resolveApp = Path.Combine(profile, @"AppData\Roaming\Blackmagic Design\DaVinci Resolve");
        if (source.ProbeRoot(resolveApp) != RootPresence.Missing || source.ProbeRoot(resolve) != RootPresence.Missing || config.State != AppConfigReadState.NotConfigured)
        {
            if (config.State is AppConfigReadState.Invalid or AppConfigReadState.Unreadable)
            { Add("davinci", null, config.State.ToString(), "설정 위치를 해석하지 못했으므로 기본 폴더로 대체하지 않았습니다.", null); }
            else
            {
                Observe("davinci", resolve, config.State == AppConfigReadState.Configured
                    ? "Resolve 전역 설정의 CacheClip 후보입니다. 프로젝트별 경로 재정의는 확인하지 않았습니다."
                    : "기본 동영상 폴더의 CacheClip 후보만 확인했습니다. 프로젝트별·옮겨진 위치는 확인하지 않았습니다.");
            }
        }
        var capcutApp = Path.Combine(profile, @"AppData\Local\CapCut\User Data");
        if (source.ProbeRoot(capcutApp) != RootPresence.Missing)
        { Observe("capcut", Path.Combine(capcutApp, "Cache"), "Windows 기본 User Data/Cache 후보만 확인했습니다. 버전별·사용자 지정 위치는 확인하지 않았습니다."); }
        return output;

        void Observe(string app, string path, string scope)
        {
            ct.ThrowIfCancellationRequested();
            if (!PathScope.IsDriveAbsolute(path) || PathScope.IsDriveRoot(path)) { Add(app, null, "Unsupported", scope, null); return; }
            try { path = CachePathInspector.CanonicalPath(environment, path); }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            { Add(app, null, "Unreadable", scope, null); return; }
            if (expired()) { Add(app, path, "TimedOut", scope, null); return; }
            // 고정 루트와 모든 상위 폴더를 확인한다. 온라인 전용 위치를 열어 내려받거나 정션을 통과하지 않는다.
            for (var parent = path; parent is not null && !PathScope.IsDriveRoot(parent); parent = PathScope.GetParent(parent))
            {
                var presence = source.ProbeRoot(parent);
                if (presence is RootPresence.ReparsePoint or RootPresence.Placeholder or RootPresence.AccessDenied or RootPresence.Error)
                { Add(app, path, presence.ToString(), scope, null); return; }
            }
            bool Blocked(string candidate) => otherUser(candidate) || IsProtected(candidate, path, app == "davinci", protection);
            var target = new ObservationTarget(0, path, ["*"], true, app, ObservationPrecedence.Reviewed, TargetSource.UserConfig, [app], true, [], [], false);
            var measured = new TargetedEnumerator(source, time).Measure(target, Blocked, new(StringComparer.OrdinalIgnoreCase), TimeSpan.FromSeconds(5), expired, ct, new ReparseAncestorCheck(source));
            var state = measured.State == TargetState.Observed && measured.Skips.Total > 0 ? "Partial" : measured.State.ToString();
            Add(app, path, state, scope, measured);
        }
        void Add(string app, string? path, string state, string scope, TargetMeasurement? observed)
        {
            void Field(string name, MeasurementValue value, string? unit = null) => output.Add(new(VideoEditorCacheRule.Field(app, name), value, unit,
                "제한된 영상 편집 캐시 메타데이터 조회", now, state == "Partial" ? MeasurementQuality.Partial : MeasurementQuality.Observed));
            Field("state", new TextValue(state)); Field("scope", new TextValue(scope));
            if (path is not null) { Field("paths", new TextListValue([path])); }
            if (observed is { IsObserved: true })
            { Field("bytes", new IntegerValue(observed.Bytes), "bytes"); Field("files", new IntegerValue(observed.FileCount)); Field("skipped", new IntegerValue(observed.Skips.Total)); }
        }
    }
    internal static bool IsProtected(string candidate, string root, bool resolve, ResolvedProtection protection)
    {
        foreach (var entry in protection.SourceRoots.Where(p => PathScope.IsSameOrUnder(candidate, p.Path)))
        {
            // 읽기 예외는 Videos 바로 아래 CacheClip 하나뿐. 겹친 Documents/동기화/시스템 보호는 해제하지 않는다.
            if (resolve && entry.Origin == ProtectedRootOrigin.KnownFolder && entry.Label == "Videos"
                && root.Equals(Path.Combine(entry.Path, "CacheClip"), StringComparison.OrdinalIgnoreCase)
                && PathScope.IsSameOrUnder(candidate, root)) { continue; }
            return true;
        }
        return false;
    }
}
