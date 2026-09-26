/**
 * @file    : WindowsElevationState.cs
 * @author  : rudals252
 * @brief   : WindowsIdentity로 현재 프로세스의 관리자 권한 여부와 실행 사용자 SID를 시작 시 한 번 읽어 두는 권한 상태 구현
 */

// 기본 패키지
using System.Security.Principal;

namespace PcOptimizer.App.Services;

/// <summary>
/// 현재 프로세스 토큰의 권한 상태입니다. 프로세스 수명 동안 바뀌지 않으므로 만들 때 한 번만 읽습니다.
/// </summary>
public sealed class WindowsElevationState : IElevationState
{
    private WindowsElevationState(bool isElevated, string? currentUserSid)
    {
        IsElevated = isElevated;
        CurrentUserSid = currentUserSid;
    }

    /// <inheritdoc />
    public bool IsElevated { get; }

    /// <inheritdoc />
    public string? CurrentUserSid { get; }

    /// <summary>
    /// 현재 프로세스의 권한 상태를 읽습니다.
    /// </summary>
    /// <returns>권한 상태.</returns>
    public static WindowsElevationState Capture()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var isElevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        return new WindowsElevationState(isElevated, identity.User?.Value);
    }
}
