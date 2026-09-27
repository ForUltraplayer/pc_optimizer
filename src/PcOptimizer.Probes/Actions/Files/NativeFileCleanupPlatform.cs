/**
 * @file    : NativeFileCleanupPlatform.cs
 * @author  : rudals252
 * @brief   : 조상 핸들 고정·128비트 ID/메타데이터 재확인·검증 핸들 삭제, 경로 재귀 삭제 없음
 */
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Files;

internal sealed class NativeFileCleanupPlatform : IFileCleanupPlatform
{
    public ICleanupFile Open(string path, bool directory, bool delete) => new OpenFile(path, directory, delete);
    public IEnumerable<string> Enumerate(string directory) => Directory.EnumerateFileSystemEntries(directory);
    public FileAttributes Attributes(string path) => File.GetAttributes(path);
    public long? FreeBytes(string root)
    {
        try { return new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    private sealed class OpenFile : ICleanupFile
    {
        private readonly List<SafeFileHandle> _parents = [];
        private readonly List<DirectoryStamp> _stamps = [];
        private readonly SafeFileHandle _file;
        private readonly string _path;
        private readonly bool _delete;
        internal OpenFile(string path, bool directory, bool delete)
        {
            _path = CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, path);
            _delete = delete;
            if (directory && delete) { throw new UnauthorizedAccessException("DirectoryDeletionForbidden"); }
            try
            {
                var ancestors = new Stack<string>();
                for (var parent = Path.GetDirectoryName(_path); parent is not null; parent = Path.GetDirectoryName(parent)) { ancestors.Push(parent); }
                while (ancestors.TryPop(out var ancestor))
                {
                    var handle = OpenHandle(ancestor, directory: true, delete: false);
                    _parents.Add(handle);
                    var stamp = ReadStamp(handle, ancestor, []);
                    if (!stamp.IsDirectory || !stamp.IsPlain) { throw new UnauthorizedAccessException("LinkedAncestor"); }
                    _stamps.Add(new(ancestor, stamp.Identity));
                }
                _file = OpenHandle(_path, directory, delete);
                var node = Read();
                if (node.IsDirectory != directory || !node.IsPlain) { throw new UnauthorizedAccessException("LinkedOrWrongType"); }
            }
            catch { _file?.Dispose(); foreach (var handle in _parents) { handle.Dispose(); } throw; }
        }
        public FileStamp Read() => ReadStamp(_file, _path, _stamps.ToArray());
        public void MarkForDeletion()
        {
            if (!_delete) { throw new UnauthorizedAccessException("ReadOnlyHandle"); }
            byte delete = 1;
            if (!SetFileInformationByHandle(_file, 4, ref delete, 1)) { throw Error("DeleteFailed"); }
        }
        public void Dispose()
        {
            _file.Dispose();
            for (var i = _parents.Count - 1; i >= 0; i--) { _parents[i].Dispose(); }
        }
    }
    private static SafeFileHandle OpenHandle(string path, bool directory, bool delete)
    {
        var handle = NativeMethods.CreateFile(path, NativeMethods.FILE_READ_ATTRIBUTES | (delete ? 0x10000u : 0),
            delete ? 0u : directory ? NativeMethods.FILE_SHARE_READ : 7u, 0, NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_OPEN_REPARSE_POINT | (directory ? NativeMethods.FILE_FLAG_BACKUP_SEMANTICS : 0), 0);
        if (handle.IsInvalid) { var error = Error("OpenFailed"); handle.Dispose(); throw error; }
        return handle;
    }
    private static FileStamp ReadStamp(SafeFileHandle handle, string path, IReadOnlyList<DirectoryStamp> parents)
    {
        if (!NativeMethods.GetFileInformationByHandleEx(handle, 18, out var id, (uint)Marshal.SizeOf<NativeMethods.FileIdInfo>())
            || !GetFileInformationByHandle(handle, out var info)
            || !GetFileInformationByHandleEx(handle, 0, out var basic, (uint)Marshal.SizeOf<BasicInfo>())) { throw Error("MetadataFailed"); }
        if (id.FileIdLow == 0 && id.FileIdHigh == 0) { throw new IOException("FileIdUnavailable"); }
        return new(path, new(id.VolumeSerialNumber, id.FileIdLow, id.FileIdHigh), checked((long)(((ulong)info.SizeHigh << 32) | info.SizeLow)),
            basic.Creation, basic.Write, basic.Change, (FileAttributes)info.Attributes, info.Links, parents);
    }
    private static IOException Error(string code) => new(code, new Win32Exception(Marshal.GetLastWin32Error()));
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicInfo { internal long Creation, Access, Write, Change; internal uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInfoNative
    {
        internal uint Attributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfoNative info);
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out BasicInfo info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref byte info, uint size);
}
