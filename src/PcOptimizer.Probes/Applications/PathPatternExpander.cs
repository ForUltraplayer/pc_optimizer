/**
 * @file    : PathPatternExpander.cs
 * @author  : rudals252
 * @brief   : 폴더 구성 요소에 * ? 와일드카드가 든 절대 경로를 부모 폴더 열거로 펼치는 제한된 확장기(구성 요소당 일치 64개·전체 256개 한도, 보호 루트 안은 열거하지 않음, 중간 구성 요소의 reparse·placeholder는 따라가지 않음, 파일 내용 읽기 없음)
 */

// 기본 패키지
using System.Security;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 와일드카드 경로 확장 결과입니다.
/// </summary>
/// <param name="Matches">펼친 실제 경로(와일드카드가 없으면 입력 그대로 하나).</param>
/// <param name="Exceeded">일치 개수 한도를 넘었는지 여부(넘으면 일부만 적용하지 않도록 쓰는 쪽이 규칙을 건너뜀).</param>
/// <param name="Incomplete">열거하지 못한 부모(접근 거부·사용 중)가 있어 결과가 불완전한지 여부.</param>
/// <param name="ProtectedSkipped">보호 루트 안이라 열거하지 않은 부모가 있는지 여부.</param>
public sealed record ExpansionResult(IReadOnlyList<string> Matches, bool Exceeded, bool Incomplete, bool ProtectedSkipped);

/// <summary>
/// 와일드카드 경로 확장기입니다(규칙 탐지·FileKey 폴더 공용).
/// </summary>
public sealed class PathPatternExpander
{
    /// <summary>와일드카드 구성 요소 하나(부모 하나 기준)의 최대 일치 수.</summary>
    public const int MAX_MATCHES_PER_SEGMENT = 64;

    /// <summary>경로 하나를 펼친 전체 결과의 최대 수.</summary>
    public const int MAX_EXPANDED_PATHS = 256;

    private readonly IDirectoryEntrySource _source;

    /// <summary>
    /// 확장기를 만듭니다.
    /// </summary>
    /// <param name="source">디렉터리 항목 열거.</param>
    public PathPatternExpander(IDirectoryEntrySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>
    /// 경로를 펼칩니다.
    /// </summary>
    /// <param name="path">드라이브 절대 경로(구성 요소에 와일드카드 가능).</param>
    /// <param name="directoriesOnly">마지막 구성 요소도 폴더만 고를지 여부(false면 파일도 포함, 탐지용).</param>
    /// <param name="isProtected">보호 루트 확인 함수(보호 루트 안의 폴더는 열거하지 않음).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>확장 결과.</returns>
    public ExpansionResult Expand(string path, bool directoriesOnly, Func<string, bool> isProtected, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(isProtected);

        var segments = Winapp2PathSyntax.Segments(path);
        if (!segments.Any(Winapp2PathSyntax.HasWildcard))
        {
            return new ExpansionResult([PathScope.Normalize(path)], false, false, false);
        }

        List<string> current = [segments[0] + PathScope.SEPARATOR];
        var incomplete = false;
        var protectedSkipped = false;
        for (var index = 1; index < segments.Length; index++)
        {
            var segment = segments[index];
            var isLast = index == segments.Length - 1;
            if (!Winapp2PathSyntax.HasWildcard(segment))
            {
                current = [.. current.Select(prefix => Path.Join(prefix, segment))];
                continue;
            }

            var next = new List<string>();
            foreach (var prefix in current)
            {
                ct.ThrowIfCancellationRequested();
                if (isProtected(prefix))
                {
                    protectedSkipped = true;
                    continue;
                }

                var matches = MatchChildren(prefix, segment, isLast && !directoriesOnly, ct, out var failed);
                incomplete |= failed;
                if (matches is null)
                {
                    return new ExpansionResult([], true, incomplete, protectedSkipped);
                }

                next.AddRange(matches);
                if (next.Count > MAX_EXPANDED_PATHS)
                {
                    return new ExpansionResult([], true, incomplete, protectedSkipped);
                }
            }

            current = next;
        }

        return new ExpansionResult([.. current.Select(PathScope.Normalize)], false, incomplete, protectedSkipped);
    }

    /// <summary>
    /// 부모 폴더에서 패턴에 맞는 항목을 고른다. 한도를 넘으면 null, 열거 실패는 failed로 알린다(없는 폴더는 실패가 아님).
    /// </summary>
    private List<string>? MatchChildren(string parent, string pattern, bool includeFiles, CancellationToken ct, out bool failed)
    {
        failed = false;
        var result = new List<string>();
        try
        {
            foreach (var entry in _source.Enumerate(parent))
            {
                ct.ThrowIfCancellationRequested();
                if (!RulePatternMatcher.Matches(pattern, entry.Name) || (!entry.IsDirectory && !includeFiles))
                {
                    continue;
                }

                // 중간·마지막 폴더 모두 reparse·placeholder는 따라가지 않는다(탐지용 마지막 파일은 존재만 본다).
                if (entry.IsDirectory && (EntryAttributes.IsPlaceholder(entry.Attributes) || EntryAttributes.IsReparsePoint(entry.Attributes)))
                {
                    continue;
                }

                result.Add(Path.Join(parent, entry.Name));
                if (result.Count > MAX_MATCHES_PER_SEGMENT)
                {
                    return null;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            return result;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            failed = true;
        }

        return result;
    }
}
