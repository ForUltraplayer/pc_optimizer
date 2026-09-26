/**
 * @file    : ResolvedProtection.cs
 * @author  : rudals252
 * @brief   : 실제 경로로 해석된 보호 루트 목록과 경로가 보호 루트와 같거나 그 아래인지(대소문자 무시·디렉터리 경계) 확인하는 조회 모델
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 보호 루트의 출처 종류입니다.
/// </summary>
public enum ProtectedRootOrigin
{
    /// <summary>Known Folder(문서·사진·바탕화면·동영상·음악).</summary>
    KnownFolder,

    /// <summary>시스템 경로(Program Files, System32, Installer, WinSxS).</summary>
    SystemPath,

    /// <summary>감지된 클라우드 동기화 루트.</summary>
    CloudSync,
}

/// <summary>
/// 해석된 보호 루트 하나입니다.
/// </summary>
/// <param name="Path">정규화 경로.</param>
/// <param name="Origin">출처 종류.</param>
/// <param name="Label">출처 이름(예: "Documents", "%ProgramFiles%", "OneDrive"). 개인 경로가 아닙니다.</param>
public sealed record ProtectedRoot(string Path, ProtectedRootOrigin Origin, string Label);

/// <summary>
/// 해석된 보호 정책입니다. 이 루트들의 내용은 순회하지 않습니다.
/// </summary>
public sealed class ResolvedProtection
{
    private readonly HashSet<string> _paths;

    /// <summary>
    /// 해석된 루트로 보호 모델을 만듭니다(같은 경로는 한 번만 보관).
    /// </summary>
    /// <param name="roots">보호 루트.</param>
    /// <param name="unresolvedCount">해석하지 못한(해당 없음 포함) 정책 항목 수.</param>
    public ResolvedProtection(IEnumerable<ProtectedRoot> roots, int unresolvedCount = 0)
    {
        ArgumentNullException.ThrowIfNull(roots);
        _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<ProtectedRoot>();
        foreach (var root in roots)
        {
            var normalized = PathScope.Normalize(root.Path);
            if (_paths.Add(normalized))
            {
                kept.Add(root with { Path = normalized });
            }
        }

        Roots = kept.AsReadOnly();
        UnresolvedCount = unresolvedCount;
    }

    /// <summary>보호 루트(정책 순서, 중복 제거).</summary>
    public IReadOnlyList<ProtectedRoot> Roots { get; }

    /// <summary>해석하지 못한 정책 항목 수(예: 설치되지 않은 동기화 앱, 없는 환경 변수).</summary>
    public int UnresolvedCount { get; }

    /// <summary>
    /// 경로가 보호 루트와 같거나 그 아래인지 확인합니다.
    /// </summary>
    /// <param name="path">절대 경로.</param>
    /// <returns>보호되면 true.</returns>
    public bool IsProtected(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var current = PathScope.Normalize(path); current is not null; current = PathScope.GetParent(current))
        {
            if (_paths.Contains(current))
            {
                return true;
            }
        }

        return false;
    }
}
