/**
 * @file    : NvidiaBranchResolver.cs
 * @author  : rudals252
 * @brief   : 설치 NVIDIA 버전이 Game Ready/Studio 공개 목록 중 한쪽에만 있을 때만 계열을 정하는 순수 판정(양쪽·어느 쪽도 아님·Studio 목록 없음은 모호)
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 설치 드라이버의 계열을 공개 목록 포함 여부로 정합니다(스펙 §5 GPU 드라이버 행).
/// 같은 버전이 두 목록에 모두 있거나 어느 쪽에도 없으면 모호하며, Studio 목록을 받지 못했으면 한쪽에만 있는지 알 수 없으므로 모호합니다.
/// </summary>
public static class NvidiaBranchResolver
{
    /// <summary>
    /// 계열을 정합니다.
    /// </summary>
    /// <param name="installedVersion">설치 버전(NVIDIA 표기).</param>
    /// <param name="gameReadyVersions">Game Ready 목록 버전.</param>
    /// <param name="studioVersions">Studio 목록 버전. 목록을 받지 못했으면 null(빈 목록은 "Studio 배포 없음"으로 확인된 것).</param>
    /// <returns>판정 결과.</returns>
    public static NvidiaBranchResolution Resolve(
        string? installedVersion,
        IReadOnlyList<string> gameReadyVersions,
        IReadOnlyList<string>? studioVersions)
    {
        ArgumentNullException.ThrowIfNull(gameReadyVersions);

        if (!DriverVersionNumber.TryParse(installedVersion, out _))
        {
            return Ambiguous(NvidiaBranchAmbiguity.InstalledUnparseable);
        }

        if (studioVersions is null)
        {
            return Ambiguous(NvidiaBranchAmbiguity.StudioUnavailable);
        }

        var inGameReady = DriverVersionComparer.ContainsVersion(gameReadyVersions, installedVersion);
        var inStudio = DriverVersionComparer.ContainsVersion(studioVersions, installedVersion);
        return (inGameReady, inStudio) switch
        {
            (true, false) => new NvidiaBranchResolution(NvidiaDriverBranch.GameReady, NvidiaBranchAmbiguity.None),
            (false, true) => new NvidiaBranchResolution(NvidiaDriverBranch.Studio, NvidiaBranchAmbiguity.None),
            (true, true) => Ambiguous(NvidiaBranchAmbiguity.InBoth),
            _ => Ambiguous(NvidiaBranchAmbiguity.InNeither),
        };
    }

    /// <summary>
    /// 모호 결과를 만든다.
    /// </summary>
    private static NvidiaBranchResolution Ambiguous(NvidiaBranchAmbiguity reason)
    {
        return new NvidiaBranchResolution(NvidiaDriverBranch.Ambiguous, reason);
    }
}
