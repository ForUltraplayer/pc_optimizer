/**
 * @file    : AdvancedRegistryPlatform.cs
 * @author  : rudals252
 * @brief   : 고정 레지스트리 경로를 링크 없이 열어 DWORD 원본을 재비교·변경
 */
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Advanced;

internal interface IAdvancedRegistryPlatform
{
    string Identity(AdvancedOption option, ActionSession session);
    RollbackValue Read(AdvancedOption option, ActionSession session);
    bool CompareExchange(AdvancedOption option, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit);
}

internal sealed class AdvancedRegistryPlatform : IAdvancedRegistryPlatform
{
    private readonly Guid? _fixture;
    internal AdvancedRegistryPlatform(Guid? fixture = null) { _fixture = fixture; }
    public string Identity(AdvancedOption option, ActionSession session)
    {
        CheckSession(option, session);
        if (_fixture is not null || option != AdvancedOption.Hags) { return option.ToString(); }
        // CurrentControlSet은 Windows가 만든 REG_LINK이므로 이를 추종하지 않고 Select의 숫자로 고정 경로를 만든다.
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var select = root.OpenSubKey(@"SYSTEM\Select", false);
        if (select?.GetValue("Current") is not int number || number is < 1 or > 999) { throw new ActionUnavailableException("AdvancedKeyUnavailable"); }
        return "ControlSet" + number.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
    }
    private static (RegistryHive Hive, string Path, string Name) Location(AdvancedOption option) => option switch
    {
        AdvancedOption.Mpo => (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode"),
        AdvancedOption.Hags => (RegistryHive.LocalMachine, Hardware.GraphicsSettingsProbe.HAGS_SUB_KEY, Hardware.GraphicsSettingsProbe.HAGS_VALUE_NAME),
        AdvancedOption.GameMode => (RegistryHive.CurrentUser, Hardware.GameModeSettingsProbe.GAME_MODE_SUB_KEY, Hardware.GameModeSettingsProbe.GAME_MODE_VALUE_NAME),
        _ => throw new ActionUnavailableException("TargetRejected"),
    };
    private static void CheckSession(AdvancedOption option, ActionSession session)
    {
        if (!session.IsKnown || (option == AdvancedOption.GameMode && session.Scope != ActionUserScope.Full)
            || SystemActionSession.Read(session.Scope) != session) { throw new ActionUnavailableException("SessionChanged"); }
    }
    private SafeRegistryHandle Open(AdvancedOption option, ActionSession session, bool write)
    {
        CheckSession(option, session);
        var location = Location(option);
        using var root = RegistryKey.OpenBaseKey(_fixture is null ? location.Hive : RegistryHive.CurrentUser, RegistryView.Registry64);
        var path = _fixture is { } fixture ? @"Software\PcOptimizer.Tests\" + fixture.ToString("N") + @"\Advanced"
            : option == AdvancedOption.Hags ? @"SYSTEM\" + Identity(option, session) + @"\Control\GraphicsDrivers" : location.Path;
        SafeRegistryHandle? owned = null;
        var parent = root.Handle;
        try
        {
            var parts = path.Split('\\');
            for (var index = 0; index < parts.Length; index++)
            {
                var code = RegOpenKeyExW(parent, parts[index], 8, 0x20119 | (write && index == parts.Length - 1 ? 2 : 0), out var child);
                if (code != 0) { child.Dispose(); throw new ActionUnavailableException("AdvancedKeyUnavailable"); }
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
    public RollbackValue Read(AdvancedOption option, ActionSession session)
    {
        using var key = Open(option, session, false);
        return ReadValue(key, Location(option).Name);
    }
    private static RollbackValue ReadValue(SafeRegistryHandle key, string name)
    {
        uint size = 4;
        var data = new byte[4];
        var code = RegQueryValueExW(key, name, IntPtr.Zero, out var type, data, ref size);
        if (code == 2) { return AdvancedOptions.Absent; }
        if (code != 0 || type != 4 || size != 4) { throw new ActionUnavailableException("AdvancedValueUnsupported"); }
        return new(true, 4, data);
    }
    public bool CompareExchange(AdvancedOption option, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit)
    {
        if (AdvancedOptions.Interpret(option, expected) is null || AdvancedOptions.Interpret(option, desired) is null) { return false; }
        using var key = Open(option, session, true);
        var name = Location(option).Name;
        if (!ReadValue(key, name).SameAs(expected)) { return false; }
        CheckSession(option, session); beforeCommit();
        if (!ReadValue(key, name).SameAs(expected)) { return false; }
        // Windows는 원자적 레지스트리 CAS를 제공하지 않는다. 고정 핸들 재비교 뒤 한 값만 변경한다.
        var code = desired.Exists ? RegSetValueExW(key, name, 0, 4, desired.Data, desired.Data.Length) : RegDeleteValueW(key, name);
        if (code != 0 && !(code == 2 && !desired.Exists)) { throw new ActionUnavailableException("AdvancedWriteFailed"); }
        return ReadValue(key, name).SameAs(desired);
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
