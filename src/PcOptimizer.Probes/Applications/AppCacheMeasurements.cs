/**
 * @file    : AppCacheMeasurements.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 관측 결과를 AppCacheProbeContract 측정값(규칙 목록 무결성·버전·지원/미지원 수, 탐지 수, 규칙별 관측, Squirrel 버전 폴더, 앱 설정 상태)으로 바꾸는 도우미(설정 파일 내용은 넣지 않고 경로 값만)
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications.ConfigReaders;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 탐지 요약입니다.
/// </summary>
/// <param name="Detected">탐지된 지원 규칙 수.</param>
/// <param name="NotDetected">탐지되지 않은 지원 규칙 수.</param>
/// <param name="Unknown">탐지 여부를 확인하지 못한 지원 규칙 수.</param>
/// <param name="RuntimeUnsupported">탐지됐지만 경로를 펼치지 못한 사유별 규칙 수.</param>
internal sealed record DetectionSummary(int Detected, int NotDetected, int Unknown, IReadOnlyDictionary<UnsupportedRuleReason, int> RuntimeUnsupported);

/// <summary>
/// 앱 캐시 측정값 생성 도우미입니다. 크기가 없는 상태(보호·병합 등)는 크기 측정값을 만들지 않습니다(0바이트로 바꾸지 않음).
/// </summary>
internal static class AppCacheMeasurements
{
    private const string SOURCE_RULES = @"rules\sources.json + winapp2.ini + supplement.ini + rule-metadata.json";
    private const string SOURCE_DETECTION = "RuleDetector (registry key existence, file attributes)";
    private const string SOURCE_SCAN = "Shared file scan totals + targeted metadata enumeration";
    private const string SOURCE_CONFIG = "App config readers (cache location keys only)";

    /// <summary>
    /// 규칙 목록 상태만 담은 측정값(실패 결과용).
    /// </summary>
    public static List<Measurement> ForCatalogState(string state, string? failedFile, DateTimeOffset observedAt)
    {
        var list = new List<Measurement> { Text(AppCacheProbeContract.CATALOG_STATE, state, SOURCE_RULES, observedAt) };
        if (failedFile is not null)
        {
            list.Add(Text(AppCacheProbeContract.CATALOG_FAILED_FILE, failedFile, SOURCE_RULES, observedAt));
        }

        return list;
    }

    /// <summary>
    /// 전체 측정값을 만든다.
    /// </summary>
    public static List<Measurement> Build(
        RuleCatalog catalog,
        DetectionSummary detection,
        IReadOnlyList<RuleObservation> rules,
        IReadOnlyList<SquirrelApp> squirrel,
        IReadOnlyList<ClassifiedAppConfig> configs,
        IReadOnlyDictionary<string, string> configRuleIds,
        bool elevated,
        TimeSpan elapsed,
        DateTimeOffset observedAt)
    {
        var list = ForCatalogState(catalog.State, null, observedAt);
        void Add(string name, MeasurementValue value, string source, string? unit = null, MeasurementQuality quality = MeasurementQuality.Observed)
            => list.Add(new Measurement(name, value, unit, source, observedAt, quality));

        var community = catalog.CommunityReport!;
        var supplement = catalog.SupplementReport!;
        Add(AppCacheProbeContract.WINAPP2_VERSION, new TextValue(catalog.Winapp2Source?.UpstreamVersion ?? string.Empty), SOURCE_RULES);
        Add(AppCacheProbeContract.WINAPP2_COMMIT, new TextValue(catalog.Winapp2Source?.UpstreamCommit ?? string.Empty), SOURCE_RULES);
        Add(AppCacheProbeContract.WINAPP2_SHA256, new TextValue(catalog.Winapp2Source?.Sha256 ?? string.Empty), SOURCE_RULES);
        Add(AppCacheProbeContract.WINAPP2_LICENSE, new TextValue(catalog.Winapp2Source?.License ?? string.Empty), SOURCE_RULES);
        Add(AppCacheProbeContract.COMMUNITY_TOTAL, new IntegerValue(community.TotalRules), SOURCE_RULES);
        Add(AppCacheProbeContract.COMMUNITY_SUPPORTED, new IntegerValue(community.SupportedRules), SOURCE_RULES);
        Add(AppCacheProbeContract.SUPPLEMENT_TOTAL, new IntegerValue(supplement.TotalRules), SOURCE_RULES);
        Add(AppCacheProbeContract.SUPPLEMENT_SUPPORTED, new IntegerValue(supplement.SupportedRules), SOURCE_RULES);
        var parseReasons = community.UnsupportedByReason.Concat(supplement.UnsupportedByReason)
            .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));
        Add(AppCacheProbeContract.UNSUPPORTED_BY_REASON, new TextListValue(ReasonCounts(parseReasons)), SOURCE_RULES);
        Add(AppCacheProbeContract.PARSE_MS, new IntegerValue((long)catalog.ParseElapsed.TotalMilliseconds), SOURCE_RULES, AppCacheProbeContract.UNIT_MILLISECONDS);
        Add(AppCacheProbeContract.DETECTED_COUNT, new IntegerValue(detection.Detected), SOURCE_DETECTION);
        Add(AppCacheProbeContract.NOT_DETECTED_COUNT, new IntegerValue(detection.NotDetected), SOURCE_DETECTION);
        Add(AppCacheProbeContract.DETECTION_UNKNOWN_COUNT, new IntegerValue(detection.Unknown), SOURCE_DETECTION);
        Add(AppCacheProbeContract.RUNTIME_UNSUPPORTED_BY_REASON, new TextListValue(ReasonCounts(detection.RuntimeUnsupported)), SOURCE_DETECTION);
        Add(AppCacheProbeContract.ELEVATED_DEFAULTS_ONLY, new BooleanValue(elevated), SOURCE_CONFIG);
        Add(AppCacheProbeContract.ELAPSED_MS, new IntegerValue((long)elapsed.TotalMilliseconds), SOURCE_SCAN, AppCacheProbeContract.UNIT_MILLISECONDS);

        Add(AppCacheProbeContract.RULE_COUNT, new IntegerValue(rules.Count), SOURCE_SCAN);
        for (var index = 0; index < rules.Count; index++)
        {
            AddRule(list, rules[index], index, observedAt);
        }

        Add(AppCacheProbeContract.SQUIRREL_COUNT, new IntegerValue(squirrel.Count), SOURCE_SCAN);
        for (var index = 0; index < squirrel.Count; index++)
        {
            string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.SQUIRREL_PREFIX, index, field);
            Add(Name(AppCacheProbeContract.FIELD_APP), new TextValue(squirrel[index].App), SOURCE_SCAN);
            Add(Name(AppCacheProbeContract.FIELD_VERSION_FOLDERS), new TextListValue(squirrel[index].VersionFolders), SOURCE_SCAN);
            Add(Name(AppCacheProbeContract.FIELD_PATH), new TextValue(squirrel[index].Parent), SOURCE_SCAN);
        }

        Add(AppCacheProbeContract.CONFIG_COUNT, new IntegerValue(configs.Count), SOURCE_CONFIG);
        for (var index = 0; index < configs.Count; index++)
        {
            var config = configs[index];
            string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.CONFIG_PREFIX, index, field);
            Add(Name(AppCacheProbeContract.FIELD_APP), new TextValue(config.Reading.App), SOURCE_CONFIG);
            Add(Name(AppCacheProbeContract.FIELD_STATE), new TextValue(config.State), SOURCE_CONFIG);
            Add(Name(AppCacheProbeContract.FIELD_CONFIG_ORIGIN), new TextValue(OriginCode(config.Reading.Origin)), SOURCE_CONFIG);
            Add(Name(AppCacheProbeContract.FIELD_RULE_ID), new TextValue(configRuleIds.GetValueOrDefault(config.Reading.App, string.Empty)), SOURCE_CONFIG);
            Add(Name(AppCacheProbeContract.FIELD_PATHS), new TextListValue(config.Reading.Paths), SOURCE_CONFIG);
            if (config.Reading.LibraryCount is { } libraries)
            {
                Add(Name(AppCacheProbeContract.FIELD_LIBRARY_COUNT), new IntegerValue(libraries), SOURCE_CONFIG);
            }
        }

        return list;
    }

    /// <summary>
    /// 규칙 하나의 측정값을 더한다.
    /// </summary>
    private static void AddRule(List<Measurement> list, RuleObservation rule, int index, DateTimeOffset observedAt)
    {
        string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, index, field);
        void Add(string field, MeasurementValue value, string? unit = null, MeasurementQuality quality = MeasurementQuality.Observed)
            => list.Add(new Measurement(Name(field), value, unit, SOURCE_SCAN, observedAt, quality));

        Add(AppCacheProbeContract.FIELD_ID, new TextValue(rule.Rule.Id));
        Add(AppCacheProbeContract.FIELD_NAME, new TextValue(rule.Rule.Name));
        Add(AppCacheProbeContract.FIELD_APP, new TextValue(rule.Metadata?.AppLabel ?? rule.Rule.Name));
        Add(AppCacheProbeContract.FIELD_ORIGIN, new TextValue(rule.Rule.Origin == RuleOrigin.Supplement ? AppCacheProbeContract.ORIGIN_SUPPLEMENT : AppCacheProbeContract.ORIGIN_COMMUNITY));
        Add(AppCacheProbeContract.FIELD_STATE, new TextValue(rule.State));
        if (rule.Bytes is { } bytes)
        {
            var quality = rule.Partial ? MeasurementQuality.Partial : rule.DuplicatesPossible ? MeasurementQuality.Estimated : MeasurementQuality.Observed;
            Add(AppCacheProbeContract.FIELD_BYTES, new IntegerValue(bytes), AppCacheProbeContract.UNIT_BYTES, quality);
            Add(AppCacheProbeContract.FIELD_FILE_COUNT, new IntegerValue(rule.FileCount), null, quality);
        }

        Add(AppCacheProbeContract.FIELD_PARTIAL, new BooleanValue(rule.Partial));
        Add(AppCacheProbeContract.FIELD_DUPLICATES_POSSIBLE, new BooleanValue(rule.DuplicatesPossible));
        Add(AppCacheProbeContract.FIELD_TARGET_COUNT, new IntegerValue(rule.Paths.Count));
        Add(AppCacheProbeContract.FIELD_PROTECTED_TARGETS, new IntegerValue(rule.ProtectedTargets));
        Add(AppCacheProbeContract.FIELD_EXCLUDED_TARGETS, new IntegerValue(rule.ExcludedTargets));
        Add(AppCacheProbeContract.FIELD_MERGED_TARGETS, new IntegerValue(rule.MergedTargets));
        Add(AppCacheProbeContract.FIELD_SHARED_WITH, new TextListValue(rule.SharedWith));
        Add(AppCacheProbeContract.FIELD_PATHS, new TextListValue(rule.Paths));
        Add(AppCacheProbeContract.FIELD_SKIP_ACCESS_DENIED, new IntegerValue(rule.Skips.AccessDenied));
        Add(AppCacheProbeContract.FIELD_SKIP_IN_USE, new IntegerValue(rule.Skips.InUse));
        Add(AppCacheProbeContract.FIELD_SKIP_TIMEOUT, new IntegerValue(rule.Skips.Timeout));
        Add(AppCacheProbeContract.FIELD_SKIP_PROTECTED, new IntegerValue(rule.Skips.ProtectedExcluded));
        Add(AppCacheProbeContract.FIELD_SKIP_REPARSE, new IntegerValue(rule.Skips.Reparse));
        Add(AppCacheProbeContract.FIELD_SKIP_PLACEHOLDER, new IntegerValue(rule.Skips.Placeholder));
        Add(AppCacheProbeContract.FIELD_CONFIG_SOURCE, new TextValue(rule.ConfigSource));
        if (rule.Metadata?.ConfigReader is { } reader)
        {
            Add(AppCacheProbeContract.FIELD_CONFIG_APP, new TextValue(reader));
        }

        Add(AppCacheProbeContract.FIELD_HAS_WARNING, new BooleanValue(rule.Rule.HasWarning));
        Add(AppCacheProbeContract.FIELD_REG_KEY_COUNT, new IntegerValue(rule.Rule.RegKeyCount));
        Add(AppCacheProbeContract.FIELD_REVIEWED, new BooleanValue(rule.Metadata is not null));
        if (rule.Metadata is { } metadata)
        {
            Add(AppCacheProbeContract.FIELD_IMPACT_BENEFIT, new TextValue(metadata.Impact.Benefit));
            Add(AppCacheProbeContract.FIELD_IMPACT_SIDE_EFFECT, new TextValue(metadata.Impact.SideEffect));
            Add(AppCacheProbeContract.FIELD_IMPACT_REGENERATION, new TextValue(metadata.Impact.Regeneration));
            Add(AppCacheProbeContract.FIELD_APP_VERSION_NOTES, new TextValue(metadata.AppVersionNotes));
            Add(AppCacheProbeContract.FIELD_SOURCES, new TextListValue(metadata.Sources));
        }
    }

    /// <summary>
    /// "사유=개수" 목록(개수 내림차순, 같으면 사유 이름순).
    /// </summary>
    private static List<string> ReasonCounts(IReadOnlyDictionary<UnsupportedRuleReason, int> counts)
    {
        return [.. counts
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => pair.Key.ToString() + AppCacheProbeContract.COUNT_SEPARATOR + pair.Value.ToString(CultureInfo.InvariantCulture))];
    }

    /// <summary>
    /// 값 출처 코드.
    /// </summary>
    private static string OriginCode(AppConfigValueOrigin origin)
    {
        return origin switch
        {
            AppConfigValueOrigin.Environment => AppCacheProbeContract.CONFIG_ORIGIN_ENVIRONMENT,
            AppConfigValueOrigin.UserFile => AppCacheProbeContract.CONFIG_ORIGIN_USER_FILE,
            AppConfigValueOrigin.Registry => AppCacheProbeContract.CONFIG_ORIGIN_REGISTRY,
            _ => AppCacheProbeContract.CONFIG_ORIGIN_NONE,
        };
    }

    /// <summary>
    /// 문자열 측정값.
    /// </summary>
    private static Measurement Text(string name, string value, string source, DateTimeOffset observedAt)
    {
        return new Measurement(name, new TextValue(value), null, source, observedAt, MeasurementQuality.Observed);
    }
}
