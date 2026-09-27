/**
 * @file    : FileCleanupContracts.cs
 * @author  : rudals252
 * @brief   : 고정 파일 집합과 핸들 기반 삭제 플랫폼 계약
 */
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Files;

internal sealed record DirectoryStamp(string Path, FileIdentity Identity);
internal sealed record FileStamp(string Path, FileIdentity Identity, long Length, long Created, long Written, long Changed,
    FileAttributes Attributes, uint Links, IReadOnlyList<DirectoryStamp> Parents)
{
    internal bool Matches(FileStamp other) => Path.Equals(other.Path, StringComparison.OrdinalIgnoreCase)
        && Identity == other.Identity && Length == other.Length && Created == other.Created && Written == other.Written
        && Changed == other.Changed && Attributes == other.Attributes && Links == other.Links && Parents.SequenceEqual(other.Parents);
    internal bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;
    internal bool IsPlain => (Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x40000 | (FileAttributes)0x400000)) == 0;
}
internal sealed record CleanupTarget(string Key, string Root, string Label, bool Recursive, string[] Patterns, TimeSpan MinimumAge, Func<string, bool> Protected);
internal interface ICleanupFile : IDisposable
{
    FileStamp Read();
    void MarkForDeletion();
}
internal interface IFileCleanupPlatform
{
    ICleanupFile Open(string path, bool directory, bool delete);
    IEnumerable<string> Enumerate(string directory);
    FileAttributes Attributes(string path);
    long? FreeBytes(string root);
}
internal sealed record FileCleanupSnapshot(CleanupTarget Target, FileStamp Root, IReadOnlyList<FileStamp> Files, long Bytes, int Excluded);
