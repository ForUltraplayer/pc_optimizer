/**
 * @file    : DirectoryEntry.cs
 * @author  : rudals252
 * @brief   : 디렉터리 열거 항목 하나의 메타데이터(이름·속성·논리 크기·수정 시각·디렉터리 여부) 값과 루트 존재 확인 결과 열거형, 열거 공급자 계약
 */

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 디렉터리 열거 항목 하나의 메타데이터입니다. 파일 내용은 담지 않습니다.
/// </summary>
/// <param name="Name">항목 이름(경로 아님).</param>
/// <param name="Attributes">파일 속성(reparse·placeholder·압축·희소 판정에 사용).</param>
/// <param name="Length">논리 크기(디렉터리는 0).</param>
/// <param name="LastWriteTimeUtc">마지막 수정 시각(UTC). 마지막 사용 시각이 아닙니다.</param>
/// <param name="IsDirectory">디렉터리 여부.</param>
public readonly record struct DirectoryEntry(
    string Name,
    FileAttributes Attributes,
    long Length,
    DateTimeOffset LastWriteTimeUtc,
    bool IsDirectory);

/// <summary>
/// 스캔 루트 경로의 존재 확인 결과입니다.
/// </summary>
public enum RootPresence
{
    /// <summary>순회할 수 있는 일반 디렉터리.</summary>
    Directory,

    /// <summary>없음(오류 아님).</summary>
    Missing,

    /// <summary>접근 거부.</summary>
    AccessDenied,

    /// <summary>reparse point(정션·심볼릭 링크 등)라서 따라가지 않음.</summary>
    ReparsePoint,

    /// <summary>디렉터리가 아님.</summary>
    NotDirectory,

    /// <summary>그 밖의 오류.</summary>
    Error,
}

/// <summary>
/// 디렉터리 항목 열거 계약입니다. 실제 구현은 <c>FileSystemEnumerator&lt;T&gt;</c>로 파일마다 핸들을 열지 않고 메타데이터만 읽습니다.
/// </summary>
public interface IDirectoryEntrySource
{
    /// <summary>
    /// 스캔 루트의 존재·형태를 확인합니다(reparse point를 따라가지 않음).
    /// </summary>
    /// <param name="path">루트 경로.</param>
    /// <returns>확인 결과.</returns>
    RootPresence ProbeRoot(string path);

    /// <summary>
    /// 디렉터리 하나의 직접 항목을 열거합니다(하위로 내려가지 않음, 숨김·시스템 항목 포함).
    /// 열거 중 실패하면 <see cref="UnauthorizedAccessException"/> 또는 <see cref="IOException"/>을 던지며, 그 전까지 돌려준 항목은 유효합니다.
    /// </summary>
    /// <param name="directoryPath">디렉터리 경로.</param>
    /// <returns>항목(지연 열거).</returns>
    IEnumerable<DirectoryEntry> Enumerate(string directoryPath);
}
