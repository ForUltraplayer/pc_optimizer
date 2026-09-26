/**
 * @file    : FileSystemDirectoryEntrySource.cs
 * @author  : rudals252
 * @brief   : System.IO.Enumeration.FileSystemEnumerator로 디렉터리 하나의 항목 메타데이터를 파일 핸들 없이 열거하는 실제 구현(숨김·시스템 포함, 접근 실패는 예외로 알림, 쓰기 없음)
 */

// 기본 패키지
using System.IO.Enumeration;
using System.Security;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 실제 파일 시스템 항목 열거 구현입니다. 파일 내용을 읽지 않고 FindFirstFile/NtQueryDirectoryFile 수준의 메타데이터만 씁니다.
/// </summary>
public sealed class FileSystemDirectoryEntrySource : IDirectoryEntrySource
{
    /// <summary>
    /// 숨김·시스템 항목을 건너뛰지 않고, 접근 거부를 무시하지 않으며(예외로 알림), 하위로 내려가지 않는 열거 옵션.
    /// </summary>
    private static readonly EnumerationOptions OPTIONS = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    /// <summary>공유 인스턴스.</summary>
    public static FileSystemDirectoryEntrySource Instance { get; } = new();

    /// <inheritdoc />
    public RootPresence ProbeRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            // GetAttributes는 reparse point 자체의 속성을 돌려주므로 대상 쪽으로 따라가지 않는다.
            var attributes = File.GetAttributes(path);
            if (!attributes.HasFlag(FileAttributes.Directory))
            {
                return RootPresence.NotDirectory;
            }

            return attributes.HasFlag(FileAttributes.ReparsePoint) ? RootPresence.ReparsePoint : RootPresence.Directory;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return RootPresence.Missing;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            return RootPresence.AccessDenied;
        }
        catch (IOException)
        {
            return RootPresence.Error;
        }
    }

    /// <inheritdoc />
    public IEnumerable<DirectoryEntry> Enumerate(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        using var enumerator = new EntryEnumerator(directoryPath);
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }

    /// <summary>
    /// 항목을 값 형식으로 바꾸는 열거자입니다(항목마다 핸들을 열지 않음).
    /// </summary>
    private sealed class EntryEnumerator(string directory) : FileSystemEnumerator<DirectoryEntry>(directory, OPTIONS)
    {
        /// <inheritdoc />
        protected override DirectoryEntry TransformEntry(ref FileSystemEntry entry)
        {
            return new DirectoryEntry(
                entry.FileName.ToString(),
                entry.Attributes,
                entry.IsDirectory ? 0 : entry.Length,
                entry.LastWriteTimeUtc,
                entry.IsDirectory);
        }
    }
}
