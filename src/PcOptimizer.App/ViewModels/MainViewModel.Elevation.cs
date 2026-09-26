/**
 * @file    : MainViewModel.Elevation.cs
 * @author  : rudals252
 * @brief   : 메인 화면 모델의 관리자 권한 재검사 부분(재검사 요청 명령·UAC 취소/실패 안내·관리자 재검사 인스턴스 배너). 기존 창의 상태·결과는 바꾸지 않는다
 */

// 서드파티 패키지
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 메인 화면 모델의 관리자 권한 재검사 부분입니다. 버튼은 일반 권한일 때만 누를 수 있고, 결과는 별도 창·별도 검사 ID로만 나옵니다.
/// </summary>
public sealed partial class MainViewModel
{
    private readonly IElevationState _elevationState;
    private readonly ElevationRelauncher _relauncher;

    /// <summary>관리자 권한 재검사 창을 시작하는 중인지 여부(UAC 대화 상자가 떠 있는 동안 포함).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestElevatedRescanCommand))]
    private bool _isRelaunching;

    /// <summary>이 인스턴스의 시작 방식.</summary>
    public ScanLaunchMode LaunchMode { get; }

    /// <summary>현재 프로세스가 관리자 권한인지 여부.</summary>
    public bool IsElevated => _elevationState.IsElevated;

    /// <summary>관리자 권한 재검사 버튼 설명(이미 관리자 권한이면 그 사실).</summary>
    public string ElevatedRescanTooltip => IsElevated ? Strings.Tooltip_ElevatedRescan_AlreadyElevated : Strings.Tooltip_ElevatedRescan;

    /// <summary>관리자 권한 재검사 인스턴스의 배너(일반 시작이면 null).</summary>
    public string? ElevatedBannerText => LaunchMode switch
    {
        ScanLaunchMode.ElevatedSameUser => Strings.Banner_ElevatedSameUser,
        ScanLaunchMode.ElevatedDifferentUser => Strings.Banner_ElevatedDifferentUser,
        _ => null,
    };

    /// <summary>배너를 표시하는지 여부.</summary>
    public bool HasElevatedBanner => ElevatedBannerText is not null;

    /// <summary>
    /// 같은 실행 파일을 관리자 권한으로 별도 창에 시작합니다. 이 창의 상태·결과는 바꾸지 않고 안내 문구만 표시합니다
    /// (UAC 취소 시 "관리자 권한 요청이 취소되었어요", 그 밖의 실패는 예외 형식 이름만).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRequestElevatedRescan))]
    private async Task RequestElevatedRescanAsync()
    {
        IsRelaunching = true;

        // UAC 대화 상자가 닫힐 때까지 셸 실행이 반환되지 않을 수 있으므로 UI 스레드 밖에서 호출한다.
        var result = await Task.Run(_relauncher.Relaunch).ConfigureAwait(false);
        var message = result.Outcome switch
        {
            ElevationRelaunchOutcome.Started => Strings.Elevation_Started,
            ElevationRelaunchOutcome.Cancelled => Strings.Elevation_Cancelled,
            _ => DisplayText.Format(Strings.Elevation_Failed, result.ErrorCode ?? string.Empty),
        };

        await _dispatcher.InvokeAsync(() =>
        {
            IsRelaunching = false;
            StatusMessage = message;
        }).ConfigureAwait(false);
    }

    /// <summary>관리자 권한 재검사를 요청할 수 있는지 여부(일반 권한이고 요청 중이 아닐 때만).</summary>
    private bool CanRequestElevatedRescan() => !IsElevated && !IsRelaunching;
}
