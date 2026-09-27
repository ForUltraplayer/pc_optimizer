/**
 * @file    : AdobeCacheTargets.cs
 * @author  : rudals252
 * @brief   : Adobe 기본 미디어 캐시의 오래된 오디오 변환·파형 파일만 실행 카탈로그에 등록
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>앱 설정을 추측하거나 커뮤니티 규칙을 삭제 권한으로 쓰지 않는 고정 카탈로그입니다.</summary>
public static class AdobeCacheTargets
{
    /// <summary>기본 Media Cache Files 폴더의 90일 이상 된 cfa/pek입니다.</summary>
    public const string Media = "adobe-default-media";
    /// <summary>기본 Peak Files 폴더의 90일 이상 된 pek입니다.</summary>
    public const string Peaks = "adobe-default-peaks";
    internal const string Impact = "휴지통을 거치지 않으며 되돌릴 수 없습니다. 다음에 미디어를 열면 오디오 변환·파형을 다시 만들어 시간이 걸릴 수 있습니다. 정리 중에는 Adobe 영상·오디오 앱을 실행하지 마세요. 옮긴 캐시 위치·데이터베이스·프로젝트·렌더 결과물은 포함하지 않습니다.";

    internal static CleanupTarget Resolve(string key, ActionSession session)
    {
        if (session.Scope != ActionUserScope.Full || !session.IsKnown || SystemActionSession.Read() != session)
        { throw new ActionUnavailableException("ScopeExcluded"); }
        _ = RollbackStore.CurrentRoot(session.Sid);
        var root = DefaultRoot(key, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            p => CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, p));
        var protection = UserTempTargets.ProtectionFor(root, session);
        if (!Directory.Exists(root)) { throw new ActionUnavailableException("AdobeCacheMissing"); }
        return Target(key, root, protection, AdobeProcessGuard.Check);
    }

    internal static string DefaultRoot(string key, string profile, string roaming, Func<string, string> canonical)
    {
        var folder = key switch { Media => "Media Cache Files", Peaks => "Peak Files", _ => throw new ActionUnavailableException("TargetRejected") };
        if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(roaming)
            || !canonical(roaming).Equals(canonical(Path.Combine(profile, "AppData", "Roaming")), StringComparison.OrdinalIgnoreCase))
        { throw new ActionUnavailableException("AdobeLocationUnsupported"); }
        return canonical(Path.Combine(roaming, "Adobe", "Common", folder));
    }

    internal static CleanupTarget Target(string key, string root, Func<string, bool> protection, Func<string?> idle) => key switch
    {
        Media => new(key, root, "Adobe 기본 미디어 캐시 · 90일 이상 된 .cfa/.pek", true, ["*.cfa", "*.pek"], TimeSpan.FromDays(90), protection, idle, Impact),
        Peaks => new(key, root, "Adobe 기본 파형 캐시 · 90일 이상 된 .pek", true, ["*.pek"], TimeSpan.FromDays(90), protection, idle, Impact),
        _ => throw new ActionUnavailableException("TargetRejected"),
    };
}
