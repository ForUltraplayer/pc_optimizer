/**
 * @file    : UserScopeResolver.cs
 * @author  : rudals252
 * @brief   : 프로세스 토큰 사용자 SID와 대화형 로그온 사용자 SID를 비교해 사용자 범위(전체/시스템만)를 정하고, SID를 알아내지 못한 경우를 따로 구분하는 순수 판정기
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 사용자 범위를 정합니다(I/O 없음).
/// </summary>
public static class UserScopeResolver
{
    /// <summary>
    /// 두 SID로 사용자 범위를 정합니다.
    /// </summary>
    /// <param name="tokenUserSid">현재 프로세스 토큰 사용자 SID(모르면 null).</param>
    /// <param name="interactiveUserSid">현재 세션의 대화형 로그온 사용자 SID(모르면 null).</param>
    /// <returns>
    /// 둘 다 있고 같으면(대소문자 무시) 전체. 다르거나 어느 하나라도 모르면 시스템만
    /// (원래 사용자라고 확인할 수 없으면 사용자별 항목을 읽지 않는 보수적 선택).
    /// </returns>
    public static UserScopeMode Resolve(string? tokenUserSid, string? interactiveUserSid)
    {
        return tokenUserSid is not null && interactiveUserSid is not null
            && string.Equals(tokenUserSid, interactiveUserSid, StringComparison.OrdinalIgnoreCase)
            ? UserScopeMode.Full
            : UserScopeMode.SystemOnly;
    }

    /// <summary>
    /// 두 SID 중 하나라도 알아내지 못했는지 판단합니다. 이때 <see cref="Resolve"/>는 보수적으로 시스템만을 고르지만,
    /// 다른 계정으로 확인된 것이 아니므로 배너는 "확인하지 못함" 문구를 씁니다.
    /// </summary>
    /// <param name="tokenUserSid">현재 프로세스 토큰 사용자 SID(모르면 null).</param>
    /// <param name="interactiveUserSid">현재 세션의 대화형 로그온 사용자 SID(모르면 null).</param>
    /// <returns>어느 한쪽이라도 null이면 true.</returns>
    public static bool IsUnresolved(string? tokenUserSid, string? interactiveUserSid)
    {
        return tokenUserSid is null || interactiveUserSid is null;
    }
}
