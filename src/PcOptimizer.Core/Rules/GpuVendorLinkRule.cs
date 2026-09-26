/**
 * @file    : GpuVendorLinkRule.cs
 * @author  : rudals252
 * @brief   : AMD/Intel GPU 어댑터마다 설치 버전과 공식 드라이버 페이지·도구 링크 정보, 별도의 '최신 여부는 공식 도구에서 확인' CannotVerify(Unsupported)를 만드는 순수 판정 규칙(네트워크 없음)
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// AMD/Intel 공식 링크 규칙입니다(스펙 §5 GPU 드라이버 행: AMD/Intel은 설치 정보 Info + 공식 링크, 최신 비교 Unsupported).
/// 설치 GPU 프로브(로컬)만 읽으므로 온라인 확인 여부와 관계없이 나옵니다. NVIDIA·가상·기타 어댑터에는 새 카드를 만들지 않습니다.
/// 링크는 공식 링크 표에 있는 항목만 쓰며 표가 없으면 링크 없이 정보만 보여 줍니다.
/// </summary>
public sealed class GpuVendorLinkRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "driver.vendorLinks";

    /// <summary>공식 링크 정보 Finding ID 접두사(뒤에 PnP 장치 ID).</summary>
    public const string LINK_FINDING_ID_PREFIX = "gpu-vendor-link:";

    /// <summary>최신 비교 미지원 Finding ID 접두사(뒤에 PnP 장치 ID).</summary>
    public const string LATEST_FINDING_ID_PREFIX = "gpu-latest-unsupported:";

    /// <summary>AMD 공급업체 키(공식 링크 표).</summary>
    public const string AMD_VENDOR_KEY = "amd";

    /// <summary>Intel 공급업체 키(공식 링크 표).</summary>
    public const string INTEL_VENDOR_KEY = "intel";

    private const string INDEX_ID_FORMAT = "index-{0}";
    private const int DISPLAY_INDEX_OFFSET = 1;

    private readonly VendorLinkCatalog? _catalog;

    /// <summary>
    /// 규칙을 만듭니다.
    /// </summary>
    /// <param name="catalog">공식 링크 표(읽지 못했으면 null).</param>
    public GpuVendorLinkRule(VendorLinkCatalog? catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Id => RULE_ID;

    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.TryGetProbe(GpuProbeContract.PROBE_ID, out var result)
            || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
        {
            return [];
        }

        var count = SnapshotValues.Integer(snapshot, GpuProbeContract.PROBE_ID, GpuProbeContract.ADAPTER_COUNT) ?? 0;
        var findings = new List<Finding>();
        for (var index = 0; index < count; index++)
        {
            string? Text(string field) => SnapshotValues.Text(snapshot, GpuProbeContract.PROBE_ID, GpuProbeContract.AdapterMeasurementName(index, field));

            var pnpId = Text(GpuProbeContract.FIELD_PNP_DEVICE_ID);
            var (vendorKey, vendorName) = GpuVendorClassifier.Classify(pnpId) switch
            {
                GpuVendor.Amd => (AMD_VENDOR_KEY, CoreStrings.VendorGpu_Name_Amd),
                GpuVendor.Intel => (INTEL_VENDOR_KEY, CoreStrings.VendorGpu_Name_Intel),
                _ => (null, null),
            };

            if (vendorKey is null || vendorName is null)
            {
                continue;
            }

            var name = Text(GpuProbeContract.FIELD_NAME) ?? SnapshotValues.Format(CoreStrings.Gpu_AdapterFallbackName, index + DISPLAY_INDEX_OFFSET);
            var suffix = pnpId ?? string.Format(CultureInfo.InvariantCulture, INDEX_ID_FORMAT, index);
            Measurement[] measured = SnapshotValues.WithPrefix(result, GpuProbeContract.AdapterMeasurementPrefix(index));
            findings.Add(CreateLinkInfo(
                LINK_FINDING_ID_PREFIX + suffix,
                name,
                vendorKey,
                vendorName,
                Text(GpuProbeContract.FIELD_DRIVER_VERSION) ?? CoreStrings.VendorGpu_ValueUnknown,
                Text(GpuProbeContract.FIELD_DRIVER_DATE) ?? CoreStrings.VendorGpu_ValueUnknown,
                measured));
            findings.Add(new Finding(
                id: LATEST_FINDING_ID_PREFIX + suffix,
                category: FindingCategory.Driver,
                title: SnapshotValues.Format(CoreStrings.VendorGpu_Title_Unsupported, name),
                measured: [],
                evidence: SnapshotValues.Format(CoreStrings.VendorGpu_Evidence_Unsupported, vendorName),
                verdict: Verdict.CannotVerify,
                cannotVerifyReason: CannotVerifyReason.Unsupported,
                detail: null,
                recommendation: null,
                impact: null,
                actions: []));
        }

        return findings;
    }

    /// <summary>
    /// 설치 버전과 공식 페이지·도구 링크 정보를 만든다.
    /// </summary>
    private Finding CreateLinkInfo(
        string id, string name, string vendorKey, string vendorName, string version, string date, IReadOnlyList<Measurement> measured)
    {
        var links = _catalog is null
            ? []
            : _catalog.ForVendor(vendorKey).Select(entry => (FindingAction)new OpenLinkAction(entry.Url, entry.Label)).ToList();
        return new Finding(
            id: id,
            category: FindingCategory.Driver,
            title: SnapshotValues.Format(CoreStrings.VendorGpu_Title_Info, name, vendorName),
            measured: measured,
            evidence: SnapshotValues.Format(CoreStrings.VendorGpu_Evidence_Info, version, date, vendorName),
            verdict: Verdict.Info,
            cannotVerifyReason: null,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [.. links, new ShowDetailsAction()]);
    }
}
