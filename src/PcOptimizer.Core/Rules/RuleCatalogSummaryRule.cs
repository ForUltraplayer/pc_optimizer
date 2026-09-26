/**
 * @file    : RuleCatalogSummaryRule.cs
 * @author  : rudals252
 * @brief   : 포함 규칙 스냅샷의 버전·커밋, 무결성 확인 상태, 지원/사유별 미지원·탐지 규칙 수, 앱 카드 수와 카드로 보이지 않는 앱(파일 없음·합침·보호·확인 불가)·개인정보 관련 제외 규칙 수를 요약하는 정보 규칙(무결성 실패·보호 정책 무효는 확인 불가)
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 규칙 목록 요약 규칙입니다(스펙 §5.1 "지원한 규칙/건너뛴 규칙 수와 원본 버전을 리포트에 기록한다").
/// </summary>
public sealed class RuleCatalogSummaryRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "appCache.catalog";

    /// <summary>Finding ID.</summary>
    public const string FINDING_ID = "appCache.catalog";

    private const string PROBE_ID = AppCacheProbeContract.PROBE_ID;
    private const string DETAIL_SEPARATOR = "\n";
    private const string LIST_SEPARATOR = ", ";
    private const int SHORT_COMMIT_LENGTH = 7;
    private const string UNKNOWN_VALUE = "?";

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(PROBE_ID, out var result))
        {
            return [];
        }

        var state = SnapshotValues.Text(snapshot, PROBE_ID, AppCacheProbeContract.CATALOG_STATE);
        var measured = result.Measurements.Where(m => m.Name.StartsWith(AppCacheProbeContract.MEASUREMENT_PREFIX + "catalog.", StringComparison.Ordinal)
            || m.Name.StartsWith(AppCacheProbeContract.MEASUREMENT_PREFIX + "detection.", StringComparison.Ordinal)).ToList();
        return state switch
        {
            null => [],
            AppCacheProbeContract.CATALOG_VERIFIED => [CreateSummary(snapshot, measured)],
            AppCacheProbeContract.CATALOG_POLICY_INVALID => [Create(
                CoreStrings.AppCache_Catalog_Title_PolicyInvalid, measured, CoreStrings.AppCache_Catalog_Evidence_PolicyInvalid, Verdict.CannotVerify, CannotVerifyReason.ProbeError, null)],
            _ => [Create(
                CoreStrings.AppCache_Catalog_Title_Failed,
                measured,
                CoreStrings.AppCache_Catalog_Evidence_Failed,
                Verdict.CannotVerify,
                CannotVerifyReason.ProbeError,
                SnapshotValues.Format(
                    CoreStrings.AppCache_Catalog_Detail_Failed,
                    SnapshotValues.Text(snapshot, PROBE_ID, AppCacheProbeContract.CATALOG_FAILED_FILE) ?? UNKNOWN_VALUE,
                    state))],
        };
    }

    /// <summary>
    /// 무결성을 확인한 요약 정보를 만든다.
    /// </summary>
    private static Finding CreateSummary(ScanSnapshot snapshot, List<Measurement> measured)
    {
        long Count(string name) => SnapshotValues.Integer(snapshot, PROBE_ID, name) ?? 0;

        var communityTotal = Count(AppCacheProbeContract.COMMUNITY_TOTAL);
        var communitySupported = Count(AppCacheProbeContract.COMMUNITY_SUPPORTED);
        var supplementTotal = Count(AppCacheProbeContract.SUPPLEMENT_TOTAL);
        var supplementSupported = Count(AppCacheProbeContract.SUPPLEMENT_SUPPORTED);
        var supported = communitySupported + supplementSupported;
        var unsupported = (communityTotal - communitySupported) + (supplementTotal - supplementSupported);
        var commit = SnapshotValues.Text(snapshot, PROBE_ID, AppCacheProbeContract.WINAPP2_COMMIT) ?? UNKNOWN_VALUE;
        var shortCommit = commit.Length > SHORT_COMMIT_LENGTH ? commit[..SHORT_COMMIT_LENGTH] : commit;

        var details = new List<string>();
        if (SnapshotValues.TextList(snapshot, PROBE_ID, AppCacheProbeContract.UNSUPPORTED_BY_REASON) is { Count: > 0 } reasons)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Catalog_Detail_Unsupported, DescribeReasons(reasons)));
        }

        if (SnapshotValues.TextList(snapshot, PROBE_ID, AppCacheProbeContract.RUNTIME_UNSUPPORTED_BY_REASON) is { Count: > 0 } runtime)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Catalog_Detail_Runtime, DescribeReasons(runtime)));
        }

        if (Count(AppCacheProbeContract.DETECTION_UNKNOWN_COUNT) is > 0 and var unknown)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Catalog_Detail_Unknown, unknown));
        }

        var (cards, folded) = AppCacheCardPlanner.Plan(snapshot);
        details.Add(SnapshotValues.Format(CoreStrings.AppCache_Catalog_Detail_Cards, cards.Count));
        details.Add(SnapshotValues.Format(CoreStrings.AppCache_Catalog_Detail_Folded, folded.NoFilesApps, folded.MergedApps, folded.ProtectedApps, folded.UnverifiedApps));
        if (folded.SensitiveRules > 0)
        {
            details.Add(SnapshotValues.Format(CoreStrings.AppCache_Catalog_Detail_Sensitive, folded.SensitiveRules));
        }

        if (SnapshotValues.Boolean(snapshot, PROBE_ID, AppCacheProbeContract.ELEVATED_DEFAULTS_ONLY) == true)
        {
            details.Add(CoreStrings.AppCache_Catalog_Detail_Elevated);
        }

        details.Add(CoreStrings.AppCache_Catalog_Detail_Effects);
        details.Add(CoreStrings.AppCache_Catalog_Detail_License);

        return Create(
            SnapshotValues.Format(CoreStrings.AppCache_Catalog_Title, supported, unsupported, Count(AppCacheProbeContract.DETECTED_COUNT)),
            measured,
            SnapshotValues.Format(
                CoreStrings.AppCache_Catalog_Evidence,
                SnapshotValues.Text(snapshot, PROBE_ID, AppCacheProbeContract.WINAPP2_VERSION) ?? UNKNOWN_VALUE,
                shortCommit,
                supplementTotal),
            Verdict.Info,
            null,
            string.Join(DETAIL_SEPARATOR, details));
    }

    /// <summary>
    /// "사유=개수" 목록을 한국어 사유 이름과 개수로 바꾼다.
    /// </summary>
    private static string DescribeReasons(IReadOnlyList<string> entries)
    {
        return string.Join(LIST_SEPARATOR, entries.Select(entry =>
        {
            var separator = entry.IndexOf(AppCacheProbeContract.COUNT_SEPARATOR);
            var code = separator < 0 ? entry : entry[..separator];
            var count = separator < 0 ? string.Empty : entry[(separator + 1)..];
            return string.Format(CultureInfo.CurrentCulture, CoreStrings.AppCache_Reason_CountFormat, ReasonLabel(code), count);
        }));
    }

    /// <summary>
    /// 미지원 사유 코드의 화면용 이름.
    /// </summary>
    private static string ReasonLabel(string code)
    {
        if (!Enum.TryParse<UnsupportedRuleReason>(code, ignoreCase: false, out var reason))
        {
            return CoreStrings.AppCache_Reason_Unknown;
        }

        return reason switch
        {
            UnsupportedRuleReason.UnknownKey => CoreStrings.AppCache_Reason_UnknownKey,
            UnsupportedRuleReason.DetectOs => CoreStrings.AppCache_Reason_DetectOs,
            UnsupportedRuleReason.SpecialDetect => CoreStrings.AppCache_Reason_SpecialDetect,
            UnsupportedRuleReason.UnsupportedExclude => CoreStrings.AppCache_Reason_UnsupportedExclude,
            UnsupportedRuleReason.UnresolvedVariable => CoreStrings.AppCache_Reason_UnresolvedVariable,
            UnsupportedRuleReason.VolumeRootPath => CoreStrings.AppCache_Reason_VolumeRootPath,
            UnsupportedRuleReason.UncOrDevicePath => CoreStrings.AppCache_Reason_UncOrDevicePath,
            UnsupportedRuleReason.MalformedEntry => CoreStrings.AppCache_Reason_MalformedEntry,
            UnsupportedRuleReason.NoSafeDetection => CoreStrings.AppCache_Reason_NoSafeDetection,
            UnsupportedRuleReason.RegistryOnly => CoreStrings.AppCache_Reason_RegistryOnly,
            UnsupportedRuleReason.UnsupportedDetectRoot => CoreStrings.AppCache_Reason_UnsupportedDetectRoot,
            UnsupportedRuleReason.UnsupportedDirective => CoreStrings.AppCache_Reason_UnsupportedDirective,
            UnsupportedRuleReason.WildcardBoundExceeded => CoreStrings.AppCache_Reason_WildcardBoundExceeded,
            _ => CoreStrings.AppCache_Reason_Unknown,
        };
    }

    /// <summary>
    /// 요약 Finding을 만든다.
    /// </summary>
    private static Finding Create(string title, IReadOnlyList<Measurement> measured, string evidence, Verdict verdict, CannotVerifyReason? reason, string? detail)
    {
        return new Finding(
            id: FINDING_ID,
            category: FindingCategory.AppCache,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: reason,
            detail: detail,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
