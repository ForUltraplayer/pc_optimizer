/**
 * @file    : AppCacheTestData.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 규칙 단위 테스트용 가짜 측정값(규칙 목록 요약·규칙별 관측·Squirrel 버전 폴더·앱 설정 상태) 생성 도우미와 금지 문구 목록
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 규칙 관측 하나입니다. 크기가 null이면 크기 측정값이 없습니다.
/// </summary>
internal sealed record FakeAppRule(
    string Id,
    string Name,
    string State,
    long? Bytes = null,
    long Files = 0,
    bool Partial = false,
    string Origin = AppCacheProbeContract.ORIGIN_COMMUNITY,
    string ConfigSource = AppCacheProbeContract.CONFIG_SOURCE_NOT_APPLICABLE,
    string? ConfigApp = null,
    (string Benefit, string SideEffect, string Regeneration)? Impact = null,
    string[]? SharedWith = null,
    int ProtectedTargets = 0,
    int MergedTargets = 0,
    long SkipAccessDenied = 0,
    long SkipTimeout = 0,
    bool HasWarning = false,
    string[]? Paths = null);

/// <summary>
/// 앱 캐시 규칙 테스트 데이터 도우미입니다. 값은 모두 가짜입니다.
/// </summary>
internal static class AppCacheTestData
{
    /// <summary>
    /// 어떤 앱 캐시 문구에도 나오면 안 되는 표현(스펙 §5 앱 캐시 행, 보충 규칙 문단).
    /// </summary>
    public static readonly string[] FORBIDDEN_PHRASES =
    [
        "삭제해도 안전",
        "안전하게 삭제",
        "확보 가능",
        "미사용",
        "구버전",
        "재다운로드 가능",
        "삭제 가능",
    ];

    /// <summary>
    /// 측정값 하나를 만든다.
    /// </summary>
    public static Measurement M(string name, MeasurementValue value)
    {
        return FileScanTestData.M(name, value);
    }

    /// <summary>
    /// 무결성 확인을 마친 요약 측정값.
    /// </summary>
    public static List<Measurement> Catalog(
        int communityTotal = 4068, int communitySupported = 3500, int supplementTotal = 7, int supplementSupported = 7,
        int detected = 2, int notDetected = 3400, int unknown = 1, string[]? unsupported = null, string[]? runtime = null, bool elevated = false)
    {
        return
        [
            M(AppCacheProbeContract.CATALOG_STATE, new TextValue(AppCacheProbeContract.CATALOG_VERIFIED)),
            M(AppCacheProbeContract.WINAPP2_VERSION, new TextValue("260915")),
            M(AppCacheProbeContract.WINAPP2_COMMIT, new TextValue("53ae41946c3d3c8bb348d81081c34b6050f0b561")),
            M(AppCacheProbeContract.WINAPP2_LICENSE, new TextValue("CC-BY-SA-4.0")),
            M(AppCacheProbeContract.COMMUNITY_TOTAL, new IntegerValue(communityTotal)),
            M(AppCacheProbeContract.COMMUNITY_SUPPORTED, new IntegerValue(communitySupported)),
            M(AppCacheProbeContract.SUPPLEMENT_TOTAL, new IntegerValue(supplementTotal)),
            M(AppCacheProbeContract.SUPPLEMENT_SUPPORTED, new IntegerValue(supplementSupported)),
            M(AppCacheProbeContract.UNSUPPORTED_BY_REASON, new TextListValue(unsupported ?? ["RegistryOnly=500", "UnresolvedVariable=68"])),
            M(AppCacheProbeContract.DETECTED_COUNT, new IntegerValue(detected)),
            M(AppCacheProbeContract.NOT_DETECTED_COUNT, new IntegerValue(notDetected)),
            M(AppCacheProbeContract.DETECTION_UNKNOWN_COUNT, new IntegerValue(unknown)),
            M(AppCacheProbeContract.RUNTIME_UNSUPPORTED_BY_REASON, new TextListValue(runtime ?? [])),
            M(AppCacheProbeContract.ELEVATED_DEFAULTS_ONLY, new BooleanValue(elevated)),
        ];
    }

    /// <summary>
    /// 규칙별 관측 측정값.
    /// </summary>
    public static List<Measurement> Rules(params FakeAppRule[] rules)
    {
        var list = new List<Measurement> { M(AppCacheProbeContract.RULE_COUNT, new IntegerValue(rules.Length)) };
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, index, field);
            list.Add(M(Name(AppCacheProbeContract.FIELD_ID), new TextValue(rule.Id)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_NAME), new TextValue(rule.Name)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_ORIGIN), new TextValue(rule.Origin)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_STATE), new TextValue(rule.State)));
            if (rule.Bytes is { } bytes)
            {
                list.Add(M(Name(AppCacheProbeContract.FIELD_BYTES), new IntegerValue(bytes)));
                list.Add(M(Name(AppCacheProbeContract.FIELD_FILE_COUNT), new IntegerValue(rule.Files)));
            }

            list.Add(M(Name(AppCacheProbeContract.FIELD_PARTIAL), new BooleanValue(rule.Partial)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_DUPLICATES_POSSIBLE), new BooleanValue(true)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_PROTECTED_TARGETS), new IntegerValue(rule.ProtectedTargets)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_MERGED_TARGETS), new IntegerValue(rule.MergedTargets)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_EXCLUDED_TARGETS), new IntegerValue(0)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_SHARED_WITH), new TextListValue(rule.SharedWith ?? [])));
            list.Add(M(Name(AppCacheProbeContract.FIELD_PATHS), new TextListValue(rule.Paths ?? [@"C:\Users\tester\AppData\Local\Vendor\Cache"])));
            list.Add(M(Name(AppCacheProbeContract.FIELD_SKIP_ACCESS_DENIED), new IntegerValue(rule.SkipAccessDenied)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_SKIP_TIMEOUT), new IntegerValue(rule.SkipTimeout)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_CONFIG_SOURCE), new TextValue(rule.ConfigSource)));
            if (rule.ConfigApp is not null)
            {
                list.Add(M(Name(AppCacheProbeContract.FIELD_CONFIG_APP), new TextValue(rule.ConfigApp)));
            }

            list.Add(M(Name(AppCacheProbeContract.FIELD_HAS_WARNING), new BooleanValue(rule.HasWarning)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_REVIEWED), new BooleanValue(rule.Impact is not null)));
            if (rule.Impact is { } impact)
            {
                list.Add(M(Name(AppCacheProbeContract.FIELD_IMPACT_BENEFIT), new TextValue(impact.Benefit)));
                list.Add(M(Name(AppCacheProbeContract.FIELD_IMPACT_SIDE_EFFECT), new TextValue(impact.SideEffect)));
                list.Add(M(Name(AppCacheProbeContract.FIELD_IMPACT_REGENERATION), new TextValue(impact.Regeneration)));
                list.Add(M(Name(AppCacheProbeContract.FIELD_APP_VERSION_NOTES), new TextValue("테스트 버전 메모")));
                list.Add(M(Name(AppCacheProbeContract.FIELD_SOURCES), new TextListValue(["https://example.invalid/doc"])));
            }
        }

        return list;
    }

    /// <summary>
    /// Squirrel 버전 폴더 측정값.
    /// </summary>
    public static List<Measurement> Squirrel(params (string App, string[] Folders)[] apps)
    {
        var list = new List<Measurement> { M(AppCacheProbeContract.SQUIRREL_COUNT, new IntegerValue(apps.Length)) };
        for (var index = 0; index < apps.Length; index++)
        {
            string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.SQUIRREL_PREFIX, index, field);
            list.Add(M(Name(AppCacheProbeContract.FIELD_APP), new TextValue(apps[index].App)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_VERSION_FOLDERS), new TextListValue(apps[index].Folders)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_PATH), new TextValue(@"C:\Users\tester\AppData\Local\" + apps[index].App)));
        }

        return list;
    }

    /// <summary>
    /// 앱 설정 리더 상태 측정값.
    /// </summary>
    public static List<Measurement> Configs(params (string App, string State)[] configs)
    {
        var list = new List<Measurement> { M(AppCacheProbeContract.CONFIG_COUNT, new IntegerValue(configs.Length)) };
        for (var index = 0; index < configs.Length; index++)
        {
            string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.CONFIG_PREFIX, index, field);
            list.Add(M(Name(AppCacheProbeContract.FIELD_APP), new TextValue(configs[index].App)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_STATE), new TextValue(configs[index].State)));
            list.Add(M(Name(AppCacheProbeContract.FIELD_CONFIG_ORIGIN), new TextValue(AppCacheProbeContract.CONFIG_ORIGIN_NONE)));
        }

        return list;
    }

    /// <summary>
    /// 앱 캐시 프로브 결과 스냅샷.
    /// </summary>
    public static ScanSnapshot Snapshot(ProbeStatus status, params IEnumerable<Measurement>[] parts)
    {
        return EngineTestData.CreateSnapshot(EngineTestData.CreateResult(AppCacheProbeContract.PROBE_ID, status, [.. parts.SelectMany(part => part)]));
    }

    /// <summary>
    /// Finding의 모든 사용자 문장(제목·근거·상세·영향)을 합친다.
    /// </summary>
    public static string Text(Finding finding)
    {
        return string.Join('\n', finding.Title, finding.Evidence, finding.Detail ?? string.Empty, finding.Impact?.Benefit ?? string.Empty, finding.Impact?.SideEffect ?? string.Empty);
    }
}
