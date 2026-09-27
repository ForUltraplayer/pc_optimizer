/**
 * @file    : AppCacheRule.cs
 * @author  : rudals252
 * @brief   : 관측한 파일이 있는 앱마다 카드 하나(검토한 보충 규칙만 캐시 문구·설정 열기, 커뮤니티 규칙은 중립 문구와 상세 보기만, 규칙별 크기·영향·설정 범위는 상세에), 관측이 실패한 앱마다 사유가 있는 확인 불가 카드 하나와 앱 설정 위치 확인 불가를 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 앱 캐시 규칙입니다(스펙 §5 앱 캐시 행, §7 화면).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>카드는 앱 단위입니다. 관측한 파일이 있는 앱은 Info 카드, 관측이 실패한 앱(보호·접근 거부·시간 초과·검사하지 못함)은 사유가 있는 확인 불가 카드이며, 파일 없음·겹쳐 합친 앱만 <see cref="RuleCatalogSummaryRule"/> 요약에 개수로 접습니다.</item>
/// <item>"캐시·임시 파일 후보" 문구와 저장소 설정 열기는 파일을 센 규칙이 모두 검토한 보충 규칙인 카드에만 씁니다. 커뮤니티 규칙은 "규칙 위치의 파일"로 표시합니다.</item>
/// <item>원본 Warning 문구가 있거나 비밀번호·쿠키·세션·기록·자격 증명 관련 이름의 규칙은 카드에서 뺍니다.</item>
/// <item>후보(Candidate)를 만들지 않고, 크기를 비울 수 있는 용량으로 표현하지 않으며, 문장에 경로를 넣지 않습니다(경로는 측정값에만).</item>
/// </list>
/// </remarks>
public sealed class AppCacheRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "appCache.rules";

    /// <summary>앱 카드 Finding ID 접두사(뒤에 앱 이름).</summary>
    public const string FINDING_ID_PREFIX = "appCache.app:";

    /// <summary>앱 설정 위치 Finding ID 접두사(뒤에 앱 설정 리더 이름).</summary>
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
        var plan = AppCacheCardPlanner.Plan(snapshot);
        var findings = plan.Cards.Select(card => CreateCard(snapshot, result, card, elevated)).ToList();
        findings.AddRange(plan.Unverified.Select(app => CreateUnverifiedCard(snapshot, result, app)));
        var configCount = SnapshotValues.Integer(snapshot, PROBE_ID, AppCacheProbeContract.CONFIG_COUNT) ?? 0;
        for (var index = 0; index < configCount; index++)
        {
            if (AppCacheConfigFindings.Evaluate(snapshot, result, index) is { } finding)
            {
                findings.Add(finding);
            }
        }

        // 새 위치별 관측이 있으면 같은 보충 카드/승격 설정 안내만 대체한다. 다른 커뮤니티 앱 정보는 보존한다.
        if (ShaderCacheRule.HasGroup(snapshot, "steam"))
        { findings.RemoveAll(f => f.Id is FINDING_ID_PREFIX + "Steam 셰이더 캐시" or CONFIG_FINDING_ID_PREFIX + "steam"); }
        if (ShaderCacheRule.HasGroup(snapshot, "graphics"))
        { findings.RemoveAll(f => f.Id == FINDING_ID_PREFIX + "NVIDIA·Direct3D 셰이더 캐시"); }
        return findings;
    }

    /// <summary>
    /// 앱 카드 하나를 만든다.
    /// </summary>
    private static Finding CreateCard(ScanSnapshot snapshot, ProbeResult result, AppCacheCard card, bool elevated)
    {
        var observed = card.Rules.Where(rule => rule.Bytes is > 0).ToList();
        var duplicates = observed.Any(rule => Boolean(snapshot, rule, AppCacheProbeContract.FIELD_DUPLICATES_POSSIBLE) ?? true);
        var sizeNote = duplicates ? CoreStrings.FileScan_SizeNote_Estimated : CoreStrings.FileScan_SizeNote_Verified;
        var files = card.FileCount.ToString(COUNT_FORMAT, CultureInfo.InvariantCulture);
        var size = ByteSizeText.Format(card.Bytes);
        var reviewedRule = card.Reviewed ? observed.First(rule => rule.Supplement) : null;

        var title = (card.Reviewed, card.Partial) switch
        {
            (true, false) => SnapshotValues.Format(CoreStrings.AppCache_Title_Observed, card.App, size),
            (true, true) => SnapshotValues.Format(CoreStrings.AppCache_Title_Partial, card.App, size),
            (false, false) => SnapshotValues.Format(CoreStrings.AppCache_Title_CommunityObserved, card.App, size),
            _ => SnapshotValues.Format(CoreStrings.AppCache_Title_CommunityPartial, card.App, size),
        };
        var evidence = SnapshotValues.Format(card.Partial ? CoreStrings.AppCache_Evidence_Partial : CoreStrings.AppCache_Evidence_Observed, files, sizeNote);
        var impact = reviewedRule is not null && Text(snapshot, reviewedRule, AppCacheProbeContract.FIELD_IMPACT_BENEFIT) is { } benefit
            && Text(snapshot, reviewedRule, AppCacheProbeContract.FIELD_IMPACT_SIDE_EFFECT) is { } sideEffect
            ? new Impact(benefit, sideEffect)
            : new Impact(CoreStrings.AppCache_Impact_Unknown, CoreStrings.AppCache_Impact_UnknownSideEffect);

        var measured = card.Rules
            .SelectMany(rule => SnapshotValues.WithPrefix(result, AppCacheProbeContract.ItemPrefix(AppCacheProbeContract.RULE_PREFIX, rule.Index)))
            .ToList();
        IReadOnlyList<FindingAction> actions = card.Reviewed
            ? [new ShowDetailsAction(), new OpenSettingsAction(STORAGE_SETTINGS_URI)]
            : [new ShowDetailsAction()];
        return new Finding(
            id: FINDING_ID_PREFIX + card.App,
            category: FindingCategory.AppCache,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: string.Join(DETAIL_SEPARATOR, Details(snapshot, card, reviewedRule, elevated)),
            recommendation: null,
            impact: impact,
            actions: actions);
    }

    /// <summary>
    /// 관측이 실패한 앱의 확인 불가 카드를 만든다(크기를 말하지 않음, 상세 보기만).
    /// </summary>
    private static Finding CreateUnverifiedCard(ScanSnapshot snapshot, ProbeResult result, AppCacheUnverifiedApp app)
    {
        var evidence = app.State switch
        {
            AppCacheProbeContract.RULE_STATE_ACCESS_DENIED => CoreStrings.AppCache_Evidence_AccessDenied,
            AppCacheProbeContract.RULE_STATE_TIMED_OUT => CoreStrings.AppCache_Evidence_Timeout,
            AppCacheProbeContract.RULE_STATE_PROTECTED => CoreStrings.AppCache_Evidence_Protected,
            _ => CoreStrings.AppCache_Evidence_NotObserved,
        };
        var details = app.Rules.Select(rule => RuleLine(snapshot, rule)).ToList();
        AddTargetDetails(details, snapshot, app.Rules);
        return new Finding(
            id: FINDING_ID_PREFIX + app.App,
            category: FindingCategory.AppCache,
            title: SnapshotValues.Format(CoreStrings.AppCache_Title_CannotVerify, app.App),
            measured: app.Rules.SelectMany(rule => SnapshotValues.WithPrefix(result, AppCacheProbeContract.ItemPrefix(AppCacheProbeContract.RULE_PREFIX, rule.Index))).ToList(),
            evidence: evidence,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: app.Reason,
            detail: string.Join(DETAIL_SEPARATOR, details),
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }

    /// <summary>
    /// 규칙 한 줄: 관측 크기, 관측 실패 사유, 또는 파일 없음.
    /// </summary>
    private static string RuleLine(ScanSnapshot snapshot, AppCacheRuleEntry rule)
    {
        if (rule.Bytes is > 0)
        {
            return SnapshotValues.Format(rule.Partial ? CoreStrings.AppCache_Detail_RuleLinePartial : CoreStrings.AppCache_Detail_RuleLine,
                rule.Id, ByteSizeText.Format(rule.Bytes.Value), rule.FileCount.ToString(COUNT_FORMAT, CultureInfo.InvariantCulture),
                Integer(snapshot, rule, AppCacheProbeContract.FIELD_TARGET_COUNT));
        }

        if (AppCacheCardPlanner.IsFailed(rule))
        {
            var label = rule.State switch
            {
                AppCacheProbeContract.RULE_STATE_ACCESS_DENIED => CoreStrings.AppCache_State_AccessDenied,
                AppCacheProbeContract.RULE_STATE_TIMED_OUT => CoreStrings.AppCache_State_TimedOut,
                AppCacheProbeContract.RULE_STATE_PROTECTED => CoreStrings.AppCache_State_Protected,
                _ => CoreStrings.AppCache_State_NotObserved,
            };
            return SnapshotValues.Format(CoreStrings.AppCache_Detail_RuleUnverified, rule.Id, label);
        }

        return SnapshotValues.Format(CoreStrings.AppCache_Detail_RuleNoFiles, rule.Id);
    }

    /// <summary>
    /// 카드 상세: 규칙별 크기, 영향(검토 또는 미확인), 설정 범위, 겹친 규칙, 위치 수, 건너뜀.
    /// </summary>
    private static List<string> Details(ScanSnapshot snapshot, AppCacheCard card, AppCacheRuleEntry? reviewedRule, bool elevated)
    {
        var details = card.Rules.Select(rule => RuleLine(snapshot, rule)).ToList();

        if (reviewedRule is not null && Text(snapshot, reviewedRule, AppCacheProbeContract.FIELD_IMPACT_BENEFIT) is { } benefit)
        {
            details.Add(SnapshotValues.Format(
                CoreStrings.AppCache_Detail_Impact,
                benefit,
                Text(snapshot, reviewedRule, AppCacheProbeContract.FIELD_IMPACT_SIDE_EFFECT) ?? CoreStrings.Unclassified_NoValue,
                Text(snapshot, reviewedRule, AppCacheProbeContract.FIELD_IMPACT_REGENERATION) ?? CoreStrings.Unclassified_NoValue));
            if (Text(snapshot, reviewedRule, AppCacheProbeContract.FIELD_APP_VERSION_NOTES) is { } notes)
            {
                details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_AppVersion, notes));
            }
        }
        else
        {
            details.Add(CoreStrings.AppCache_Detail_ImpactUnknown);
        }

        foreach (var rule in card.Rules.Where(rule => rule.Supplement))
        {
            AddConfigDetail(details, Text(snapshot, rule, AppCacheProbeContract.FIELD_CONFIG_SOURCE), Text(snapshot, rule, AppCacheProbeContract.FIELD_CONFIG_APP), elevated);
        }

        var shared = card.Rules
            .SelectMany(rule => SnapshotValues.TextList(snapshot, PROBE_ID, Name(rule, AppCacheProbeContract.FIELD_SHARED_WITH)) ?? [])
            .Where(id => card.Rules.All(rule => rule.Id != id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (shared.Count > 0)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_SharedWith, string.Join(LIST_SEPARATOR, shared)));
        }

        AddTargetDetails(details, snapshot, card.Rules);
        return details;
    }

    /// <summary>
    /// 규칙들의 보호·겹침·제외 위치 수와 건너뜀 합을 상세에 더한다.
    /// </summary>
    private static void AddTargetDetails(List<string> details, ScanSnapshot snapshot, IReadOnlyList<AppCacheRuleEntry> rules)
    {
        foreach (var (field, template) in new[]
        {
            (AppCacheProbeContract.FIELD_PROTECTED_TARGETS, CoreStrings.AppCache_Detail_Protected),
            (AppCacheProbeContract.FIELD_MERGED_TARGETS, CoreStrings.AppCache_Detail_Merged),
            (AppCacheProbeContract.FIELD_EXCLUDED_TARGETS, CoreStrings.AppCache_Detail_Excluded),
        })
        {
            if (rules.Sum(rule => Integer(snapshot, rule, field)) is > 0 and var total)
            {
                details.Add(SnapshotValues.Format(template, total));
            }
        }

        long[] skips =
        [
            rules.Sum(rule => Integer(snapshot, rule, AppCacheProbeContract.FIELD_SKIP_ACCESS_DENIED)),
            rules.Sum(rule => Integer(snapshot, rule, AppCacheProbeContract.FIELD_SKIP_IN_USE)),
            rules.Sum(rule => Integer(snapshot, rule, AppCacheProbeContract.FIELD_SKIP_TIMEOUT)),
            rules.Sum(rule => Integer(snapshot, rule, AppCacheProbeContract.FIELD_SKIP_PROTECTED)),
            rules.Sum(rule => Integer(snapshot, rule, AppCacheProbeContract.FIELD_SKIP_REPARSE)),
            rules.Sum(rule => Integer(snapshot, rule, AppCacheProbeContract.FIELD_SKIP_PLACEHOLDER)),
        ];
        if (skips.Any(skip => skip > 0))
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Detail_Skips, [.. skips.Cast<object>()]));
        }
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
    /// 규칙 필드 측정 이름.
    /// </summary>
    private static string Name(AppCacheRuleEntry rule, string field)
    {
        return AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, rule.Index, field);
    }

    /// <summary>
    /// 규칙 문자열 필드.
    /// </summary>
    private static string? Text(ScanSnapshot snapshot, AppCacheRuleEntry rule, string field)
    {
        return SnapshotValues.Text(snapshot, PROBE_ID, Name(rule, field));
    }

    /// <summary>
    /// 규칙 정수 필드(없으면 0).
    /// </summary>
    private static long Integer(ScanSnapshot snapshot, AppCacheRuleEntry rule, string field)
    {
        return SnapshotValues.Integer(snapshot, PROBE_ID, Name(rule, field)) ?? 0;
    }

    /// <summary>
    /// 규칙 불리언 필드.
    /// </summary>
    private static bool? Boolean(ScanSnapshot snapshot, AppCacheRuleEntry rule, string field)
    {
        return SnapshotValues.Boolean(snapshot, PROBE_ID, Name(rule, field));
    }
}
