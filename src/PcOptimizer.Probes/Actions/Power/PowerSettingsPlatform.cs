/**
 * @file    : PowerSettingsPlatform.cs
 * @author  : rudals252
 * @brief   : 설치 전원 계획 열거 및 현재 사용자 PowerSetActiveScheme 쓰기 경계
 */
using System.Runtime.InteropServices;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Power;

internal interface IPowerSettingsPlatform
{
    IReadOnlyList<Guid> Installed();
    Guid Current();
    byte AcStatus();
    void Set(Guid scheme);
}

internal sealed class PowerSettingsPlatform : IPowerSettingsPlatform
{
    public IReadOnlyList<Guid> Installed()
    {
        var values = new List<Guid>();
        for (uint index = 0; index < 256; index++)
        {
            uint size = 16;
            var error = PowerEnumerate(0, 0, 0, 16, index, out var value, ref size);
            if (error == 259) { return values; } // ERROR_NO_MORE_ITEMS
            if (error != 0 || size != 16 || value == Guid.Empty || values.Contains(value)) { throw new ActionUnavailableException("PowerReadFailed"); }
            values.Add(value);
        }
        throw new ActionUnavailableException("PowerReadFailed");
    }
    public Guid Current() => Win32PowerPlatform.Instance.GetActiveScheme(out var value) == 0 && value != Guid.Empty
        ? value : throw new ActionUnavailableException("PowerReadFailed");
    public byte AcStatus() => Win32PowerPlatform.Instance.GetPowerStatus(out var value) == 0 && value is not null ? value.AcLineStatus : (byte)255;
    public void Set(Guid scheme)
    {
        var error = PowerSetActiveScheme(0, in scheme);
        if (error != 0) { throw new ActionUnavailableException("PowerWriteFailed"); }
    }
    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PowerEnumerate(nint root, nint scheme, nint subgroup, uint access, uint index, out Guid buffer, ref uint size);
    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PowerSetActiveScheme(nint root, in Guid scheme);
}
