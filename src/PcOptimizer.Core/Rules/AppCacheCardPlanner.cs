/**
 * @file    : AppCacheCardPlanner.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 규칙별 측정값을 앱 단위 카드(관측한 파일이 있는 앱), 앱 단위 확인 불가(보호·접근 거부·시간 초과·검사하지 못함)와 요약에 접어 넣을 개수(파일 없음·겹쳐 합친 앱, 개인정보 관련 규칙 제외)로 나누는 순수 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 규칙 하나의 측정값 묶음입니다.
/// </summary>
/// <param name="Index">측정 인덱스.</param>
/// <param name="Id">규칙 ID.</param>
/// <param name="App">앱 이름(카드 묶음 기준).</param>
/// <param name="State">상태(<c>RULE_STATE_*</c>).</param>
/// <param name="Bytes">관측 논리 크기(관측·부분일 때만).</param>
/// <param name="FileCount">관측 파일 수.</param>
/// <param name="Partial">부분 관측 여부.</param>
/// <param name="Supplement">검토한 보충 규칙인지 여부.</param>
/// <param name="Reviewed">검토 영향이 있는지 여부.</param>
internal sealed record AppCacheRuleEntry(int Index, string Id, string App, string? State, long? Bytes, long FileCount, bool Partial, bool Supplement, bool Reviewed);

/// <summary>
/// 앱 하나의 카드입니다(관측한 파일이 있는 앱만).
/// </summary>
/// <param name="App">앱 이름.</param>
/// <param name="Rules">앱의 규칙(관측한 파일이 없는 규칙 포함, 상세 보기용).</param>
/// <param name="Bytes">관측 논리 크기 합.</param>
/// <param name="FileCount">관측 파일 수 합.</param>
/// <param name="Partial">부분 관측이 있는지 여부.</param>
/// <param name="Reviewed">파일을 센 규칙이 모두 검토한 보충 규칙인지 여부(캐시 문구·설정 열기 허용).</param>
internal sealed record AppCacheCard(string App, IReadOnlyList<AppCacheRuleEntry> Rules, long Bytes, long FileCount, bool Partial, bool Reviewed);

/// <summary>
/// 관측이 실패해 확인 불가 카드로 보이는 앱입니다(파일을 관측한 규칙이 없고 실패한 규칙이 있음).
/// </summary>
/// <param name="App">앱 이름.</param>
/// <param name="Rules">앱의 규칙(상세 보기용).</param>
/// <param name="State">대표 실패 상태(<c>RULE_STATE_*</c>, 접근 거부 > 시간 초과 > 검사하지 못함 > 보호).</param>
/// <param name="Reason">확인 불가 사유.</param>
internal sealed record AppCacheUnverifiedApp(string App, IReadOnlyList<AppCacheRuleEntry> Rules, string State, CannotVerifyReason Reason);

/// <summary>
/// 카드로 보이지 않고 요약에 접은 개수입니다(관측이 실패한 앱은 접지 않음).
/// </summary>
/// <param name="NoFilesApps">파일이 관측되지 않은 앱 수(위치 없음·0바이트·규칙 제외).</param>
/// <param name="MergedApps">모든 위치가 다른 규칙과 겹쳐 그쪽에서 센 앱 수.</param>
/// <param name="SensitiveRules">개인정보 관련으로 제외한 규칙 수.</param>
internal sealed record AppCacheFoldedCounts(int NoFilesApps, int MergedApps, int SensitiveRules);

/// <summary>
/// 앱 캐시 카드 계획입니다.
/// </summary>
/// <param name="Cards">관측한 파일이 있는 앱 카드(크기 내림차순, 같으면 앱 이름순).</param>
/// <param name="Unverified">관측이 실패한 앱(앱 이름순).</param>
/// <param name="Folded">요약에 접은 개수.</param>
internal sealed record AppCachePlan(IReadOnlyList<AppCacheCard> Cards, IReadOnlyList<AppCacheUnverifiedApp> Unverified, AppCacheFoldedCounts Folded);

/// <summary>
/// 앱 캐시 카드 계획 도우미입니다(카드는 앱 단위: 파일을 관측한 앱은 Info, 관측이 실패한 앱은 확인 불가, 파일 없음·겹침만 요약에 접음).
/// </summary>
/// <remarks>
/// 원본 Warning 문구가 있거나 이름에 비밀번호·쿠키·세션·기록·자격 증명 낱말이 든 규칙은 캐시로 부를 수 없는 개인 데이터일 수 있어 카드에서 빼고 개수만 셉니다.
/// </remarks>
internal static class AppCacheCardPlanner
{
    /// <summary>개인정보 관련 규칙으로 보는 이름 낱말(대소문자 무시, 복수형 포함).</summary>
    public static readonly string[] SENSITIVE_NAME_WORDS = ["password", "cookie", "session", "history", "credential"];

    /// <summary>관측 실패 상태의 대표 사유 우선순위(앞이 먼저).</summary>
    public static readonly string[] FAILURE_PRECEDENCE =
    [
        AppCacheProbeContract.RULE_STATE_ACCESS_DENIED,
        AppCacheProbeContract.RULE_STATE_TIMED_OUT,
        AppCacheProbeContract.RULE_STATE_NOT_OBSERVED,
        AppCacheProbeContract.RULE_STATE_PARTIAL,
        AppCacheProbeContract.RULE_STATE_PROTECTED,
    ];

    private const string PROBE_ID = AppCacheProbeContract.PROBE_ID;

    /// <summary>
    /// 규칙 측정값을 카드·확인 불가 앱·접은 개수로 나눕니다.
    /// </summary>
    /// <param name="snapshot">스냅샷.</param>
    /// <returns>계획.</returns>
    public static AppCachePlan Plan(ScanSnapshot snapshot)
    {
        var count = SnapshotValues.Integer(snapshot, PROBE_ID, AppCacheProbeContract.RULE_COUNT) ?? 0;
        var entries = new List<AppCacheRuleEntry>();
        var sensitive = 0;
        for (var index = 0; index < count; index++)
        {
            string Name(string field) => AppCacheProbeContract.Name(AppCacheProbeContract.RULE_PREFIX, index, field);
            var id = SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_ID)) ?? index.ToString(CultureInfo.InvariantCulture);
            var name = SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_NAME)) ?? id;
            if (SnapshotValues.Boolean(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_HAS_WARNING)) == true || IsSensitiveName(name))
            {
                sensitive++;
                continue;
            }

            entries.Add(new AppCacheRuleEntry(
                index,
                id,
                SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_APP)) ?? name,
                SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_STATE)),
                SnapshotValues.Integer(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_BYTES)),
                SnapshotValues.Integer(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_FILE_COUNT)) ?? 0,
                SnapshotValues.Boolean(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_PARTIAL)) ?? false,
                SnapshotValues.Text(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_ORIGIN)) == AppCacheProbeContract.ORIGIN_SUPPLEMENT,
                SnapshotValues.Boolean(snapshot, PROBE_ID, Name(AppCacheProbeContract.FIELD_REVIEWED)) ?? false));
        }

        var cards = new List<AppCacheCard>();
        var unverified = new List<AppCacheUnverifiedApp>();
        int noFiles = 0, merged = 0;
        foreach (var app in entries.GroupBy(entry => entry.App, StringComparer.OrdinalIgnoreCase))
        {
            var rules = app.ToList();
            var observed = rules.Where(entry => entry.Bytes is > 0).ToList();
            if (observed.Count > 0)
            {
                cards.Add(new AppCacheCard(
                    rules[0].App,
                    rules.AsReadOnly(),
                    observed.Sum(entry => entry.Bytes!.Value),
                    observed.Sum(entry => entry.FileCount),
                    rules.Any(entry => entry.Partial),
                    observed.All(entry => entry.Supplement && entry.Reviewed)));
                continue;
            }

            if (FailureState(rules) is { } failure)
            {
                unverified.Add(new AppCacheUnverifiedApp(rules[0].App, rules.AsReadOnly(), failure, ReasonFor(failure)));
            }
            else if (rules.Any(entry => entry.State == AppCacheProbeContract.RULE_STATE_MERGED))
            {
                merged++;
            }
            else
            {
                noFiles++;
            }
        }

        return new AppCachePlan(
            [.. cards.OrderByDescending(card => card.Bytes).ThenBy(card => card.App, StringComparer.OrdinalIgnoreCase)],
            [.. unverified.OrderBy(app => app.App, StringComparer.OrdinalIgnoreCase)],
            new AppCacheFoldedCounts(noFiles, merged, sensitive));
    }

    /// <summary>
    /// 규칙 상태가 관측 실패(보호·접근 거부·시간 초과·검사하지 못함, 파일 없는 부분 관측)인지 확인합니다.
    /// </summary>
    /// <param name="rule">규칙.</param>
    /// <returns>관측한 파일 없이 실패했으면 true.</returns>
    public static bool IsFailed(AppCacheRuleEntry rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Bytes is not > 0 && rule.State is { } state && FAILURE_PRECEDENCE.Contains(state, StringComparer.Ordinal);
    }

    /// <summary>
    /// 실패 상태의 확인 불가 사유(접근 거부 → AccessDenied, 시간 초과 → Timeout, 보호 → Unsupported, 그 밖 → PartialData).
    /// </summary>
    /// <param name="state">규칙 상태.</param>
    /// <returns>사유.</returns>
    public static CannotVerifyReason ReasonFor(string state)
    {
        return state switch
        {
            AppCacheProbeContract.RULE_STATE_ACCESS_DENIED => CannotVerifyReason.AccessDenied,
            AppCacheProbeContract.RULE_STATE_TIMED_OUT => CannotVerifyReason.Timeout,
            AppCacheProbeContract.RULE_STATE_PROTECTED => CannotVerifyReason.Unsupported,
            _ => CannotVerifyReason.PartialData,
        };
    }

    /// <summary>
    /// 이름에 개인정보 관련 낱말이 있는지 확인합니다.
    /// </summary>
    /// <param name="name">규칙 이름.</param>
    /// <returns>있으면 true.</returns>
    public static bool IsSensitiveName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return SENSITIVE_NAME_WORDS.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 파일을 관측하지 못한 앱의 대표 실패 상태(없으면 null: 파일 없음·겹침만 있음).
    /// </summary>
    private static string? FailureState(List<AppCacheRuleEntry> rules)
    {
        var failed = rules.Where(IsFailed).Select(entry => entry.State!).ToHashSet(StringComparer.Ordinal);
        return FAILURE_PRECEDENCE.FirstOrDefault(failed.Contains);
    }
}
