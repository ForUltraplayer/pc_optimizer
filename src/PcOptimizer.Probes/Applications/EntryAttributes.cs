/**
 * @file    : EntryAttributes.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 탐지·확장·관측에서 공유 파일 스캔과 같은 건너뜀 기준(클라우드 placeholder: Offline·RecallOnOpen·RecallOnDataAccess, reparse point)을 판정하는 속성 도우미
 */

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 디렉터리 항목 속성 판정 도우미입니다. 공유 파일 스캔(<c>VolumeTraversalRun</c>)과 같은 속성 값을 씁니다.
/// </summary>
internal static class EntryAttributes
{
    /// <summary>FILE_ATTRIBUTE_RECALL_ON_OPEN.</summary>
    public const FileAttributes RECALL_ON_OPEN = (FileAttributes)0x00040000;

    /// <summary>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS.</summary>
    public const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes)0x00400000;

    /// <summary>클라우드 placeholder로 보는 속성.</summary>
    public const FileAttributes PLACEHOLDER = FileAttributes.Offline | RECALL_ON_OPEN | RECALL_ON_DATA_ACCESS;

    /// <summary>
    /// 클라우드 placeholder인지 확인합니다(내용을 건드리면 내려받기가 일어날 수 있음).
    /// </summary>
    /// <param name="attributes">속성.</param>
    /// <returns>placeholder이면 true.</returns>
    public static bool IsPlaceholder(FileAttributes attributes)
    {
        return (attributes & PLACEHOLDER) != 0;
    }

    /// <summary>
    /// reparse point(정션·심볼릭 링크 등)인지 확인합니다.
    /// </summary>
    /// <param name="attributes">속성.</param>
    /// <returns>reparse point이면 true.</returns>
    public static bool IsReparsePoint(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
