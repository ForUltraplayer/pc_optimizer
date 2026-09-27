/**
 * @file    : SystemActionSession.cs
 * @author  : rudals252
 * @brief   : 조치 계획을 현재 프로세스 토큰 SID와 Windows 세션에 귀속시키는 조회 전용 공급자
 */
using System.Diagnostics;
using System.Security.Principal;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions;

/// <summary>프로세스의 실제 사용자 식별자를 읽습니다. UI 범위 확인 결과는 호출자가 전달합니다.</summary>
public static class SystemActionSession
{
    /// <summary>사용자 범위와 실제 토큰을 함께 기록합니다. 확인 실패는 실행 거절로 처리합니다.</summary>
    public static ActionSession Read(ActionUserScope scope = ActionUserScope.Full)
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            using var process = Process.GetCurrentProcess();
            return new(identity.User?.Value ?? string.Empty, process.SessionId, scope);
        }
        catch { return new(string.Empty, -1, ActionUserScope.Unknown); }
    }
}
