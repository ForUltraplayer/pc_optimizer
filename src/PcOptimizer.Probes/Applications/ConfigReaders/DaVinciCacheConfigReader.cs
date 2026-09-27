/**
 * @file    : DaVinciCacheConfigReader.cs
 * @author  : rudals252
 * @brief   : Resolve의 작은 텍스트 설정에서 CacheClip 위치만 읽고(키가 없으면 첫 미디어 저장소의 CacheClip 기본값) 프로젝트·미디어 저장소 전체는 반환하지 않음
 */
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

internal sealed class DaVinciCacheConfigReader(IPathEnvironment environment, IDirectoryEntrySource source)
{
    internal const string RelativeConfig = @"AppData\Roaming\Blackmagic Design\DaVinci Resolve\Preferences\config.dat";
    internal const int MaxBytes = 64 * 1024;
    internal const string DefaultCacheDir = "CacheClip";
    internal AppConfigReading Read()
    {
        var profile = environment.GetUserProfilePath();
        if (profile is null) { return new("davinci", AppConfigReadState.Unreadable, AppConfigValueOrigin.None, []); }
        var content = ConfigFileText.Read(environment, source, Path.Combine(profile, RelativeConfig), MaxBytes);
        if (content.Unreadable) { return new("davinci", AppConfigReadState.Unreadable, AppConfigValueOrigin.UserFile, []); }
        return content.Text is null ? AppConfigReading.NotConfigured("davinci") : Parse(content.Text);
    }
    internal static AppConfigReading Parse(string text)
    {
        AppConfigReading Invalid() => new("davinci", AppConfigReadState.Invalid, AppConfigValueOrigin.UserFile, []);
        if (text.Length > MaxBytes || text.Contains('\0')) { return Invalid(); }
        var keys = new HashSet<string>(["RenderCaching.CacheDir", "Site.Count", "Site.1.FS.1.Type", "Site.1.FS.1.Root"], StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            var equals = line.IndexOf('='); if (equals < 0) { continue; }
            var key = line[..equals].Trim();
            if (keys.Contains(key) && !values.TryAdd(key, line[(equals + 1)..].Trim())) { return Invalid(); }
        }
        // Resolve 20.1(이 PC 관측)은 기본 설정에서 RenderCaching.CacheDir 키를 쓰지 않는다. 키가 없으면 첫 미디어 저장소의 CacheClip이 기본 위치다.
        var cache = values.GetValueOrDefault("RenderCaching.CacheDir", DefaultCacheDir);
        string? path;
        if (cache == DefaultCacheDir)
        {
            if (values.GetValueOrDefault("Site.Count") != "1" || values.GetValueOrDefault("Site.1.FS.1.Type") != "IOFileSys"
                || ConfigPathValue.Parse(values.GetValueOrDefault("Site.1.FS.1.Root")) is not { } storage) { return Invalid(); }
            path = ConfigPathValue.Parse(storage + @"\CacheClip");
        }
        else { path = ConfigPathValue.Parse(cache); }
        // 임의 저장 폴더를 캐시로 단정하지 않고 확인한 폴더 이름만 관측 후보로 돌려준다.
        if (path is null || !Path.GetFileName(path).Equals("CacheClip", StringComparison.OrdinalIgnoreCase)) { return Invalid(); }
        return new("davinci", AppConfigReadState.Configured, AppConfigValueOrigin.UserFile, [path]);
    }
}
