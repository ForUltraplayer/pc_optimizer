/**
 * @file    : UnelevatedShellLauncher.cs
 * @author  : rudals252
 * @brief   : 기존 데스크톱 셸(같은 세션)에만 HTTPS 주소를 위임하며, 셸이 없어도 앱이 직접 브라우저를 시작하는 폴백은 하지 않음
 */
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.App.Services;

/// <summary>새 프로세스를 만들지 않는 데스크톱 셸 연결입니다.</summary>
public interface IDesktopShell : IDisposable
{
    /// <summary>현재 세션의 데스크톱 셸(같은 세션·같은 창·같은 프로세스)인지 확인합니다.</summary>
    bool IsSessionShell { get; }
    /// <summary>그 셸이 비승격 토큰인지 확인합니다(UAC를 끈 PC에서는 false일 수 있음).</summary>
    bool IsUnelevated { get; }
    /// <summary>검증된 절대 HTTPS 주소를 전달합니다.</summary>
    void Open(string absoluteUri);
}

/// <summary>기존 비승격 셸에 링크를 전달하며 실패는 복사 안내로 처리합니다.</summary>
public sealed class UnelevatedShellLauncher
{
    private readonly Func<IDesktopShell?> _connect;
    /// <summary>현재 데스크톱 COM 연결을 사용합니다.</summary>
    public UnelevatedShellLauncher() : this(DesktopShellConnection.Connect) { }
    /// <summary>테스트용 셸 공급자를 지정합니다.</summary>
    public UnelevatedShellLauncher(Func<IDesktopShell?> connect) => _connect = connect ?? throw new ArgumentNullException(nameof(connect));
    /// <summary>마지막 위임에서 셸이 승격 토큰이었는지(UAC 꺼짐 등). 기록용이며 동작을 바꾸지 않습니다.</summary>
    public bool ShellWasElevated { get; private set; }
    /// <summary>정규 주소를 세션의 셸에 위임하고 셸이 없거나 실패하면 예외로 알립니다.</summary>
    public void Launch(string absoluteUri)
    {
        if (!OfficialUrl.TryParse(absoluteUri, out var uri)
            || uri.AbsoluteUri.Contains(',', StringComparison.Ordinal)
            || Uri.UnescapeDataString(uri.AbsoluteUri).Contains(',', StringComparison.Ordinal))
        { throw new ArgumentException("모호하지 않은 HTTPS 주소만 열 수 있습니다.", nameof(absoluteUri)); }
        using var shell = _connect();
        if (shell is null || !shell.IsSessionShell) { throw new InvalidOperationException("현재 세션의 데스크톱 셸을 확인하지 못했습니다."); }
        // UAC를 끈 PC는 셸도 관리자 토큰이다. 그 경우에도 사용자의 평소 권한이 그 셸이므로 위임하며, 이 앱이 브라우저를 직접 시작하지는 않는다.
        ShellWasElevated = !shell.IsUnelevated;
        shell.Open(uri.AbsoluteUri);
    }
}
