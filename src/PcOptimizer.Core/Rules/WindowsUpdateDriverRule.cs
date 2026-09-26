/**
 * @file    : WindowsUpdateDriverRule.cs
 * @author  : rudals252
 * @brief   : Windows Update 드라이버 검색 결과를 후보 n개(Windows 업데이트 설정 열기·제목 목록), 성공 0건 '해당 서비스에서 후보 없음', 일부 오류 성공 안내, 재부팅 필요 정보로 바꾸는 순수 판정 규칙
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// Windows Update 드라이버 규칙입니다(스펙 §5 Windows 업데이트 드라이버 행).
/// 구성된 업데이트 서비스(WSUS·정책 포함)의 검색 결과일 뿐이며, 모든 제조사의 최신 드라이버를 포괄한다고 주장하지 않습니다.
/// 프로브가 건너뜀·실패·취소면 아무것도 만들지 않습니다(엔진이 사유 카드를 만듦).
/// </summary>
public sealed class WindowsUpdateDriverRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "driver.windowsUpdate";

    /// <summary>후보 Finding ID.</summary>
    public const string CANDIDATES_FINDING_ID = "wu-drivers:candidates";

    /// <summary>후보 없음 Finding ID.</summary>
    public const string NONE_FINDING_ID = "wu-drivers:none";

    /// <summary>일부 오류 성공 Finding ID.</summary>
    public const string PARTIAL_FINDING_ID = "wu-drivers:partial";

    /// <summary>재부팅 필요 Finding ID.</summary>
    public const string REBOOT_FINDING_ID = "wu-drivers:reboot";

    /// <summary>Windows 업데이트 설정 URI(App 허용 목록과 같아야 함).</summary>
    public const string WINDOWS_UPDATE_SETTINGS_URI = "ms-settings:windowsupdate-optionalupdates";

    private const string LINE_SEPARATOR = "\n";

    /// <summary>Candidate 안전 수준(설치는 Windows 설정에서 하며 재부팅이 필요할 수 있음).</summary>
    private static readonly SafetyLevel CANDIDATE_SAFETY = SafetyLevel.Caution;

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(WindowsUpdateProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var resultCode = SnapshotValues.Integer(snapshot, WindowsUpdateProbeContract.PROBE_ID, WindowsUpdateProbeContract.RESULT_CODE);
        var count = (int)(SnapshotValues.Integer(snapshot, WindowsUpdateProbeContract.PROBE_ID, WindowsUpdateProbeContract.UPDATE_COUNT) ?? 0);
        Measurement[] measured = [.. result.Measurements];
        var findings = new List<Finding>();

        if (count > 0)
        {
            findings.Add(CreateCandidates(snapshot, measured, count));
        }
        else if (resultCode == WindowsUpdateProbeContract.RESULT_SUCCEEDED)
        {
            findings.Add(Info(NONE_FINDING_ID, CoreStrings.Wu_Title_None, CoreStrings.Wu_Evidence_None, measured));
        }

        if (resultCode == WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS)
        {
            findings.Add(Info(PARTIAL_FINDING_ID, CoreStrings.Wu_Title_Partial, SnapshotValues.Format(CoreStrings.Wu_Evidence_Partial, resultCode), measured));
        }

        if (SnapshotValues.Boolean(snapshot, WindowsUpdateProbeContract.PROBE_ID, WindowsUpdateProbeContract.REBOOT_REQUIRED) == true)
        {
            findings.Add(Info(REBOOT_FINDING_ID, CoreStrings.Wu_Title_Reboot, CoreStrings.Wu_Evidence_Reboot, []));
        }

        return findings;
    }

    /// <summary>
    /// 후보 n개 Finding을 만든다(제목 목록은 상세에, 설정 열기 동작).
    /// </summary>
    private static Finding CreateCandidates(ScanSnapshot snapshot, IReadOnlyList<Measurement> measured, int count)
    {
        var lines = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var title = SnapshotValues.Text(
                snapshot, WindowsUpdateProbeContract.PROBE_ID, WindowsUpdateProbeContract.UpdateMeasurementName(index, WindowsUpdateProbeContract.FIELD_TITLE));
            lines.Add(SnapshotValues.Format(CoreStrings.Wu_Detail_ItemFormat, title ?? CoreStrings.Wu_UntitledUpdate));
        }

        return new Finding(
            id: CANDIDATES_FINDING_ID,
            category: FindingCategory.Driver,
            title: SnapshotValues.Format(CoreStrings.Wu_Title_Candidates, count),
            measured: measured,
            evidence: SnapshotValues.Format(CoreStrings.Wu_Evidence_Candidates, count),
            verdict: Verdict.Candidate,
            cannotVerifyReason: null,
            detail: SnapshotValues.Format(CoreStrings.Wu_Detail_Candidates, string.Join(LINE_SEPARATOR, lines)),
            recommendation: new Recommendation(CoreStrings.Wu_Recommendation_Text, CoreStrings.Wu_Recommendation_Condition),
            impact: new Impact(CoreStrings.Wu_Impact_Benefit, CoreStrings.Wu_Impact_SideEffect),
            actions: [new OpenSettingsAction(WINDOWS_UPDATE_SETTINGS_URI), new ShowDetailsAction()],
            explanation: new Explanation(CoreStrings.WuDriver_Explain_What, CoreStrings.WuDriver_Explain_Effect, CoreStrings.WuDriver_Explain_Caution),
            safety: CANDIDATE_SAFETY);
    }

    /// <summary>
    /// 정보 Finding을 만든다.
    /// </summary>
    private static Finding Info(string id, string title, string evidence, IReadOnlyList<Measurement> measured)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Driver,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
