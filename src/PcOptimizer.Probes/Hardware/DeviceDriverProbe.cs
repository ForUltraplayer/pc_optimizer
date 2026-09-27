/**
 * @file    : DeviceDriverProbe.cs
 * @author  : rudals252
 * @brief   : Win32_PnPSignedDriver에서 칩셋·유선 랜·Wi-Fi·오디오 드라이버의 이름·버전·날짜·제공자만 골라 기록하는 조회 전용 프로브(장치 ID·일련번호 미기록)
 */
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Hardware;

/// <summary>드라이버 안내(SP3)가 "설치 버전·날짜"를 보여 주는 근거입니다. 최신 여부는 판단하지 않습니다.</summary>
public sealed class DeviceDriverProbe : IProbe
{
    /// <summary>WMI 클래스.</summary>
    public const string DRIVER_CLASS = "Win32_PnPSignedDriver";
    /// <summary>WMI 제공자 시간 상한.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(8);
    private const string SOURCE = "WMI " + DRIVER_CLASS;
    private const string PROPERTY_DEVICE_NAME = "DeviceName";
    private const string PROPERTY_DEVICE_CLASS = "DeviceClass";
    private const string PROPERTY_DRIVER_VERSION = "DriverVersion";
    private const string PROPERTY_DRIVER_DATE = "DriverDate";
    private const string PROPERTY_PROVIDER = "DriverProviderName";
    private const string PROPERTY_MANUFACTURER = "Manufacturer";
    private const string CLASS_NET = "NET";
    private const string CLASS_MEDIA = "MEDIA";
    private const string CLASS_SYSTEM = "SYSTEM";
    private const int WMI_DATE_LENGTH = 8;
    private static readonly string[] PROPERTIES = [PROPERTY_DEVICE_NAME, PROPERTY_DEVICE_CLASS, PROPERTY_DRIVER_VERSION, PROPERTY_DRIVER_DATE, PROPERTY_PROVIDER, PROPERTY_MANUFACTURER];
    private static readonly string[] WIFI_HINTS = ["Wi-Fi", "WiFi", "Wireless", "WLAN", "802.11", "Wi Fi"];
    private static readonly string[] VIRTUAL_NET_HINTS = ["Virtual", "VPN", "Bluetooth", "Hyper-V", "TAP-", "WAN Miniport", "Miniport", "Loopback", "Npcap", "VMware", "VirtualBox", "Tunnel", "Teredo", "ISATAP", "Wintun", "WireGuard", "Filter", "Debug", "RAS "];
    private static readonly string[] AUDIO_HINTS = ["Audio", "Sound", "High Definition", "Realtek", "오디오", "USB Audio", "Speaker", "Microphone", "Headset"];
    private static readonly string[] AUDIO_EXCLUDE_HINTS = ["NVIDIA", "AMD High Definition", "Intel Display Audio", "Intel(R) Display Audio", "Display Audio", "Virtual", "Steam Streaming", "Parsec", "Voicemod", "VB-Audio", "Camera", "Bluetooth", "Hands-Free"];
    private static readonly string[] CHIPSET_VENDORS = ["Intel", "Advanced Micro Devices", "AMD"];
    private static readonly string[] CHIPSET_HINTS = ["Chipset", "SMBus", "PCI Express Root", "Root Complex", "Platform", "LPC", "eSPI", "Host Bridge", "PCH", "Power Management", "GPIO", "Serial IO", "I2C", "PSP", "Thermal", "Management Engine", "Data Fabric", "PCI Bus", "IOMMU"];
    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>실제 WMI를 씁니다.</summary>
    public DeviceDriverProbe() : this(WmiClient.Instance, SystemClock.Instance) { }

    /// <summary>WMI·시계를 주입합니다.</summary>
    public DeviceDriverProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi); ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi; _clock = clock;
    }

    /// <inheritdoc />
    public string Id => DeviceDriverProbeContract.PROBE_ID;
    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;
    /// <inheritdoc />
    public bool RequiresElevation => false;
    /// <inheritdoc />
    public bool RequiresNetwork => false;
    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;
    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_LOCAL_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();
        var observedAt = _clock.UtcNow;
        var result = _wmi.Query(DRIVER_CLASS, PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            return Task.FromResult(new ProbeResult(Id, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(DRIVER_CLASS, result)], observedAt, TimeSpan.Zero, context.UserContext));
        }
        var buckets = DeviceDriverProbeContract.Categories.ToDictionary(c => c, _ => new List<DriverRow>(), StringComparer.Ordinal);
        foreach (var row in result.Rows)
        {
            ct.ThrowIfCancellationRequested();
            var name = WmiResultInterpreter.GetText(row, PROPERTY_DEVICE_NAME);
            var deviceClass = WmiResultInterpreter.GetText(row, PROPERTY_DEVICE_CLASS);
            var version = WmiResultInterpreter.GetText(row, PROPERTY_DRIVER_VERSION);
            if (name is null || deviceClass is null || version is null) { continue; }
            var provider = WmiResultInterpreter.GetText(row, PROPERTY_PROVIDER) ?? WmiResultInterpreter.GetText(row, PROPERTY_MANUFACTURER);
            if (Classify(deviceClass, name, provider) is not { } category) { continue; }
            var date = ParseDate(WmiResultInterpreter.GetText(row, PROPERTY_DRIVER_DATE));
            var bucket = buckets[category];
            // 같은 이름·버전은 한 번만 기록한다(다기능 장치가 여러 행으로 나온다).
            if (bucket.Any(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && b.Version == version)) { continue; }
            if (bucket.Count < DeviceDriverProbeContract.MAX_ITEMS) { bucket.Add(new(name, version, date, provider)); }
        }
        var measurements = new List<Measurement>();
        foreach (var category in DeviceDriverProbeContract.Categories)
        {
            var items = buckets[category];
            measurements.Add(new(DeviceDriverProbeContract.CountName(category), new IntegerValue(items.Count), null, SOURCE, observedAt, MeasurementQuality.Observed));
            for (var i = 0; i < items.Count; i++)
            {
                measurements.Add(new(DeviceDriverProbeContract.ItemName(category, i, DeviceDriverProbeContract.FIELD_NAME), new TextValue(items[i].Name), null, SOURCE, observedAt, MeasurementQuality.Observed));
                measurements.Add(new(DeviceDriverProbeContract.ItemName(category, i, DeviceDriverProbeContract.FIELD_VERSION), new TextValue(items[i].Version), null, SOURCE, observedAt, MeasurementQuality.Observed));
                if (items[i].Date is { } date) { measurements.Add(new(DeviceDriverProbeContract.ItemName(category, i, DeviceDriverProbeContract.FIELD_DATE), new TextValue(date), null, SOURCE, observedAt, MeasurementQuality.Observed)); }
                if (items[i].Provider is { } provider) { measurements.Add(new(DeviceDriverProbeContract.ItemName(category, i, DeviceDriverProbeContract.FIELD_PROVIDER), new TextValue(provider), null, SOURCE, observedAt, MeasurementQuality.Observed)); }
            }
        }
        return Task.FromResult(new ProbeResult(Id, ProbeStatus.Success, measurements, [], observedAt, TimeSpan.Zero, context.UserContext));
    }

    /// <summary>장치 클래스와 이름으로 분류합니다. 가상 어댑터·그래픽카드 HDMI 오디오 등은 제외합니다. 해당 없으면 null.</summary>
    internal static string? Classify(string deviceClass, string name, string? provider)
    {
        if (deviceClass.Equals(CLASS_NET, StringComparison.OrdinalIgnoreCase))
        {
            if (VIRTUAL_NET_HINTS.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase))) { return null; }
            return WIFI_HINTS.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)) ? DeviceDriverProbeContract.CATEGORY_WIFI : DeviceDriverProbeContract.CATEGORY_LAN;
        }
        if (deviceClass.Equals(CLASS_MEDIA, StringComparison.OrdinalIgnoreCase))
        {
            if (AUDIO_EXCLUDE_HINTS.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase))) { return null; }
            return AUDIO_HINTS.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)) ? DeviceDriverProbeContract.CATEGORY_AUDIO : null;
        }
        if (deviceClass.Equals(CLASS_SYSTEM, StringComparison.OrdinalIgnoreCase))
        {
            var vendor = provider ?? string.Empty;
            var vendorMatch = CHIPSET_VENDORS.Any(v => vendor.Contains(v, StringComparison.OrdinalIgnoreCase) || name.Contains(v, StringComparison.OrdinalIgnoreCase));
            return vendorMatch && CHIPSET_HINTS.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)) ? DeviceDriverProbeContract.CATEGORY_CHIPSET : null;
        }
        return null;
    }

    /// <summary>WMI 날짜(yyyyMMddHHmmss.ffffff±zzz)에서 날짜 부분만 ISO 8601로 바꿉니다.</summary>
    internal static string? ParseDate(string? text)
    {
        if (text is null || text.Length < WMI_DATE_LENGTH || !text[..WMI_DATE_LENGTH].All(char.IsAsciiDigit)) { return null; }
        return text[..4] + "-" + text[4..6] + "-" + text[6..8];
    }

    private sealed record DriverRow(string Name, string Version, string? Date, string? Provider);
}
