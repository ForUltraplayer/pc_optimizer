/**
 * @file    : TroubleshootingViewModel.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함 화면 모델. 증상 선택 → 권장 순서의 도구 카드(언제/무엇/주의·안전 수준·절차), 직접 실행의 진행 출력·결과·재부팅 안내, 내장 도구 열기, 공식 링크 열기
 */
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Troubleshooting;
using PcOptimizer.Probes.Troubleshooting;

namespace PcOptimizer.App.ViewModels;

/// <summary>증상 목록 항목입니다. "all"은 모든 도구를 분류별로 보여 줍니다. 선택 상태는 칩 강조에 씁니다.</summary>
public sealed partial class SymptomItem(string id, string title, string summary) : ObservableObject
{
    /// <summary>증상 ID.</summary>
    public string Id { get; } = id;
    /// <summary>증상 제목(사용자 말투).</summary>
    public string Title { get; } = title;
    /// <summary>권장 순서 요약.</summary>
    public string Summary { get; } = summary;
    /// <summary>현재 선택된 증상인지.</summary>
    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>도구 카드 하나입니다. 실행은 부모 화면 모델이 한 번에 하나만 합니다.</summary>
public sealed partial class ToolCardViewModel : ObservableObject
{
    private readonly TroubleshootingViewModel _owner;
    internal ToolCardViewModel(TroubleshootingViewModel owner, TroubleshootingTool tool, string? note, int order)
    {
        _owner = owner; Tool = tool; Note = note; Order = order;
        Steps = tool.Steps.Select((s, i) => $"{i + 1}. {s}").ToArray();
    }
    /// <summary>도구 정의입니다.</summary>
    public TroubleshootingTool Tool { get; }
    /// <summary>증상 절차에서의 순서(모든 도구 보기는 0).</summary>
    public int Order { get; }
    /// <summary>증상별 사용 이유(모든 도구 보기는 null).</summary>
    public string? Note { get; }
    /// <summary>순서 표기입니다.</summary>
    public string OrderText => Order > 0 ? $"{Order}단계" : Tool.Category;
    /// <summary>제목입니다.</summary>
    public string Title => Tool.Name;
    /// <summary>번호를 붙인 절차입니다.</summary>
    public IReadOnlyList<string> Steps { get; }
    /// <summary>절차 첫 줄에 두는 경고입니다.</summary>
    public string? Warning => Tool.Warning;
    /// <summary>경고 표시 여부입니다.</summary>
    public bool HasWarning => Tool.Warning is not null;
    /// <summary>증상 메모 표시 여부입니다.</summary>
    public bool HasNote => Note is not null;
    /// <summary>안전 수준 배지 문구입니다.</summary>
    public string SafetyText => Tool.Safety switch { SafetyLevel.Safe => "안전", SafetyLevel.Caution => "주의", _ => "되돌릴 수 없음" };
    /// <summary>실행 방식 설명입니다.</summary>
    public string ModeText => Tool.Mode switch
    {
        ToolMode.DirectCommand => "이 앱이 Windows 명령을 고정 인자로 실행하고 결과를 보여 줍니다.",
        ToolMode.BuiltInTool => "Windows에 들어 있는 도구를 엽니다.",
        _ => "외부 도구입니다. 공식 배포처만 열고 파일을 대신 받지 않습니다. 버전은 직접 최신을 고르세요.",
    };
    /// <summary>재부팅 안내입니다.</summary>
    public string RebootText => Tool.RebootRequired ? "완료 후 다시 시작이 필요합니다." : "다시 시작 없이 끝납니다.";
    /// <summary>주 버튼 문구입니다.</summary>
    public string ActionText => Tool.Mode switch { ToolMode.DirectCommand => "실행", ToolMode.BuiltInTool => "열기", _ => "공식 사이트 열기" };
    /// <summary>절차 펼침 상태입니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepsButtonText))]
    private bool _showSteps;
    /// <summary>절차 버튼 문구입니다.</summary>
    public string StepsButtonText => ShowSteps ? "절차 접기" : "사용 절차 보기";
    /// <summary>절차 펼침/접기.</summary>
    [RelayCommand]
    private void ToggleSteps() => ShowSteps = !ShowSteps;
    /// <summary>주 동작(실행/열기/링크).</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task ActAsync() => _owner.ActAsync(this);
    private bool CanAct() => !_owner.IsRunning;
    internal void RefreshCanAct() => ActCommand.NotifyCanExecuteChanged();
}

/// <summary>실행은 공용 작업 관문을 거쳐 검사·조치와 겹치지 않습니다. 출력에 개인 경로가 있어도 화면 밖(파일)에는 남기지 않습니다.</summary>
public sealed partial class TroubleshootingViewModel : ObservableObject, IDisposable
{
    /// <summary>모든 도구 보기의 증상 ID입니다.</summary>
    public const string ALL_TOOLS_ID = "all";
    /// <summary>화면에 남기는 출력 줄 상한입니다.</summary>
    public const int MAX_OUTPUT_LINES = 400;
    private const string LOG_CATEGORY = nameof(TroubleshootingViewModel);
    private readonly TroubleshootingCatalog _catalog;
    private readonly TroubleshootingService _service;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<string, bool> _openLink;
    private readonly Func<string, bool> _openSettings;
    private readonly VendorLinkCatalog? _links;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _running;
    private bool _disposed;

    /// <summary>카탈로그와 실행 창구를 연결합니다.</summary>
    public TroubleshootingViewModel(TroubleshootingCatalog catalog, TroubleshootingService service, IUiDispatcher dispatcher,
        Func<string, bool> openLink, Func<string, bool> openSettings, VendorLinkCatalog? links, IAppLogger? logger = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _openLink = openLink ?? throw new ArgumentNullException(nameof(openLink));
        _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        _links = links;
        _logger = logger ?? NullAppLogger.Instance;
        Symptoms = [.. catalog.Symptoms.Select(s => new SymptomItem(s.Id, s.Title, s.Summary)), new(ALL_TOOLS_ID, "모든 도구 보기", "분류별로 모든 도구를 보여 줍니다. 증상을 모르면 위에서 고르세요.")];
        SelectedSymptom = Symptoms[0];
    }

    /// <summary>증상 목록(마지막은 모든 도구).</summary>
    public IReadOnlyList<SymptomItem> Symptoms { get; }
    /// <summary>선택한 증상입니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SymptomSummary), nameof(SymptomTitle))]
    private SymptomItem? _selectedSymptom;
    partial void OnSelectedSymptomChanged(SymptomItem? value)
    {
        foreach (var item in Symptoms) { item.IsSelected = ReferenceEquals(item, value); }
        Cards = Build(value?.Id);
    }
    /// <summary>선택한 증상의 제목입니다.</summary>
    public string SymptomTitle => SelectedSymptom?.Title ?? "";
    /// <summary>선택한 증상의 요약입니다.</summary>
    public string SymptomSummary => SelectedSymptom?.Summary ?? "";
    /// <summary>선택한 증상의 권장 순서 카드입니다. 선택이 바뀔 때만 다시 만들어 화면과 같은 인스턴스를 유지합니다.</summary>
    [ObservableProperty]
    private IReadOnlyList<ToolCardViewModel> _cards = [];
    private List<ToolCardViewModel> Build(string? id)
    {
        if (id is null) { return []; }
        if (id == ALL_TOOLS_ID) { return _catalog.Tools.Select(t => new ToolCardViewModel(this, t, null, 0)).ToList(); }
        var symptom = _catalog.Symptoms.FirstOrDefault(s => s.Id == id);
        return symptom is null ? [] : symptom.Steps.Select((step, i) => (Tool: _catalog.FindTool(step.ToolId), step.Note, Order: i + 1))
            .Where(x => x.Tool is not null).Select(x => new ToolCardViewModel(this, x.Tool!, x.Note, x.Order)).ToList();
    }
    /// <summary>증상 선택 명령입니다.</summary>
    [RelayCommand]
    private void SelectSymptom(SymptomItem? item) { if (item is not null) { SelectedSymptom = item; } }

    /// <summary>직접 실행이 진행 중인지.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isRunning;
    /// <summary>진행 중인 도구 이름입니다.</summary>
    [ObservableProperty]
    private string _runningTitle = "";
    /// <summary>화면 안내입니다.</summary>
    [ObservableProperty]
    private string _status = "증상을 고르면 권장 순서대로 도구를 보여 줍니다. 각 도구의 '사용 절차 보기'를 먼저 읽으세요.";
    /// <summary>출력 줄(상한 적용).</summary>
    public ObservableCollection<string> Output { get; } = [];
    /// <summary>출력 영역 표시 여부입니다.</summary>
    public bool HasOutput => IsRunning || Output.Count > 0;
    /// <summary>마지막 결과 문구입니다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private string? _resultText;
    /// <summary>결과 표시 여부입니다.</summary>
    public bool HasResult => ResultText is not null;
    /// <summary>마지막 결과가 성공인지.</summary>
    [ObservableProperty]
    private bool _resultSucceeded;
    /// <summary>재부팅 안내 표시 여부입니다.</summary>
    [ObservableProperty]
    private bool _showRebootNote;

    internal async Task ActAsync(ToolCardViewModel card)
    {
        var tool = card.Tool;
        switch (tool.Mode)
        {
            case ToolMode.ExternalGuide:
                var url = _links?.Entries.FirstOrDefault(e => e.Id == tool.LinkId)?.Url;
                Status = url is not null && _openLink(url) ? $"{tool.Name}의 공식 사이트를 브라우저로 열었어요. 파일은 직접 받으세요." : "공식 사이트를 열지 못했어요. 링크 표를 확인하세요.";
                return;
            case ToolMode.BuiltInTool:
                Status = _service.OpenBuiltIn(tool.OpenTarget!, _openSettings) ? $"{tool.Name}을(를) 열었어요. 절차를 따라 진행하세요." : $"{tool.Name}을(를) 열지 못했어요. 이 Windows 버전에 없거나 실행이 막혔어요.";
                return;
            default:
                await RunAsync(tool).ConfigureAwait(false);
                return;
        }
    }

    private async Task RunAsync(TroubleshootingTool tool)
    {
        if (IsRunning || _disposed) { return; }
        using var cts = new CancellationTokenSource();
        _running = cts;
        await _dispatcher.InvokeAsync(() =>
        {
            Output.Clear(); ResultText = null; ShowRebootNote = false; RunningTitle = tool.Name; IsRunning = true;
            Status = $"{tool.Name} 실행 중… 끝날 때까지 이 창을 두세요. 취소하면 명령을 강제로 멈춥니다.";
            foreach (var c in Cards) { c.RefreshCanAct(); }
        });
        var progress = new Progress<string>(line => _ = _dispatcher.InvokeAsync(() =>
        {
            if (Output.Count >= MAX_OUTPUT_LINES) { Output.RemoveAt(0); }
            Output.Add(line); OnPropertyChanged(nameof(HasOutput));
        }));
        RepairCommandResult result;
        try { result = await _service.RunAsync(tool.Command!, progress, cts.Token).ConfigureAwait(false); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _logger.Warn(LOG_CATEGORY, $"RunFailure type={ex.GetType().Name}");
            result = new(tool.Command!, false, null, RepairCommandRunner.CODE_FAILED, string.Empty, tool.RebootRequired);
        }
        _running = null;
        await _dispatcher.InvokeAsync(() =>
        {
            IsRunning = false;
            ResultSucceeded = result.Succeeded;
            ResultText = Describe(tool, result);
            ShowRebootNote = result.Succeeded && result.RebootRequired;
            Status = result.Succeeded ? "완료했어요. 아래 결과와 절차의 다음 단계를 확인하세요." : "끝나지 않았어요. 아래 결과를 확인하세요.";
            OnPropertyChanged(nameof(HasOutput));
            foreach (var c in Cards) { c.RefreshCanAct(); }
        });
    }

    private static string Describe(TroubleshootingTool tool, RepairCommandResult result) => result.Code switch
    {
        RepairCommandRunner.CODE_COMPLETED => $"{tool.Name}: 정상 완료" + (result.ExitCode is { } c && c != 0 ? $" (코드 {c})" : ""),
        RepairCommandRunner.CODE_FAILED => $"{tool.Name}: 오류로 끝났어요" + (result.ExitCode is { } e ? $" (종료 코드 {e})" : "") + ". 출력의 마지막 줄을 확인하고, 절차의 오류 항목을 따르세요.",
        RepairCommandRunner.CODE_CANCELLED => $"{tool.Name}: 취소했어요. 중간에 멈춘 작업은 다시 실행하면 이어서 검사합니다.",
        RepairCommandRunner.CODE_TIMED_OUT => $"{tool.Name}: 시간 상한을 넘어 멈췄어요. 디스크가 느리거나 손상이 많을 수 있어요.",
        RepairCommandRunner.CODE_BUSY => "지금은 검사나 다른 조치가 진행 중이라 실행하지 않았어요. 끝난 뒤 다시 누르세요.",
        RepairCommandRunner.CODE_EXECUTABLE_MISSING => $"{tool.Name}: 이 Windows에 해당 명령이 없어요.",
        TroubleshootingService.CODE_PROTECTION_DISABLED => "시스템 보호가 꺼져 있어 복원 지점을 만들 수 없어요. 절차의 안내대로 보호를 켠 뒤 다시 실행하세요.",
        _ => $"{tool.Name}: 실행하지 못했어요 ({result.Code}).",
    };

    /// <summary>진행 중인 명령을 강제로 멈춥니다.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _running?.Cancel();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _running?.Cancel();
    }
}
