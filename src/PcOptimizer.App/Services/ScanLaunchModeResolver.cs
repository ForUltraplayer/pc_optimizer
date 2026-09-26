/**
 * @file    : ScanLaunchModeResolver.cs
 * @author  : rudals252
 * @brief   : 재검사 인자·관리자 권한 여부·현재 SID로 검사 시작 방식(일반/같은 사용자/다른 사용자 재검사)을 정하는 판정기
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 시작 방식을 정합니다.
/// </summary>
public static class ScanLaunchModeResolver
{
    /// <summary>
    /// 재검사 인자·권한·현재 SID로 시작 방식을 정합니다.
    /// </summary>
    /// <param name="arguments">해석한 재검사 인자(없으면 null).</param>
    /// <param name="isElevated">현재 프로세스가 관리자 권한인지 여부.</param>
    /// <param name="currentUserSid">현재 실행 사용자 SID(모르면 null).</param>
    /// <returns>
    /// 인자가 없거나 승격되지 않았으면 일반 시작. SID가 원래 사용자와 같으면(대소문자 무시) 같은 사용자 재검사,
    /// 다르거나 현재 SID를 모르면 다른 사용자 재검사(원래 사용자라고 확인할 수 없으면 사용자별 항목을 읽지 않는 보수적 선택).
    /// </returns>
    public static ScanLaunchMode Resolve(ElevatedRescanArguments? arguments, bool isElevated, string? currentUserSid)
    {
        if (arguments is null || !isElevated)
        {
            return ScanLaunchMode.Normal;
        }

        return currentUserSid is not null && string.Equals(arguments.OriginSid, currentUserSid, StringComparison.OrdinalIgnoreCase)
            ? ScanLaunchMode.ElevatedSameUser
            : ScanLaunchMode.ElevatedDifferentUser;
    }
}
