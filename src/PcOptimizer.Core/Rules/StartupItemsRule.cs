/**
 * @file    : StartupItemsRule.cs
 * @author  : rudals252
 * @brief   : 시작 항목별 활성/비활성/알 수 없음 정보, 부팅 영향 미지원(Unsupported), 항목 수와 수집 범위(예약 작업·서비스·패키지 앱 제외) 요약을 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 시작 프로그램 규칙입니다(스펙 §5 시작 프로그램 행).
/// <list type="bullet">
/// <item>항목마다 Info 하나: 이름·위치·활성/비활성/알 수 없음. 개수나 활성 여부로 후보(Candidate)를 만들지 않습니다.</item>
/// <item>부팅 영향: CannotVerify(Unsupported) 하나. 작업 관리자 비공개 지표를 읽거나 개수로 추정하지 않습니다.</item>
/// <item>요약 Info 하나: 항목 수와 수집 범위(예약 작업·서비스·패키지 앱 자동 실행 미포함). 읽지 못한 위치는 상세에 이름만 적습니다.</item>
/// </list>
/// 제목·근거·상세에는 전체 경로를 넣지 않습니다(명령 문자열은 측정값에만 있고 내보내기 익명화가 처리).
/// </summary>
public sealed class StartupItemsRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "startup.items";

    /// <summary>항목별 Finding ID 접두사(뒤에 "출처 코드:이름").</summary>
    public const string FINDING_ID_PREFIX = "startup-item:";

    /// <summary>요약 Finding ID.</summary>
    public const string SUMMARY_FINDING_ID = "startup:summary";

    /// <summary>부팅 영향 미지원 Finding ID.</summary>
    public const string BOOT_IMPACT_FINDING_ID = "startup:boot-impact";

    /// <summary>시작 앱 설정 URI.</summary>
    public const string STARTUP_SETTINGS_URI = "ms-settings:startupapps";

    private const string ID_SEPARATOR = ":";
    private const string LIST_SEPARATOR = ", ";
    private const string FIRST_BYTE_FORMAT = "0x{0:X2}";
    private static readonly char[] PATH_SEPARATORS = ['\\', '/'];

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(StartupItemsProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var count = SnapshotValues.Integer(snapshot, StartupItemsProbeContract.PROBE_ID, StartupItemsProbeContract.ITEM_COUNT) ?? 0;
        var findings = new List<Finding>
        {
            CreateSummary(snapshot, result, count),
            CreateBootImpact(),
        };

        for (var index = 0; index < count; index++)
        {
            findings.Add(EvaluateItem(snapshot, result, index));
        }

        return findings;
    }

    /// <summary>
    /// 항목 수와 수집 범위를 알리는 요약 Info를 만든다. 읽지 못한 위치가 있으면 상세에 위치 이름만 적는다.
    /// </summary>
    private static Finding CreateSummary(ScanSnapshot snapshot, ProbeResult result, long count)
    {
        var unreadable = SnapshotValues.TextList(snapshot, StartupItemsProbeContract.PROBE_ID, StartupItemsProbeContract.UNREADABLE_SOURCES) ?? [];
        var detail = unreadable.Count == 0
            ? null
            : SnapshotValues.Format(CoreStrings.Startup_Detail_Unreadable, string.Join(LIST_SEPARATOR, unreadable.Select(SourceLabel)));

        Measurement[] measured = [.. result.Measurements.Where(m =>
            m.Name == StartupItemsProbeContract.ITEM_COUNT || m.Name == StartupItemsProbeContract.UNREADABLE_SOURCES)];

        return new Finding(
            id: SUMMARY_FINDING_ID,
            category: FindingCategory.Startup,
            title: SnapshotValues.Format(CoreStrings.Startup_Title_Summary, count.ToString(CultureInfo.CurrentCulture)),
            measured: measured,
            evidence: snapshot.GetMeasurement(StartupItemsProbeContract.PROBE_ID, "systemOnly")?.Value is BooleanValue { Value: true }
                ? "모든 사용자용 레지스트리와 공용 시작프로그램 폴더만 확인했습니다. 현재 사용자 등록·개인 폴더는 포함하지 않습니다."
                : CoreStrings.Startup_Evidence_Coverage,
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: detail,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction(), new OpenSettingsAction(STARTUP_SETTINGS_URI)]);
    }

    /// <summary>
    /// 부팅 영향을 1차에서 확인하지 않는다는 CannotVerify(Unsupported)를 만든다.
    /// </summary>
    private static Finding CreateBootImpact()
    {
        return new Finding(
            id: BOOT_IMPACT_FINDING_ID,
            category: FindingCategory.Startup,
            title: CoreStrings.Startup_Title_BootImpact,
            measured: [],
            evidence: CoreStrings.Startup_Evidence_BootImpact,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: CannotVerifyReason.Unsupported,
            detail: null,
            recommendation: null,
            impact: null,
            actions: []);
    }

    /// <summary>
    /// 항목 하나를 정보로 만든다(활성 상태는 StartupApproved 원시 값의 정확한 해석만 사용).
    /// </summary>
    private static Finding EvaluateItem(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string? Text(string field) => SnapshotValues.Text(snapshot, StartupItemsProbeContract.PROBE_ID, StartupItemsProbeContract.ItemMeasurementName(index, field));

        // 값 이름은 공백일 수 있으므로(기본값) 원문을 그대로 읽는다.
        var rawName = snapshot.GetMeasurement(StartupItemsProbeContract.PROBE_ID, StartupItemsProbeContract.ItemMeasurementName(index, StartupItemsProbeContract.FIELD_NAME))?.Value is TextValue name
            ? name.Value
            : string.Empty;
        var source = Text(StartupItemsProbeContract.FIELD_SOURCE) ?? string.Empty;
        var lookup = Text(StartupItemsProbeContract.FIELD_APPROVED_LOOKUP);
        var kind = Text(StartupItemsProbeContract.FIELD_APPROVED_KIND);
        var firstByte = SnapshotValues.Integer(
            snapshot, StartupItemsProbeContract.PROBE_ID, StartupItemsProbeContract.ItemMeasurementName(index, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        var state = StartupApprovedClassifier.Classify(lookup, kind, firstByte);

        var stateLabel = state switch
        {
            StartupApprovedState.Enabled => CoreStrings.Startup_State_Enabled,
            StartupApprovedState.Disabled => CoreStrings.Startup_State_Disabled,
            _ => CoreStrings.Startup_State_Unknown,
        };

        return new Finding(
            id: FINDING_ID_PREFIX + source + ID_SEPARATOR + rawName,
            category: FindingCategory.Startup,
            title: SnapshotValues.Format(CoreStrings.Startup_Title_Item, DisplayName(rawName), stateLabel),
            measured: SnapshotValues.WithPrefix(result, StartupItemsProbeContract.ItemMeasurementPrefix(index)),
            evidence: SnapshotValues.Format(CoreStrings.Startup_Evidence_Item, SourceLabel(source), StateEvidence(state, lookup, kind, firstByte)),
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction(), new OpenSettingsAction(STARTUP_SETTINGS_URI)]);
    }

    /// <summary>
    /// 활성 상태 근거 문장을 고른다.
    /// </summary>
    private static string StateEvidence(StartupApprovedState state, string? lookup, string? kind, long? firstByte)
    {
        if (state == StartupApprovedState.Enabled)
        {
            return CoreStrings.Startup_Evidence_Enabled;
        }

        if (state == StartupApprovedState.Disabled)
        {
            return CoreStrings.Startup_Evidence_Disabled;
        }

        return lookup switch
        {
            StartupItemsProbeContract.LOOKUP_NOT_TRACKED => CoreStrings.Startup_Evidence_NotTracked,
            StartupItemsProbeContract.LOOKUP_UNREADABLE => CoreStrings.Startup_Evidence_Unreadable,
            StartupItemsProbeContract.LOOKUP_FOUND => SnapshotValues.Format(
                CoreStrings.Startup_Evidence_UnknownValue,
                kind ?? CoreStrings.Startup_Value_None,
                firstByte is { } value ? string.Format(CultureInfo.InvariantCulture, FIRST_BYTE_FORMAT, value) : CoreStrings.Startup_Value_None),
            _ => CoreStrings.Startup_Evidence_Missing,
        };
    }

    /// <summary>
    /// 화면용 항목 이름. 빈 이름은 '(기본값)', 경로 형태의 이름은 마지막 구성 요소(파일 이름)만 보여 전체 경로를 노출하지 않는다.
    /// </summary>
    private static string DisplayName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return CoreStrings.Startup_Name_Default;
        }

        var trimmed = rawName.Trim().Trim('"').TrimEnd(PATH_SEPARATORS);
        var lastSeparator = trimmed.LastIndexOfAny(PATH_SEPARATORS);
        var leaf = lastSeparator >= 0 ? trimmed[(lastSeparator + 1)..] : trimmed;
        return string.IsNullOrWhiteSpace(leaf) ? CoreStrings.Startup_Name_Default : leaf;
    }

    /// <summary>
    /// 출처 코드의 화면용 위치 이름(경로 아님).
    /// </summary>
    private static string SourceLabel(string source)
    {
        return source switch
        {
            StartupItemsProbeContract.SOURCE_HKCU_RUN => CoreStrings.Startup_Source_HkcuRun,
            StartupItemsProbeContract.SOURCE_HKCU_RUN_ONCE => CoreStrings.Startup_Source_HkcuRunOnce,
            StartupItemsProbeContract.SOURCE_HKLM64_RUN => CoreStrings.Startup_Source_Hklm64Run,
            StartupItemsProbeContract.SOURCE_HKLM64_RUN_ONCE => CoreStrings.Startup_Source_Hklm64RunOnce,
            StartupItemsProbeContract.SOURCE_HKLM32_RUN => CoreStrings.Startup_Source_Hklm32Run,
            StartupItemsProbeContract.SOURCE_HKLM32_RUN_ONCE => CoreStrings.Startup_Source_Hklm32RunOnce,
            StartupItemsProbeContract.SOURCE_USER_FOLDER => CoreStrings.Startup_Source_UserFolder,
            StartupItemsProbeContract.SOURCE_COMMON_FOLDER => CoreStrings.Startup_Source_CommonFolder,
            _ => CoreStrings.Startup_Source_Unknown,
        };
    }
}
