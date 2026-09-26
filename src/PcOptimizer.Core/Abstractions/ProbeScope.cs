/**
 * @file    : ProbeScope.cs
 * @author  : rudals252
 * @brief   : 프로브가 읽는 상태의 범위(시스템 전체 / 실행 사용자별 HKCU·프로필) 열거형. 다른 계정으로 승격된 재검사에서 사용자별 수집을 막는 데 쓴다
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 프로브가 읽는 상태의 범위입니다.
/// </summary>
/// <remarks>
/// 관리자 권한 재검사에서 UAC에 다른 관리자 계정을 입력하면 실행 사용자가 바뀝니다. 이때 <see cref="User"/> 범위 프로브는
/// 관리자 계정의 HKCU·프로필을 읽게 되므로 실행하지 않고 원래 창에서 확인하도록 안내합니다(스펙 §6).
/// </remarks>
public enum ProbeScope
{
    /// <summary>시스템 전체 상태(HKLM, WMI, 장치, 볼륨 등). 실행 사용자와 무관합니다.</summary>
    System,

    /// <summary>실행 사용자별 상태(HKCU, 사용자 프로필 폴더, 사용자별 설정 등).</summary>
    User,
}
