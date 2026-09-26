/**
 * @file    : SquirrelVersionFoldersRule.cs
 * @author  : rudals252
 * @brief   : Squirrel 설치 앱(Discord·Slack·GitHub Desktop)의 app-* 버전 폴더 이름만 나열하는 정보 규칙(크기 합산·사용 여부 판정 없음)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// Squirrel 버전 폴더 규칙입니다(스펙 §5 보충 문단 "Squirrel의 app-*는 버전 폴더 존재만 Info로 표시하며, 이름·날짜만으로 '미사용 구버전'이나 삭제 가능으로 판정하지 않는다").
/// </summary>
public sealed class SquirrelVersionFoldersRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "appCache.squirrel";

    /// <summary>Finding ID 접두사(뒤에 앱 폴더 이름).</summary>
    public const string FINDING_ID_PREFIX = "appCache.squirrel:";

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

        var findings = new List<Finding>();
        var count = SnapshotValues.Integer(snapshot, PROBE_ID, AppCacheProbeContract.SQUIRREL_COUNT) ?? 0;
        for (var index = 0; index < count; index++)
        {
            var app = SnapshotValues.Text(snapshot, PROBE_ID, AppCacheProbeContract.Name(AppCacheProbeContract.SQUIRREL_PREFIX, index, AppCacheProbeContract.FIELD_APP));
            var folders = SnapshotValues.TextList(snapshot, PROBE_ID, AppCacheProbeContract.Name(AppCacheProbeContract.SQUIRREL_PREFIX, index, AppCacheProbeContract.FIELD_VERSION_FOLDERS));
            if (app is null || folders is null)
            {
                continue;
            }

            findings.Add(new Finding(
                id: FINDING_ID_PREFIX + app,
                category: FindingCategory.AppCache,
                title: SnapshotValues.Format(CoreStrings.AppCache_Squirrel_Title, app, folders.Count),
                measured: SnapshotValues.WithPrefix(result, AppCacheProbeContract.ItemPrefix(AppCacheProbeContract.SQUIRREL_PREFIX, index)),
                evidence: CoreStrings.AppCache_Squirrel_Evidence,
                verdict: Verdict.Info,
                cannotVerifyReason: null,
                detail: SnapshotValues.Format(CoreStrings.AppCache_Squirrel_Detail, string.Join(LIST_SEPARATOR, folders)),
                recommendation: null,
                impact: null,
                actions: [new ShowDetailsAction()]));
        }

        return findings;
    }
}
