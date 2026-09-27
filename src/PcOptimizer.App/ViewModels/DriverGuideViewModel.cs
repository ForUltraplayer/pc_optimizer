/**
 * @file    : DriverGuideViewModel.cs
 * @author  : rudals252
 * @brief   : 드라이버 안내 화면 모델(스펙 §5). 검사 결과에서 제조사·모델·설치 드라이버를 채우고, 모델명 복사·제조사 지원 페이지·그래픽 공식 링크를 허용 목록으로만 연다
 */
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.ViewModels;

/// <summary>그래픽 공식 링크 버튼 하나입니다.</summary>
public sealed record GpuLinkItem(string Label, string Url);

/// <summary>최신 여부는 판단하지 않고 "어디서 무엇을 받을지"만 안내합니다.</summary>
public sealed partial class DriverGuideViewModel : ObservableObject
{
    private readonly VendorLinkCatalog? _links;
    private readonly IClipboard _clipboard;
    private readonly Func<string, bool> _openLink;

    /// <summary>링크 표·클립보드·링크 실행기를 연결합니다. 검사 전에는 안내 문구만 있는 빈 모델입니다.</summary>
    public DriverGuideViewModel(VendorLinkCatalog? links, IClipboard clipboard, Func<string, bool> openLink)
    {
        _links = links;
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _openLink = openLink ?? throw new ArgumentNullException(nameof(openLink));
        Guide = DriverGuideBuilder.Build(null, links);
    }

    /// <summary>현재 안내 모델입니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChassisText), nameof(IdentityText), nameof(HasIdentity), nameof(HasSupportLink), nameof(SupportLabel), nameof(Items), nameof(GpuLinks), nameof(Notes), nameof(HasGpuLinks))]
    [NotifyCanExecuteChangedFor(nameof(CopyModelCommand), nameof(OpenSupportCommand))]
    private DriverGuide _guide;
    /// <summary>화면 안내입니다.</summary>
    [ObservableProperty]
    private string _status = "검사 결과를 바탕으로 어디서 무엇을 받을지 알려 드려요.";

    /// <summary>검사 스냅샷으로 안내를 다시 만듭니다(null이면 검사 전 상태).</summary>
    public void Refresh(ScanSnapshot? snapshot)
    {
        Guide = DriverGuideBuilder.Build(snapshot, _links);
        Status = snapshot is null ? "먼저 메인 화면에서 검사를 실행하세요." : Guide.HasIdentity ? "아래 제조사 지원 페이지에서 네 가지 드라이버를 받으세요." : "검사에서 제조사·모델을 읽지 못했어요. CPU-Z의 Mainboard 탭으로 확인하세요.";
    }

    /// <summary>PC 종류 표기입니다.</summary>
    public string ChassisText => Guide.Chassis switch { ChassisKind.Laptop => "노트북", ChassisKind.Desktop => "데스크톱", _ => "종류 확인 불가" };
    /// <summary>제조사·모델 표기입니다.</summary>
    public string IdentityText => Guide.HasIdentity ? Guide.ModelForCopy : "검사 전이거나 읽지 못함";
    /// <summary>제조사·모델이 있는지.</summary>
    public bool HasIdentity => Guide.HasIdentity;
    /// <summary>지원 페이지 링크가 있는지.</summary>
    public bool HasSupportLink => Guide.SupportLink is not null;
    /// <summary>지원 페이지 버튼 문구입니다.</summary>
    public string SupportLabel => Guide.SupportLabel ?? "제조사 지원 페이지(표에 없음)";
    /// <summary>받을 드라이버 네 가지입니다.</summary>
    public IReadOnlyList<DriverGuideItem> Items => Guide.Items;
    /// <summary>그래픽 공식 링크입니다.</summary>
    public IReadOnlyList<GpuLinkItem> GpuLinks => Guide.GpuLinks.Select(l => new GpuLinkItem(l.Label, l.Url)).ToArray();
    /// <summary>그래픽 링크 표시 여부입니다.</summary>
    public bool HasGpuLinks => Guide.GpuLinks.Count > 0;
    /// <summary>안내 문장들입니다.</summary>
    public IReadOnlyList<string> Notes => Guide.Notes;

    /// <summary>"제조사 모델"을 클립보드에 복사합니다(제조사 사이트 검색창에 붙여 넣기).</summary>
    [RelayCommand(CanExecute = nameof(HasIdentity))]
    private void CopyModel()
    {
        _clipboard.SetText(Guide.ModelForCopy);
        Status = $"'{Guide.ModelForCopy}'을(를) 복사했어요. 제조사 사이트의 검색창에 붙여 넣으세요.";
    }

    /// <summary>제조사 공식 지원 페이지를 엽니다.</summary>
    [RelayCommand(CanExecute = nameof(HasSupportLink))]
    private void OpenSupport()
    {
        Status = _openLink(Guide.SupportLink!) ? "제조사 지원 페이지를 브라우저로 열었어요. 모델명으로 검색한 뒤 아래 네 가지를 받으세요." : "지원 페이지를 열지 못했어요.";
    }

    /// <summary>그래픽 공식 링크를 엽니다.</summary>
    [RelayCommand]
    private void OpenGpuLink(GpuLinkItem? item)
    {
        if (item is null) { return; }
        Status = _openLink(item.Url) ? $"{item.Label}을(를) 열었어요. 내 그래픽카드 모델을 고르고 설치 파일을 받으세요." : "링크를 열지 못했어요.";
    }
}
