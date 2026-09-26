/**
 * @file    : ProtectedKnownFolder.cs
 * @author  : rudals252
 * @brief   : 보호 정책이 지정할 수 있는 Known Folder 종류(문서·사진·바탕화면·동영상·음악) 열거형
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 보호 정책에서 쓰는 Known Folder 종류입니다. 이름은 protect.json의 <c>folder</c> 값과 같습니다.
/// </summary>
public enum ProtectedKnownFolder
{
    /// <summary>문서.</summary>
    Documents,

    /// <summary>사진.</summary>
    Pictures,

    /// <summary>바탕화면.</summary>
    Desktop,

    /// <summary>동영상.</summary>
    Videos,

    /// <summary>음악.</summary>
    Music,
}
