/**
 * @file    : IRegistryReader.cs
 * @author  : rudals252
 * @brief   : 레지스트리 값 하나(DWORD·문자열), 키의 모든 값 또는 하위 키 이름을 읽는 조회 전용 계약과 결과(키/값 없음·형식·값·접근 거부·오류 구분) 레코드. 테스트에서 fixture 값으로 바꿔 끼운다
 */

// 기본 패키지
using Microsoft.Win32;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 레지스트리 루트입니다(조회에 필요한 두 가지만).
/// </summary>
public enum RegistryRoot
{
    /// <summary>HKEY_LOCAL_MACHINE(값 하나 읽기는 64비트 보기, 키 전체 읽기는 지정한 보기).</summary>
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
/// 키 안의 값 하나(이름·형식·문자열 또는 이진 데이터)입니다.
/// </summary>
/// <param name="Name">값 이름(기본값은 빈 문자열).</param>
/// <param name="Kind">값 형식 이름(RegistryValueKind 이름).</param>
/// <param name="Text">문자열 데이터(String/ExpandString일 때만, 환경 변수는 펼치지 않음).</param>
/// <param name="Binary">이진 데이터(Binary일 때만).</param>
public sealed record RegistryValueEntry(string Name, string Kind, string? Text, IReadOnlyList<byte>? Binary);

/// <summary>
/// 키 전체 읽기 결과입니다. Status가 Found일 때만 Values가 의미가 있습니다(키가 없으면 빈 목록 + KeyMissing).
/// </summary>
/// <param name="Status">읽기 상태(Found·KeyMissing·AccessDenied·Error).</param>
/// <param name="Values">값 목록(열거 순서).</param>
/// <param name="ErrorCode">실패 원인(예외 형식 이름, 메시지 원문 아님).</param>
public sealed record RegistryKeyReading(RegistryReadStatus Status, IReadOnlyList<RegistryValueEntry> Values, string? ErrorCode);

/// <summary>
/// 하위 키 이름 열거 결과입니다. Status가 Found일 때만 Names가 의미가 있습니다(키가 없으면 빈 목록 + KeyMissing).
/// </summary>
/// <param name="Status">읽기 상태(Found·KeyMissing·AccessDenied·Error).</param>
/// <param name="Names">하위 키 이름(열거 순서).</param>
/// <param name="ErrorCode">실패 원인(예외 형식 이름, 메시지 원문 아님).</param>
public sealed record RegistrySubKeyReading(RegistryReadStatus Status, IReadOnlyList<string> Names, string? ErrorCode);

/// <summary>
/// 문자열 값 하나의 읽기 결과입니다. Status가 Found일 때만 Text가 있습니다.
/// </summary>
/// <param name="Status">읽기 상태(Found·KeyMissing·ValueMissing·AccessDenied·Error).</param>
/// <param name="Text">문자열 데이터(환경 변수는 펼치지 않음).</param>
/// <param name="ErrorCode">실패 원인(예외 형식 이름 또는 값 형식 이름, 메시지 원문 아님).</param>
public sealed record RegistryStringReading(RegistryReadStatus Status, string? Text, string? ErrorCode);

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

    /// <summary>
    /// 키의 모든 값을 읽습니다(하위 키는 읽지 않음).
    /// </summary>
    /// <param name="root">루트.</param>
    /// <param name="view">레지스트리 보기(64비트 / 32비트 WOW6432Node).</param>
    /// <param name="subKey">하위 키 경로.</param>
    /// <returns>키 읽기 결과.</returns>
    RegistryKeyReading ReadKeyValues(RegistryRoot root, RegistryView view, string subKey);

    /// <summary>
    /// 키의 하위 키 이름을 읽습니다(값과 더 깊은 하위 키는 읽지 않음).
    /// </summary>
    /// <param name="root">루트.</param>
    /// <param name="view">레지스트리 보기.</param>
    /// <param name="subKey">하위 키 경로.</param>
    /// <returns>하위 키 이름 읽기 결과.</returns>
    RegistrySubKeyReading ReadSubKeyNames(RegistryRoot root, RegistryView view, string subKey);

    /// <summary>
    /// 문자열 값 하나만 읽습니다(키의 다른 값은 읽지 않음, 환경 변수는 펼치지 않음).
    /// </summary>
    /// <param name="root">루트.</param>
    /// <param name="view">레지스트리 보기.</param>
    /// <param name="subKey">하위 키 경로.</param>
    /// <param name="valueName">값 이름.</param>
    /// <returns>문자열 값 읽기 결과(String/ExpandString이 아니면 Error).</returns>
    RegistryStringReading ReadStringValue(RegistryRoot root, RegistryView view, string subKey, string valueName);
}
