/**
 * @file    : DisplayTrialViewModel.cs
 * @author  : rudals252
 * @brief   : 평가용 주사율 선택·실행 확인·유지·원복·중단 기록 복구 연결
 */
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Actions.Display;

namespace PcOptimizer.App.ViewModels;

/// <summary>화면의 시계는 표시용이며 실제 기한과 복구는 실행 워커가 소유합니다.</summary>
public sealed partial class DisplayTrialViewModel : ObservableObject, IDisposable
{
    private readonly DisplayTrialCoordinator _service;
    private readonly IOperationCoordinator _operations;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<Task> _rescan;
    private CancellationTokenSource? _cancellation;
    private Guid? _plan, _restore;
    private bool _disposed;
    /// <summary>검사와 같은 관문 및 앱 수명의 실행기를 연결합니다.</summary>
    public DisplayTrialViewModel(DisplayTrialCoordinator service, IOperationCoordinator operations, IUiDispatcher dispatcher, Func<Task> rescan)
    { _service = service; _operations = operations; _dispatcher = dispatcher; _rescan = rescan; _operations.Changed += OnOperationChanged; }
    /// <summary>검증한 현재 해상도의 주사율 후보입니다.</summary>
    public ObservableCollection<DisplayTrialChoice> Choices { get; } = [];
    /// <summary>중단 기록과 유지 후 되돌리기 기록입니다.</summary>
    public ObservableCollection<DisplayTrialRecord> Records { get; } = [];
    /// <summary>목록에서 선택한 값만 준비하며 바로 적용하지 않습니다.</summary>
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(PrepareCommand))]
    private DisplayTrialChoice? _selected;
    /// <summary>재열기 뒤에도 최근 결과를 보존합니다.</summary>
    [ObservableProperty] private string _status = "주사율 후보와 복구 기록을 확인하세요.";
    /// <summary>실행 전 현재 값·목표 값 또는 되돌리기 대상을 표시합니다.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPreview))]
    private string _preview = "";
    /// <summary>호출이 끝나기 전 창 종료로 원복 워커를 잃지 않도록 사용합니다.</summary>
    [ObservableProperty] private bool _isWorking;
    /// <summary>표시용 남은 시간이며 이 값이 지연돼도 실행기의 기한 검사는 유지됩니다.</summary>
    [ObservableProperty] private string _countdown = "";
    /// <summary>미리보기 존재 여부입니다.</summary>
    public bool HasPreview => Preview.Length > 0;
    private bool CanBegin() => !_disposed && !IsWorking && !_operations.State.IsBusy;
    private bool CanPrepare() => CanBegin() && Selected is not null;
    private bool CanExecute() => CanBegin() && (_plan is not null || _restore is not null);
    private bool CanDecide() => !_disposed && _service.Status is { State: "Waiting", Remaining: var remaining } && remaining > TimeSpan.Zero;
    private bool CanRestore(DisplayTrialRecord? record) => CanBegin() && record?.CanRestore == true;
    private void NotifyGates()
    {
        RefreshCommand.NotifyCanExecuteChanged(); PrepareCommand.NotifyCanExecuteChanged(); ExecuteCommand.NotifyCanExecuteChanged();
        KeepCommand.NotifyCanExecuteChanged(); RevertCommand.NotifyCanExecuteChanged(); PrepareRestoreCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsWorkingChanged(bool value) => NotifyGates();
    private async void OnOperationChanged(object? sender, EventArgs e)
    { if (!_disposed) { await _dispatcher.InvokeAsync(() => { if (!_disposed) { NotifyGates(); } }); } }
    /// <summary>UI 표시를 갱신할 뿐 타임아웃을 연장하거나 원복을 수행하지 않습니다.</summary>
    public void Tick()
    {
        var trial = _service.Status;
        Countdown = trial is { State: "Waiting" } ? $"{Math.Ceiling(trial.Remaining.TotalSeconds):0}초 안에 [유지]를 누르지 않으면 원래 주사율로 복원합니다."
            : trial is null ? "" : "설정을 확인하고 있습니다. 실제 작업이 끝날 때까지 기다려 주세요.";
        KeepCommand.NotifyCanExecuteChanged(); RevertCommand.NotifyCanExecuteChanged();
    }
    /// <summary>창 닫기/앱 종료 시 취소를 요청하며 실제 원복 종료까지 기다리게 합니다.</summary>
    public void RequestClose()
    {
        _cancellation?.Cancel(); if (_service.Status is { } trial) { _service.Revert(trial.Id); }
        if (IsWorking) { Status = "중단·원복 결과를 확인한 뒤 창을 닫을 수 있습니다."; }
    }
    [RelayCommand(CanExecute = nameof(CanBegin))]
    private async Task Refresh()
    {
        IsWorking = true; _plan = null; _restore = null; Preview = ""; Selected = null;
        try { await LoadCatalog(); } finally { IsWorking = false; }
    }
    /// <summary>카드의 정확한 모니터/주사율을 다시 조회해 선택하고 사전 검사만 수행합니다. 이 호출은 화면을 변경하지 않습니다.</summary>
    public async Task PrepareFindingAsync(Finding finding)
    {
        if (!CanBegin()) { Status = "진행 중인 작업이 끝난 뒤 다시 시도하세요."; return; }
        var target = DisplayFindingTarget.Read(finding);
        _plan = null; _restore = null; Preview = ""; Selected = null;
        if (target is null) { Status = "이 검사 결과에서 시험 대상을 식별하지 못했습니다. 다시 검사하세요."; NotifyGates(); return; }
        await Refresh();
        Selected = Choices.SingleOrDefault(c => string.Equals(c.DeviceKey, target.DeviceKey, StringComparison.OrdinalIgnoreCase) && c.DesiredHz == target.Hz);
        if (Selected is null) { Status = "해당 모니터의 주사율 후보가 달라졌거나 지금은 지원되지 않습니다. 다시 검사하세요."; return; }
        if (CanPrepare()) { await Prepare(); }
    }
    private async Task LoadCatalog()
    {
        var catalog = await _service.InspectAsync(CancellationToken.None);
        Choices.Clear(); foreach (var choice in catalog.Choices) { Choices.Add(choice); }
        Records.Clear(); foreach (var record in catalog.Records) { Records.Add(record); }
        Status = catalog.Code == "Ready" ? Records.Any(r => r.NeedsRecovery) ? "중단된 시험 기록이 있습니다. 원복 대상을 확인해 주세요."
            : Choices.Count == 0 ? "시험 가능한 다른 주사율을 찾지 못했습니다. Windows 설정에서 확인하세요." : "주사율을 선택한 뒤 적용할 내용을 확인하세요."
            : Describe(catalog.Code);
    }
    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private async Task Prepare()
    {
        if (Selected is not { } choice) { return; }
        IsWorking = true; _restore = null; _plan = null; Preview = "";
        using var cts = new CancellationTokenSource(); _cancellation = cts;
        try
        {
            var prepared = await _service.PrepareAsync(choice.DeviceKey, choice.DesiredHz, cts.Token);
            _plan = prepared.Id;
            Preview = prepared.Id is null ? "" : $"{choice.Title}\n현재 {prepared.BeforeHz} Hz → 시험 {prepared.DesiredHz} Hz. 해상도는 유지합니다. 화면이 잠시 꺼질 수 있습니다.\n15초 안에 유지하지 않으면 원복을 시도합니다. 앱 강제 종료·PC 전원 차단 중에는 자동 원복을 보장하지 않습니다.";
            Status = prepared.Id is null ? Describe(prepared.Code) : "아직 적용하지 않았습니다. 화면과 목표 주사율을 확인한 뒤 시험을 시작하세요.";
        }
        finally { _cancellation = null; IsWorking = false; }
    }
    [RelayCommand(CanExecute = nameof(CanRestore))]
    private void PrepareRestore(DisplayTrialRecord? record)
    {
        if (record is null) { return; }
        _plan = null; _restore = record.Id; Preview = $"이 기록의 원래 주사율 {record.BeforeHz} Hz로 되돌립니다.\n현재 값과 장치가 기록과 일치할 때만 실행합니다. 화면이 잠시 꺼질 수 있습니다.";
        Status = "되돌릴 내용을 확인하고 실행하세요."; NotifyGates();
    }
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task Execute()
    {
        var plan = _plan; var restore = _restore;
        if (plan is null && restore is null) { return; }
        _plan = null; _restore = null; Preview = ""; IsWorking = true;
        using var cts = new CancellationTokenSource(); _cancellation = cts;
        try
        {
            Status = restore is null ? "주사율을 시험 적용합니다. 화면이 보이면 [유지]를 눌러 주세요." : "원래 주사율로 복원 중입니다.";
            var result = restore is { } id ? await _service.RestoreAsync(id, cts.Token) : await _service.StartAsync(plan!.Value, cts.Token);
            await LoadCatalog();
            Status = result.Kept ? "선택한 주사율을 유지하고 프로필에 저장했습니다."
                : result.Restored ? $"원래 표시 설정을 확인했습니다. {Describe(result.Code)}"
                : result.Started ? $"표시 설정의 복원을 확인하지 못했습니다. Windows 디스플레이 설정에서 확인하세요. {Describe(result.Code)}"
                : $"표시 설정을 변경하지 않았습니다. {Describe(result.Code)}";
            if (result.Started)
            {
                try { await _rescan(); } catch { Status += " 진단 재검사는 완료하지 못했습니다. 다시 검사해 주세요."; }
            }
        }
        finally { _cancellation = null; IsWorking = false; Tick(); }
    }
    [RelayCommand(CanExecute = nameof(CanDecide))]
    private void Keep() { if (_service.Status is { } trial) { _service.Keep(trial.Id); } Tick(); }
    [RelayCommand(CanExecute = nameof(CanDecide))]
    private void Revert() { if (_service.Status is { } trial) { _service.Revert(trial.Id); } Tick(); }
    private static string Describe(string code) => code switch
    {
        "Restored" or "Reverted" => "복원을 마쳤습니다.", "TrialExpired" => "유지 기한이 지났습니다.", "Cancelled" => "중단 요청을 처리했습니다.",
        "Busy" => "다른 작업이 끝난 뒤 다시 시도하세요.", "ScopeExcluded" or "SessionChanged" => "로그인 사용자와 실행 계정을 확인할 수 없어 실행하지 않습니다.",
        "CurrentValueChanged" => "외부에서 표시 설정이 바뀌어 덮어쓰지 않았습니다.", "DisplayDeviceChanged" => "모니터 연결이 바뀌었습니다. 다시 확인하세요.",
        "DisplayUnsupported" => "이 출력 경로나 표시 모드는 자동 시험 대상이 아닙니다.", "PlanExpired" => "확인 내용이 만료됐습니다. 다시 준비하세요.",
        "DisplayTestFailed" => "드라이버가 선택한 모드의 사전 시험을 거절했습니다.", "DisplayApplyFailed" => "시험 적용을 완료하지 못했습니다.",
        "DisplaySaveFailed" => "주사율 영구 저장을 완료하지 못했습니다.", "DisplayVerificationFailed" => "실제 표시 상태가 요청한 값과 일치하지 않습니다.",
        "RecoveryRecordFailed" or "RecordsUnavailable" => "복구 기록을 완전히 확인하거나 저장하지 못했습니다. 설정과 기록을 다시 확인하세요.",
        "RecoveryRequired" => "중단된 주사율 시험을 먼저 복구해야 새 시험을 시작할 수 있습니다.",
        "AlreadyApplied" => "이미 선택한 주사율입니다.", _ => "작업을 완료하지 못했습니다. 설정과 복구 기록을 확인하세요."
    };
    /// <summary>새 UI 명령만 중단합니다. 이미 시작한 원복은 워커에서 계속합니다.</summary>
    public void Dispose() { if (_disposed) { return; } RequestClose(); _disposed = true; _operations.Changed -= OnOperationChanged; }
}
