/**
 * @file    : IClipboard.cs
 * @author  : rudals252
 * @brief   : 텍스트를 클립보드에 넣는 계약(테스트에서 기록 대역으로 바꿈)
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 클립보드에 텍스트를 넣는 계약입니다. 뷰모델은 WPF Clipboard를 직접 쓰지 않고 이 계약만 씁니다.
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// 텍스트를 클립보드에 넣습니다(UI 스레드에서 호출).
    /// </summary>
    /// <param name="text">넣을 텍스트.</param>
    void SetText(string text);
}
