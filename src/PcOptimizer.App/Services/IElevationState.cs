/**
 * @file    : IElevationState.cs
 * @author  : rudals252
 * @brief   : 현재 프로세스의 관리자 권한 여부와 실행 사용자 SID를 알려 주는 계약(테스트에서 일반 권한·다른 SID로 바꿔 끼움)
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 현재 프로세스의 권한 상태입니다.
/// </summary>
public interface IElevationState
{
    /// <summary>관리자 권한(승격된 토큰)으로 실행 중인지 여부.</summary>
    bool IsElevated { get; }

    /// <summary>실행 사용자의 SID 문자열(알 수 없으면 null). 로그·리포트에 남기지 않습니다.</summary>
    string? CurrentUserSid { get; }
}
