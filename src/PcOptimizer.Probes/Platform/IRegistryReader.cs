/**
 * @file    : IRegistryReader.cs
 * @author  : rudals252
 * @brief   : 레지스트리 값 하나를 읽는 조회 전용 계약과 결과(키/값 없음·형식·값·접근 거부·오류 구분) 레코드. 테스트에서 fixture 값으로 바꿔 끼운다
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 레지스트리 루트입니다(조회에 필요한 두 가지만).
/// </summary>
public enum RegistryRoot
{
    /// <summary>HKEY_LOCAL_MACHINE(64비트 보기).</summary>
    LocalMachine,

    /// <summary>HKEY_CURRENT_USER.</summary>
    CurrentUser,
}

/// <summary>
/// 레지스트리 값 읽기 상태입니다. "값 없음"과 "읽기 실패"를 구분합니다.
/// </summary>
public enum RegistryReadStatus
{
    /// <summary>값을 읽었습니다.</summary>
    Found,

    /// <summary>키가 없습니다.</summary>
    KeyMissing,

    /// <summary>키는 있지만 값이 없습니다.</summary>
    ValueMissing,

    /// <summary>접근이 거부되었습니다.</summary>
    AccessDenied,

    /// <summary>그 밖의 오류입니다.</summary>
    Error,
}

/// <summary>
/// 레지스트리 값 읽기 결과입니다.
/// </summary>
/// <param name="Status">읽기 상태.</param>
/// <param name="Kind">값 형식 이름(RegistryValueKind 이름, Found일 때만).</param>
/// <param name="DwordValue">DWORD 값(형식이 DWord일 때만).</param>
/// <param name="ErrorCode">실패 원인(예외 형식 이름, 메시지 원문 아님).</param>
public sealed record RegistryValueReading(RegistryReadStatus Status, string? Kind, long? DwordValue, string? ErrorCode);

/// <summary>
/// 레지스트리 조회 계약입니다. 쓰기 메서드는 두지 않으며 구현은 예외 대신 상태로 실패를 알립니다.
/// </summary>
public interface IRegistryReader
{
    /// <summary>
    /// 값 하나를 읽습니다.
    /// </summary>
    /// <param name="root">루트.</param>
    /// <param name="subKey">하위 키 경로.</param>
    /// <param name="valueName">값 이름.</param>
    /// <returns>읽기 결과.</returns>
    RegistryValueReading ReadValue(RegistryRoot root, string subKey, string valueName);
}
