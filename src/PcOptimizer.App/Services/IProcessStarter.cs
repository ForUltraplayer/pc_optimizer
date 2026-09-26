/**
 * @file    : IProcessStarter.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 재검사 인스턴스를 셸로 시작하는 계약(테스트에서 가짜 시작기로 바꿔 인자·동사·UAC 취소를 검증)
 */

// 기본 패키지
using System.Diagnostics;

namespace PcOptimizer.App.Services;

/// <summary>
/// 프로세스 시작 계약입니다. 실패는 예외(<see cref="System.ComponentModel.Win32Exception"/> 등)로 알립니다.
/// </summary>
public interface IProcessStarter
{
    /// <summary>
    /// 프로세스를 시작합니다(시작한 프로세스를 기다리지 않음).
    /// </summary>
    /// <param name="startInfo">시작 정보.</param>
    void Start(ProcessStartInfo startInfo);
}
