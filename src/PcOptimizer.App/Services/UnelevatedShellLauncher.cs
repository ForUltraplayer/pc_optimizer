/**
 * @file    : UnelevatedShellLauncher.cs
 * @author  : rudals252
 * @brief   : 기존 데스크톱 셸에만 HTTPS 주소를 위임하며 승격 실행으로 폴백하지 않음
 */
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.App.Services;

/// <summary>새 프로세스를 만들지 않는 데스크톱 셸 연결입니다.</summary>
public interface IDesktopShell : IDisposable
{
    /// <summary>현재 세션의 비승격 셸인지 확인합니다.</summary>
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
    /// <summary>정규 주소를 위임하고 확인 불가·실패 시 예외로 알립니다.</summary>
    public void Launch(string absoluteUri)
    {
        if (!OfficialUrl.TryParse(absoluteUri, out var uri)
            || uri.AbsoluteUri.Contains(',', StringComparison.Ordinal)
            || Uri.UnescapeDataString(uri.AbsoluteUri).Contains(',', StringComparison.Ordinal))
        { throw new ArgumentException("모호하지 않은 HTTPS 주소만 열 수 있습니다.", nameof(absoluteUri)); }
        using var shell = _connect();
        if (shell is null || !shell.IsUnelevated) { throw new InvalidOperationException("일반 권한 데스크톱 셸을 확인하지 못했습니다."); }
        shell.Open(uri.AbsoluteUri);
    }
}
