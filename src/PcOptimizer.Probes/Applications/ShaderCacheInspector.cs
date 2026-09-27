/**
 * @file    : ShaderCacheInspector.cs
 * @author  : rudals252
 * @brief   : Steam 라이브러리별 shadercache와 사용자 NVIDIA/Direct3D 캐시의 제한된 읽기 전용 관측
 */
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

internal sealed class ShaderCacheInspector(IPathEnvironment environment, IRegistryReader registry, IDirectoryEntrySource source, TimeProvider time)
{
    internal List<Measurement> Inspect(ResolvedProtection protection, Func<string, bool> otherUser, bool verified,
        DateTimeOffset now, Func<bool> expired, CancellationToken ct)
    {
        var output = new List<Measurement>();
        if (!verified || environment.GetUserProfilePath() is not { } profile) { return output; }
        var programFiles = new[] { "ProgramFiles", "ProgramFiles(x86)" }.Select(environment.GetEnvironmentVariable)
            .OfType<string>().Select(Canonical).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Protected(string candidate, bool steam) => otherUser(candidate) || protection.SourceRoots.Any(p => PathScope.IsSameOrUnder(candidate, p.Path)
            && !(steam && p.Origin == ProtectedRootOrigin.SystemPath && p.Label is "%ProgramFiles%" or "%ProgramFiles(x86)%" && programFiles.Contains(p.Path)));
        bool ConfigAllowed(string path) => Canonical(path) is { } full && !Protected(full, true);
        var steam = new SteamLibraryReader(environment, registry, source, ConfigAllowed).Read();
        if (steam.State != AppConfigReadState.NotConfigured)
        {
            if (steam.State != AppConfigReadState.Configured || steam.Paths.Count > ShaderCacheRule.MaxRows)
            { Add("steam", 0, "라이브러리 설정", null, steam.State == AppConfigReadState.Configured ? "LimitExceeded" : steam.State.ToString(), null); Count("steam", 1); }
            else
            {
                var paths = steam.Paths.Select(p => Canonical(p) ?? p).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                for (var i = 0; i < paths.Length; i++) { Observe("steam", i, $"라이브러리 {i + 1}", paths[i], true); }
                Count("steam", paths.Length);
            }
        }
        var local = Canonical(environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Path.Combine(profile, "AppData", "Local"));
        if (local is null) { Add("graphics", 0, "사용자 캐시 위치", null, "Unsupported", null); Count("graphics", 1); return output; }
        var graphics = new[] { ("NVIDIA DXCache", @"NVIDIA\DXCache"), ("NVIDIA GLCache", @"NVIDIA\GLCache"), ("Windows Direct3D", "D3DSCache"), ("NVIDIA NV_Cache", @"NVIDIA Corporation\NV_Cache") };
        var roots = graphics.Select(g => Path.Combine(local, g.Item2)).ToArray();
        if (roots.Any(p => AncestorFailure(p) is not null || source.ProbeRoot(p) != RootPresence.Missing))
        {
            for (var i = 0; i < roots.Length; i++) { Observe("graphics", i, graphics[i].Item1, roots[i], false); }
            Count("graphics", roots.Length);
        }
        return output;

        void Count(string group, int count) => output.Add(new(ShaderCacheRule.CountField(group), new IntegerValue(count), null, "셰이더 캐시 한정 관측", now, MeasurementQuality.Observed));
        void Observe(string group, int index, string label, string path, bool steamCache)
        {
            ct.ThrowIfCancellationRequested();
            if (Canonical(path) is not { } full) { Add(group, index, label, null, "Unsupported", null); return; }
            if (expired()) { Add(group, index, label, full, "TimedOut", null); return; }
            if (Protected(full, steamCache)) { Add(group, index, label, full, "Protected", null); return; }
            if (AncestorFailure(full) is { } failure) { Add(group, index, label, full, failure.ToString(), null); return; }
            var target = new ObservationTarget(index, full, ["*"], true, group, ObservationPrecedence.Reviewed, TargetSource.UserConfig, [group], true, [], [], false);
            var measured = new TargetedEnumerator(source, time).Measure(target, p => Protected(p, steamCache), new(StringComparer.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(3), expired, ct, new ReparseAncestorCheck(source));
            Add(group, index, label, full, measured.State == TargetState.Observed && measured.Skips.Total > 0 ? "Partial" : measured.State.ToString(), measured);
        }
        void Add(string group, int index, string label, string? path, string state, TargetMeasurement? measured)
        {
            void Field(string key, MeasurementValue value) => output.Add(new(ShaderCacheRule.Field(group, index, key), value, key == "bytes" ? "bytes" : null,
                "셰이더 캐시 한정 관측", now, state == "Partial" ? MeasurementQuality.Partial : MeasurementQuality.Observed));
            Field("label", new TextValue(label)); Field("state", new TextValue(state));
            if (path is not null) { Field("paths", new TextListValue([path])); }
            if (measured is { IsObserved: true }) { Field("bytes", new IntegerValue(measured.Bytes)); Field("files", new IntegerValue(measured.FileCount)); }
        }
    }
    private RootPresence? AncestorFailure(string path)
    {
        var parents = new Stack<string>();
        for (var parent = path; parent is not null && !PathScope.IsDriveRoot(parent); parent = PathScope.GetParent(parent)) { parents.Push(parent); }
        foreach (var parent in parents)
        {
            var presence = source.ProbeRoot(parent);
            if (presence is RootPresence.ReparsePoint or RootPresence.Placeholder or RootPresence.AccessDenied or RootPresence.Error) { return presence; }
        }
        return null;
    }
    private string? Canonical(string path)
    {
        try { return CachePathInspector.CanonicalPath(environment, path); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }
}
