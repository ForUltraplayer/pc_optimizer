/**
 * @file    : TrimPolicyProbeContract.cs
 * @author  : rudals252
 * @brief   : TRIM(삭제 알림) 정책 프로브와 규칙이 공유하는 프로브 ID·파일 시스템별 DisableDeleteNotify 측정 이름·원시 값 의미 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// TRIM 정책 프로브(Probes)와 <see cref="TrimPolicyRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 값은 OS 삭제 알림 정책(fsutil behavior query DisableDeleteNotify)이며 장치 지원·실제 수행 여부가 아닙니다.
/// </summary>
public static class TrimPolicyProbeContract
{
    /// <summary>TRIM 정책 프로브 ID.</summary>
    public const string PROBE_ID = "storage.trimPolicy";

    /// <summary>NTFS 파일 시스템 이름.</summary>
    public const string FILE_SYSTEM_NTFS = "NTFS";

    /// <summary>ReFS 파일 시스템 이름.</summary>
    public const string FILE_SYSTEM_REFS = "ReFS";

    /// <summary>DisableDeleteNotify: 삭제 알림 허용(TRIM 정책 켜짐).</summary>
    public const long DELETE_NOTIFY_ENABLED = 0;

    /// <summary>DisableDeleteNotify: 삭제 알림 막음(TRIM 정책 꺼짐).</summary>
    public const long DELETE_NOTIFY_DISABLED = 1;

    private const string NAME_PREFIX = "trim.";
    private const string NAME_SUFFIX = ".disableDeleteNotify";

    /// <summary>규칙이 판정하는 파일 시스템 목록(측정 순서).</summary>
    public static IReadOnlyList<string> FileSystems { get; } = [FILE_SYSTEM_NTFS, FILE_SYSTEM_REFS];

    /// <summary>
    /// 파일 시스템별 DisableDeleteNotify 측정 이름을 만듭니다(예: "trim.ntfs.disableDeleteNotify").
    /// </summary>
    /// <param name="fileSystem">파일 시스템 이름.</param>
    /// <returns>측정 이름.</returns>
    public static string DisableDeleteNotifyName(string fileSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileSystem);
        return NAME_PREFIX + fileSystem.ToLowerInvariant() + NAME_SUFFIX;
    }
}
