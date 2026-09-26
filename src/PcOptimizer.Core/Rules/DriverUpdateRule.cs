/**
 * @file    : DriverUpdateRule.cs
 * @author  : rudals252
 * @brief   : NVIDIA 온라인 조회 결과로 어댑터별 같은 계열 최신 비교(높음 → 조건부 후보, 같음·더 높음 → 정보)와 계열·제품 매핑 모호·조회 실패·설치 버전 불명의 확인 불가를 만드는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// NVIDIA 드라이버 업데이트 규칙입니다(스펙 §5 GPU 드라이버 행 중 최신 비교 부분).
/// <list type="bullet">
/// <item>설치 버전이 한 계열 목록에만 있을 때만 그 계열의 최신(베타 제외)과 수치로 비교합니다.</item>
/// <item>최신이 높으면 Candidate '그래픽 드라이버 업데이트 후보가 있어요'(조건·영향·공식 배포 설명/다운로드 링크). 같거나 설치가 높으면 Info.</item>
/// <item>계열이 모호하거나(양쪽·어느 쪽도 아님·Studio 목록 없음) 제품 매핑이 정확히 하나가 아니면 CannotVerify(Ambiguous)와 이유·공식 페이지 링크.</item>
/// <item>'이 기기 권장'·'설치 호환성 검증 완료'로 표현하지 않고, 낮은 버전을 고장으로 표현하지 않습니다.</item>
/// </list>
/// 프로브가 건너뜀·실패·취소면 아무것도 만들지 않습니다(엔진이 사유 카드를 만들고 설치 드라이버 카드는 <see cref="InstalledDriverRule"/>이 유지).
/// </summary>
public sealed class DriverUpdateRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "driver.update";

    /// <summary>어댑터별 Finding ID 접두사(뒤에 PnP 장치 ID가 붙음).</summary>
    public const string FINDING_ID_PREFIX = "driver-update:";

    /// <summary>NVIDIA 공식 드라이버 페이지를 찾는 공급업체 키.</summary>
    public const string NVIDIA_VENDOR_KEY = "nvidia";

    private const string INDEX_ID_FORMAT = "index-{0}";
    private const int DISPLAY_INDEX_OFFSET = 1;

    private readonly VendorLinkCatalog? _catalog;

    /// <summary>
    /// 규칙을 만듭니다.
    /// </summary>
    /// <param name="catalog">공식 링크 표(읽지 못했으면 null, 이때 NVIDIA 공식 페이지 링크는 붙이지 않음).</param>
    public DriverUpdateRule(VendorLinkCatalog? catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(NvidiaLookupProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var count = SnapshotValues.Integer(snapshot, NvidiaLookupProbeContract.PROBE_ID, NvidiaLookupProbeContract.ADAPTER_COUNT) ?? 0;
        var findings = new List<Finding>();
        for (var index = 0; index < count; index++)
        {
            findings.Add(EvaluateAdapter(NvidiaAdapterView.Read(snapshot, result, index)));
        }

        return findings;
    }

    /// <summary>
    /// 어댑터 하나를 판정한다.
    /// </summary>
    private Finding EvaluateAdapter(NvidiaAdapterView adapter)
    {
        var name = adapter.Name ?? SnapshotValues.Format(CoreStrings.DriverUpdate_AdapterFallbackName, adapter.Index + DISPLAY_INDEX_OFFSET);
        var id = FINDING_ID_PREFIX + (adapter.PnpDeviceId ?? string.Format(CultureInfo.InvariantCulture, INDEX_ID_FORMAT, adapter.Index));

        switch (adapter.State)
        {
            case NvidiaLookupProbeContract.STATE_LISTED:
                return EvaluateListed(adapter, id, name);
            case NvidiaLookupProbeContract.STATE_PRODUCT_AMBIGUOUS:
                return CannotVerify(
                    id,
                    SnapshotValues.Format(CoreStrings.DriverUpdate_Title_ProductAmbiguous, name),
                    adapter.Measured,
                    SnapshotValues.Format(CoreStrings.DriverUpdate_Evidence_ProductAmbiguous, adapter.ProductMatchCount ?? 0),
                    CannotVerifyReason.Ambiguous,
                    CoreStrings.DriverUpdate_Detail_ProductAmbiguous,
                    NvidiaDriversPageLinks());
            case NvidiaLookupProbeContract.STATE_VERSION_UNKNOWN:
                return CannotVerify(
                    id,
                    SnapshotValues.Format(CoreStrings.DriverUpdate_Title_VersionUnknown, name),
                    adapter.Measured,
                    CoreStrings.DriverUpdate_Evidence_VersionUnknown,
                    CannotVerifyReason.PartialData,
                    detail: null,
                    links: []);
            default:
                var reason = Enum.TryParse<CannotVerifyReason>(adapter.FailureReason, ignoreCase: false, out var parsed)
                    && Enum.IsDefined(parsed)
                    ? parsed
                    : CannotVerifyReason.ProbeError;
                return CannotVerify(
                    id,
                    SnapshotValues.Format(CoreStrings.DriverUpdate_Title_LookupFailed, name),
                    adapter.Measured,
                    CannotVerifyTexts.EvidenceFor(reason),
                    reason,
                    detail: null,
                    links: []);
        }
    }

    /// <summary>
    /// 계열 목록을 받은 어댑터를 판정한다: 계열 확정 시 같은 계열 최신과 비교, 모호하면 이유와 두 계열 공식 배포 설명 링크.
    /// </summary>
    private static Finding EvaluateListed(NvidiaAdapterView adapter, string id, string name)
    {
        var installed = adapter.InstalledVersion ?? CoreStrings.DriverUpdate_Value_Unknown;
        var installedDate = adapter.InstalledDate ?? CoreStrings.DriverUpdate_Value_Unknown;
        var resolution = NvidiaBranchResolver.Resolve(adapter.InstalledVersion, adapter.GameReady.Versions ?? [], adapter.StudioVersionsIfAvailable);

        if (resolution.Branch == NvidiaDriverBranch.Ambiguous)
        {
            return AmbiguousBranch(adapter, id, name, installed, installedDate, resolution.Ambiguity);
        }

        var branch = resolution.Branch == NvidiaDriverBranch.GameReady ? adapter.GameReady : adapter.Studio;
        var branchName = resolution.Branch == NvidiaDriverBranch.GameReady ? CoreStrings.DriverUpdate_Branch_GameReady : CoreStrings.DriverUpdate_Branch_Studio;
        if (branch.LatestVersion is null
            || !DriverVersionComparer.TryCompare(branch.LatestVersion, adapter.InstalledVersion, out var comparison))
        {
            return CannotVerify(
                id,
                SnapshotValues.Format(CoreStrings.DriverUpdate_Title_LatestMissing, name),
                adapter.Measured,
                SnapshotValues.Format(CoreStrings.DriverUpdate_Evidence_LatestMissing, branchName),
                CannotVerifyReason.PartialData,
                detail: null,
                links: []);
        }

        var evidence = SnapshotValues.Format(
            CoreStrings.DriverUpdate_Evidence_Compare,
            installed,
            installedDate,
            branchName,
            branch.LatestVersion,
            branch.LatestReleaseDate ?? CoreStrings.DriverUpdate_Value_Unknown);

        if (comparison > 0)
        {
            return Candidate(adapter, id, name, evidence, branch);
        }

        var title = comparison == 0 ? CoreStrings.DriverUpdate_Title_UpToDate : CoreStrings.DriverUpdate_Title_Newer;
        return new Finding(
            id: id,
            category: FindingCategory.Driver,
            title: SnapshotValues.Format(title, name),
            measured: adapter.Measured,
            evidence: evidence,
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }

    /// <summary>
    /// 같은 계열 최신이 높을 때의 조건부 후보를 만든다(공식 배포 설명·다운로드 링크, 앱은 내려받지 않음).
    /// </summary>
    private static Finding Candidate(NvidiaAdapterView adapter, string id, string name, string evidence, NvidiaBranchView branch)
    {
        var actions = new List<FindingAction>();
        if (branch.LatestDetailsUrl is { } details)
        {
            actions.Add(new OpenLinkAction(details, CoreStrings.Link_OfficialReleaseNotes));
        }

        if (branch.LatestDownloadUrl is { } download)
        {
            actions.Add(new OpenLinkAction(download, CoreStrings.Link_OfficialDownload));
        }

        actions.Add(new ShowDetailsAction());
        return new Finding(
            id: id,
            category: FindingCategory.Driver,
            title: SnapshotValues.Format(CoreStrings.DriverUpdate_Title_Candidate, name),
            measured: adapter.Measured,
            evidence: evidence,
            verdict: Verdict.Candidate,
            cannotVerifyReason: null,
            detail: CoreStrings.DriverUpdate_Detail_Candidate,
            recommendation: new Recommendation(CoreStrings.DriverUpdate_Recommendation_Text, CoreStrings.DriverUpdate_Recommendation_Condition),
            impact: new Impact(CoreStrings.DriverUpdate_Impact_Benefit, CoreStrings.DriverUpdate_Impact_SideEffect),
            actions: actions);
    }

    /// <summary>
    /// 계열 모호 확인 불가를 만든다(이유와 두 계열의 공식 배포 설명 링크).
    /// </summary>
    private static Finding AmbiguousBranch(
        NvidiaAdapterView adapter, string id, string name, string installed, string installedDate, NvidiaBranchAmbiguity ambiguity)
    {
        var detailTemplate = ambiguity switch
        {
            NvidiaBranchAmbiguity.InBoth => CoreStrings.DriverUpdate_Detail_InBoth,
            NvidiaBranchAmbiguity.StudioUnavailable => CoreStrings.DriverUpdate_Detail_StudioUnavailable,
            _ => CoreStrings.DriverUpdate_Detail_InNeither,
        };

        var links = new List<FindingAction>();
        if (adapter.GameReady.LatestDetailsUrl is { } gameReady)
        {
            links.Add(new OpenLinkAction(gameReady, CoreStrings.Link_GameReadyReleaseNotes));
        }

        if (adapter.StudioVersionsIfAvailable is not null && adapter.Studio.LatestDetailsUrl is { } studio)
        {
            links.Add(new OpenLinkAction(studio, CoreStrings.Link_StudioReleaseNotes));
        }

        var studioLatest = adapter.StudioVersionsIfAvailable is null
            ? CoreStrings.DriverUpdate_Value_Unavailable
            : adapter.Studio.LatestVersion ?? CoreStrings.DriverUpdate_Value_Unknown;
        return CannotVerify(
            id,
            SnapshotValues.Format(CoreStrings.DriverUpdate_Title_Ambiguous, name),
            adapter.Measured,
            SnapshotValues.Format(
                CoreStrings.DriverUpdate_Evidence_Ambiguous,
                installed,
                installedDate,
                adapter.GameReady.LatestVersion ?? CoreStrings.DriverUpdate_Value_Unknown,
                studioLatest),
            CannotVerifyReason.Ambiguous,
            SnapshotValues.Format(detailTemplate, installed),
            links);
    }

    /// <summary>
    /// 공식 링크 표의 NVIDIA 공식 드라이버 페이지 링크(표가 없으면 빈 목록).
    /// </summary>
    private List<FindingAction> NvidiaDriversPageLinks()
    {
        return _catalog is null
            ? []
            : [.. _catalog.ForVendor(NVIDIA_VENDOR_KEY).Select(entry => (FindingAction)new OpenLinkAction(entry.Url, entry.Label))];
    }

    /// <summary>
    /// 확인 불가 Finding을 만든다(링크 + 자세히 보기).
    /// </summary>
    private static Finding CannotVerify(
        string id,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        CannotVerifyReason reason,
        string? detail,
        IReadOnlyList<FindingAction> links)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Driver,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: Verdict.CannotVerify,
            cannotVerifyReason: reason,
            detail: detail,
            recommendation: null,
            impact: null,
            actions: [.. links, new ShowDetailsAction()]);
    }
}
