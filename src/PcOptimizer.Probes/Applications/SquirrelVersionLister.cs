/**
 * @file    : SquirrelVersionLister.cs
 * @author  : rudals252
 * @brief   : rule-metadata.json의 listing 정의(확인한 앱 폴더, app-* 패턴, Update.exe 확인 파일)로 Squirrel 설치 앱의 버전 폴더 이름만 나열하는 코드(삭제 규칙 형식 없음, 크기·사용 여부 판정 없음, 보호·다른 사용자·정션 위치 제외)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// Squirrel 앱 하나의 버전 폴더 이름입니다.
/// </summary>
/// <param name="App">앱 폴더 이름.</param>
/// <param name="Parent">앱 폴더 경로(측정값 전용).</param>
/// <param name="VersionFolders">버전 폴더 이름(정렬).</param>
internal sealed record SquirrelApp(string App, string Parent, IReadOnlyList<string> VersionFolders);

/// <summary>
/// Squirrel 버전 폴더 나열기입니다. 정리 규칙(supplement.ini)으로 적지 않아 다른 정리 도구가 앱 버전 폴더를 삭제 대상으로 읽을 수 없게 합니다.
/// </summary>
internal sealed class SquirrelVersionLister(Winapp2PathResolver resolver, IDirectoryEntrySource source)
{
    /// <summary>
    /// 폴더 이름 나열 정의가 있는 메타데이터로 앱별 버전 폴더를 모읍니다. 앱 폴더 바로 아래에 확인 파일이 있는 앱만 나옵니다.
    /// </summary>
    /// <param name="metadata">규칙 ID별 메타데이터.</param>
    /// <param name="isProtected">보호·다른 사용자 위치 확인 함수.</param>
    /// <param name="reparse">중간 폴더 reparse 검사기.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>앱별 버전 폴더.</returns>
    public IReadOnlyList<SquirrelApp> List(
        IReadOnlyDictionary<string, RuleMetadata> metadata, Func<string, bool> isProtected, ReparseAncestorCheck reparse, CancellationToken ct)
    {
        var result = new List<SquirrelApp>();
        foreach (var listing in metadata.Values.Where(meta => meta.Observation == ObservationKind.FolderNamesOnly).Select(meta => meta.Listing).OfType<FolderListing>())
        {
            foreach (var parent in listing.ParentTemplates.Select(resolver.Resolve).Where(resolution => resolution.IsResolved).SelectMany(resolution => resolution.Paths))
            {
                ct.ThrowIfCancellationRequested();
                if (isProtected(parent) || reparse.HasReparseAncestor(parent) || source.ProbeRoot(parent) != RootPresence.Directory
                    || source.ProbeRoot(Path.Join(parent, listing.Marker)) != RootPresence.NotDirectory)
                {
                    continue;
                }

                var names = ListFolders(parent, listing.Pattern);
                if (names.Count > 0)
                {
                    result.Add(new SquirrelApp(PathScope.GetLeafName(parent), parent, names));
                }
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// 앱 폴더에서 패턴에 맞는 폴더 이름만 모은다(reparse·placeholder 제외, 읽지 못하면 빈 목록).
    /// </summary>
    private List<string> ListFolders(string parent, string pattern)
    {
        try
        {
            return [.. source.Enumerate(parent)
                .Where(entry => entry.IsDirectory && !EntryAttributes.IsReparsePoint(entry.Attributes) && !EntryAttributes.IsPlaceholder(entry.Attributes)
                    && RulePatternMatcher.Matches(pattern, entry.Name))
                .Select(entry => entry.Name)
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return [];
        }
    }
}
