/**
 * @file    : DriverGuideBuilder.cs
 * @author  : rudals252
 * @brief   : 드라이버 안내(스펙 §5) 순수 빌더. 검사 스냅샷의 제조사·모델/보드·섀시·설치 드라이버와 공식 링크 표로 "어디서 무엇을 받을지" 한국어 안내 모델을 만든다(최신 여부는 판단하지 않음)
 */
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Core.Drivers;

/// <summary>설치된 드라이버 한 줄입니다.</summary>
/// <param name="Name">장치 이름.</param>
/// <param name="Version">드라이버 버전.</param>
/// <param name="Date">드라이버 날짜(ISO, 없으면 null).</param>
/// <param name="Provider">제공자(없으면 null).</param>
public sealed record InstalledDriver(string Name, string Version, string? Date, string? Provider);

/// <summary>받아야 할 드라이버 한 종류의 안내입니다.</summary>
/// <param name="Category">분류 키(DeviceDriverProbeContract).</param>
/// <param name="KoreanName">한국어 이름.</param>
/// <param name="EnglishTerms">벤더 페이지에서 보이는 영어 표기.</param>
/// <param name="MenuHint">보통 있는 메뉴 위치.</param>
/// <param name="InstallOrder">설치 순서(1부터).</param>
/// <param name="Installed">현재 설치된 드라이버(검사 결과, 없으면 빈 목록).</param>
/// <param name="StatusText">설치 상태 한 줄("설치됨 …" / "검사에서 찾지 못함").</param>
public sealed record DriverGuideItem(string Category, string KoreanName, string EnglishTerms, string MenuHint, int InstallOrder, IReadOnlyList<InstalledDriver> Installed, string StatusText);

/// <summary>드라이버 안내 모델입니다.</summary>
/// <param name="Chassis">노트북/데스크톱/알 수 없음.</param>
/// <param name="Manufacturer">제조사(노트북은 시스템 제조사, 데스크톱은 메인보드 제조사).</param>
/// <param name="Model">모델(노트북은 시스템 모델, 데스크톱은 메인보드 제품명).</param>
/// <param name="SupportLink">제조사 공식 지원 페이지(표에 없으면 null).</param>
/// <param name="SupportLabel">지원 페이지 라벨.</param>
/// <param name="Items">받을 드라이버 네 가지(설치 순서).</param>
/// <param name="GpuLinks">그래픽 드라이버 공식 링크(라벨·URL).</param>
/// <param name="Notes">사용자 안내 문장.</param>
public sealed record DriverGuide(ChassisKind Chassis, string? Manufacturer, string? Model, string? SupportLink, string? SupportLabel,
    IReadOnlyList<DriverGuideItem> Items, IReadOnlyList<(string Label, string Url)> GpuLinks, IReadOnlyList<string> Notes)
{
    /// <summary>검사 결과가 있어 안내를 만들 수 있었는지(제조사 또는 모델 중 하나라도 있음).</summary>
    public bool HasIdentity => Manufacturer is not null || Model is not null;
    /// <summary>복사 버튼에 넣는 "제조사 모델" 문자열입니다.</summary>
    public string ModelForCopy => string.Join(" ", new[] { Manufacturer, Model }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>스냅샷과 링크 표에서 안내 모델을 만듭니다. I/O가 없습니다.</summary>
public static class DriverGuideBuilder
{
    /// <summary>받을 드라이버 네 가지의 고정 정의(설치 순서).</summary>
    private static readonly (string Category, string Korean, string English, string Menu)[] DEFINITIONS =
    [
        (DeviceDriverProbeContract.CATEGORY_CHIPSET, "칩셋 드라이버", "Chipset / INF / Platform", "지원 페이지의 드라이버 목록에서 'Chipset' 또는 'INF' 항목. Intel은 'Chipset INF Utility', AMD는 'Chipset Drivers'로 표시됩니다."),
        (DeviceDriverProbeContract.CATEGORY_LAN, "유선 랜(이더넷) 드라이버", "LAN / Ethernet / Network", "'LAN' 또는 'Network' 항목. Realtek·Intel 이름이 보통 함께 적혀 있습니다."),
        (DeviceDriverProbeContract.CATEGORY_WIFI, "무선 랜(Wi-Fi·블루투스) 드라이버", "Wireless / Wi-Fi / WLAN / Bluetooth", "'Wireless' 또는 'WLAN' 항목. 블루투스는 같은 칩이라 함께 있는 경우가 많습니다. 유선만 쓰는 데스크톱은 건너뜁니다."),
        (DeviceDriverProbeContract.CATEGORY_AUDIO, "오디오 드라이버", "Audio / Realtek Audio / Sound", "'Audio' 항목. 대부분 Realtek High Definition Audio입니다."),
    ];
    private const string STATUS_NONE = "검사에서 이 종류의 드라이버를 찾지 못했어요. 장치가 없거나 기본 드라이버로 동작 중일 수 있어요.";
    private const string NOTE_LATEST = "최신 버전인지는 이 앱이 판단하지 않아요. 제조사 페이지의 날짜와 아래 설치 날짜를 비교하세요.";
    private const string NOTE_ORDER = "받은 뒤 순서: 1 칩셋 → 2 유선 랜 → 3 무선 랜 → 4 오디오. 각 설치 파일을 실행하고 재부팅을 요구하면 그때 다시 시작합니다.";
    private const string NOTE_LAPTOP = "노트북은 메인보드 제조사가 아니라 노트북 제조사 지원 페이지에서 모델명으로 찾습니다.";
    private const string NOTE_DESKTOP = "조립 데스크톱은 메인보드 제조사 지원 페이지에서 보드 모델명으로 찾습니다. 완제품 PC는 PC 제조사 페이지를 먼저 보세요.";
    private const string NOTE_NO_LINK = "제조사 규칙이 없어 지원 첫 화면 링크를 드리지 못해요. 모델명을 복사해 제조사 사이트의 검색창에 붙여 넣으세요.";
    private const string NOTE_NO_SCAN = "먼저 메인 화면에서 검사를 실행하면 제조사·모델과 설치된 드라이버를 채워요.";
    private const string NVIDIA_KEY = "nvidia";
    private const string AMD_KEY = "amd";
    private const string INTEL_KEY = "intel";

    /// <summary>스냅샷이 없으면 안내 문구만 있는 빈 모델을 만듭니다.</summary>
    public static DriverGuide Build(ScanSnapshot? snapshot, VendorLinkCatalog? links)
    {
        if (snapshot is null)
        {
            return new(ChassisKind.Unknown, null, null, null, null, DEFINITIONS.Select((d, i) => new DriverGuideItem(d.Category, d.Korean, d.English, d.Menu, i + 1, [], STATUS_NONE)).ToArray(),
                GpuLinks(links, null), [NOTE_NO_SCAN]);
        }
        string? Info(string name) => SnapshotValues.Text(snapshot, SystemInfoProbeContract.PROBE_ID, name);
        string? Details(string name) => SnapshotValues.Text(snapshot, SystemDetailsProbeContract.PROBE_ID, name);
        var chassis = ChassisClassifier.Classify(snapshot.GetMeasurement(SystemInfoProbeContract.PROBE_ID, SystemInfoProbeContract.CHASSIS_TYPES));
        var systemManufacturer = Info(SystemInfoProbeContract.MANUFACTURER);
        var systemModel = Info(SystemInfoProbeContract.MODEL);
        var boardManufacturer = Details(SystemDetailsProbeContract.BOARD_MANUFACTURER);
        var boardProduct = Details(SystemDetailsProbeContract.BOARD_PRODUCT);
        // 노트북은 시스템 제조사·모델, 데스크톱(또는 불명)은 메인보드가 우선이며 보드 정보가 없으면 시스템 값으로 대체한다.
        var laptop = chassis == ChassisKind.Laptop;
        var manufacturer = laptop ? systemManufacturer ?? boardManufacturer : boardManufacturer ?? systemManufacturer;
        var model = laptop ? systemModel ?? boardProduct : boardProduct ?? systemModel;
        var oem = links?.FindOem(manufacturer) ?? (laptop ? null : links?.FindOem(systemManufacturer));
        var items = DEFINITIONS.Select((d, i) => new DriverGuideItem(d.Category, d.Korean, d.English, d.Menu, i + 1, Installed(snapshot, d.Category), Status(Installed(snapshot, d.Category)))).ToArray();
        var notes = new List<string> { laptop ? NOTE_LAPTOP : NOTE_DESKTOP, NOTE_LATEST, NOTE_ORDER };
        if (oem is null && manufacturer is not null) { notes.Insert(0, NOTE_NO_LINK); }
        var gpuVendor = SnapshotValues.Text(snapshot, GpuProbeContract.PROBE_ID, GpuProbeContract.AdapterMeasurementName(0, GpuProbeContract.FIELD_ADAPTER_COMPATIBILITY));
        return new(chassis, manufacturer, model, oem?.Url, oem?.Label, items, GpuLinks(links, gpuVendor), notes);
    }

    private static IReadOnlyList<InstalledDriver> Installed(ScanSnapshot snapshot, string category)
    {
        var count = SnapshotValues.Integer(snapshot, DeviceDriverProbeContract.PROBE_ID, DeviceDriverProbeContract.CountName(category));
        if (count is null or <= 0 || count > DeviceDriverProbeContract.MAX_ITEMS) { return []; }
        var list = new List<InstalledDriver>();
        for (var i = 0; i < count; i++)
        {
            string? Field(string field) => SnapshotValues.Text(snapshot, DeviceDriverProbeContract.PROBE_ID, DeviceDriverProbeContract.ItemName(category, i, field));
            var name = Field(DeviceDriverProbeContract.FIELD_NAME); var version = Field(DeviceDriverProbeContract.FIELD_VERSION);
            if (name is null || version is null) { continue; }
            list.Add(new(name, version, Field(DeviceDriverProbeContract.FIELD_DATE), Field(DeviceDriverProbeContract.FIELD_PROVIDER)));
        }
        return list;
    }

    private static string Status(IReadOnlyList<InstalledDriver> installed)
        => installed.Count == 0 ? STATUS_NONE : "설치됨 · " + string.Join(" / ", installed.Take(3).Select(d => d.Version + (d.Date is null ? "" : $" ({d.Date})")));

    private static IReadOnlyList<(string Label, string Url)> GpuLinks(VendorLinkCatalog? links, string? gpuVendor)
    {
        if (links is null) { return []; }
        var key = gpuVendor?.ToLowerInvariant() switch
        {
            { } v when v.Contains(NVIDIA_KEY, StringComparison.Ordinal) => NVIDIA_KEY,
            { } v when v.Contains(AMD_KEY, StringComparison.Ordinal) || v.Contains("advanced micro", StringComparison.Ordinal) => AMD_KEY,
            { } v when v.Contains(INTEL_KEY, StringComparison.Ordinal) => INTEL_KEY,
            _ => null,
        };
        var vendors = key is null ? new[] { NVIDIA_KEY, AMD_KEY, INTEL_KEY } : [key];
        return vendors.SelectMany(v => links.ForVendor(v)).Where(e => e.Kind == VendorLinkKind.GpuVendor).Select(e => (e.Label, e.Url)).ToArray();
    }
}
