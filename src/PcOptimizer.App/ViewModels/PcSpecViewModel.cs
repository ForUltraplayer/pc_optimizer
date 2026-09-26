/**
 * @file    : PcSpecViewModel.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 화면 모델(새로 고침·한 열 줄 목록·기본 익명화 토글·텍스트 복사·TXT(UTF-8, BOM 없음)·PNG(96 DPI) 저장, 검사 중 새로 고침 막기)과 섹션·항목·확인 불가 안내 줄 모델
 */

// 기본 패키지
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// 서드파티 패키지
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// 사용자 패키지
using PcOptimizer.App.Models;
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 내 PC 사양 화면 모델입니다. 화면은 fastfetch처럼 한 열로 <see cref="HeaderLines"/>와 <see cref="Lines"/>를 나열하며,
/// 줄 구성은 <see cref="PcSpecTextFormatter.Format"/> 출력(빈 구분 줄 제외)과 같습니다. 식별 정보 포함은 기본 꺼짐입니다.
/// 사양 프로브는 검사와 공유하므로 검사 중에는 <see cref="SetBusy"/>로 새로 고침을 막습니다.
/// </summary>
public sealed partial class PcSpecViewModel : ObservableObject
{
    private const string LOG_CATEGORY = nameof(PcSpecViewModel);
    private const string FILE_NAME_PREFIX = "pc-spec-";
    private const string FILE_DATE_FORMAT = "yyyyMMdd";
    private const string TEXT_EXTENSION = ".txt";
    private const string IMAGE_EXTENSION = ".png";
    private const double IMAGE_DPI = 96;

    private static readonly UTF8Encoding UTF8_NO_BOM = new(encoderShouldEmitUTF8Identifier: false);

    private readonly PcSpecService _service;
    private readonly PcSpecTextFormatter _formatter;
    private readonly IClipboard _clipboard;
    private readonly IExportPathPicker _picker;
    private readonly Func<FrameworkElement?> _captureTargetProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLogger _logger;
    private readonly string? _machineName;
    private readonly string? _userName;

    /// <summary>사양을 읽는 중인지 여부.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(CopyTextCommand), nameof(SaveTextCommand), nameof(SaveImageCommand))]
    private bool _isLoading;

    /// <summary>PC 이름·사용자명·식별 항목을 포함할지 여부(기본 false, 공유용 익명화).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdentityMarker))]
    private bool _includeIdentity;

    /// <summary>최근 동작 결과 안내(없으면 null).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    /// <summary>현재 토글 기준의 공유용 텍스트(스냅샷이 없으면 null).</summary>
    [ObservableProperty]
    private string? _capturedText;

    /// <summary>머리글 줄("PC 사양 · 시각 · 익명화 표기", 식별 정보 포함 시 PC 이름·사용자명 줄).</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _headerLines = [];

    /// <summary>
    /// 사양 화면 모델을 만듭니다.
    /// </summary>
    /// <param name="service">사양 스냅샷 서비스.</param>
    /// <param name="formatter">텍스트 형식기.</param>
    /// <param name="clipboard">클립보드.</param>
    /// <param name="picker">저장 경로 선택기.</param>
    /// <param name="captureTargetProvider">이미지로 렌더할 요소 공급자(뷰의 CaptureRoot, 아직 없으면 null).</param>
    /// <param name="dispatcher">UI 스레드 마샬러.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="machineName">PC 이름(식별 정보 포함 시에만 표시).</param>
    /// <param name="userName">사용자명(식별 정보 포함 시에만 표시).</param>
    public PcSpecViewModel(
        PcSpecService service,
        PcSpecTextFormatter formatter,
        IClipboard clipboard,
        IExportPathPicker picker,
        Func<FrameworkElement?> captureTargetProvider,
        IUiDispatcher dispatcher,
        IAppLogger logger,
        string? machineName,
        string? userName)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(captureTargetProvider);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _service = service;
        _formatter = formatter;
        _clipboard = clipboard;
        _picker = picker;
        _captureTargetProvider = captureTargetProvider;
        _dispatcher = dispatcher;
        _logger = logger;
        _machineName = machineName;
        _userName = userName;
    }

    /// <summary>마지막으로 읽은 사양 스냅샷(아직 없으면 null).</summary>
    public PcSpecSnapshot? Snapshot { get; private set; }

    /// <summary>사양 섹션(고정 순서).</summary>
    public ObservableCollection<PcSpecSectionViewModel> Sections { get; } = [];

    /// <summary>한 열로 나열할 줄(섹션 제목 → 항목 줄 또는 확인 불가 안내 줄).</summary>
    public ObservableCollection<object> Lines { get; } = [];

    /// <summary>검사가 진행 중이라 새로 고침을 막았는지 여부.</summary>
    public bool IsScanBusy { get; private set; }

    /// <summary>현재 토글의 익명화 표기(이미지 하단에도 표시).</summary>
    public string IdentityMarker => IncludeIdentity ? Strings.Spec_Identified : Strings.Spec_Anonymized;

    /// <summary>표시할 결과 안내가 있는지 여부.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>
    /// 검사 진행 여부를 알립니다. 사양 프로브를 검사와 공유하므로 검사 중에는 새로 고침을 막습니다(UI 스레드에서 호출).
    /// </summary>
    /// <param name="busy">검사 중이면 true.</param>
    public void SetBusy(bool busy)
    {
        if (IsScanBusy == busy)
        {
            return;
        }

        IsScanBusy = busy;
        OnPropertyChanged(nameof(IsScanBusy));
        RefreshCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 사양을 새로 읽어 섹션·줄을 다시 만듭니다. 실패는 형식 이름만 기록하고 안내합니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        PcSpecSnapshot? snapshot = null;
        string? failure = null;
        try
        {
            snapshot = await _service.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.Info(LOG_CATEGORY, $"SpecCaptured sections={snapshot.Sections.Count} unavailable={snapshot.UnavailableSections.Count}");
        }
        catch (Exception ex)
        {
            _logger.Warn(LOG_CATEGORY, $"SpecRefreshFailed error={ex.GetType().Name}");
            failure = DisplayText.Format(Strings.Spec_RefreshFailed, ex.GetType().Name);
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (snapshot is not null)
            {
                Snapshot = snapshot;
                OnPropertyChanged(nameof(Snapshot));
                RebuildLines();
            }

            StatusMessage = failure;
            IsLoading = false;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 현재 토글의 텍스트를 클립보드에 넣습니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseSnapshot))]
    private Task CopyTextAsync()
    {
        if (CurrentText() is not { } text)
        {
            return Task.CompletedTask;
        }

        try
        {
            _clipboard.SetText(text);
            StatusMessage = Strings.Spec_Copied;
        }
        catch (ExternalException ex)
        {
            _logger.Warn(LOG_CATEGORY, $"SpecCopyFailed error={ex.GetType().Name}");
            StatusMessage = DisplayText.Format(Strings.Spec_CopyFailed, ex.GetType().Name);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 현재 토글의 텍스트를 사용자가 고른 경로에 UTF-8(BOM 없음)로 저장합니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseSnapshot))]
    private async Task SaveTextAsync()
    {
        if (Snapshot is not { } snapshot || CurrentText() is not { } text)
        {
            return;
        }

        var path = _picker.PickSavePath(SuggestFileName(snapshot, TEXT_EXTENSION));
        if (path is null)
        {
            return;
        }

        string message;
        try
        {
            await File.WriteAllTextAsync(path, text, UTF8_NO_BOM).ConfigureAwait(false);
            message = DisplayText.Format(Strings.Spec_Saved, Path.GetFileName(path));
            _logger.Info(LOG_CATEGORY, "SpecTextSaved");
        }
        catch (Exception ex) when (IsSaveFailure(ex))
        {
            message = DisplayText.Format(Strings.Spec_SaveFailed, ex.GetType().Name);
            _logger.Warn(LOG_CATEGORY, $"SpecTextSaveFailed error={ex.GetType().Name}");
        }

        await _dispatcher.InvokeAsync(() => StatusMessage = message).ConfigureAwait(false);
    }

    /// <summary>
    /// 뷰의 CaptureRoot(하단 익명화 표기 포함)를 96 DPI PNG로 렌더해 사용자가 고른 경로에 저장합니다. 렌더는 UI 스레드에서 합니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseSnapshot))]
    private async Task SaveImageAsync()
    {
        if (Snapshot is not { } snapshot)
        {
            return;
        }

        var target = _captureTargetProvider();
        if (target is null || target.ActualWidth <= 0 || target.ActualHeight <= 0)
        {
            StatusMessage = Strings.Spec_ImageUnavailable;
            return;
        }

        var path = _picker.PickSavePath(SuggestFileName(snapshot, IMAGE_EXTENSION));
        if (path is null)
        {
            return;
        }

        string message;
        try
        {
            var png = RenderPng(target);
            await File.WriteAllBytesAsync(path, png).ConfigureAwait(false);
            message = DisplayText.Format(Strings.Spec_Saved, Path.GetFileName(path));
            _logger.Info(LOG_CATEGORY, "SpecImageSaved");
        }
        catch (Exception ex) when (IsSaveFailure(ex) || ex is InvalidOperationException or OutOfMemoryException or ExternalException)
        {
            message = DisplayText.Format(Strings.Spec_SaveFailed, ex.GetType().Name);
            _logger.Warn(LOG_CATEGORY, $"SpecImageSaveFailed error={ex.GetType().Name}");
        }

        await _dispatcher.InvokeAsync(() => StatusMessage = message).ConfigureAwait(false);
    }

    /// <summary>새로 고칠 수 있는지 여부(읽는 중·검사 중이 아닐 때).</summary>
    private bool CanRefresh() => !IsLoading && !IsScanBusy;

    /// <summary>스냅샷을 복사·저장할 수 있는지 여부.</summary>
    private bool CanUseSnapshot() => Snapshot is not null && !IsLoading;

    /// <summary>
    /// 토글이 바뀌면 머리글·줄·텍스트를 다시 만든다.
    /// </summary>
    partial void OnIncludeIdentityChanged(bool value) => RebuildLines();

    /// <summary>
    /// 현재 토글의 텍스트(스냅샷이 없으면 null).
    /// </summary>
    private string? CurrentText()
    {
        return Snapshot is { } snapshot ? _formatter.Format(snapshot, IncludeIdentity, _machineName, _userName) : null;
    }

    /// <summary>
    /// 스냅샷과 토글로 섹션·한 열 줄 목록·머리글·텍스트를 다시 만든다(UI 스레드). 줄 구성은 텍스트 형식기와 같은 도우미를 쓴다.
    /// </summary>
    private void RebuildLines()
    {
        Sections.Clear();
        Lines.Clear();
        if (Snapshot is not { } snapshot)
        {
            HeaderLines = [];
            CapturedText = null;
            return;
        }

        HeaderLines = _formatter.HeaderLines(snapshot, IncludeIdentity, _machineName, _userName);
        foreach (var section in snapshot.Sections)
        {
            var unavailable = PcSpecTextFormatter.IsUnavailable(snapshot, section);
            var items = PcSpecTextFormatter.VisibleItems(section, IncludeIdentity).Select(item => new PcSpecItemViewModel(item.Label, item.Value)).ToList();
            var sectionModel = new PcSpecSectionViewModel(section.Title, items, unavailable);
            Sections.Add(sectionModel);
            Lines.Add(sectionModel);
            if (unavailable)
            {
                Lines.Add(new PcSpecNoteLineViewModel(Strings.Spec_SectionUnavailable));
                continue;
            }

            foreach (var item in items)
            {
                Lines.Add(item);
            }
        }

        CapturedText = _formatter.Format(snapshot, IncludeIdentity, _machineName, _userName);
    }

    /// <summary>
    /// 제안 파일 이름(pc-spec-yyyyMMdd.확장자, 캡처 시각의 현지 날짜).
    /// </summary>
    private static string SuggestFileName(PcSpecSnapshot snapshot, string extension)
    {
        return FILE_NAME_PREFIX + snapshot.CapturedAtUtc.ToLocalTime().ToString(FILE_DATE_FORMAT, CultureInfo.InvariantCulture) + extension;
    }

    /// <summary>
    /// 파일 저장 실패로 안내할 예외인지 여부.
    /// </summary>
    private static bool IsSaveFailure(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException;
    }

    /// <summary>
    /// 요소를 실제 크기·96 DPI로 PNG 바이트로 렌더한다. VisualBrush는 요소의 부모 안 오프셋을 포함해 그리므로,
    /// 보기 영역을 그 오프셋에서 시작하게 해 요소의 좌상단부터(하단 익명화 표기까지 잘리지 않게) 그린다.
    /// </summary>
    private static byte[] RenderPng(FrameworkElement target)
    {
        var width = target.ActualWidth;
        var height = target.ActualHeight;
        var bounds = new Rect(0, 0, width, height);
        var offset = VisualTreeHelper.GetOffset(target);
        var brush = new VisualBrush(target)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(offset.X, offset.Y, width, height),
        };
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(brush, null, bounds);
        }

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), IMAGE_DPI, IMAGE_DPI, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}

/// <summary>
/// 사양 섹션 줄 모델입니다(한 열 목록의 굵은 제목 줄 "[섹션]").
/// </summary>
/// <param name="Title">섹션 제목.</param>
/// <param name="Items">표시 항목(식별 정보 포함이 꺼져 있으면 식별 항목 제외).</param>
/// <param name="IsUnavailable">모든 항목 값이 없어 확인 불가인지 여부(항목 대신 안내 한 줄을 표시).</param>
public sealed record PcSpecSectionViewModel(string Title, IReadOnlyList<PcSpecItemViewModel> Items, bool IsUnavailable)
{
    /// <summary>제목 줄 표기("[운영체제]").</summary>
    public string HeaderText => PcSpecTextFormatter.SectionHeader(Title);
}

/// <summary>
/// 사양 항목 줄 모델입니다("라벨: 값", 값이 없으면 "확인 불가").
/// </summary>
/// <param name="Label">항목 라벨.</param>
/// <param name="Value">항목 값(null이면 확인 불가).</param>
public sealed record PcSpecItemViewModel(string Label, string? Value)
{
    /// <summary>라벨 표기("모델:").</summary>
    public string LabelText => PcSpecTextFormatter.LabelText(Label);

    /// <summary>값 표기(null이면 <see cref="Strings.Spec_ValueUnknown"/>).</summary>
    public string ValueText => PcSpecTextFormatter.ValueText(Value);
}

/// <summary>
/// 확인 불가 섹션의 안내 줄 모델입니다("확인 불가 — dxdiag 또는 HWiNFO64로 확인").
/// </summary>
/// <param name="Text">안내 문구.</param>
public sealed record PcSpecNoteLineViewModel(string Text);
