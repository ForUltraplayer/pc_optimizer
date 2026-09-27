/**
 * @file    : WindowsUpdateDownloadTargets.cs
 * @author  : rudals252
 * @brief   : 고정 Windows Download 하위의 오래된 일반 파일만 기존 보호 정책으로 선택
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Actions.Files;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

internal static class WindowsUpdateDownloadTargets
{
    internal static CleanupTarget Resolve(string key, ActionSession session, Func<string?> checkIdle)
    {
        if (key != WindowsUpdateCleanupAdapter.Download || !session.IsKnown || SystemActionSession.Read(session.Scope) != session)
        { throw new ActionUnavailableException("ScopeExcluded"); }
        var env = SystemPathEnvironment.Instance;
        string Canonical(string path) => CachePathInspector.CanonicalPath(env, path);
        var windows = Canonical(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (!string.Equals(Path.GetDirectoryName(Canonical(Environment.SystemDirectory)), windows, StringComparison.OrdinalIgnoreCase))
        { throw new ActionUnavailableException("TargetRejected"); }
        var root = Canonical(Path.Combine(windows, "SoftwareDistribution", "Download"));
        if (!Directory.Exists(root)) { throw new ActionUnavailableException("UpdateDownloadMissing"); }
        var profiles = UserTempTargets.OtherProfiles(Win32RegistryReader.Instance, env, "");
        var policy = ProtectionPolicyParser.Parse(FileScanService.ReadBundledPolicy() ?? "").Policy
            ?? throw new ActionUnavailableException("TargetRejected");
        var resolved = new ProtectionPolicyResolver(env, Win32RegistryReader.Instance).Resolve(policy);
        var protectedRoots = profiles.Concat(resolved.Roots.Select(p => Canonical(p.Path))).ToArray();
        if (protectedRoots.Any(p => PathScope.IsSameOrUnder(root, p) || PathScope.IsSameOrUnder(p, root)))
        { throw new ActionUnavailableException("TargetRejected"); }
        return new(key, root, "7일 이상 지난 Windows 업데이트 다운로드 파일", true, ["*"], TimeSpan.FromDays(7),
            path => protectedRoots.Any(p => PathScope.IsSameOrUnder(path, p)), checkIdle,
            "모든 사용자에게 영향을 줍니다. 실행 중인 Windows Update·BITS를 잠시 중지하고 원래 상태로 복구합니다. 파일은 휴지통 없이 삭제되며 필요하면 다시 다운로드합니다. 설치된 업데이트나 업데이트 기록은 지우지 않습니다.");
    }
}
