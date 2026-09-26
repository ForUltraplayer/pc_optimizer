/**
 * @file    : InteractiveSessionUser.cs
 * @author  : rudals252
 * @brief   : WTS API로 현재 세션의 대화형 로그온 사용자(도메인·계정명)를 읽어 SID로 바꾼다. 표준 계정이 다른 관리자 자격 증명으로 승격한 경우를 판정하는 데 쓴다
 */

// 기본 패키지
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 대화형 세션 사용자를 읽습니다. 계정명·SID는 판정에만 쓰고 기록하지 않습니다.
/// </summary>
public static class InteractiveSessionUser
{
    /// <summary>
    /// 현재 세션의 대화형 사용자 SID 문자열을 읽습니다. 사용자 이름을 읽지 못하거나 SID로 바꾸지 못하면 false.
    /// </summary>
    /// <param name="sid">SID 문자열(성공 시), 실패 시 null.</param>
    /// <returns>SID를 얻었는지 여부.</returns>
    public static bool TryGetSid(out string? sid)
    {
        sid = null;
        if (!TryQuery(NativeMethods.WTS_USER_NAME, out var user) || string.IsNullOrEmpty(user)
            || !TryQuery(NativeMethods.WTS_DOMAIN_NAME, out var domain))
        {
            return false;
        }

        try
        {
            var account = string.IsNullOrEmpty(domain) ? new NTAccount(user) : new NTAccount(domain, user);
            sid = ((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value;
            return true;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
        catch (SystemException)
        {
            return false;
        }
    }

    /// <summary>
    /// 현재 세션의 문자열 정보 하나를 읽고 WTS 버퍼를 해제합니다.
    /// </summary>
    private static bool TryQuery(int infoClass, out string? value)
    {
        value = null;
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE, NativeMethods.WTS_CURRENT_SESSION, infoClass, out var buffer, out _))
        {
            return false;
        }

        try
        {
            value = Marshal.PtrToStringUni(buffer);
            return true;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }
}
