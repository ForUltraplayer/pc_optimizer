/**
 * @file    : UserScopeMode.cs
 * @author  : rudals252
 * @brief   : 검사·조치의 사용자 범위(전체 / 시스템만). 다른 관리자 계정의 자격 증명으로 승격돼 실행되면 시스템만
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 이 인스턴스가 다루는 사용자 범위입니다.
/// </summary>
public enum UserScopeMode
{
    /// <summary>프로세스 사용자와 대화형 로그온 사용자가 같아 사용자별 항목(HKCU·프로필)까지 검사합니다.</summary>
    Full,

    /// <summary>다른 관리자 계정으로 실행 중이거나 같은 사용자임을 확인하지 못해 시스템 범위만 검사합니다.</summary>
    SystemOnly,
}
