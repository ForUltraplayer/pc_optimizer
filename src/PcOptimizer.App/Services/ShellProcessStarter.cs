/**
 * @file    : ShellProcessStarter.cs
 * @author  : rudals252
 * @brief   : Process.Start로 프로세스를 시작하고 반환 핸들을 바로 닫는 IProcessStarter 구현
 */

// 기본 패키지
using System.Diagnostics;

namespace PcOptimizer.App.Services;

/// <summary>
/// <see cref="Process.Start(ProcessStartInfo)"/>로 시작하는 구현입니다. 반환된 프로세스 핸들은 바로 닫습니다.
/// </summary>
public sealed class ShellProcessStarter : IProcessStarter
{
    /// <inheritdoc />
    public void Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        using var process = Process.Start(startInfo);
    }
}
