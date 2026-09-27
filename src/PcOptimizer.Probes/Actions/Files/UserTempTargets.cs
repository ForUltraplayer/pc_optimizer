/**
 * @file    : UserTempTargets.cs
 * @author  : rudals252
 * @brief   : 현재 등록 프로필의 기본 Temp/탐색기 캐시만 허용하고 Known Folder·동기화·다른 사용자 보호 적용
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using Microsoft.Win32;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>UI에는 코드 카탈로그 키만 노출합니다. 임의 경로는 받지 않습니다.</summary>
public static class UserTempTargets
{
    /// <summary>기본 사용자 Temp의 7일 이상 지난 파일입니다.</summary>
    public const string Temp = "user-temp";
    /// <summary>탐색기 썸네일/아이콘 캐시 파일 이름 패턴입니다.</summary>
    public const string ExplorerCache = "explorer-cache";

    internal static CleanupTarget Resolve(string key, ActionSession session)
    {
        if (session.Scope != ActionUserScope.Full || SystemActionSession.Read() != session) { throw new UnauthorizedAccessException("ScopeExcluded"); }
        _ = RollbackStore.CurrentRoot(session.Sid); // HKLM 등록 프로필·현재 Known Folder 대조(파일 생성 없음).
        var environment = SystemPathEnvironment.Instance;
        string Canonical(string path) => CachePathInspector.CanonicalPath(environment, path);
        var local = Canonical(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var root = key switch
        {
            Temp => Canonical(Path.Combine(local, "Temp")),
            ExplorerCache => Canonical(Path.Combine(local, "Microsoft", "Windows", "Explorer")),
            _ => throw new UnauthorizedAccessException("UnknownCatalogKey"),
        };
        if (key == Temp)
        {
            foreach (var variable in new[] { "TEMP", "TMP" })
            {
                if (Environment.GetEnvironmentVariable(variable) is not { } value || !Canonical(value).Equals(root, StringComparison.OrdinalIgnoreCase))
                { throw new UnauthorizedAccessException("RelocatedTempUnsupported"); }
            }
        }
        var protect = ProtectionFor(root, session);
        return new(key, root, key == Temp ? "7일 이상 지난 사용자 임시 파일" : "사용 중이 아닌 오래된 탐색기 미리보기 캐시", key == Temp,
            key == Temp ? ["*"] : [ScanRootCatalog.THUMBNAIL_CACHE_PATTERN, ScanRootCatalog.ICON_CACHE_PATTERN], TimeSpan.FromDays(7), protect);
    }

    // 앱 캐시도 임시 파일과 동일한 보호·타 SID 경계를 통과해야 한다.
    internal static Func<string, bool> ProtectionFor(string root, ActionSession session)
    {
        var environment = SystemPathEnvironment.Instance;
        string Canonical(string path) => CachePathInspector.CanonicalPath(environment, path);
        var policy = ProtectionPolicyParser.Parse(FileScanService.ReadBundledPolicy() ?? "").Policy ?? throw new UnauthorizedAccessException("ProtectionUnavailable");
        var resolved = new ProtectionPolicyResolver(environment, Win32RegistryReader.Instance).Resolve(policy);
        var protection = new ResolvedProtection(resolved.Roots.Select(p => p with { Path = Canonical(p.Path) }));
        var users = OtherUserLocationGuard.Create(Win32RegistryReader.Instance, environment, session.Sid);
        if (!users.ProfileListAvailable) { throw new UnauthorizedAccessException("ProfileListUnavailable"); }
        var others = OtherProfiles(Win32RegistryReader.Instance, environment, session.Sid);
        bool Protected(string path) => protection.IsProtected(path) || users.IsOtherUserLocation(path) || others.Any(p => PathScope.IsSameOrUnder(path, p));
        if (others.Any(p => PathScope.IsSameOrUnder(p, root))) { throw new UnauthorizedAccessException("OtherProfileInsideRoot"); }
        if (Protected(root) || protection.Roots.Any(p => p.Path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)))
        { throw new UnauthorizedAccessException("ProtectedRoot"); }
        return Protected;
    }

    // 조회용 가드의 현재 프로필 우선 허용과 달리 삭제는 중첩된 다른 SID 경로도 반드시 보호한다.
    internal static IReadOnlyList<string> OtherProfiles(IRegistryReader registry, IPathEnvironment environment, string sid)
    {
        var keys = registry.ReadSubKeyNames(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY);
        if (keys.Status != RegistryReadStatus.Found) { throw new UnauthorizedAccessException("ProfileListUnavailable"); }
        var paths = new List<string>();
        foreach (var key in keys.Names.Where(k => !string.Equals(k, sid, StringComparison.OrdinalIgnoreCase) && k is not ("S-1-5-18" or "S-1-5-19" or "S-1-5-20")))
        {
            var value = registry.ReadStringValue(RegistryRoot.LocalMachine, RegistryView.Registry64,
                OtherUserLocationGuard.PROFILE_LIST_KEY + "\\" + key, OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE);
            if (value.Status != RegistryReadStatus.Found || value.Text is null || PathTemplate.ExpandOnly(value.Text, environment) is not { } expanded)
            { throw new UnauthorizedAccessException("ProfilePathUnavailable"); }
            paths.Add(CachePathInspector.CanonicalPath(environment, expanded));
        }
        return paths;
    }
}
