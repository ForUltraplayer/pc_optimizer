/**
 * @file    : NvidiaApi.cs
 * @author  : rudals252
 * @brief   : System32 NVAPI만 로드하는 세션과 공식 DRS ABI. 원문 헤더 출처는 고지 문서 참조
 */
using System.Runtime.InteropServices;
using System.Text;

namespace PcOptimizer.Probes.Actions.Advanced;

internal sealed class NvidiaApi : IDisposable
{
    private readonly nint _module;
    private readonly Query _query;
    private bool _initialized;
    private bool _disposed;
    internal NvidiaApi()
    {
        if (!Environment.Is64BitProcess) { throw new ActionUnavailableException("GpuUnavailable"); }
        _module = DriverLibrary.Load("nvapi64.dll");
        try
        {
            _query = Marshal.GetDelegateForFunctionPointer<Query>(NativeLibrary.GetExport(_module, "nvapi_QueryInterface"));
            Check(Function<Simple>(0x0150E828)()); _initialized = true;
        }
        catch { NativeLibrary.Free(_module); throw; }
    }
    internal T Function<T>(uint id) where T : Delegate
    {
        var pointer = _query(id);
        if (pointer == 0) { throw new ActionUnavailableException("GpuApiUnavailable"); }
        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }
    internal string Stamp()
    {
        var branch = new StringBuilder(64);
        Check(Function<Version>(0x2926AAAD)(out var number, branch));
        return "nv:" + number + ":" + branch;
    }
    internal string DisplayStamp(nint display)
    {
        var gpus = new nint[64]; Check(Function<DisplayGpus>(0x34EF9506)(display, gpus, out var count));
        if (count is 0 or > 64) { throw new ActionUnavailableException("GpuTargetChanged"); }
        var identities = new List<string>();
        for (var i = 0; i < count; i++)
        {
            Check(Function<BusId>(0x1BE0B8E5)(gpus[i], out var bus));
            Check(Function<PciIds>(0x2DDFB66E)(gpus[i], out var device, out var subsystem, out var revision, out var extended));
            identities.Add($"{bus:X}:{device:X}:{subsystem:X}:{revision:X}:{extended:X}");
        }
        identities.Sort(StringComparer.Ordinal);
        return Stamp() + ":" + string.Join(";", identities);
    }
    internal static void Check(int result)
    { if (result != 0) { throw new ActionUnavailableException(result is -137 or -175 ? "GpuAccessDenied" : "GpuApiRejected"); } }
    public void Dispose()
    {
        if (_disposed) { return; } _disposed = true;
        if (_initialized) { _initialized = false; Function<Simple>(0xD22BDD7E)(); }
        NativeLibrary.Free(_module);
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Query(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Simple();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Version(out uint version, [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder branch);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Create(out nint session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SessionCall(nint session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Enumerate(nint session, uint index, out nint profile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal delegate int Find(nint session, string name, out nint profile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ProfileInfo(nint session, nint profile, ref Profile info);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int GetSetting(nint session, nint profile, uint id, ref Setting setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetSetting(nint session, nint profile, ref Setting setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int RemoveSetting(nint session, nint profile, uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int EnumDisplay(uint index, out nint display);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int DisplayName(nint display, [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int GetVideo(nint display, ref VideoGet state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetVideo(nint display, ref VideoSet state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DisplayGpus(nint display, [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = 64)] nint[] gpus, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int BusId(nint gpu, out uint bus);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PciIds(nint gpu, out uint device, out uint subsystem, out uint revision, out uint extended);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    internal struct Profile
    {
        internal uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)] internal string Name;
        internal uint GpuSupport, Predefined, AppCount, SettingCount;
        internal static Profile New() => new() { Version = (uint)Marshal.SizeOf<Profile>() | 0x10000, Name = "" };
    }
    [StructLayout(LayoutKind.Explicit, Size = 12320, Pack = 4)]
    internal struct Setting
    {
        [FieldOffset(0)] internal uint Version;
        [FieldOffset(4100)] internal uint Id;
        [FieldOffset(4104)] internal uint Type;
        [FieldOffset(4108)] internal uint Location;
        [FieldOffset(4112)] internal uint Predefined;
        [FieldOffset(4116)] internal uint PredefinedValid;
        [FieldOffset(4120)] internal uint PredefinedValue;
        [FieldOffset(8220)] internal uint Value;
        internal static Setting New(uint id) => new() { Version = 12320 | 0x10000, Id = id };
    }
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct VideoGet
    {
        [FieldOffset(0)] internal uint Version;
        [FieldOffset(4)] internal uint Component;
        [FieldOffset(8)] internal uint Device;
        [FieldOffset(12)] internal uint Flags;
        [FieldOffset(16)] internal uint Enabled;
        [FieldOffset(24)] internal uint Minimum;
        [FieldOffset(28)] internal uint Maximum;
        [FieldOffset(44)] internal uint Value;
        [FieldOffset(88)] internal uint AlgorithmValue;
        internal static VideoGet New() => new() { Version = 128 | 0x10000, Component = 0x1D };
    }
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct VideoSet
    {
        [FieldOffset(0)] internal uint Version;
        [FieldOffset(4)] internal uint Component;
        [FieldOffset(8)] internal uint Device;
        [FieldOffset(12)] internal uint Flags;
        [FieldOffset(16)] internal uint Enabled;
        [FieldOffset(20)] internal uint Value;
    }
}

internal static class DriverLibrary
{
    internal static nint Load(string name)
    {
        if (name is not ("nvapi64.dll" or "amdadlx64.dll")) { throw new ActionUnavailableException("TargetRejected"); }
        var path = Path.Combine(Environment.SystemDirectory, name);
        if (!File.Exists(path) || !SystemCacheToolBackend.IsPlainPath(path, false)) { throw new ActionUnavailableException("GpuUnavailable"); }
        // 앱/현재 폴더/PATH의 같은 이름 DLL 및 종속 라이브러리를 로드하지 않는다.
        var handle = LoadLibraryExW(path, 0, 0x100 | 0x800);
        if (handle == 0) { throw new ActionUnavailableException("GpuUnavailable"); }
        return handle;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryExW(string file, nint reserved, uint flags);
}
