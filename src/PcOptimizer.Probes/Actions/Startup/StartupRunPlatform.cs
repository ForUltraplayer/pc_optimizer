/**
 * @file    : StartupRunPlatform.cs
 * @author  : rudals252
 * @brief   : 링크를 따라가지 않는 HKCU Run 고정 핸들에서 원문 재비교 후 값 한 개 변경
 */
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Startup;

internal interface IStartupRunPlatform
{
    RollbackValue Read(string name, ActionSession session);
    bool CompareExchange(string name, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit);
}

internal sealed class StartupRunPlatform : IStartupRunPlatform
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _path;
    internal StartupRunPlatform() { _path = RunPath; }
    // 테스트는 실제 Run과 분리된 GUID 소유 키에만 접근한다.
    internal StartupRunPlatform(Guid fixture) { _path = @"Software\PcOptimizer.Tests\" + fixture.ToString("N") + @"\Run"; }
    private static void CheckSession(ActionSession expected)
    {
        if (expected.Scope != ActionUserScope.Full || !expected.IsKnown || SystemActionSession.Read() != expected) { throw new ActionUnavailableException("SessionChanged"); }
    }
    private SafeRegistryHandle Open(ActionSession session, bool write)
    {
        CheckSession(session);
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        var parent = root.Handle;
        SafeRegistryHandle? owned = null;
        try
        {
            var parts = _path.Split('\\');
            for (var i = 0; i < parts.Length; i++)
            {
                var access = 0x20019 | 0x100 | (write && i == parts.Length - 1 ? 2 : 0); // READ, WOW64_64, SET_VALUE
                var code = RegOpenKeyExW(parent, parts[i], 8, access, out var child); // OPEN_LINK, never resolve a link
                if (code != 0) { child.Dispose(); throw new ActionUnavailableException("StartupReadFailed"); }
                uint size = 0;
                code = RegQueryValueExW(child, "SymbolicLinkValue", IntPtr.Zero, out var type, null, ref size);
                if ((code is 0 or 234 && type == 6) || code is not (0 or 2 or 234))
                { child.Dispose(); throw new ActionUnavailableException("TargetRejected"); }
                owned?.Dispose(); owned = child; parent = child;
            }
            var result = owned!; owned = null; return result;
        }
        finally { owned?.Dispose(); }
    }
    public RollbackValue Read(string name, ActionSession session)
    {
        if (!StartupRegistration.ValidName(name)) { throw new ActionUnavailableException("TargetRejected"); }
        using var key = Open(session, false);
        return ReadValue(key, name);
    }
    private static RollbackValue ReadValue(SafeRegistryHandle key, string name)
    {
        uint size = 0;
        var error = RegQueryValueExW(key, name, IntPtr.Zero, out _, null, ref size);
        if (error == 2) { return StartupRegistration.Absent; }
        if (error != 0 || size > 32768) { throw new ActionUnavailableException("StartupReadFailed"); }
        var data = new byte[size];
        error = RegQueryValueExW(key, name, IntPtr.Zero, out var type, data, ref size);
        if (error != 0 || size > data.Length || type > ushort.MaxValue) { throw new ActionUnavailableException("StartupReadFailed"); }
        return new(true, (int)type, data.AsSpan(0, (int)size).ToArray());
    }
    public bool CompareExchange(string name, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit)
    {
        if (!StartupRegistration.ValidName(name) || !(desired.SameAs(StartupRegistration.Absent) || StartupRegistration.ValidValue(desired))) { return false; }
        using var pinned = Open(session, true);
        if (!ReadValue(pinned, name).SameAs(expected)) { return false; }
        CheckSession(session); beforeCommit();
        if (!ReadValue(pinned, name).SameAs(expected)) { return false; }
        // Windows 값 쓰기에 CAS API는 없다. 마지막 비교와 쓰기 사이 외부 앱과의 원자성을 주장하지 않는다.
        // 원본은 호출자의 내구 journal에 선행 저장되며 실패/관측 불일치에도 남긴다.
        var code = desired.Exists ? RegSetValueExW(pinned, name, 0, (uint)desired.NativeType, desired.Data, desired.Data.Length) : RegDeleteValueW(pinned, name);
        if (code != 0) { throw new ActionUnavailableException("StartupWriteFailed"); }
        return ReadValue(pinned, name).SameAs(desired);
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyExW(SafeRegistryHandle key, string subKey, uint options, int access, out SafeRegistryHandle result);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryValueExW(SafeRegistryHandle key, string name, IntPtr reserved, out uint type, byte[]? data, ref uint size);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegSetValueExW(SafeRegistryHandle key, string name, int reserved, uint type, byte[] data, int size);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteValueW(SafeRegistryHandle key, string name);
}
