/**
 * @file    : DesktopShellSmokeTests.cs
 * @author  : rudals252
 * @brief   : 현재 데스크톱 셸 COM 연결을 조회하며 브라우저·링크는 실행하지 않음
 */
using PcOptimizer.App.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>대화형 Windows 평가 환경에서 네이티브 셸 연결을 확인합니다.</summary>
[Trait("Category", "Smoke")]
public sealed class DesktopShellSmokeTests(ITestOutputHelper output)
{
    /// <summary>세션의 데스크톱 셸이 있으면 연결하고(UAC를 끈 PC의 승격 셸 포함, 승격 여부는 기록), 셸 창이 없으면 실제 네이티브 경로에서도 거절합니다.</summary>
    [Fact]
    public async Task DesktopConnectionDoesNotLaunchAnything()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var (hasShell, shellUnelevated) = DescribeDesktop();
                using var shell = DesktopShellConnection.Connect();
                if (hasShell)
                {
                    Assert.NotNull(shell);
                    Assert.True(shell.IsSessionShell);
                    Assert.Equal(shellUnelevated, shell.IsUnelevated);
                    output.WriteLine(shellUnelevated ? "NormalDesktop: COM connection verified; ShellExecute was not called." : "ElevatedDesktop(UAC off): COM connection verified with elevated shell; ShellExecute was not called.");
                }
                else
                {
                    Assert.Null(shell);
                    output.WriteLine("NoDesktopShell: refusal verified.");
                }
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static (bool HasShell, bool Unelevated) DescribeDesktop()
    {
        var window = GetShellWindow();
        if (window == 0) { return (false, false); }
        Assert.NotEqual(0u, GetWindowThreadProcessId(window, out var pid));
        using var process = Process.GetProcessById(checked((int)pid));
        using var current = Process.GetCurrentProcess();
        Assert.Equal(current.SessionId, process.SessionId);
        Assert.True(OpenProcessToken(process.Handle, 8, out var token));
        using (token)
        {
            Assert.True(GetTokenInformation(token, 20, out var elevation, sizeof(int), out _));
            return (true, elevation == 0);
        }
    }

    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, out int information, int length, out int returned);
}
