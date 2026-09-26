/**
 * @file    : CacheToolsViewModel.cs
 * @author  : rudals252
 * @brief   : 공식 도구 캐시 정리의 선택·미리보기·확인·실행·후속 관측 화면
 */
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Resources;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>사용자 확인 뒤에만 캐시 도구를 실행합니다.</summary>
public sealed partial class CacheToolsViewModel : ObservableObject
{
    private readonly CacheCleanupService _service;
    private readonly Func<string, bool> _confirm;
    private CacheCleanupPlan? _plan;

    /// <summary>선택 도구 목록입니다.</summary>
    public IReadOnlyList<string> Tools { get; } = ["npm", "pip", "NuGet HTTP"];

    [ObservableProperty]
    private int _selectedTool;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareCommand), nameof(ClearCommand))]
    [NotifyPropertyChangedFor(nameof(CanSelectTool))]
    private bool _isBusy;

    [ObservableProperty]
    private string _message = Strings.Cleanup_Intro;

    /// <summary>실행을 시도한 뒤에는 메인 진단을 다시 읽습니다.</summary>
    public bool NeedsRescan { get; private set; }

    /// <summary>실행 중에는 도구 선택을 바꾸지 않습니다.</summary>
    public bool CanSelectTool => !IsBusy;

    /// <summary>테스트에서는 서비스와 확인 창을 가짜 구현으로 교체합니다.</summary>
    public CacheToolsViewModel(CacheCleanupService service, Func<string, bool> confirm)
    {
        _service = service;
        _confirm = confirm;
    }

    partial void OnSelectedToolChanged(int value)
    {
        _plan = null;
        Message = Strings.Cleanup_Intro;
        ClearCommand.NotifyCanExecuteChanged();
    }

    private bool CanPrepare() => !IsBusy;
    private bool CanClear() => !IsBusy && _plan is not null;

    /// <summary>실행 파일·위치·논리 크기를 읽어 확인용으로 표시합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private async Task PrepareAsync()
    {
        _plan = null;
        IsBusy = true;
        Message = Strings.Cleanup_Checking;
        try
        {
            var result = await _service.PrepareAsync((CacheTool)SelectedTool, CancellationToken.None);
            _plan = result.Plan;
            Message = _plan is null ? FailureText(result.Reason) : DisplayText.Format(Strings.Cleanup_Preview,
                Tools[SelectedTool], _plan.Location.Executable, _plan.Location.CachePath,
                (_plan.ObservedBytes / 1_000_000d).ToString("N1", System.Globalization.CultureInfo.CurrentCulture));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Message = Strings.Cleanup_Failed; }
        finally { IsBusy = false; }
    }

    /// <summary>미리보기한 계획의 확인을 받고 일회성 실행 후 결과를 표시합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        var plan = _plan;
        if (plan is null || !_confirm(Message + Environment.NewLine + Strings.Cleanup_Confirm)) { return; }
        _plan = null;
        IsBusy = true;
        NeedsRescan = true;
        Message = Strings.Cleanup_Running;
        try
        {
            var result = await _service.ExecuteAsync(plan.Id, CancellationToken.None);
            Message = result.ToolSucceeded
                ? DisplayText.Format(Strings.Cleanup_Done, result.RemainingBytes is { } bytes
                    ? (bytes / 1_000_000d).ToString("N1", System.Globalization.CultureInfo.CurrentCulture) + " MB"
                    : Strings.Cleanup_Unverified)
                : FailureText(result.Code);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Message = Strings.Cleanup_Failed; }
        finally { IsBusy = false; }
    }

    private static string FailureText(string? code) => code switch
    {
        "NormalUserRequired" or "ToolUnavailable" => Strings.Cleanup_Unavailable,
        "PlanExpired" or "TargetChanged" or "ToolChanged" => Strings.Cleanup_Changed,
        "OutsideUserProfile" => Strings.Cleanup_OutsideProfile,
        "ToolFailed" => Strings.Cleanup_Failed,
        _ => Strings.Cleanup_Blocked,
    };
}
