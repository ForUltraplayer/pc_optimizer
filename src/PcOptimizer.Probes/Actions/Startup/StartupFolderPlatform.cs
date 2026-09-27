/**
 * @file    : StartupFolderPlatform.cs
 * @author  : rudals252
 * @brief   : 시작 바로가기 원본을 같은 볼륨의 시작 폴더 밖으로 핸들 이동하고 변조·충돌 시 복원을 거절
 */
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions.Files;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Startup;

internal sealed class StartupFolderPlatform(bool common) : IStartupRunPlatform
{
    private readonly string? _fixtureRoot;
    // 네이티브 시험은 임의 사용자 경로가 아닌 소유 GUID 임시 디렉터리로만 제한한다.
    internal StartupFolderPlatform(Guid fixtureId) : this(false)
    {
        if (fixtureId == Guid.Empty) { throw new ArgumentException(nameof(fixtureId)); }
        _fixtureRoot = Path.Combine(Path.GetTempPath(), "PcOptimizer.StartupFixture." + fixtureId.ToString("N"), "Startup");
    }
    internal static bool ValidName(string name) => StartupRegistration.ValidName(name) && name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
        && name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|', '~']) < 0 && !name.EndsWith(' ') && !name.EndsWith('.')
        && name is not "." and not "..";
    internal static bool ValidValue(RollbackValue value) => value.Exists && value.NativeType == 900 && value.Data.Length == 116;
    private string Root(ActionSession session)
    {
        if (!session.IsKnown || (!common && session.Scope != ActionUserScope.Full) || SystemActionSession.Read(session.Scope) != session)
        { throw new ActionUnavailableException("ScopeExcluded"); }
        if (_fixtureRoot is not null) { return _fixtureRoot; }
        _ = RollbackStore.CurrentRoot(session.Sid);
        string Canonical(string p) => CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, p);
        var basis = Environment.GetFolderPath(common ? Environment.SpecialFolder.CommonApplicationData : Environment.SpecialFolder.ApplicationData);
        var expected = Canonical(Path.Combine(basis, @"Microsoft\Windows\Start Menu\Programs\Startup"));
        var actual = Canonical(Environment.GetFolderPath(common ? Environment.SpecialFolder.CommonStartup : Environment.SpecialFolder.Startup));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) { throw new ActionUnavailableException("StartupFolderUnsupported"); }
        return actual;
    }
    public RollbackValue Read(string name, ActionSession session)
    {
        if (!ValidName(name)) { throw new ActionUnavailableException("StartupFolderUnsupported"); }
        var root = Root(session);
        using var pinned = new NativeFileCleanupPlatform().Open(root, true, false);
        using var file = Open(Path.Combine(root, name));
        return file is null ? StartupRegistration.Absent : Fingerprint(file);
    }
    public bool CompareExchange(string name, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit)
    {
        if (!ValidName(name)) { return false; }
        var before = desired.Exists ? desired : expected;
        if (!ValidValue(before) || expected.Exists == desired.Exists) { return false; }
        var root = Root(session);
        using var pinned = new NativeFileCleanupPlatform().Open(root, true, false); // 모든 조상과 Startup을 이동/교체로부터 고정
        var original = Path.Combine(root, name);
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant() + Convert.ToHexString(before.Data))));
        var parked = Path.Combine(Path.GetDirectoryName(root)!, ".PcOptimizer-disabled-" + suffix + ".lnk.disabled");
        var source = desired.Exists ? parked : original;
        var destination = desired.Exists ? original : parked;
        using var file = Open(source);
        if (file is null || !Fingerprint(file).SameAs(before)) { throw new ActionUnavailableException("StartupShortcutChanged"); }
        // 덮어쓰지 않는 핸들 rename으로 내용·ACL·스트림·파일 ID를 유지한다. 바로가기를 해석/실행하지 않는다.
        beforeCommit();
        if (Root(session) != root || !Fingerprint(file).SameAs(before)) { return false; }
        Rename(file.SafeFileHandle, destination);
        return Fingerprint(file).SameAs(before);
    }
    private static FileStream? Open(string path)
    {
        var handle = CreateFileW(path, 0x80000000u | 0x10000u | 0x20000u, 0, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error(); handle.Dispose();
            if (error == 2) { return null; }
            throw new IOException("StartupShortcutOpen", new Win32Exception(error));
        }
        return new FileStream(handle, FileAccess.Read);
    }
    private static RollbackValue Fingerprint(FileStream file)
    {
        var h = file.SafeFileHandle;
        if (!GetFileInformationByHandle(h, out var info) || !NativeMethods.GetFileInformationByHandleEx(h, 18, out var id, (uint)Marshal.SizeOf<NativeMethods.FileIdInfo>())
            || info.Links != 1 || (info.Attributes & ~(uint)(FileAttributes.Archive | FileAttributes.Normal)) != 0
            || info.SizeHigh != 0 || info.SizeLow is < 76 or > 32768 || (id.FileIdLow == 0 && id.FileIdHigh == 0))
        { throw new ActionUnavailableException("StartupFolderUnsupported"); }
        var streams = new byte[65536];
        if (!GetFileInformationByHandleEx(h, 7, streams, (uint)streams.Length) || BitConverter.ToUInt32(streams, 0) != 0
            || BitConverter.ToUInt32(streams, 4) != 14 || Encoding.Unicode.GetString(streams, 24, 14) != "::$DATA")
        { throw new ActionUnavailableException("StartupFolderUnsupported"); }
        file.Position = 0; var bytes = new byte[info.SizeLow]; file.ReadExactly(bytes);
        if (BitConverter.ToUInt32(bytes, 0) != 76 || new Guid(bytes.AsSpan(4, 16)) != new Guid("00021401-0000-0000-C000-000000000046"))
        { throw new ActionUnavailableException("StartupFolderUnsupported"); }
        var security = new byte[16384];
        if (!GetKernelObjectSecurity(h, 7, security, (uint)security.Length, out var securityLength) || securityLength > security.Length)
        { throw new ActionUnavailableException("StartupFolderUnsupported"); }
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        writer.Write(id.VolumeSerialNumber); writer.Write(id.FileIdLow); writer.Write(id.FileIdHigh);
        writer.Write(info.Creation); writer.Write(info.Write); writer.Write((long)info.SizeLow); writer.Write(info.Attributes);
        writer.Write(SHA256.HashData(bytes)); writer.Write(SHA256.HashData(security.AsSpan(0, (int)securityLength)));
        return new(true, 900, buffer.ToArray());
    }
    private static void Rename(SafeFileHandle file, string destination)
    {
        var name = Encoding.Unicode.GetBytes(destination);
        var offset = IntPtr.Size == 8 ? 20 : 12;
        var bytes = new byte[offset + name.Length]; // ReplaceIfExists=false, RootDirectory=NULL
        BitConverter.GetBytes(name.Length).CopyTo(bytes, offset - 4); name.CopyTo(bytes, offset);
        if (!SetFileInformationByHandle(file, 3, bytes, (uint)bytes.Length))
        { throw new IOException("StartupShortcutRename", new Win32Exception(Marshal.GetLastWin32Error())); }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Info { internal uint Attributes; internal long Creation, Access, Write; internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out Info info);
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int kind, [Out] byte[] data, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, byte[] data, uint length);
    [DllImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(SafeFileHandle file, uint parts, [Out] byte[] data, uint length, out uint needed);
}
