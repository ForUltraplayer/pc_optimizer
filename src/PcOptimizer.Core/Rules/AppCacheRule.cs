/**
 * @file    : AppCacheRule.cs
 * @author  : rudals252
 * @brief   : 탐지된 앱 캐시 규칙별 관측 크기 정보(검토한 영향 또는 영향 미확인, 사용자 설정 경로/기본 위치만 확인 범위, 겹친 규칙, 보호·부분·없음 구분)와 앱 설정 위치 확인 불가를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 앱 캐시 규칙입니다(스펙 §5 앱 캐시 행: Info '캐시/잔여 파일 후보의 관측 크기', 규칙별 검증된 영향 표시, 미검토 규칙은 영향 미확인,
/// 실행 중인 앱의 미사용 캐시로 확정하지 않음, 사용자 설정을 해석하지 못하면 기본 위치만 확인했다는 범위 표시).
/// </summary>
/// <remarks>
/// 후보(Candidate)를 만들지 않고, 크기를 비울 수 있는 용량으로 표현하지 않습니다. 제목·근거·상세에 경로를 넣지 않습니다(경로는 측정값에만).
/// </remarks>
public sealed class AppCacheRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "appCache.rules";

    /// <summary>규칙별 Finding ID 접두사(뒤에 규칙 ID).</summary>
    public const string FINDING_ID_PREFIX = "appCache.rule:";

    /// <summary>앱 설정 위치 Finding ID 접두사(뒤에 앱 이름).</summary>
    public const string CONFIG_FINDING_ID_PREFIX = "appCache.config:";

    /// <summary>저장소 설정 URI(허용 목록에 있는 값).</summary>
    public const string STORAGE_SETTINGS_URI = StorageSpaceRule.STORAGE_SETTINGS_URI;

    private const string COUNT_FORMAT = "N0";
    private const string DETAIL_SEPARATOR = "\n";
    private const string LIST_SEPARATOR = ", ";
    private const string PROBE_ID = AppCacheProbeContract.PROBE_ID;

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(PROBE_ID, out var result) || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var elevated = SnapshotValues.Boolean(snapshot, PROBE_ID, AppCacheProbeContract.ELEVATED_DEFAULTS_ONLY) ?? false;
        var findings = new List<Finding>();
        var ruleCount = SnapshotValues.Integer(snapshot, PROBE_ID, AppCacheProbeContract.RULE_COUNT) ?? 0;
        for (var index = 0; index < ruleCount; index++)
        {
            findings.Add(EvaluateRule(snapshot, result, index, elevated));
        }

        var configCount = SnapshotValues.Integer(snapshot, PROBE_ID, AppCacheProbeContract.CONFIG_COUNT) ?? 0;
        for (var index = 0; index < configCount; index++)
        {
            if (EvaluateConfig(snapshot, result, index) is { } finding)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    /// <summary>
    /// 규칙 하나를 판정한다.
    /// </summary>
    private static Finding EvaluateRule(ScanSnapshot snapshot, ProbeResult result, int index, bool elevated)
    {
        string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, index, field);
        string? Text(string field) => SnapshotValues.Text(snapshot, PROBE_ID, Name(field));
        long Count(string field) => SnapshotValues.Integer(snapshot, PROBE_ID, Name(field)) ?? 0;

        var ruleId = Text(AppCacheProbeContract.FIELD_ID) ?? index.ToString(CultureInfo.InvariantCulture);
        var name = Text(AppCacheProbeContract.FIELD_NAME) ?? ruleId;
        var state = Text(AppCacheProbeContract.FIELD_STATE);
        var bytes = SnapshotValues.Integer(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_BYTES));
        var files = Count(AppCacheProbeContract.FIELD_FILE_COUNT).ToString(COUNT_FORMAT, CultureInfo.InvariantCulture);
        var duplicates = SnapshotValues.Boolean(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_DUPLICATES_POSSIBLE)) ?? true;
        var sizeNote = duplicates ? CoreStrings.FileScan_SizeNote_Estimated : CoreStrings.FileScan_SizeNote_Verified;
        var reviewed = SnapshotValues.Boolean(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_REVIEWED)) ?? false;
        var benefit = Text(AppCacheProbeContract.FIELD_IMPACT_BENEFIT);
        var sideEffect = Text(AppCacheProbeContract.FIELD_IMPACT_SIDE_EFFECT);
        var impact = reviewed && benefit is not null && sideEffect is not null
            ? new Impact(benefit, sideEffect)
            : new Impact(CoreStrings.AppCache_Impact_Unknown, CoreStrings.AppCache_Impact_UnknownSideEffect);

        var details = new List<string>();
        if (reviewed && benefit is not null && sideEffect is not null)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_Impact, benefit, sideEffect, Text(AppCacheProbeContract.FIELD_IMPACT_REGENERATION) ?? CoreStrings.Unclassified_NoValue));
            if (Text(AppCacheProbeContract.FIELD_APP_VERSION_NOTES) is { } notes)
            {
                details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_AppVersion, notes));
            }
        }
        else
        {
            details.Add(CoreStrings.AppCache_Detail_ImpactUnknown);
        }

        AddConfigDetail(details, Text(AppCacheProbeContract.FIELD_CONFIG_SOURCE), Text(AppCacheProbeContract.FIELD_CONFIG_APP), elevated);
        AddCountDetails(snapshot, Name, details);
        if (SnapshotValues.Boolean(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_HAS_WARNING)) == true)
        {
            details.Add(CoreStrings.AppCache_Detail_Warning);
        }

        var measured = SnapshotValues.WithPrefix(result, AppCacheProbeContract.ItemPrefix(AppCacheProbeContract.RULE_PREFIX, index));
        var detail = string.Join(DETAIL_SEPARATOR, details);
        Finding Info(string title, string evidence) => Create(FINDING_ID_PREFIX + ruleId, title, measured, evidence, Verdict.Info, null, detail, impact);
        Finding NotVerified(string title, CannotVerifyReason reason, string evidence) => Create(FINDING_ID_PREFIX + ruleId, title, measured, evidence, Verdict.CannotVerify, reason, detail, impact);

        return state switch
        {
            AppCacheProbeContract.RULE_STATE_OBSERVED when bytes is { } observed => Info(
                SnapshotValues.Format(CoreStrings.AppCache_Title_Observed, name, ByteSizeText.Format(observed)),
                SnapshotValues.Format(CoreStrings.AppCache_Evidence_Observed, files, sizeNote)),
            AppCacheProbeContract.RULE_STATE_PARTIAL when bytes is { } partial => Info(
                SnapshotValues.Format(CoreStrings.AppCache_Title_Partial, name, ByteSizeText.Format(partial)),
                SnapshotValues.Format(CoreStrings.AppCache_Evidence_Partial, files, sizeNote)),
            AppCacheProbeContract.RULE_STATE_ABSENT => Info(SnapshotValues.Format(CoreStrings.AppCache_Title_Absent, name), CoreStrings.AppCache_Evidence_Absent),
            AppCacheProbeContract.RULE_STATE_MERGED => Info(SnapshotValues.Format(CoreStrings.AppCache_Title_Merged, name), CoreStrings.AppCache_Evidence_Merged),
            AppCacheProbeContract.RULE_STATE_EXCLUDED => Info(SnapshotValues.Format(CoreStrings.AppCache_Title_Excluded, name), CoreStrings.AppCache_Evidence_Excluded),
            AppCacheProbeContract.RULE_STATE_PROTECTED => NotVerified(
                SnapshotValues.Format(CoreStrings.AppCache_Title_Protected, name), CannotVerifyReason.Unsupported, CoreStrings.AppCache_Evidence_Protected),
            AppCacheProbeContract.RULE_STATE_ACCESS_DENIED => NotObserved(CannotVerifyReason.AccessDenied),
            AppCacheProbeContract.RULE_STATE_TIMED_OUT => NotObserved(CannotVerifyReason.Timeout),
            _ => NotObserved(CannotVerifyReason.PartialData),
        };

        Finding NotObserved(CannotVerifyReason reason) =>
            NotVerified(SnapshotValues.Format(CoreStrings.AppCache_Title_NotObserved, name), reason, CannotVerifyTexts.EvidenceFor(reason));
    }

    /// <summary>
    /// 앱 설정 경로를 적용했는지·기본 위치만 확인했는지 범위를 상세에 더한다.
    /// </summary>
    private static void AddConfigDetail(List<string> details, string? configSource, string? configApp, bool elevated)
    {
        switch (configSource)
        {
            case AppCacheProbeContract.CONFIG_SOURCE_USER:
                details.Add(CoreStrings.AppCache_Detail_ConfigUser);
                break;
            case AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY when elevated:
                details.Add(CoreStrings.AppCache_Detail_ConfigElevated);
                break;
            case AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY when configApp == AppCacheProbeContract.CONFIG_APP_ADOBE:
                details.Add(CoreStrings.AppCache_Detail_ConfigAdobe);
                break;
            case AppCacheProbeContract.CONFIG_SOURCE_DEFAULT_ONLY:
                details.Add(CoreStrings.AppCache_Detail_ConfigDefaultOnly);
                break;
        }
    }

    /// <summary>
    /// 겹친 규칙·보호·병합·제외 위치 수와 건너뛴 항목을 상세에 더한다(0이면 생략).
    /// </summary>
    private static void AddCountDetails(ScanSnapshot snapshot, Func<string, string> name, List<string> details)
    {
        long Count(string field) => SnapshotValues.Integer(snapshot, PROBE_ID, name(field)) ?? 0;

        if (SnapshotValues.TextList(snapshot, PROBE_ID, name(AppCacheProbeContract.FIELD_SHARED_WITH)) is { Count: > 0 } shared)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_SharedWith, string.Join(LIST_SEPARATOR, shared)));
        }

        foreach (var (field, template) in new[]
        {
            (AppCacheProbeContract.FIELD_PROTECTED_TARGETS, CoreStrings.AppCache_Detail_Protected),
            (AppCacheProbeContract.FIELD_MERGED_TARGETS, CoreStrings.AppCache_Detail_Merged),
            (AppCacheProbeContract.FIELD_EXCLUDED_TARGETS, CoreStrings.AppCache_Detail_Excluded),
        })
        {
            if (Count(field) is > 0 and var value)
            {
                details.Add(SnapshotValues.Format(template, value));
            }
        }

        long[] skips =
        [
            Count(AppCacheProbeContract.FIELD_SKIP_ACCESS_DENIED),
            Count(AppCacheProbeContract.FIELD_SKIP_IN_USE),
            Count(AppCacheProbeContract.FIELD_SKIP_TIMEOUT),
            Count(AppCacheProbeContract.FIELD_SKIP_PROTECTED),
            Count(AppCacheProbeContract.FIELD_SKIP_REPARSE),
            Count(AppCacheProbeContract.FIELD_SKIP_PLACEHOLDER),
        ];
        if (skips.Any(skip => skip > 0))
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_Skips, [.. skips.Cast<object>()]));
        }
    }

    /// <summary>
    /// 앱 설정 리더 결과 하나를 판정한다(순회하지 않은 설정 위치·형식 미검증만 Finding을 만든다).
    /// </summary>
    private static Finding? EvaluateConfig(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.CONFIG_PREFIX, index, field);

        var app = SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_APP));
        var state = SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_STATE));
        if (app is null)
        {
            return null;
        }

        (CannotVerifyReason Reason, string Evidence)? outcome = state switch
        {
            AppCacheProbeContract.CONFIG_STATE_CANNOT_VERIFY => (CannotVerifyReason.Unsupported, CoreStrings.AppCache_Config_Evidence_Adobe),
            AppCacheProbeContract.CONFIG_STATE_UNC => (CannotVerifyReason.Unsupported, CoreStrings.AppCache_Config_Evidence_Unc),
            AppCacheProbeContract.CONFIG_STATE_PROTECTED => (CannotVerifyReason.Unsupported, CoreStrings.AppCache_Config_Evidence_Protected),
            AppCacheProbeContract.CONFIG_STATE_OFFLINE => (CannotVerifyReason.Unsupported, CoreStrings.AppCache_Config_Evidence_Offline),
            AppCacheProbeContract.CONFIG_STATE_INVALID => (CannotVerifyReason.Unsupported, CoreStrings.AppCache_Config_Evidence_Invalid),
            AppCacheProbeContract.CONFIG_STATE_UNREADABLE => (CannotVerifyReason.AccessDenied, CoreStrings.AppCache_Config_Evidence_Unreadable),
            _ => null,
        };
        if (outcome is not { } found)
        {
            return null;
        }

        var title = state == AppCacheProbeContract.CONFIG_STATE_CANNOT_VERIFY
            ? CoreStrings.AppCache_Config_Title_Adobe
            : SnapshotValues.Format(CoreStrings.AppCache_Config_Title_NotTraversed, AppLabel(app));
        return new Finding(
            id: CONFIG_FINDING_ID_PREFIX + app,
            category: FindingCategory.AppCache,
            title: title,
            measured: SnapshotValues.WithPrefix(result, AppCacheProbeContract.ItemPrefix(AppCacheProbeContract.CONFIG_PREFIX, index)),
            evidence: found.Evidence,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: found.Reason,
            detail: SnapshotValues.Format(CoreStrings.AppCache_Config_Detail_Scope, ScopeLabel(app)),
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }

    /// <summary>
    /// 앱 설정 리더 이름의 화면용 이름.
    /// </summary>
    private static string AppLabel(string app)
    {
        return app switch
        {
            "npm" => CoreStrings.AppCache_ConfigApp_npm,
            "pip" => CoreStrings.AppCache_ConfigApp_pip,
            "nuget" => CoreStrings.AppCache_ConfigApp_nuget,
            "steam" => CoreStrings.AppCache_ConfigApp_steam,
            _ => CoreStrings.AppCache_ConfigApp_adobe,
        };
    }

    /// <summary>
    /// 앱별로 읽는 설정 범위 설명.
    /// </summary>
    private static string ScopeLabel(string app)
    {
        return app switch
        {
            "npm" => CoreStrings.AppCache_ConfigScope_npm,
            "pip" => CoreStrings.AppCache_ConfigScope_pip,
            "nuget" => CoreStrings.AppCache_ConfigScope_nuget,
            "steam" => CoreStrings.AppCache_ConfigScope_steam,
            _ => CoreStrings.AppCache_ConfigScope_adobe,
        };
    }

    /// <summary>
    /// 규칙별 Finding을 만든다. 상세 보기와 저장소 설정 열기를 붙인다.
    /// </summary>
    private static Finding Create(
        string id, string title, IReadOnlyList<Measurement> measured, string evidence, Verdict verdict, CannotVerifyReason? reason, string detail, Impact impact)
    {
        return new Finding(
            id: id,
            category: FindingCategory.AppCache,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: reason,
            detail: detail,
            recommendation: null,
            impact: impact,
            actions: [new ShowDetailsAction(), new OpenSettingsAction(STORAGE_SETTINGS_URI)]);
    }
}
