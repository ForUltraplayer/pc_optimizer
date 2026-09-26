/**
 * @file    : OemSupportRule.cs
 * @author  : rudals252
 * @brief   : 시스템 제조사를 정규화해 공식 링크 표의 제조사 지원 페이지 정보(노트북은 제조사 맞춤 드라이버 안내)로 연결하고, 표에 없으면 NoRule, 가상 머신은 정보, 제조사 없음은 PartialData로 내는 순수 판정 규칙
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 제조사 지원 페이지 규칙입니다(스펙 §5 노트북 OEM 행: Info, 표에 없으면 CannotVerify(NoRule)).
/// 링크는 모델별 페이지가 아니라 표에 등록된 제조사 지원 첫 화면이며, 노트북에는 GPU 공급업체 최신 드라이버를 권하지 않고
/// 제조사가 맞춘 드라이버를 먼저 확인하라는 안내를 붙입니다.
/// </summary>
public sealed class OemSupportRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "driver.oemSupport";

    /// <summary>Finding ID.</summary>
    public const string FINDING_ID = "oem-support:computer";

    private const string MICROSOFT_MANUFACTURER = "microsoft";
    private const string VIRTUAL_MACHINE_MODEL = "Virtual Machine";
    private const string DETAIL_SEPARATOR = " ";

    /// <summary>가상화 플랫폼이 보고하는 제조사(정규화 값).</summary>
    private static readonly HashSet<string> VIRTUAL_MANUFACTURERS = new(StringComparer.Ordinal)
    {
        "vmware",
        "qemu",
        "innotek gmbh",
        "xen",
        "parallels",
    };

    private readonly VendorLinkCatalog? _catalog;

    /// <summary>
    /// 규칙을 만듭니다.
    /// </summary>
    /// <param name="catalog">공식 링크 표(읽지 못했으면 null, 이때 ProbeError로 표시).</param>
    public OemSupportRule(VendorLinkCatalog? catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(SystemInfoProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        string? Text(string name) => SnapshotValues.Text(snapshot, SystemInfoProbeContract.PROBE_ID, name);

        var manufacturer = Text(SystemInfoProbeContract.MANUFACTURER);
        var model = Text(SystemInfoProbeContract.MODEL);
        var chassis = ChassisClassifier.Classify(snapshot.GetMeasurement(SystemInfoProbeContract.PROBE_ID, SystemInfoProbeContract.CHASSIS_TYPES));
        Measurement[] measured = [.. result.Measurements];

        if (manufacturer is null)
        {
            return [Create(CoreStrings.Oem_Title_Missing, measured, CoreStrings.Oem_Evidence_Missing, Verdict.CannotVerify, CannotVerifyReason.PartialData, null, [])];
        }

        var modelText = model ?? CoreStrings.Oem_ValueUnknown;
        if (IsVirtualMachine(manufacturer, model))
        {
            return [Create(
                CoreStrings.Oem_Title_Virtual,
                measured,
                SnapshotValues.Format(CoreStrings.Oem_Evidence_Virtual, manufacturer, modelText),
                Verdict.Info,
                null,
                null,
                [])];
        }

        if (_catalog is null)
        {
            return [Create(
                CoreStrings.Oem_Title_CatalogUnavailable, measured, CoreStrings.Oem_Evidence_CatalogUnavailable, Verdict.CannotVerify, CannotVerifyReason.ProbeError, null, [])];
        }

        if (_catalog.FindOem(manufacturer) is not { } entry)
        {
            return [Create(
                CoreStrings.Oem_Title_Unknown,
                measured,
                CoreStrings.Oem_Evidence_Unknown,
                Verdict.CannotVerify,
                CannotVerifyReason.NoRule,
                SnapshotValues.Format(CoreStrings.Oem_Detail_Unknown, manufacturer),
                [])];
        }

        var detail = SnapshotValues.Format(CoreStrings.Oem_Detail_Model, modelText);
        if (chassis == ChassisKind.Laptop)
        {
            detail = CoreStrings.Oem_Detail_Laptop + DETAIL_SEPARATOR + detail;
        }

        return [Create(
            SnapshotValues.Format(CoreStrings.Oem_Title_Known, entry.Label),
            measured,
            SnapshotValues.Format(CoreStrings.Oem_Evidence_Known, manufacturer, modelText, ChassisClassifier.DisplayName(chassis)),
            Verdict.Info,
            null,
            detail,
            [new OpenLinkAction(entry.Url, entry.Label)])];
    }

    /// <summary>
    /// 가상 머신 여부: 가상화 플랫폼 제조사이거나, Microsoft 제조사이면서 모델이 'Virtual Machine'(Hyper-V)인 경우.
    /// </summary>
    private static bool IsVirtualMachine(string manufacturer, string? model)
    {
        var normalized = ManufacturerNameNormalizer.Normalize(manufacturer);
        return VIRTUAL_MANUFACTURERS.Contains(normalized)
            || (string.Equals(normalized, MICROSOFT_MANUFACTURER, StringComparison.Ordinal)
                && string.Equals(model, VIRTUAL_MACHINE_MODEL, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 제조사 지원 Finding을 만든다(링크 + 자세히 보기).
    /// </summary>
    private static Finding Create(
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? reason,
        string? detail,
        IReadOnlyList<FindingAction> links)
    {
        return new Finding(
            id: FINDING_ID,
            category: FindingCategory.Driver,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: reason,
            detail: detail,
            recommendation: null,
            impact: null,
            actions: [.. links, new ShowDetailsAction()]);
    }
}
