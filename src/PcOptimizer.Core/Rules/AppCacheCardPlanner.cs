/**
 * @file    : AppCacheCardPlanner.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 규칙별 측정값을 앱 단위 카드(관측한 파일이 있는 앱만)와 요약에 접어 넣을 개수(파일 없음·겹쳐 합침·보호·확인 불가 앱, 개인정보 관련 규칙 제외)로 나누는 순수 도우미
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
/// 카드로 보이지 않고 요약에 접은 개수입니다.
/// </summary>
/// <param name="NoFilesApps">파일이 관측되지 않은 앱 수(위치 없음·빈 폴더·규칙 제외).</param>
/// <param name="MergedApps">모든 위치가 다른 규칙과 겹쳐 그쪽에서 센 앱 수.</param>
/// <param name="ProtectedApps">보호 폴더(다른 사용자 위치 포함) 안이라 재지 않은 앱 수.</param>
/// <param name="UnverifiedApps">접근 거부·시간 초과 등으로 확인하지 못한 앱 수.</param>
/// <param name="SensitiveRules">개인정보 관련으로 제외한 규칙 수.</param>
internal sealed record AppCacheFoldedCounts(int NoFilesApps, int MergedApps, int ProtectedApps, int UnverifiedApps, int SensitiveRules);

/// <summary>
/// 앱 캐시 카드 계획 도우미입니다(카드는 앱 단위, 파일을 관측한 앱만).
/// </summary>
/// <remarks>
/// 원본 Warning 문구가 있거나 이름에 비밀번호·쿠키·세션·기록·자격 증명 낱말이 든 규칙은 캐시로 부를 수 없는 개인 데이터일 수 있어 카드에서 빼고 개수만 셉니다.
/// </remarks>
internal static class AppCacheCardPlanner
{
    /// <summary>개인정보 관련 규칙으로 보는 이름 낱말(대소문자 무시, 복수형 포함).</summary>
    public static readonly string[] SENSITIVE_NAME_WORDS = ["password", "cookie", "session", "history", "credential"];

    private const string PROBE_ID = AppCacheProbeContract.PROBE_ID;

    /// <summary>
    /// 규칙 측정값을 카드와 접은 개수로 나눕니다.
    /// </summary>
    /// <param name="snapshot">스냅샷.</param>
    /// <returns>카드(크기 내림차순, 같으면 앱 이름순)와 접은 개수.</returns>
    public static (IReadOnlyList<AppCacheCard> Cards, AppCacheFoldedCounts Folded) Plan(ScanSnapshot snapshot)
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
        int noFiles = 0, merged = 0, protectedApps = 0, unverified = 0;
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

            switch (FoldedReason(rules))
            {
                case AppCacheProbeContract.RULE_STATE_MERGED:
                    merged++;
                    break;
                case AppCacheProbeContract.RULE_STATE_PROTECTED:
                    protectedApps++;
                    break;
                case AppCacheProbeContract.RULE_STATE_ABSENT:
                    noFiles++;
                    break;
                default:
                    unverified++;
                    break;
            }
        }

        return (
            [.. cards.OrderByDescending(card => card.Bytes).ThenBy(card => card.App, StringComparer.OrdinalIgnoreCase)],
            new AppCacheFoldedCounts(noFiles, merged, protectedApps, unverified, sensitive));
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
    /// 파일을 관측하지 못한 앱의 대표 사유(확인 불가 > 보호 > 겹침 > 파일 없음).
    /// </summary>
    private static string FoldedReason(List<AppCacheRuleEntry> rules)
    {
        var states = rules.Select(entry => entry.State).ToHashSet(StringComparer.Ordinal);
        if (states.Overlaps([AppCacheProbeContract.RULE_STATE_ACCESS_DENIED, AppCacheProbeContract.RULE_STATE_TIMED_OUT, AppCacheProbeContract.RULE_STATE_NOT_OBSERVED, AppCacheProbeContract.RULE_STATE_PARTIAL]))
        {
            return AppCacheProbeContract.RULE_STATE_NOT_OBSERVED;
        }

        if (states.Contains(AppCacheProbeContract.RULE_STATE_PROTECTED))
        {
            return AppCacheProbeContract.RULE_STATE_PROTECTED;
        }

        return states.Contains(AppCacheProbeContract.RULE_STATE_MERGED) ? AppCacheProbeContract.RULE_STATE_MERGED : AppCacheProbeContract.RULE_STATE_ABSENT;
    }
}
