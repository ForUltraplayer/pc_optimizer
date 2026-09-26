/**
 * @file    : InstalledDriverRule.cs
 * @author  : rudals252
 * @brief   : GPU 어댑터별 설치 드라이버 버전·날짜·하드웨어 ID(NVIDIA는 표기 버전 변환 포함)를 정보로, 가상 어댑터를 '가상 어댑터' 정보로 내는 순수 판정 규칙
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 설치 드라이버 규칙입니다(스펙 §5 GPU 드라이버 행 중 설치 정보 부분).
/// <list type="bullet">
/// <item>PCI 어댑터: Info(설치 버전·날짜·하드웨어 ID). NVIDIA는 NVIDIA 표기 버전을 함께, AMD/Intel은 '변환 없음'.</item>
/// <item>PCI가 아닌 어댑터: Info '가상 어댑터'.</item>
/// <item>드라이버 버전 누락: CannotVerify(PartialData).</item>
/// </list>
/// 최신 버전 비교는 온라인 확인 단계의 일이므로 여기서는 다루지 않으며 네트워크를 쓰지 않습니다.
/// Finding ID는 PnP 장치 ID입니다.
/// </summary>
public sealed class InstalledDriverRule : IRule
{
    /// <summary>규칙 ID.</summary>
    public const string RULE_ID = "driver.installed";

    /// <summary>어댑터별 Finding ID 접두사(뒤에 PnP 장치 ID가 붙음).</summary>
    public const string FINDING_ID_PREFIX = "gpu-driver:";

    private const string INDEX_ID_FORMAT = "index-{0}";
    private const int DISPLAY_INDEX_OFFSET = 1;

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
            findings.Add(EvaluateAdapter(snapshot, result, index));
        }

        return findings;
    }

    /// <summary>
    /// 어댑터 하나를 판정한다.
    /// </summary>
    private static Finding EvaluateAdapter(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string? Text(string field) => SnapshotValues.Text(snapshot, GpuProbeContract.PROBE_ID, GpuProbeContract.AdapterMeasurementName(index, field));

        Measurement[] measured = SnapshotValues.WithPrefix(result, GpuProbeContract.AdapterMeasurementPrefix(index));
        var pnpId = Text(GpuProbeContract.FIELD_PNP_DEVICE_ID);
        var name = Text(GpuProbeContract.FIELD_NAME)
            ?? SnapshotValues.Format(CoreStrings.Gpu_AdapterFallbackName, index + DISPLAY_INDEX_OFFSET);
        var version = Text(GpuProbeContract.FIELD_DRIVER_VERSION);
        var date = Text(GpuProbeContract.FIELD_DRIVER_DATE) ?? CoreStrings.Gpu_ValueUnknown;
        var id = FINDING_ID_PREFIX + (pnpId ?? string.Format(CultureInfo.InvariantCulture, INDEX_ID_FORMAT, index));

        if (version is null)
        {
            return Create(id, SnapshotValues.Format(CoreStrings.Gpu_Title_VersionMissing, name), measured, CoreStrings.Gpu_Evidence_VersionMissing, Verdict.CannotVerify, CannotVerifyReason.PartialData);
        }

        var vendor = GpuVendorClassifier.Classify(pnpId);
        if (vendor == GpuVendor.Virtual)
        {
            return Create(
                id,
                SnapshotValues.Format(CoreStrings.Gpu_Title_Virtual, name),
                measured,
                SnapshotValues.Format(CoreStrings.Gpu_Evidence_Virtual, version, date),
                Verdict.Info,
                cannotVerifyReason: null);
        }

        var vendorVersion = vendor == GpuVendor.Nvidia ? NvidiaDriverVersion.FromWindowsVersion(version) : null;
        var displayVersion = vendorVersion is null ? version : SnapshotValues.Format(CoreStrings.Gpu_VersionWithVendor, vendorVersion, version);
        var hardwareIds = SnapshotValues.TextList(snapshot, GpuProbeContract.PROBE_ID, GpuProbeContract.AdapterMeasurementName(index, GpuProbeContract.FIELD_HARDWARE_IDS));
        var hardwareId = hardwareIds?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? CoreStrings.Gpu_ValueUnknown;

        return Create(
            id,
            SnapshotValues.Format(CoreStrings.Gpu_Title_Installed, name, displayVersion),
            measured,
            SnapshotValues.Format(CoreStrings.Gpu_Evidence_Installed, version, vendorVersion ?? CoreStrings.Gpu_VendorVersion_None, date, hardwareId),
            Verdict.Info,
            cannotVerifyReason: null);
    }

    /// <summary>
    /// 드라이버 분류 Finding을 만든다(권고 없음, 상세 보기만).
    /// </summary>
    private static Finding Create(string id, string title, IReadOnlyList<Measurement> measured, string evidence, Verdict verdict, CannotVerifyReason? cannotVerifyReason)
    {
        return new Finding(
            id: id,
            category: FindingCategory.Driver,
            title: title,
            measured: measured,
            evidence: evidence,
            verdict: verdict,
            cannotVerifyReason: cannotVerifyReason,
            detail: null,
            recommendation: null,
            impact: null,
            actions: [new ShowDetailsAction()]);
    }
}
