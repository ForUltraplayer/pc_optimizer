/**
 * @file    : GraphicsCacheTargets.cs
 * @author  : rudals252
 * @brief   : 현재 사용자 LocalAppData의 NVIDIA DXCache·GLCache·NV_Cache와 Windows D3DSCache에서 오래된 셰이더 캐시 파일만 실행 카탈로그에 등록
 */
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Files;

/// <summary>
/// NVIDIA 공식 절차(캐시 끄기 → 재부팅 → 전체 삭제 → 설정 복원)와 달리, 드라이버가 열어 두지 않은 오래된 파일만 개별 핸들로 지우는 부분 정리입니다.
/// 사용 중인 캐시 파일은 독점 열기에 실패해 건너뛰며, 폴더·설정·드라이버 상태는 바꾸지 않습니다.
/// </summary>
public static class GraphicsCacheTargets
{
    /// <summary>NVIDIA DirectX 셰이더 캐시(%LocalAppData%\NVIDIA\DXCache).</summary>
    public const string NvidiaDx = "nvidia-dxcache";
    /// <summary>NVIDIA OpenGL 셰이더 캐시(%LocalAppData%\NVIDIA\GLCache).</summary>
    public const string NvidiaGl = "nvidia-glcache";
    /// <summary>NVIDIA 통합 캐시(%LocalAppData%\NVIDIA Corporation\NV_Cache).</summary>
    public const string NvidiaNv = "nvidia-nvcache";
    /// <summary>Windows Direct3D 셰이더 캐시(%LocalAppData%\D3DSCache).</summary>
    public const string Direct3D = "d3d-shadercache";
    /// <summary>이 기간 안에 만들어지거나 바뀐 캐시는 최근에 쓰인 것으로 보고 남깁니다.</summary>
    public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(30);
    internal const string Impact = "휴지통을 거치지 않으며 되돌릴 수 없습니다. 30일 이상 쓰이지 않은 캐시 파일만 확인하고, 드라이버나 게임이 열어 둔 파일은 건너뜁니다. "
        + "지운 캐시는 다음 실행 때 다시 만들어 첫 로딩·끊김이 늘 수 있습니다. NVIDIA 공식 전체 초기화 절차(캐시 끄기·재부팅·전체 삭제)와 다른 부분 정리이며, 드라이버 설정은 바꾸지 않습니다.";
    private static readonly string[] AllPatterns = ["*"];

    /// <summary>등록된 키 목록입니다.</summary>
    public static readonly IReadOnlyList<string> Keys = [NvidiaDx, NvidiaGl, NvidiaNv, Direct3D];

    internal static CleanupTarget Resolve(string key, ActionSession session)
    {
        if (session.Scope != ActionUserScope.Full || !session.IsKnown || SystemActionSession.Read() != session)
        { throw new ActionUnavailableException("ScopeExcluded"); }
        _ = RollbackStore.CurrentRoot(session.Sid);
        var root = DefaultRoot(key, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            p => CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, p));
        var protection = UserTempTargets.ProtectionFor(root, session);
        if (!Directory.Exists(root)) { throw new ActionUnavailableException("GraphicsCacheMissing"); }
        return Target(key, root, protection);
    }

    /// <summary>LocalAppData가 프로필 아래 기본 위치일 때만 캐시 폴더를 정합니다. 옮긴 프로필·다른 사용자는 지원하지 않습니다.</summary>
    internal static string DefaultRoot(string key, string profile, string local, Func<string, string> canonical)
    {
        var relative = RelativePath(key);
        if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(local)
            || !canonical(local).Equals(canonical(Path.Combine(profile, "AppData", "Local")), StringComparison.OrdinalIgnoreCase))
        { throw new ActionUnavailableException("GraphicsLocationUnsupported"); }
        return canonical(Path.Combine(local, relative));
    }

    /// <summary>키별 LocalAppData 하위 경로입니다. 관측 프로브(ShaderCacheInspector)와 같은 위치를 씁니다.</summary>
    internal static string RelativePath(string key) => key switch
    {
        NvidiaDx => Path.Combine("NVIDIA", "DXCache"),
        NvidiaGl => Path.Combine("NVIDIA", "GLCache"),
        NvidiaNv => Path.Combine("NVIDIA Corporation", "NV_Cache"),
        Direct3D => "D3DSCache",
        _ => throw new ActionUnavailableException("TargetRejected"),
    };

    /// <summary>사용자 표시 이름입니다.</summary>
    public static string Label(string key) => key switch
    {
        NvidiaDx => "NVIDIA DirectX 셰이더 캐시",
        NvidiaGl => "NVIDIA OpenGL 셰이더 캐시",
        NvidiaNv => "NVIDIA 통합 캐시(NV_Cache)",
        Direct3D => "Windows Direct3D 셰이더 캐시",
        _ => throw new ActionUnavailableException("TargetRejected"),
    };

    internal static CleanupTarget Target(string key, string root, Func<string, bool> protection)
        => new(key, root, Label(key) + " · 30일 이상 쓰이지 않은 파일", true, AllPatterns, MinimumAge, protection, null, Impact);
}
