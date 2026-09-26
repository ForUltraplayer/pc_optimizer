/**
 * @file    : AppCacheTargetBuilder.cs
 * @author  : rudals252
 * @brief   : 탐지된 지원 규칙의 FileKey·ExcludeKey를 이 PC 경로로 펼쳐 관측 후보·제외 조건을 만들고(앱 설정 경로는 검토 우선순위로 앞에), 폴더 이름만 보는 규칙(Squirrel)은 Update.exe가 있는 앱의 app-* 이름만 모으며, 펼칠 수 없는 규칙은 통째로 건너뛰는 구성기
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 규칙 하나의 구성 결과입니다.
/// </summary>
/// <param name="Rule">규칙.</param>
/// <param name="Metadata">검토 메타데이터(없으면 null).</param>
/// <param name="RuntimeFailure">이 PC에서 경로를 펼치지 못한 사유(펼쳤으면 null, 그때 규칙 전체를 건너뜀).</param>
/// <param name="ProtectedWildcards">보호 루트 안이라 와일드카드를 열거하지 않은 FileKey 수.</param>
/// <param name="IncompleteExpansions">접근 거부 등으로 일부만 펼친 FileKey 수.</param>
/// <param name="Config">연결된 앱 설정 분류 결과(없으면 null).</param>
internal sealed record RuleTargets(
    CleaningRule Rule, RuleMetadata? Metadata, UnsupportedRuleReason? RuntimeFailure, int ProtectedWildcards, int IncompleteExpansions, ClassifiedAppConfig? Config);

/// <summary>
/// Squirrel 앱 하나의 버전 폴더 이름입니다.
/// </summary>
/// <param name="App">앱 폴더 이름.</param>
/// <param name="Parent">앱 폴더 경로(측정값 전용).</param>
/// <param name="VersionFolders">app-* 폴더 이름(정렬).</param>
internal sealed record SquirrelApp(string App, string Parent, IReadOnlyList<string> VersionFolders);

/// <summary>
/// 구성 결과 전체입니다.
/// </summary>
/// <param name="Rules">규칙별 결과(보충 규칙 먼저).</param>
/// <param name="Candidates">관측 후보.</param>
/// <param name="Exclusions">제외 조건.</param>
/// <param name="Squirrel">Squirrel 앱.</param>
internal sealed record TargetBuild(
    IReadOnlyList<RuleTargets> Rules, IReadOnlyList<ObservationCandidate> Candidates, IReadOnlyList<ExclusionSpec> Exclusions, IReadOnlyList<SquirrelApp> Squirrel);

/// <summary>
/// 관측 후보 구성기입니다.
/// </summary>
internal sealed class AppCacheTargetBuilder(Winapp2PathResolver resolver, PathPatternExpander expander, IDirectoryEntrySource source)
{
    /// <summary>Squirrel 설치 확인 파일(앱 폴더 바로 아래).</summary>
    public const string SQUIRREL_UPDATER = "Update.exe";

    private static readonly IReadOnlyList<string> ALL_FILES = [RulePatternMatcher.ALL_FILES];

    /// <summary>
    /// 후보를 만듭니다.
    /// </summary>
    /// <param name="detected">탐지된 지원 규칙.</param>
    /// <param name="metadata">규칙 ID별 메타데이터.</param>
    /// <param name="configs">리더 이름별 앱 설정 분류 결과.</param>
    /// <param name="isProtected">보호 루트 확인 함수.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>구성 결과.</returns>
    public TargetBuild Build(
        IReadOnlyList<CleaningRule> detected,
        IReadOnlyDictionary<string, RuleMetadata> metadata,
        IReadOnlyDictionary<string, ClassifiedAppConfig> configs,
        Func<string, bool> isProtected,
        CancellationToken ct)
    {
        var rules = new List<RuleTargets>();
        var candidates = new List<ObservationCandidate>();
        var exclusions = new List<ExclusionSpec>();
        var squirrel = new List<SquirrelApp>();
        foreach (var rule in detected.OrderBy(rule => rule.Origin == RuleOrigin.Supplement ? 0 : 1))
        {
            ct.ThrowIfCancellationRequested();
            var meta = metadata.GetValueOrDefault(rule.Id);
            var config = meta?.ConfigReader is { } app ? configs.GetValueOrDefault(app) : null;
            if (meta?.Observation == ObservationKind.FolderNamesOnly)
            {
                var failure = CollectSquirrel(rule, isProtected, squirrel, ct);
                rules.Add(new RuleTargets(rule, meta, failure, 0, 0, config));
                continue;
            }

            var ruleCandidates = new List<ObservationCandidate>();
            var ruleExclusions = new List<ExclusionSpec>();
            var precedence = rule.Origin == RuleOrigin.Supplement ? ObservationPrecedence.Reviewed : ObservationPrecedence.Community;
            foreach (var path in config?.AppliedPaths ?? [])
            {
                ruleCandidates.Add(new ObservationCandidate(rule.Id, ObservationPrecedence.Reviewed, path, ALL_FILES, true, TargetSource.UserConfig));
            }

            var (runtimeFailure, protectedWildcards, incomplete) = ExpandFileKeys(rule, precedence, isProtected, ruleCandidates, ct);
            runtimeFailure ??= ResolveExclusions(rule, ruleExclusions);
            if (runtimeFailure is null)
            {
                candidates.AddRange(ruleCandidates);
                exclusions.AddRange(ruleExclusions);
            }

            rules.Add(new RuleTargets(rule, meta, runtimeFailure, protectedWildcards, incomplete, config));
        }

        return new TargetBuild(rules.AsReadOnly(), candidates.AsReadOnly(), exclusions.AsReadOnly(), squirrel.AsReadOnly());
    }

    /// <summary>
    /// FileKey 폴더를 펼쳐 후보에 더한다. 펼칠 수 없으면 사유를 돌려준다.
    /// </summary>
    private (UnsupportedRuleReason? Failure, int ProtectedWildcards, int Incomplete) ExpandFileKeys(
        CleaningRule rule, ObservationPrecedence precedence, Func<string, bool> isProtected, List<ObservationCandidate> output, CancellationToken ct)
    {
        var protectedWildcards = 0;
        var incomplete = 0;
        foreach (var fileKey in rule.FileKeys)
        {
            var resolution = resolver.Resolve(fileKey.PathTemplate);
            if (!resolution.IsResolved)
            {
                return (resolution.Failure, protectedWildcards, incomplete);
            }

            foreach (var path in resolution.Paths)
            {
                var expansion = expander.Expand(path, directoriesOnly: true, isProtected, ct);
                if (expansion.Exceeded)
                {
                    return (UnsupportedRuleReason.WildcardBoundExceeded, protectedWildcards, incomplete);
                }

                protectedWildcards += expansion.ProtectedSkipped ? 1 : 0;
                incomplete += expansion.Incomplete ? 1 : 0;
                output.AddRange(expansion.Matches.Select(match => new ObservationCandidate(rule.Id, precedence, match, fileKey.Patterns, fileKey.Recurse, TargetSource.Default)));
            }
        }

        return (null, protectedWildcards, incomplete);
    }

    /// <summary>
    /// ExcludeKey 경로의 변수를 펼친다(와일드카드는 비교 때 쓰도록 남김). 펼칠 수 없으면 사유를 돌려준다.
    /// </summary>
    private UnsupportedRuleReason? ResolveExclusions(CleaningRule rule, List<ExclusionSpec> output)
    {
        foreach (var exclude in rule.ExcludeKeys)
        {
            var resolution = resolver.Resolve(exclude.PathTemplate);
            if (!resolution.IsResolved)
            {
                return resolution.Failure;
            }

            output.AddRange(resolution.Paths.Select(path => new ExclusionSpec(rule.Id, exclude.Kind, path, exclude.Patterns)));
        }

        return null;
    }

    /// <summary>
    /// Squirrel 규칙: FileKey 폴더(app-*)를 펼쳐, 부모 폴더에 Update.exe가 있는 앱의 버전 폴더 이름만 모은다(크기 관측 없음).
    /// </summary>
    private UnsupportedRuleReason? CollectSquirrel(CleaningRule rule, Func<string, bool> isProtected, List<SquirrelApp> output, CancellationToken ct)
    {
        foreach (var fileKey in rule.FileKeys)
        {
            var resolution = resolver.Resolve(fileKey.PathTemplate);
            if (!resolution.IsResolved)
            {
                return resolution.Failure;
            }

            foreach (var path in resolution.Paths)
            {
                var expansion = expander.Expand(path, directoriesOnly: true, isProtected, ct);
                if (expansion.Exceeded)
                {
                    return UnsupportedRuleReason.WildcardBoundExceeded;
                }

                foreach (var group in expansion.Matches.GroupBy(match => PathScope.GetParent(match)!, StringComparer.OrdinalIgnoreCase))
                {
                    if (source.ProbeRoot(Path.Join(group.Key, SQUIRREL_UPDATER)) != RootPresence.NotDirectory)
                    {
                        continue;
                    }

                    var names = group.Select(PathScope.GetLeafName).Order(StringComparer.OrdinalIgnoreCase).ToList();
                    output.Add(new SquirrelApp(PathScope.GetLeafName(group.Key), group.Key, names.AsReadOnly()));
                }
            }
        }

        return null;
    }
}
