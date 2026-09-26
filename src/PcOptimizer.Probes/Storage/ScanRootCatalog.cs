/**
 * @file    : ScanRootCatalog.cs
 * @author  : rudals252
 * @brief   : 명시적 스캔 루트(프로필·ProgramData·TEMP·Windows 임시·업데이트 다운로드·탐색기 캐시·배달 최적화)를 해석·정규화·검증(다른 프로필·볼륨 루트·보호 루트 거부)하고 중첩 루트를 제거하는 계획기
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 스캔 루트 계획기입니다(스펙 §5.2 "사용자 프로필과 ProgramData를 기본 루트로 하고 TEMP·Windows 임시 위치… 시스템 위치는 지정한 임시 하위 경로만").
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>다른 루트 안에 있는 루트는 따로 순회하지 않습니다(중첩 루트 합산 방지). 크기는 바깥 루트 순회의 디렉터리별 합계에서 얻습니다.</item>
/// <item>사용자 폴더 모음(프로필의 부모) 아래이면서 현재 프로필 밖인 경로, 볼륨 루트, UNC는 거부합니다.</item>
/// <item>보호 루트 안의 루트는 순회하지 않습니다.</item>
/// </list>
/// </remarks>
public sealed class ScanRootCatalog
{
    /// <summary>탐색기 썸네일 캐시 파일 이름 패턴.</summary>
    public const string THUMBNAIL_CACHE_PATTERN = "thumbcache_*.db";

    /// <summary>탐색기 아이콘 캐시 파일 이름 패턴.</summary>
    public const string ICON_CACHE_PATTERN = "iconcache_*.db";

    private const string PROGRAM_DATA_TEMPLATE = "%ProgramData%";
    private const string USER_TEMP_TEMPLATE = "%TEMP%";
    private const string WINDOWS_TEMP_TEMPLATE = @"%SystemRoot%\Temp";
    private const string UPDATE_DOWNLOAD_TEMPLATE = @"%SystemRoot%\SoftwareDistribution\Download";
    private const string EXPLORER_CACHE_TEMPLATE = @"%LocalAppData%\Microsoft\Windows\Explorer";
    private const string DELIVERY_OPTIMIZATION_TEMPLATE = @"%SystemRoot%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization";

    /// <summary>표준 임시 위치 정의(순서 = 보고 순서).</summary>
    private static readonly LocationDefinition[] LOCATIONS =
    [
        new(FileScanProbeContract.LOCATION_USER_TEMP, USER_TEMP_TEMPLATE, false, []),
        new(FileScanProbeContract.LOCATION_WINDOWS_TEMP, WINDOWS_TEMP_TEMPLATE, true, []),
        new(FileScanProbeContract.LOCATION_UPDATE_DOWNLOAD, UPDATE_DOWNLOAD_TEMPLATE, true, []),
        new(FileScanProbeContract.LOCATION_THUMBNAIL_CACHE, EXPLORER_CACHE_TEMPLATE, false, [THUMBNAIL_CACHE_PATTERN, ICON_CACHE_PATTERN]),
        new(FileScanProbeContract.LOCATION_DELIVERY_OPTIMIZATION, DELIVERY_OPTIMIZATION_TEMPLATE, true, []),
    ];

    private readonly IPathEnvironment _environment;

    /// <summary>
    /// 계획기를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경.</param>
    public ScanRootCatalog(IPathEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>
    /// 스캔 계획을 만듭니다.
    /// </summary>
    /// <param name="protection">해석된 보호 루트.</param>
    /// <returns>계획.</returns>
    public ScanPlan Build(ResolvedProtection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);

        var rawProfile = _environment.GetUserProfilePath();
        var profilePlan = Plan(FileScanProbeContract.ROOT_PROFILE, rawProfile);
        var profile = profilePlan.State == ScanRootPlanState.Planned ? profilePlan.Path : null;
        var usersFolder = profile is null ? null : PathScope.GetParent(profile);
        var locations = LOCATIONS
            .Select(definition => new ScanLocationPlan(
                definition.Id, Plan(definition.Id, PathTemplate.ExpandOnly(definition.Template, _environment)).Path, definition.IsSystem, definition.Patterns))
            .ToList();

        var candidates = new List<ScanRootPlan>
        {
            Validate(profilePlan, profile, usersFolder, protection),
            Validate(Plan(FileScanProbeContract.ROOT_PROGRAM_DATA, PathTemplate.ExpandOnly(PROGRAM_DATA_TEMPLATE, _environment)), profile, usersFolder, protection),
        };
        candidates.AddRange(LOCATIONS.Select(definition =>
            Validate(Plan(definition.Id, PathTemplate.ExpandOnly(definition.Template, _environment)), profile, usersFolder, protection)));

        var roots = RemoveNested(candidates);
        var selectionRoots = roots
            .Where(root => root.State == ScanRootPlanState.Planned
                && (root.Id == FileScanProbeContract.ROOT_PROFILE || root.Id == FileScanProbeContract.ROOT_PROGRAM_DATA))
            .Select(root => root.Path!)
            .ToList();
        var excluded = protection.Roots.Select(root => root.Path)
            .Concat(locations.Where(location => location.Path is not null).Select(location => location.Path!))
            .ToList();

        return new ScanPlan(roots, locations, selectionRoots, excluded);
    }

    /// <summary>
    /// 순회 예정 루트가 들어 있을 볼륨 수를 셉니다(파일 시스템에 접근하지 않고 환경 변수만 펼침). 기본 타임아웃 계산용입니다.
    /// </summary>
    /// <returns>볼륨 수(최소 1).</returns>
    public int CountPlannedVolumes()
    {
        var templates = new[] { PROGRAM_DATA_TEMPLATE }.Concat(LOCATIONS.Select(location => location.Template));
        var volumes = templates
            .Select(template => PathTemplate.ExpandOnly(template, _environment))
            .Append(_environment.GetUserProfilePath())
            .Where(path => path is not null && PathScope.IsDriveAbsolute(path))
            .Select(path => char.ToUpperInvariant(path![0]))
            .Distinct()
            .Count();
        return Math.Max(1, volumes);
    }

    /// <summary>
    /// 펼친 경로로 루트 계획을 만든다. 펼치지 못하면 Unresolved, 드라이브 절대 경로가 아니거나 볼륨 루트이면 Rejected.
    /// </summary>
    private ScanRootPlan Plan(string id, string? expanded)
    {
        if (string.IsNullOrWhiteSpace(expanded))
        {
            return new ScanRootPlan(id, null, ScanRootPlanState.Unresolved);
        }

        var normalized = PathTemplate.NormalizeAbsolute(expanded, _environment);
        return normalized is null
            ? new ScanRootPlan(id, null, ScanRootPlanState.Rejected)
            : new ScanRootPlan(id, normalized, ScanRootPlanState.Planned);
    }

    /// <summary>
    /// 다른 사용자 프로필·보호 루트 안의 루트를 거른다.
    /// </summary>
    private static ScanRootPlan Validate(ScanRootPlan plan, string? profile, string? usersFolder, ResolvedProtection protection)
    {
        if (plan.State != ScanRootPlanState.Planned || plan.Path is null)
        {
            return plan;
        }

        if (usersFolder is not null && profile is not null
            && PathScope.IsStrictlyUnder(plan.Path, usersFolder) && !PathScope.IsSameOrUnder(plan.Path, profile))
        {
            return plan with { State = ScanRootPlanState.Rejected };
        }

        return protection.IsProtected(plan.Path) ? plan with { State = ScanRootPlanState.Protected } : plan;
    }

    /// <summary>
    /// 짧은 경로부터 보며 이미 남긴 루트 안에 있는(또는 같은) 루트를 중첩으로 표시한다. 결과는 정의 순서를 유지한다.
    /// </summary>
    private static List<ScanRootPlan> RemoveNested(List<ScanRootPlan> candidates)
    {
        var kept = new List<ScanRootPlan>();
        var result = candidates.ToDictionary(plan => plan.Id, plan => plan, StringComparer.Ordinal);
        foreach (var plan in candidates.Where(p => p.State == ScanRootPlanState.Planned).OrderBy(p => p.Path!.Length))
        {
            var outer = kept.FirstOrDefault(root => PathScope.IsSameOrUnder(plan.Path!, root.Path!));
            if (outer is null)
            {
                kept.Add(plan);
            }
            else
            {
                result[plan.Id] = plan with { State = ScanRootPlanState.Nested, NestedIn = outer.Id };
            }
        }

        return [.. candidates.Select(plan => result[plan.Id])];
    }

    /// <summary>
    /// 표준 임시 위치 정의입니다.
    /// </summary>
    private sealed record LocationDefinition(string Id, string Template, bool IsSystem, IReadOnlyList<string> Patterns);
}
