/**
 * @file    : RollbackStore.cs
 * @author  : rudals252
 * @brief   : 관리자 소유 전용 ACL·경로 핸들 고정·독점 잠금·상한·원자 교체 복구 저장소
 */
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions;

/// <summary>현재 계정의 로컬 프로필 아래에만 기록합니다. 기존의 느슨한 ACL은 고쳐서 신뢰하지 않고 거절합니다.</summary>
public sealed class RollbackStore : IRollbackStore
{
    private readonly string? _fixtureRoot;
    /// <summary>실제 저장 위치는 환경 변수 대신 현재 토큰과 등록된 프로필을 대조하여 결정합니다.</summary>
    public RollbackStore() { }
    internal RollbackStore(string fixtureRoot) { _fixtureRoot = Path.GetFullPath(fixtureRoot); }

    /// <summary>작업 수명 동안 저장소 경로와 프로세스 간 독점 잠금을 유지합니다.</summary>
    public IRollbackTransaction Open(ActionSession session)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!session.IsKnown || session.Sid != identity.User?.Value
            || session.SessionId != System.Diagnostics.Process.GetCurrentProcess().SessionId
            || !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        { throw new UnauthorizedAccessException("SessionOrElevationRequired"); }
        var root = _fixtureRoot ?? CurrentRoot(session.Sid);
        return new Transaction(root, session.Sid);
    }
    internal static string CurrentRoot(string sid)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + sid);
        var registered = key?.GetValue("ProfileImagePath") as string;
        if (string.IsNullOrWhiteSpace(registered) || !SamePath(registered, profile)
            || !SamePath(Path.Combine(profile, "AppData", "Local"), local))
        { throw new UnauthorizedAccessException("UnverifiedProfile"); }
        var owner = new DirectoryInfo(profile).GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier))?.Value;
        if (owner != sid && owner != AdminSid && owner != SystemSid) { throw new UnauthorizedAccessException("ProfileOwner"); }
        return Path.Combine(local, "PcOptimizer", "rollback");
    }
    private static bool SamePath(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
    private const string AdminSid = "S-1-5-32-544";
    private const string SystemSid = "S-1-5-18";

    private sealed class Transaction : IRollbackTransaction
    {
        private readonly string _root;
        private readonly string _sid;
        private readonly List<SafeFileHandle> _pins = [];
        private FileStream? _lock;
        private bool _disposed;
        internal Transaction(string root, string sid)
        {
            _root = root;
            _sid = sid;
            try
            {
                if (!Path.IsPathFullyQualified(root) || root.StartsWith(@"\\", StringComparison.Ordinal)
                    || root.AsSpan(2).Contains(':')) { throw new UnauthorizedAccessException("LocalPathRequired"); }
                // 조상부터 OPEN_REPARSE_POINT로 열고 삭제/쓰기 공유를 막는다. 확인 후 바꿔치기를 허용하지 않는다.
                var ancestors = new Stack<string>();
                for (var path = new DirectoryInfo(root); path is not null; path = path.Parent) { ancestors.Push(path.FullName); }
                while (ancestors.TryPop(out var path))
                {
                    if (!Directory.Exists(path))
                    {
                        // 기존 프로필 구조는 만들지 않는다. 앱 폴더와 rollback 루트만 생성한다.
                        if (!SamePath(path, root) && !SamePath(path, Path.GetDirectoryName(root)!)) { throw new DirectoryNotFoundException("ProfileMissing"); }
                        if (SamePath(path, root)) { Native.CreateProtectedDirectory(path); }
                        else { Directory.CreateDirectory(path); }
                    }
                    var handle = Native.Open(path, 0x20080, 1, 3, directory: true);
                    try { Native.CheckPlain(handle, directory: true); }
                    catch { handle.Dispose(); throw; }
                    _pins.Add(handle);
                }
                CheckAcl(new DirectoryInfo(root).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner), true);
                var lockPath = Path.Combine(root, "store.lock");
                _lock = Native.OpenFile(lockPath, write: true, createNew: !File.Exists(lockPath), exclusive: true);
                CheckAcl(_lock.GetAccessControl(), false);
            }
            catch { Dispose(); throw; }
        }
        public RollbackRecord? Read(Guid id)
        {
            EnsureOpen();
            if (id == Guid.Empty) { throw new InvalidDataException("InvalidId"); }
            var path = RecordPath(id);
            if (!File.Exists(path)) { return null; }
            using var file = Native.OpenFile(path, false, false);
            CheckAcl(file.GetAccessControl(), false);
            if (file.Length > RollbackCodec.MaxBytes) { throw new InvalidDataException("RecordTooLarge"); }
            var bytes = new byte[checked((int)file.Length)];
            file.ReadExactly(bytes);
            var record = RollbackCodec.Decode(bytes, _sid);
            if (record.Id != id) { throw new InvalidDataException("RecordIdMismatch"); }
            return record;
        }
        public RollbackCatalog ReadAll()
        {
            EnsureOpen();
            var records = new List<RollbackRecord>();
            var issues = new List<RollbackIssue>();
            var entries = Directory.EnumerateFileSystemEntries(_root).Take(10001).ToArray();
            if (entries.Length > 10000) { throw new InvalidDataException("TooManyRecords"); }
            foreach (var path in entries)
            {
                var name = Path.GetFileName(path);
                if (name == "store.lock") { continue; }
                if (!name.EndsWith(".json", StringComparison.Ordinal) || !Guid.TryParseExact(name[..^5], "N", out var id))
                { issues.Add(new("unknown", "UnexpectedOrPartialFile")); continue; }
                try
                {
                    var record = Read(id);
                    if (record is null) { issues.Add(new(name, "MissingRecord")); }
                    else { records.Add(record); }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
                { issues.Add(new(name, "UntrustedRecord")); }
            }
            return new(records, issues);
        }
        public void Save(RollbackRecord record)
        {
            EnsureOpen();
            var bytes = RollbackCodec.Encode(record, _sid);
            RollbackCodec.CheckTransition(Read(record.Id), record);
            var temporary = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".pending");
            // 파일 생성 순간부터 관리자 소유/전용 ACL이다. 생성 후 ACL을 고치는 시간 창이 없다.
            using (var file = Native.OpenFile(temporary, true, true))
            {
                CheckAcl(file.GetAccessControl(), false);
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            // 같은 볼륨 원자 교체. 실패/부분 파일은 숨기거나 성공으로 처리하지 않는다.
            Native.Commit(temporary, RecordPath(record.Id));
            using var committed = Native.OpenFile(RecordPath(record.Id), true, false);
            committed.Flush(flushToDisk: true);
        }
        public int PruneCompleted(DateTimeOffset olderThan)
        {
            EnsureOpen();
            var count = 0;
            foreach (var record in ReadAll().Records.Where(r => r.CanExpire && r.UpdatedAt < olderThan))
            { File.Delete(RecordPath(record.Id)); count++; }
            return count;
        }
        private string RecordPath(Guid id) => Path.Combine(_root, id.ToString("N") + ".json");
        private void EnsureOpen() { ObjectDisposedException.ThrowIf(_disposed, this); }
        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _lock?.Dispose();
            for (var i = _pins.Count - 1; i >= 0; i--) { _pins[i].Dispose(); }
        }
    }
    private static void CheckAcl(FileSystemSecurity security, bool directory)
    {
        if (!security.AreAccessRulesProtected || security.GetOwner(typeof(SecurityIdentifier))?.Value != AdminSid)
        { throw new UnauthorizedAccessException("UntrustedOwnerOrAcl"); }
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        if (rules.Length != 2 || rules.Select(r => r.IdentityReference.Value).Distinct().Count() != 2
            || rules.Any(r => r.IdentityReference.Value is not (AdminSid or SystemSid) || r.AccessControlType != AccessControlType.Allow
                || r.FileSystemRights != FileSystemRights.FullControl || r.IsInherited || r.PropagationFlags != PropagationFlags.None
                || r.InheritanceFlags != (directory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None)))
        { throw new UnauthorizedAccessException("UntrustedAcl"); }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes { internal int Length; internal nint Descriptor; internal int Inherit; }
        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            internal uint Attributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectoryW(string path, ref SecurityAttributes security);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision, out nint descriptor, out uint size);
        [DllImport("kernel32.dll")]
        private static extern nint LocalFree(nint pointer);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileExW(string existing, string destination, uint flags);

        internal static SafeFileHandle Open(string path, uint access, uint share, uint creation, bool directory, nint security = 0)
        {
            var handle = CreateFileW(path, access, share, security, creation, 0x00200000u | (directory ? 0x02000000u : 0), 0);
            if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new IOException("OpenFailed", new Win32Exception(error)); }
            return handle;
        }
        internal static void CheckPlain(SafeFileHandle handle, bool directory)
        {
            if (!GetFileInformationByHandle(handle, out var info)) { throw new IOException("FileInformationFailed", new Win32Exception(Marshal.GetLastWin32Error())); }
            if ((info.Attributes & (uint)FileAttributes.ReparsePoint) != 0 || ((info.Attributes & (uint)FileAttributes.Directory) != 0) != directory
                || (!directory && info.Links != 1)) { throw new UnauthorizedAccessException("LinkOrTypeRejected"); }
        }
        private static nint Descriptor(bool directory)
        {
            var inherit = directory ? "OICI" : "";
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW($"O:BAG:BAD:P(A;{inherit};FA;;;SY)(A;{inherit};FA;;;BA)", 1, out var pointer, out _))
            { throw new IOException("SecurityDescriptorFailed", new Win32Exception(Marshal.GetLastWin32Error())); }
            return pointer;
        }
        internal static void CreateProtectedDirectory(string path)
        {
            var pointer = Descriptor(true);
            try
            {
                var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = pointer };
                if (!CreateDirectoryW(path, ref security)) { throw new IOException("CreateDirectoryFailed", new Win32Exception(Marshal.GetLastWin32Error())); }
            }
            finally { LocalFree(pointer); }
        }
        internal static FileStream OpenFile(string path, bool write, bool createNew, bool exclusive = false)
        {
            var descriptor = createNew ? Descriptor(false) : 0;
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            var attributes = createNew ? Marshal.AllocHGlobal(security.Length) : 0;
            try
            {
                if (createNew) { Marshal.StructureToPtr(security, attributes, false); }
                var handle = Open(path, write ? 0xC0020000 : 0x80020000, exclusive ? 0u : 1u, createNew ? 1u : 3u, false, attributes);
                try { CheckPlain(handle, false); return new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read); }
                catch { handle.Dispose(); throw; }
            }
            finally
            {
                if (attributes != 0) { Marshal.FreeHGlobal(attributes); }
                if (descriptor != 0) { LocalFree(descriptor); }
            }
        }
        internal static void Commit(string source, string destination)
        {
            if (!MoveFileExW(source, destination, 0x1 | 0x8)) { throw new IOException("AtomicCommitFailed", new Win32Exception(Marshal.GetLastWin32Error())); }
        }
    }
}
