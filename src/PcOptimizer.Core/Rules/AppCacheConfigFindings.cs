/**
 * @file    : AppCacheConfigFindings.cs
 * @author  : rudals252
 * @brief   : 앱 설정 리더 결과 중 순회하지 않은 설정 위치(UNC·보호·오프라인·해석 불가·읽기 실패)와 형식을 검증하지 못한 Adobe 설정을 확인 불가 Finding으로 만드는 앱 캐시 규칙 도우미
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 앱 설정 위치 확인 불가 Finding 도우미입니다. 적용·설정 없음 상태는 Finding을 만들지 않고 앱 카드 상세의 범위 문장으로만 알립니다.
/// </summary>
internal static class AppCacheConfigFindings
{
    private const string PROBE_ID = AppCacheProbeContract.PROBE_ID;

    /// <summary>
    /// 앱 설정 결과 하나를 판정합니다(해당 없으면 null).
    /// </summary>
    /// <param name="snapshot">스냅샷.</param>
    /// <param name="result">앱 캐시 프로브 결과.</param>
    /// <param name="index">설정 결과 인덱스.</param>
    /// <returns>확인 불가 Finding 또는 null.</returns>
    public static Finding? Evaluate(ScanSnapshot snapshot, ProbeResult result, int index)
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
            id: AppCacheRule.CONFIG_FINDING_ID_PREFIX + app,
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
}
