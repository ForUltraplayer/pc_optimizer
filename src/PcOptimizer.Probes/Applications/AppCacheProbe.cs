/**
 * @file    : AppCacheProbe.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 프로브(사용자 범위, 관리자 권한 불필요). 포함 규칙 무결성 확인 → 공유 파일 스캔 → 규칙 탐지 → 앱 설정 리더(일반 권한만) → 후보 구성·관측 계획 → 공유 합계/대상 열거 관측 → 규칙별 측정값. 파일 삭제·레지스트리 변경·앱 실행·내려받기 없음
 */

// 기본 패키지
using System.Security.Principal;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 앱 캐시 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="AppCacheProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>규칙은 어셈블리 포함 리소스만 쓰고(파일 시스템에서 읽지 않음) SHA-256 일관성을 확인합니다. 없거나 다르면 Failed + ProbeError(리소스 문장)이며 다른 검사에는 영향이 없습니다.</item>
/// <item>보호 정책이 무효라 공유 스캔이 순회하지 않았으면 앱 캐시 위치도 순회하지 않습니다(Failed). 보호 루트와 다른 사용자 위치(다른 프로필·휴지통의 다른 SID)는 들어가지 않습니다.</item>
/// <item>관리자 권한 검사에서는 사용자 쓰기 가능한 앱 설정(환경 변수·설정 파일·레지스트리 라이브러리 경로)을 적용하지 않고 기본 위치만 봅니다(스펙 §6).</item>
/// <item>읽지 못한 항목·시간 초과가 있으면 Partial과 사유별 개수 Issue를 돌려줍니다(경로는 Issue에 넣지 않음).</item>
/// </list>
/// </remarks>
public sealed class AppCacheProbe : IProbe
{
    private readonly RuleCatalogLoader _loader;
    private readonly IFileScanService _fileScan;
    private readonly RuleDetector _detector;
    private readonly AppCacheTargetBuilder _builder;
    private readonly SquirrelVersionLister _squirrel;
    private readonly AppCacheMeasurer _measurer;
    private readonly IReadOnlyList<IAppConfigReader> _readers;
    private readonly IDirectoryEntrySource _source;
    private readonly IRegistryReader _registry;
    private readonly IPathEnvironment _environment;
    private readonly IClock _clock;
    private readonly TimeProvider _time;
    private readonly string? _currentUserSid;

    /// <summary>
    /// 프로브를 만듭니다.
    /// </summary>
    /// <param name="loader">포함 규칙 로더.</param>
    /// <param name="fileScan">공유 파일 스캔 서비스.</param>
    /// <param name="environment">경로 환경.</param>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="source">디렉터리 항목 열거.</param>
    /// <param name="readers">앱 설정 리더.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="time">시간 공급자(예산 측정).</param>
    /// <param name="currentUserSid">현재 사용자 SID(휴지통의 다른 사용자 폴더를 거르는 데 씀, 모르면 null).</param>
    public AppCacheProbe(
        RuleCatalogLoader loader,
        IFileScanService fileScan,
        IPathEnvironment environment,
        IRegistryReader registry,
        IDirectoryEntrySource source,
        IReadOnlyList<IAppConfigReader> readers,
        IClock clock,
        TimeProvider time,
        string? currentUserSid)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(fileScan);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(readers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(time);
        _loader = loader;
        _fileScan = fileScan;
        var resolver = new Winapp2PathResolver(environment);
        _detector = new RuleDetector(registry, source, resolver);
        _builder = new AppCacheTargetBuilder(resolver, new PathPatternExpander(source));
        _squirrel = new SquirrelVersionLister(resolver, source);
        _measurer = new AppCacheMeasurer(source, time);
        _readers = readers;
        _source = source;
        _registry = registry;
        _environment = environment;
        _clock = clock;
        _time = time;
        _currentUserSid = currentUserSid;

        var total = fileScan.BudgetPerVolume * fileScan.CountPlannedVolumes() + FileScanProbe.FILE_SCAN_TIMEOUT_MARGIN;
        DefaultTimeout = total > FileScanProbe.FILE_SCAN_TIMEOUT_CAP ? FileScanProbe.FILE_SCAN_TIMEOUT_CAP : total;
    }

    /// <inheritdoc />
    public string Id => AppCacheProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.AppCache;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.User;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout { get; }

    /// <summary>
    /// 실제 레지스트리·파일 시스템·환경과 어셈블리 포함 규칙을 쓰는 프로브를 만듭니다.
    /// </summary>
    /// <param name="fileScan">공유 파일 스캔 서비스(파일 스캔 프로브와 같은 인스턴스).</param>
    /// <param name="rules">포함 규칙 로더(없으면 <see cref="RuleCatalogLoader.CreateEmbedded"/>).</param>
    /// <returns>프로브.</returns>
    public static AppCacheProbe CreateDefault(IFileScanService fileScan, RuleCatalogLoader? rules = null)
    {
        var environment = SystemPathEnvironment.Instance;
        var source = FileSystemDirectoryEntrySource.Instance;
        var registry = Win32RegistryReader.Instance;
        return new AppCacheProbe(
            rules ?? RuleCatalogLoader.CreateEmbedded(),
            fileScan,
            environment,
            registry,
            source,
            [
                new NpmConfigReader(environment, source),
                new PipConfigReader(environment, source),
                new NuGetConfigReader(environment, source),
                new SteamLibraryReader(environment, registry, source),
                new AdobeMediaCacheConfigReader(),
            ],
            SystemClock.Instance,
            TimeProvider.System,
            CurrentUserSid());
    }

    /// <summary>
    /// 현재 프로세스 사용자의 SID(읽지 못하면 null).
    /// </summary>
    private static string? CurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value;
    }

    /// <inheritdoc />
    public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();
        await Task.Yield();

        var start = _time.GetTimestamp();
        var catalog = _loader.Load();
        if (!catalog.IsVerified)
        {
            return Failed(context, AppCacheMeasurements.ForCatalogState(catalog.State, catalog.FailedFile, _clock.UtcNow),
                ProbeText.Format(ProbeStrings.AppCache_CatalogInvalid, catalog.FailedFile ?? string.Empty, catalog.State));
        }

        var shared = await _fileScan.GetOrScanAsync(context, ct).ConfigureAwait(false);
        if (!shared.IsPolicyValid)
        {
            return Failed(context, AppCacheMeasurements.ForCatalogState(AppCacheProbeContract.CATALOG_POLICY_INVALID, null, _clock.UtcNow),
                ProbeText.Format(ProbeStrings.AppCache_PolicyInvalid, shared.PolicyError));
        }

        var measureStart = _time.GetTimestamp();
        var protection = shared.Protection!;
        var otherUsers = OtherUserLocationGuard.Create(_registry, _environment, _currentUserSid);
        var reparse = new ReparseAncestorCheck(_source);
        bool IsExcludedLocation(string path) => protection.IsProtected(path) || otherUsers.IsOtherUserLocation(path);
        var detection = _detector.Detect(context.ScanId, catalog.Rules, IsExcludedLocation, ct, reparse);
        var configs = ReadConfigs(context.IsElevated, catalog.Rules, catalog.Metadata, IsExcludedLocation);
        var configDetected = ConfigDetectedRuleIds(catalog.Rules, catalog.Metadata, configs);
        var detected = catalog.Rules
            .Where(rule => rule.IsSupported
                && ((detection.TryGetValue(rule.Id, out var state) && state == DetectionState.Detected) || configDetected.Contains(rule.Id)))
            .ToList();
        var detectedIds = detected.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var reportedConfigs = configs.Where(config => IsLinkedToDetected(config, catalog.Metadata, detectedIds)).ToList();
        var build = _builder.Build(detected, catalog.Metadata, configs.ToDictionary(config => config.Reading.App, StringComparer.Ordinal), IsExcludedLocation, reparse, ct);
        var squirrel = _squirrel.List(catalog.Metadata, IsExcludedLocation, reparse, ct);
        var measurable = build.Rules.Where(rule => rule.RuntimeFailure is null).ToList();
        var plan = ObservationPlanner.Plan(build.Candidates, build.Exclusions, [.. protection.Roots.Select(root => root.Path)]);
        var remaining = DefaultTimeout - _time.GetElapsedTime(start) - FileScanProbe.FILE_SCAN_TIMEOUT_MARGIN;
        var budget = remaining < AppCacheMeasurer.APP_CACHE_TARGET_BUDGET ? AppCacheMeasurer.APP_CACHE_TARGET_BUDGET
            : remaining > ScanOptions.DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME ? ScanOptions.DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME
            : remaining;
        var results = _measurer.Measure(plan, shared, IsExcludedLocation, budget, ct, reparse);
        var observations = RuleObservationAggregator.Aggregate(measurable, plan, results);

        var summary = new DetectionSummary(
            detected.Count,
            detection.Count(pair => pair.Value == DetectionState.NotDetected && !detectedIds.Contains(pair.Key)),
            detection.Count(pair => pair.Value == DetectionState.Unknown && !detectedIds.Contains(pair.Key)),
            build.Rules.Where(rule => rule.RuntimeFailure is not null).GroupBy(rule => rule.RuntimeFailure!.Value).ToDictionary(group => group.Key, group => group.Count()));
        var configRuleIds = build.Rules.Where(rule => rule.Metadata?.ConfigReader is not null)
            .GroupBy(rule => rule.Metadata!.ConfigReader!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Rule.Id, StringComparer.Ordinal);
        var observedAt = _clock.UtcNow;
        var measurements = AppCacheMeasurements.Build(
            catalog, summary, observations, squirrel, reportedConfigs, configRuleIds, context.IsElevated, otherUsers.ProfileListAvailable, _time.GetElapsedTime(measureStart), observedAt);
        var issues = CollectIssues(results, observations);
        return new ProbeResult(Id, issues.Count > 0 ? ProbeStatus.Partial : ProbeStatus.Success, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext);
    }

    /// <summary>
    /// 앱 설정 리더가 연결된 모든 보충 규칙의 설정을 읽는다(탐지 여부와 무관: 기본 폴더가 없고 설정으로 옮긴 캐시도 찾기 위함).
    /// 관리자 권한 검사에서는 읽지 않고 적용하지 않았다고 표시한다(Adobe는 파일을 읽지 않으므로 그대로).
    /// </summary>
    private List<ClassifiedAppConfig> ReadConfigs(
        bool elevated, IReadOnlyList<CleaningRule> rules, IReadOnlyDictionary<string, RuleMetadata> metadata, Func<string, bool> isProtected)
    {
        var wanted = rules
            .Where(rule => rule.IsSupported && rule.Origin == RuleOrigin.Supplement)
            .Select(rule => metadata.GetValueOrDefault(rule.Id)?.ConfigReader)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var result = new List<ClassifiedAppConfig>();
        foreach (var reader in _readers.Where(reader => wanted.Contains(reader.App)))
        {
            if (elevated && reader.App != AdobeMediaCacheConfigReader.APP)
            {
                var skipped = new AppConfigReading(reader.App, AppConfigReadState.NotConfigured, AppConfigValueOrigin.None, []);
                result.Add(new ClassifiedAppConfig(skipped, AppCacheProbeContract.CONFIG_STATE_NOT_APPLIED_ELEVATED, []));
                continue;
            }

            result.Add(AppConfigPathClassifier.Classify(reader.Read(), isProtected, _source));
        }

        return result;
    }

    /// <summary>
    /// 설정 값(환경 변수·설정 키·레지스트리)이 있는 앱 설정을 그 보충 규칙의 탐지 근거로 본다(기본 폴더가 없어도 탐지).
    /// 해석 불가 값도 사용자가 위치를 바꿨다는 근거이므로 포함한다(그때는 확인 불가 Finding으로 범위를 알림).
    /// </summary>
    private static HashSet<string> ConfigDetectedRuleIds(
        IReadOnlyList<CleaningRule> rules, IReadOnlyDictionary<string, RuleMetadata> metadata, IReadOnlyList<ClassifiedAppConfig> configs)
    {
        var configured = configs
            .Where(config => config.Reading.State is AppConfigReadState.Configured or AppConfigReadState.Invalid)
            .Select(config => config.Reading.App)
            .ToHashSet(StringComparer.Ordinal);
        return rules
            .Where(rule => rule.IsSupported && rule.Origin == RuleOrigin.Supplement
                && metadata.GetValueOrDefault(rule.Id)?.ConfigReader is { } app && configured.Contains(app))
            .Select(rule => rule.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 앱 설정이 탐지된 보충 규칙에 연결되어 있는지 확인한다(설치되지 않은 앱의 설정 없음 상태는 보고하지 않음).
    /// </summary>
    private static bool IsLinkedToDetected(ClassifiedAppConfig config, IReadOnlyDictionary<string, RuleMetadata> metadata, HashSet<string> detectedIds)
    {
        return metadata.Values.Any(meta => meta.ConfigReader == config.Reading.App && detectedIds.Contains(meta.RuleId));
    }

    /// <summary>
    /// 읽지 못한 항목·시간 초과를 사유별 개수 Issue로 모은다(경로 없음).
    /// </summary>
    private static List<Issue> CollectIssues(IReadOnlyList<TargetMeasurement> results, IReadOnlyList<RuleObservation> observations)
    {
        var skips = observations.Aggregate(default(SkipCounts), (sum, rule) => sum.Add(rule.Skips));
        var issues = new List<Issue>();
        if (skips.AccessDenied > 0)
        {
            issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.AppCache_AccessDenied, skips.AccessDenied)));
        }

        if (skips.InUse > 0)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.AppCache_InUse, skips.InUse)));
        }

        var timedOut = results.Count(result => result.State == TargetState.TimedOut || result.Skips.Timeout > 0);
        if (timedOut > 0)
        {
            issues.Add(new Issue(CannotVerifyReason.Timeout, ProbeText.Format(ProbeStrings.AppCache_Timeout, timedOut)));
        }

        return issues;
    }

    /// <summary>
    /// 실패 결과를 만든다.
    /// </summary>
    private ProbeResult Failed(ScanContext context, List<Measurement> measurements, string summary)
    {
        var observedAt = _clock.UtcNow;
        return new ProbeResult(Id, ProbeStatus.Failed, measurements, [new Issue(CannotVerifyReason.ProbeError, summary)], observedAt, TimeSpan.Zero, context.UserContext);
    }
}
