/**
 * @file    : CacheSupportLinks.cs
 * @author  : rudals252
 * @brief   : 캐시 종류별 공식 안내의 정확한 HTTPS 주소만 코드로 허용
 */
namespace PcOptimizer.Core.Actions;

/// <summary>드라이버 다운로드 표와 별개인 캐시 관리 공식 안내입니다.</summary>
public static class CacheSupportLinks
{
    /// <summary>NVIDIA의 캐시 비활성화·재부팅·정리·원복 절차입니다.</summary>
    public const string NvidiaShader = "https://nvidia.custhelp.com/app/answers/detail/a_id/5735/";
    /// <summary>셰이더 캐시와 다른 Steam 다운로드 캐시의 공식 안내입니다.</summary>
    public const string SteamDownload = "https://help.steampowered.com/en/faqs/view/6AD7-820D-8BE5-E51F";
    /// <summary>쿼리·하위 경로를 추가하지 않은 두 주소만 허용합니다.</summary>
    public static bool IsAllowed(string? url) => url is NvidiaShader or SteamDownload;
}
