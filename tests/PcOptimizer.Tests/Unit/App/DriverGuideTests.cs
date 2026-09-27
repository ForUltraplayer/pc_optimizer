/**
 * @file    : DriverGuideTests.cs
 * @author  : rudals252
 * @brief   : 드라이버 안내 빌더(노트북/데스크톱 식별 우선순위·OEM 링크·설치 드라이버·검사 전 상태), 장치 드라이버 분류기, 화면 모델 명령 회귀
 */
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Hardware;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 WMI·클립보드·브라우저는 쓰지 않습니다.</summary>
public sealed class DriverGuideTests
{
    private static readonly VendorLinkCatalog Links = VendorLinkCatalogLoader.LoadEmbedded().Catalog!;

    private static ScanSnapshot Snapshot(string? chassisCode, string sysManufacturer, string sysModel, string? boardManufacturer, string? boardProduct, int lanCount = 1)
    {
        var now = DateTimeOffset.UtcNow;
        Measurement M(string name, MeasurementValue value) => new(name, value, null, "fixture", now, MeasurementQuality.Observed);
        var info = new List<Measurement> { M(SystemInfoProbeContract.MANUFACTURER, new TextValue(sysManufacturer)), M(SystemInfoProbeContract.MODEL, new TextValue(sysModel)) };
        if (chassisCode is not null) { info.Add(M(SystemInfoProbeContract.CHASSIS_TYPES, new TextListValue([chassisCode]))); }
        var details = new List<Measurement>();
        if (boardManufacturer is not null) { details.Add(M(SystemDetailsProbeContract.BOARD_MANUFACTURER, new TextValue(boardManufacturer))); }
        if (boardProduct is not null) { details.Add(M(SystemDetailsProbeContract.BOARD_PRODUCT, new TextValue(boardProduct))); }
        var drivers = new List<Measurement> { M(DeviceDriverProbeContract.CountName(DeviceDriverProbeContract.CATEGORY_LAN), new IntegerValue(lanCount)) };
        for (var i = 0; i < lanCount; i++)
        {
            drivers.Add(M(DeviceDriverProbeContract.ItemName(DeviceDriverProbeContract.CATEGORY_LAN, i, DeviceDriverProbeContract.FIELD_NAME), new TextValue("Intel(R) Ethernet Controller I226-V")));
            drivers.Add(M(DeviceDriverProbeContract.ItemName(DeviceDriverProbeContract.CATEGORY_LAN, i, DeviceDriverProbeContract.FIELD_VERSION), new TextValue("2.1.4.3")));
            drivers.Add(M(DeviceDriverProbeContract.ItemName(DeviceDriverProbeContract.CATEGORY_LAN, i, DeviceDriverProbeContract.FIELD_DATE), new TextValue("2025-03-01")));
        }
        foreach (var c in new[] { DeviceDriverProbeContract.CATEGORY_CHIPSET, DeviceDriverProbeContract.CATEGORY_WIFI, DeviceDriverProbeContract.CATEGORY_AUDIO }) { drivers.Add(M(DeviceDriverProbeContract.CountName(c), new IntegerValue(0))); }
        return new(Guid.NewGuid(), [
            new(SystemInfoProbeContract.PROBE_ID, ProbeStatus.Success, info, [], now, TimeSpan.Zero, new("fixture", true)),
            new(SystemDetailsProbeContract.PROBE_ID, ProbeStatus.Success, details, [], now, TimeSpan.Zero, new("fixture", true)),
            new(DeviceDriverProbeContract.PROBE_ID, ProbeStatus.Success, drivers, [], now, TimeSpan.Zero, new("fixture", true))]);
    }

    /// <summary>데스크톱은 메인보드 제조사·제품명을, 노트북은 시스템 제조사·모델을 씁니다. OEM 링크는 표의 공식 지원 첫 화면입니다.</summary>
    [Fact]
    public void IdentityFollowsChassisKind()
    {
        var desktop = DriverGuideBuilder.Build(Snapshot("3", "System manufacturer", "System Product Name", "ASUSTeK COMPUTER INC.", "ROG STRIX B650E-F GAMING WIFI"), Links);
        Assert.Equal(ChassisKind.Desktop, desktop.Chassis); Assert.Equal("ASUSTeK COMPUTER INC.", desktop.Manufacturer); Assert.Equal("ROG STRIX B650E-F GAMING WIFI", desktop.Model);
        Assert.NotNull(desktop.SupportLink); Assert.True(Links.IsAllowedLink(desktop.SupportLink)); Assert.Contains("ASUSTeK COMPUTER INC. ROG STRIX", desktop.ModelForCopy);
        var laptop = DriverGuideBuilder.Build(Snapshot("10", "LENOVO", "82XV", "LENOVO", "LNVNB161216"), Links);
        Assert.Equal(ChassisKind.Laptop, laptop.Chassis); Assert.Equal("82XV", laptop.Model); Assert.Contains(laptop.Notes, n => n.Contains("노트북"));
        Assert.Equal(4, laptop.Items.Count); Assert.Equal([1, 2, 3, 4], laptop.Items.Select(i => i.InstallOrder));
        var lan = laptop.Items.Single(i => i.Category == DeviceDriverProbeContract.CATEGORY_LAN);
        Assert.Single(lan.Installed); Assert.StartsWith("설치됨", lan.StatusText); Assert.Contains("2.1.4.3", lan.StatusText);
        Assert.Contains("찾지 못했어요", laptop.Items.Single(i => i.Category == DeviceDriverProbeContract.CATEGORY_WIFI).StatusText);
    }

    /// <summary>표에 없는 제조사는 링크 없이 모델명 복사 안내를 넣고, 검사 전에는 안내만 있는 빈 모델입니다.</summary>
    [Fact]
    public void UnknownManufacturerAndNoScanAreHonest()
    {
        var unknown = DriverGuideBuilder.Build(Snapshot("3", "Some OEM", "X1", "Unknown Boards Ltd", "UB-1"), Links);
        Assert.Null(unknown.SupportLink); Assert.Contains(unknown.Notes, n => n.Contains("모델명을 복사"));
        var none = DriverGuideBuilder.Build(null, Links);
        Assert.False(none.HasIdentity); Assert.Equal("", none.ModelForCopy); Assert.Contains(none.Notes, n => n.Contains("검사를 실행"));
        Assert.True(none.GpuLinks.Count >= 3);
    }

    /// <summary>NET은 가상 어댑터를 빼고 유선/무선으로, MEDIA는 그래픽카드 HDMI 오디오를 빼고, SYSTEM은 Intel/AMD 플랫폼 장치만 칩셋으로 분류합니다.</summary>
    [Theory]
    [InlineData("NET", "Intel(R) Ethernet Controller I226-V", "Intel", "lan")]
    [InlineData("NET", "Intel(R) Wi-Fi 6E AX210 160MHz", "Intel", "wifi")]
    [InlineData("NET", "Hyper-V Virtual Ethernet Adapter", "Microsoft", null)]
    [InlineData("NET", "WAN Miniport (IP)", "Microsoft", null)]
    [InlineData("MEDIA", "Realtek High Definition Audio", "Realtek", "audio")]
    [InlineData("MEDIA", "NVIDIA High Definition Audio", "NVIDIA", null)]
    [InlineData("SYSTEM", "AMD SMBus", "Advanced Micro Devices, Inc", "chipset")]
    [InlineData("SYSTEM", "Intel(R) PCI Express Root Port #5 - 7AB4", "Intel", "chipset")]
    [InlineData("SYSTEM", "Microsoft ACPI-Compliant System", "Microsoft", null)]
    [InlineData("DISPLAY", "NVIDIA GeForce RTX 4080 SUPER", "NVIDIA", null)]
    public void ClassifierMapsKnownDevices(string deviceClass, string name, string provider, string? expected)
        => Assert.Equal(expected, DeviceDriverProbe.Classify(deviceClass, name, provider));

    /// <summary>WMI 날짜에서 날짜 부분만 ISO로 바꾸고 형식이 다르면 null입니다.</summary>
    [Fact]
    public void WmiDateIsReducedToIsoDate()
    {
        Assert.Equal("2025-03-01", DeviceDriverProbe.ParseDate("20250301000000.000000+000"));
        Assert.Null(DeviceDriverProbe.ParseDate("2025-03")); Assert.Null(DeviceDriverProbe.ParseDate(null));
    }

    /// <summary>모델명 복사와 지원 페이지 열기는 검사 결과가 있을 때만 가능하며 허용 목록 URL만 엽니다.</summary>
    [Fact]
    public void ViewModelCopiesModelAndOpensAllowedLinks()
    {
        var copied = new List<string>(); var opened = new List<string>();
        var vm = new DriverGuideViewModel(Links, new FakeClipboard(copied), url => { opened.Add(url); return true; });
        Assert.False(vm.CopyModelCommand.CanExecute(null)); Assert.False(vm.OpenSupportCommand.CanExecute(null)); Assert.Contains("검사", vm.IdentityText);
        vm.Refresh(Snapshot("3", "System manufacturer", "System Product Name", "Micro-Star International Co., Ltd.", "MAG B650 TOMAHAWK WIFI (MS-7D75)"));
        Assert.True(vm.CopyModelCommand.CanExecute(null)); vm.CopyModelCommand.Execute(null);
        Assert.Equal("Micro-Star International Co., Ltd. MAG B650 TOMAHAWK WIFI (MS-7D75)", Assert.Single(copied));
        vm.OpenSupportCommand.Execute(null); Assert.True(Links.IsAllowedLink(Assert.Single(opened)));
        vm.OpenGpuLinkCommand.Execute(vm.GpuLinks[0]); Assert.Equal(2, opened.Count); Assert.All(opened, u => Assert.True(Links.IsAllowedLink(u)));
        vm.Refresh(null); Assert.False(vm.CopyModelCommand.CanExecute(null));
    }

    private sealed class FakeClipboard(List<string> copied) : IClipboard
    {
        public void SetText(string text) => copied.Add(text);
    }
}
