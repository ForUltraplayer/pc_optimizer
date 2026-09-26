/**
 * @file    : ScanLaunchMode.cs
 * @author  : rudals252
 * @brief   : 앱 인스턴스의 검사 시작 방식(일반, 같은 사용자 관리자 재검사, 다른 계정으로 승격된 관리자 재검사) 열거형
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 앱 인스턴스의 검사 시작 방식입니다.
/// </summary>
public enum ScanLaunchMode
{
    /// <summary>일반 시작(재검사 인자 없음 또는 형식 오류, 또는 실제로 승격되지 않음).</summary>
    Normal,

    /// <summary>원래 사용자와 같은 계정으로 승격된 관리자 재검사(모든 프로브 실행, 별도 검사 ID).</summary>
    ElevatedSameUser,

    /// <summary>원래 사용자와 다른 관리자 계정으로 승격된 재검사(시스템 범위 프로브만 실행).</summary>
    ElevatedDifferentUser,
}
