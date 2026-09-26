/**
 * @file    : DriverRuleTestData.cs
 * @author  : rudals252
 * @brief   : 드라이버 온라인 비교·공식 링크 규칙 테스트용 가짜 NVIDIA 조회/Windows Update 검색/시스템 정보 결과 생성 도우미와 테스트용 공식 링크 표·금지 문구
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 계열 최신 항목입니다.
/// </summary>
public sealed record FakeLatest(string Version, string Date, string DetailsUrl, string DownloadUrl, string Name = "GeForce Game Ready Driver");

/// <summary>
/// 가짜 NVIDIA 조회 어댑터 하나입니다. <see cref="StudioVersions"/>가 null이면 Studio 목록을 받지 못한 경우입니다.
/// </summary>
public sealed record FakeNvidiaAdapter(
    string Name,
    string PnpId,
    string? InstalledVersion,
    string State = NvidiaLookupProbeContract.STATE_LISTED,
    string[]? GameReadyVersions = null,
    FakeLatest? GameReadyLatest = null,
    string[]? StudioVersions = null,
    FakeLatest? StudioLatest = null,
    long MatchCount = 1,
    string? FailureReason = null,
    string InstalledDate = "2026-08-20");

/// <summary>
/// 드라이버 규칙 테스트 데이터 도우미입니다. 버전·날짜·URL은 테스트 예시이며 이 PC 값이 아닙니다.
/// </summary>
internal static class DriverRuleTestData
{
    /// <summary>NVIDIA 테스트 PnP ID.</summary>
    public const string NVIDIA_PNP = @"PCI\VEN_10DE&DEV_0001&SUBSYS_00000000&REV_A1\4&TEST&0&0009";

    /// <summary>Game Ready 최신 예시.</summary>
    public static readonly FakeLatest GR_LATEST = new(
        "617.14", "2026-09-22", "https://www.nvidia.com/en-us/drivers/details/279803/", "https://us.download.nvidia.com/Windows/617.14/617.14-desktop-win10-win11-64bit-international-dch-whql.exe");

    /// <summary>Studio 최신 예시.</summary>
    public static readonly FakeLatest STUDIO_LATEST = new(
        "616.92", "2026-09-09", "https://www.nvidia.com/en-us/drivers/details/278455/", "https://us.download.nvidia.com/Windows/616.92/616.92-desktop-win10-win11-64bit-international-nsd-dch-whql.exe", "NVIDIA Studio Driver");

    /// <summary>드라이버 업데이트 후보에 나오면 안 되는 표현(스펙 §5 GPU 드라이버 행, 오래된 버전을 고장으로 표현하지 않음).</summary>
    public static readonly string[] FORBIDDEN_DRIVER_PHRASES = ["권장", "호환성 검증 완료", "이 기기 권장", "고장", "문제", "오류", "위험"];

    /// <summary>테스트용 공식 링크 표.</summary>
    public static readonly VendorLinkCatalog CATALOG = VendorLinkCatalogParser.Parse("""
        {
          "schemaVersion": 1,
          "entries": [
            { "id": "nvidia-drivers", "kind": "gpuVendor", "key": "nvidia", "label": "NVIDIA 공식 드라이버", "url": "https://www.nvidia.com/en-us/drivers/", "officialDomains": ["nvidia.com"] },
            { "id": "amd-drivers", "kind": "gpuVendor", "key": "amd", "label": "AMD 공식 드라이버", "url": "https://www.amd.com/en/support/download/drivers.html", "officialDomains": ["amd.com"] },
            { "id": "amd-auto-detect", "kind": "tool", "key": "amd", "label": "AMD 자동 감지 도구", "url": "https://www.amd.com/en/resources/support-articles/faqs/GPU-131.html", "officialDomains": ["amd.com"] },
            { "id": "intel-dsa", "kind": "tool", "key": "intel", "label": "Intel 드라이버 지원 도우미", "url": "https://www.intel.com/content/www/us/en/support/detect.html", "officialDomains": ["intel.com"] },
            { "id": "msi-support", "kind": "oem", "key": "msi", "manufacturers": ["Micro-Star International Co., Ltd."], "label": "MSI 공식 지원", "url": "https://www.msi.com/support/download", "officialDomains": ["msi.com"] },
            { "id": "dell-support", "kind": "oem", "key": "dell", "manufacturers": ["Dell Inc."], "label": "Dell 공식 지원", "url": "https://www.dell.com/support/home/", "officialDomains": ["dell.com"] },
            { "id": "microsoft-surface", "kind": "oem", "key": "microsoft", "manufacturers": ["Microsoft Corporation"], "label": "Surface 공식 지원", "url": "https://support.microsoft.com/en-us/surface/", "officialDomains": ["microsoft.com"] }
          ]
        }
        """).Catalog!;

    /// <summary>
    /// NVIDIA 조회 결과를 만든다.
    /// </summary>
    public static ProbeResult NvidiaResult(ProbeStatus status, params FakeNvidiaAdapter[] adapters)
    {
        var measurements = new List<Measurement> { RuleTestData.Integer(NvidiaLookupProbeContract.ADAPTER_COUNT, adapters.Length) };
        for (var index = 0; index < adapters.Length; index++)
        {
            var adapter = adapters[index];
            string Name(string field) => NvidiaLookupProbeContract.AdapterMeasurementName(index, field);
            string Branch(string branch, string suffix) => Name(NvidiaLookupProbeContract.BranchField(branch, suffix));

            measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.FIELD_NAME), adapter.Name));
            measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.FIELD_PNP_DEVICE_ID), adapter.PnpId));
            measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.FIELD_INSTALLED_DATE), adapter.InstalledDate));
            if (adapter.InstalledVersion is { } installed)
            {
                measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.FIELD_INSTALLED_VERSION), installed));
            }

            measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.FIELD_LOOKUP_STATE), adapter.State));
            measurements.Add(RuleTestData.Integer(Name(NvidiaLookupProbeContract.FIELD_PRODUCT_MATCH_COUNT), adapter.MatchCount));
            if (adapter.FailureReason is { } reason)
            {
                measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.FIELD_FAILURE_REASON), reason));
            }

            if (adapter.GameReadyVersions is { } gameReady)
            {
                measurements.Add(RuleTestData.TextList(Branch(NvidiaLookupProbeContract.BRANCH_GAME_READY, NvidiaLookupProbeContract.SUFFIX_VERSIONS), gameReady));
                AddLatest(measurements, index, NvidiaLookupProbeContract.BRANCH_GAME_READY, adapter.GameReadyLatest);
            }

            if (adapter.State == NvidiaLookupProbeContract.STATE_LISTED)
            {
                measurements.Add(HardwareRuleTestData.Boolean(Branch(NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_AVAILABLE), adapter.StudioVersions is not null));
            }

            if (adapter.StudioVersions is { } studio)
            {
                measurements.Add(RuleTestData.TextList(Branch(NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_VERSIONS), studio));
                AddLatest(measurements, index, NvidiaLookupProbeContract.BRANCH_STUDIO, adapter.StudioLatest);
            }
        }

        return EngineTestData.CreateResult(NvidiaLookupProbeContract.PROBE_ID, status, measurements);
    }

    /// <summary>
    /// Windows Update 검색 결과를 만든다. 제목 목록의 길이가 업데이트 수다.
    /// </summary>
    public static ProbeResult WindowsUpdateResult(ProbeStatus status, int resultCode, bool? rebootRequired, params string[] titles)
    {
        var measurements = new List<Measurement>
        {
            RuleTestData.Integer(WindowsUpdateProbeContract.RESULT_CODE, resultCode),
            RuleTestData.Integer(WindowsUpdateProbeContract.UPDATE_COUNT, titles.Length),
        };

        for (var index = 0; index < titles.Length; index++)
        {
            measurements.Add(RuleTestData.Text(WindowsUpdateProbeContract.UpdateMeasurementName(index, WindowsUpdateProbeContract.FIELD_TITLE), titles[index]));
        }

        if (rebootRequired is { } reboot)
        {
            measurements.Add(HardwareRuleTestData.Boolean(WindowsUpdateProbeContract.REBOOT_REQUIRED, reboot));
        }

        return EngineTestData.CreateResult(WindowsUpdateProbeContract.PROBE_ID, status, measurements);
    }

    /// <summary>
    /// 시스템 정보 결과를 만든다(null은 값 없음).
    /// </summary>
    public static ProbeResult SystemInfoResult(string? manufacturer, string? model, params string[] chassis)
    {
        var measurements = new List<Measurement>();
        if (manufacturer is not null)
        {
            measurements.Add(RuleTestData.Text(SystemInfoProbeContract.MANUFACTURER, manufacturer));
        }

        if (model is not null)
        {
            measurements.Add(RuleTestData.Text(SystemInfoProbeContract.MODEL, model));
        }

        measurements.Add(RuleTestData.TextList(SystemInfoProbeContract.CHASSIS_TYPES, chassis));
        return EngineTestData.CreateResult(SystemInfoProbeContract.PROBE_ID, ProbeStatus.Success, measurements);
    }

    /// <summary>
    /// Finding의 사용자에게 보이는 문장을 모두 이어 붙인다(금지 문구 확인용).
    /// </summary>
    public static string VisibleText(Finding finding)
    {
        return string.Join(
            "\n",
            finding.Title,
            finding.Evidence,
            finding.Detail ?? string.Empty,
            finding.Recommendation?.Text ?? string.Empty,
            finding.Recommendation?.Condition ?? string.Empty,
            finding.Impact?.Benefit ?? string.Empty,
            finding.Impact?.SideEffect ?? string.Empty,
            string.Join("\n", finding.Actions.OfType<OpenLinkAction>().Select(action => action.Label ?? string.Empty)));
    }

    /// <summary>
    /// 계열 최신 항목 측정값을 추가한다.
    /// </summary>
    private static void AddLatest(List<Measurement> measurements, int index, string branch, FakeLatest? latest)
    {
        if (latest is null)
        {
            return;
        }

        string Name(string suffix) => NvidiaLookupProbeContract.AdapterMeasurementName(index, NvidiaLookupProbeContract.BranchField(branch, suffix));
        measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION), latest.Version));
        measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.SUFFIX_LATEST_RELEASE_DATE), latest.Date));
        measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.SUFFIX_LATEST_NAME), latest.Name));
        measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.SUFFIX_LATEST_DETAILS_URL), latest.DetailsUrl));
        measurements.Add(RuleTestData.Text(Name(NvidiaLookupProbeContract.SUFFIX_LATEST_DOWNLOAD_URL), latest.DownloadUrl));
    }
}
