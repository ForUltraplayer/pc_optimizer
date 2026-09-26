/**
 * @file    : WpfClipboard.cs
 * @author  : rudals252
 * @brief   : System.Windows.Clipboard.SetText로 텍스트를 넣는 IClipboard 구현
 */

// 기본 패키지
using System.Windows;

namespace PcOptimizer.App.Services;

/// <summary>
/// WPF 클립보드 구현입니다. 다른 프로그램이 클립보드를 잡고 있으면 <see cref="System.Runtime.InteropServices.ExternalException"/>이 날 수 있으며 호출자가 처리합니다.
/// </summary>
public sealed class WpfClipboard : IClipboard
{
    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clipboard.SetText(text);
    }
}
