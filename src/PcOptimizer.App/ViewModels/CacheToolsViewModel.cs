/**
 * @file    : CacheToolsViewModel.cs
 * @author  : rudals252
 * @brief   : 공식 도구 캐시 정리의 선택·미리보기·확인·실행·후속 관측 화면. 사용자 폴더 도구는 터미널 직접 실행 명령을 안내한다
 */
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Resources;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>사용자 확인 뒤에만 캐시 도구를 실행합니다.</summary>
public sealed partial class CacheToolsViewModel : ObservableObject
{
    /// <summary>사용자 폴더에 설치된 도구를 사용자가 터미널에서 직접 실행할 공식 명령(<see cref="CacheTool"/> 순서)입니다.</summary>
    private static readonly IReadOnlyList<string> DIRECT_COMMANDS =
        ["npm cache clean --force", "python -m pip cache purge", "dotnet nuget locals http-cache --clear"];

    private readonly CacheCleanupService _service;
    private readonly Func<string, bool> _confirm;
    private CacheCleanupPlan? _plan;

    /// <summary>선택 도구 목록입니다.</summary>
    public IReadOnlyList<string> Tools { get; } = ["npm", "pip", "NuGet HTTP"];

    /// <summary>선택한 공식 도구의 인덱스.</summary>
    [ObservableProperty]
    private int _selectedTool;

    /// <summary>조회 또는 실행 진행 여부.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareCommand), nameof(ClearCommand))]
    [NotifyPropertyChangedFor(nameof(CanSelectTool))]
    private bool _isBusy;

    /// <summary>현재 단계와 결과의 안내.</summary>
    [ObservableProperty]
    private string _message = Strings.Cleanup_Intro;

    /// <summary>실제 도구가 시작된 최근 정리 결과.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutcome))]
    private CleanupOutcomeViewModel? _outcome;

    /// <summary>실제 정리 실행 결과 표시 여부.</summary>
    public bool HasOutcome => Outcome is not null;

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
            var tool = (CacheTool)SelectedTool;
            var result = await _service.PrepareAsync(tool, CancellationToken.None);
            _plan = result.Plan;
            Message = _plan is null ? FailureText(result.Reason, tool) : FormatPreview(_plan);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Message = Strings.Cleanup_QueryFailed; }
        finally { IsBusy = false; }
    }

    /// <summary>미리보기한 계획의 확인을 받고 일회성 실행 후 결과를 표시합니다.</summary>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        var plan = _plan;
        if (plan is null || !_confirm(FormatPreview(plan) + Environment.NewLine + Strings.Cleanup_Impact
            + Environment.NewLine + Strings.Cleanup_Confirm)) { return; }
        _plan = null;
        IsBusy = true;
        Message = Strings.Cleanup_Running;
        try
        {
            var result = await _service.ExecuteAsync(plan.Id, CancellationToken.None);
            if (!result.Started)
            {
                var reason = FailureText(result.Code, plan.Location.Tool);
                Message = reason == Strings.Cleanup_NotExecuted ? reason : Strings.Cleanup_NotExecuted + " " + reason;
                return;
            }
            NeedsRescan = true;
            Outcome = new CleanupOutcomeViewModel(Tools[(int)plan.Location.Tool], result);
            Message = result.ToolSucceeded
                ? DisplayText.Format(Strings.Cleanup_Done, result.RemainingBytes is { } bytes
                    ? (bytes / 1_000_000d).ToString("N1", System.Globalization.CultureInfo.CurrentCulture) + " MB"
                    : Strings.Cleanup_Unverified)
                : FailureText(result.Code, plan.Location.Tool);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Message = Strings.Cleanup_NotExecuted;
        }
        finally { IsBusy = false; }
    }

    private string FormatPreview(CacheCleanupPlan plan) => DisplayText.Format(Strings.Cleanup_Preview,
        Tools[(int)plan.Location.Tool], plan.Location.Executable, plan.Location.CachePath,
        (plan.ObservedBytes / 1_000_000d).ToString("N1", System.Globalization.CultureInfo.CurrentCulture));

    private static string FailureText(string? code, CacheTool tool) => code switch
    {
        "ToolUnavailable" => Strings.Cleanup_Unavailable,
        SystemCacheToolBackend.USER_SCOPE_EXCLUDED => Strings.Cleanup_UserScopeExcluded,
        SystemCacheToolBackend.TOOL_NOT_IN_PROTECTED_LOCATION => DisplayText.Format(Strings.Cleanup_ToolUserWritable, DIRECT_COMMANDS[(int)tool]),
        "PlanExpired" or "TargetChanged" or "ToolChanged" => Strings.Cleanup_Changed,
        "SessionChanged" => Strings.Cleanup_SessionChanged,
        "OutsideUserProfile" => Strings.Cleanup_OutsideProfile,
        "Busy" => Strings.Cleanup_Busy,
        "ProcessStillRunning" => Strings.Cleanup_ProcessStillRunning,
        "StartFailed" or "PreflightFailed" => Strings.Cleanup_NotExecuted,
        "ToolFailed" => Strings.Cleanup_Failed,
        _ => Strings.Cleanup_Blocked,
    };
}
