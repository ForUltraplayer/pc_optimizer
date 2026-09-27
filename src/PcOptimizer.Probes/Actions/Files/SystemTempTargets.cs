/**
 * @file    : SystemTempTargets.cs
 * @author  : rudals252
 * @brief   : Windows Known Folder의 Temp만 허용하고 모든 등록 사용자 프로필·보호 루트 제외
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>시스템 임시 파일 고정 대상입니다. 서비스 정지나 업데이트 정리는 수행하지 않습니다.</summary>
public static class SystemTempTargets
{
    /// <summary>Windows Temp의 오래된 일반 파일만 선택합니다.</summary>
    public const string Temp = "windows-temp";
    internal static CleanupTarget Resolve(string key, ActionSession session)
    {
        if (key != Temp || !session.IsKnown || SystemActionSession.Read(session.Scope) != session)
        { throw new ActionUnavailableException("ScopeExcluded"); }
        var env = SystemPathEnvironment.Instance;
        string Canonical(string path) => CachePathInspector.CanonicalPath(env, path);
        var windows = Canonical(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        var system = Canonical(Environment.SystemDirectory);
        if (!string.Equals(Path.GetDirectoryName(system), windows, StringComparison.OrdinalIgnoreCase)) { throw new ActionUnavailableException("TargetRejected"); }
        var root = Canonical(Path.Combine(windows, "Temp"));
        // 시스템 조치는 현재 SID를 포함한 모든 등록 사용자 프로필을 제외한다.
        var profiles = UserTempTargets.OtherProfiles(Win32RegistryReader.Instance, env, "");
        var policy = ProtectionPolicyParser.Parse(FileScanService.ReadBundledPolicy() ?? "").Policy ?? throw new ActionUnavailableException("TargetRejected");
        var resolved = new ProtectionPolicyResolver(env, Win32RegistryReader.Instance).Resolve(policy);
        var protectedRoots = profiles.Concat(resolved.Roots.Select(p => Canonical(p.Path))).ToArray();
        if (protectedRoots.Any(p => PathScope.IsSameOrUnder(root, p) || PathScope.IsSameOrUnder(p, root))) { throw new ActionUnavailableException("TargetRejected"); }
        bool Protected(string path) => protectedRoots.Any(p => PathScope.IsSameOrUnder(path, p));
        return new(Temp, root, "7일 이상 지난 Windows 임시 파일 (모든 사용자에게 영향 가능)", true, ["*"], TimeSpan.FromDays(7), Protected);
    }
}
