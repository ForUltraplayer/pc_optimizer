/**
 * @file    : LinkButtonViewModel.cs
 * @author  : rudals252
 * @brief   : 카드의 공식 링크 버튼 하나(이름표·URL·누를 때만 여는 명령) 표시 모델
 */

// 서드파티 패키지
using CommunityToolkit.Mvvm.Input;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 공식 링크 버튼 하나입니다. 사용자가 누를 때만 카드의 여는 동작(링크 정책 재검증 포함)을 호출합니다.
/// </summary>
public sealed partial class LinkButtonViewModel
{
    private readonly Action<string> _open;

    /// <summary>
    /// 링크 버튼을 만듭니다.
    /// </summary>
    /// <param name="label">버튼 이름표.</param>
    /// <param name="url">링크 URL(허용 목록 검증을 통과한 값).</param>
    /// <param name="open">누를 때 호출할 여는 동작.</param>
    public LinkButtonViewModel(string label, string url, Action<string> open)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(open);
        Label = label;
        Url = url;
        _open = open;
    }

    /// <summary>버튼 이름표.</summary>
    public string Label { get; }

    /// <summary>링크 URL.</summary>
    public string Url { get; }

    /// <summary>
    /// 링크를 엽니다(여는 동작이 허용 목록을 다시 확인).
    /// </summary>
    [RelayCommand]
    private void Open()
    {
        _open(Url);
    }
}
