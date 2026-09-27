/**
 * @file    : AmdVideoPlatform.cs
 * @author  : rudals252
 * @brief   : AMD 공식 ADLX IADLXVideoUpscale의 지원·상태·On/Off를 GPU별로 제어
 */
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Management;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Advanced;

internal sealed class AmdVideoPlatform : IGpuPlatform
{
    internal sealed class Adlx : IDisposable
    {
        private readonly nint _module;
        private readonly List<nint> _owned = [];
        private readonly Terminate _terminate;
        private bool _initialized;
        private bool _disposed;
        private readonly Func<string, string> _driverStamp = DriverStamp;
        private readonly Dictionary<nint, string> _videoStamps = [];
        internal readonly nint List, Multimedia;
        internal readonly string Stamp;
        internal Adlx()
        {
            _module = DriverLibrary.Load("amdadlx64.dll");
            try
            {
                _terminate = Marshal.GetDelegateForFunctionPointer<Terminate>(NativeLibrary.GetExport(_module, "ADLXTerminate"));
                Check(Marshal.GetDelegateForFunctionPointer<Version>(NativeLibrary.GetExport(_module, "ADLXQueryFullVersion"))(out var version));
                var major = version >> 48; var minor = (version >> 32) & 0xFFFF;
                if (major is < 1 or > 2 || (major == 1 && minor < 4)) { throw new ActionUnavailableException("GpuApiUnavailable"); }
                var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(_module, "ADLXInitialize"));
                var result = initialize(version, out var system);
                if (result != 0) { throw new ActionUnavailableException("GpuApiUnavailable"); }
                _initialized = true;
                (List, Multimedia) = OpenInterfaces(system);
                Stamp = "adlx:" + version + ":" + System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory, "amdadlx64.dll")).FileVersion;
            }
            catch { Dispose(); throw; }
        }
        internal Adlx(nint system, Action terminate, Func<string, string> driverStamp)
        {
            _terminate = () => { terminate(); return 0; }; _initialized = true; _driverStamp = driverStamp; Stamp = "fixture-adlx";
            try { (List, Multimedia) = OpenInterfaces(system); }
            catch { Dispose(); throw; }
        }
        private (nint, nint) OpenInterfaces(nint system)
        {
            Check(V<GetInterface>(system, 1)(system, out var list)); Own(list);
            Check(V<Query>(system, 2)(system, "IADLXSystem2", out var system2)); Own(system2);
            Check(V<GetInterface>(system2, 4)(system2, out var multimedia)); Own(multimedia);
            return (list, multimedia);
        }
        private nint Own(nint pointer)
        { if (pointer == 0) { throw new ActionUnavailableException("GpuApiRejected"); } _owned.Add(pointer); return pointer; }
        internal uint Count => V<CountItems>(List, 3)(List);
        internal nint Gpu(uint index) { Check(V<GetAt>(List, 11)(List, index, out var gpu)); return Own(gpu); }
        private string Pnp(nint gpu)
        {
            Check(V<GetString>(gpu, 9)(gpu, out var pnp));
            var identity = Marshal.PtrToStringAnsi(pnp);
            if (string.IsNullOrWhiteSpace(identity)) { throw new ActionUnavailableException("GpuTargetChanged"); }
            return identity;
        }
        internal string Key(nint gpu) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Pnp(gpu).ToUpperInvariant())));
        internal string Name(nint gpu)
        { Check(V<GetString>(gpu, 7)(gpu, out var name)); return Marshal.PtrToStringAnsi(name) ?? "AMD GPU"; }
        internal nint Video(nint gpu)
        {
            Check(V<GetForGpu>(Multimedia, 4)(Multimedia, gpu, out var video)); Own(video);
            _videoStamps[video] = Stamp + ":" + _driverStamp(Pnp(gpu)); return video;
        }
        internal nint Find(string key)
        {
            if (key.Length != 64 || Count > 32) { throw new ActionUnavailableException("TargetRejected"); }
            for (uint i = 0; i < Count; i++) { var gpu = Gpu(i); if (Key(gpu) == key) { return Video(gpu); } }
            throw new ActionUnavailableException("GpuTargetChanged");
        }
        internal GpuSettingState Read(nint video)
        {
            Check(V<GetBool>(video, 3)(video, out var supported));
            if (supported != 1) { throw new ActionUnavailableException("GpuUnavailable"); }
            Check(V<GetBool>(video, 4)(video, out var enabled));
            if (enabled > 1) { throw new ActionUnavailableException("GpuValueUnsupported"); }
            // 선명도는 변경하지 않는다. 외부 선명도 변경을 설정 On/Off의 원복으로 덮어쓰지 않음.
            return new(_videoStamps[video], 0, enabled, enabled == 1);
        }
        internal void Set(nint video, bool enabled) => Check(V<SetBool>(video, 7)(video, enabled ? (byte)1 : (byte)0));
        public void Dispose()
        {
            if (_disposed) { return; } _disposed = true;
            for (var i = _owned.Count - 1; i >= 0; i--) { V<Release>(_owned[i], 1)(_owned[i]); }
            _owned.Clear();
            if (_initialized) { _initialized = false; _terminate(); }
            if (_module != 0) { NativeLibrary.Free(_module); }
        }
    }
    private static string DriverStamp(string pnp)
    {
        // GPU의 PNP가 같은 WMI 장치만 대조한다. PNP를 쿼리에 삽입하거나 기록하지 않는다.
        using var search = new ManagementObjectSearcher("SELECT PNPDeviceID, DriverVersion FROM Win32_VideoController");
        search.Options.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            using var items = search.Get();
            foreach (ManagementObject item in items)
            {
                using (item)
                {
                    if (!string.Equals(item["PNPDeviceID"] as string, pnp, StringComparison.OrdinalIgnoreCase)) { continue; }
                    if (item["DriverVersion"] is string version && System.Version.TryParse(version, out _)) { return version; }
                }
            }
        }
        catch (ManagementException) { throw new ActionUnavailableException("GpuTargetChanged"); }
        throw new ActionUnavailableException("GpuTargetChanged");
    }
    internal static T V<T>(nint pointer, int slot) where T : Delegate
    {
        if (pointer == 0) { throw new ActionUnavailableException("GpuApiRejected"); }
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size));
    }
    internal static void Check(int result) { if (result is not (0 or 1)) { throw new ActionUnavailableException("GpuApiRejected"); } }
    public IReadOnlyList<GpuOptionTarget> Enumerate(CancellationToken ct)
    {
        using var api = new Adlx(); var items = new List<GpuOptionTarget>();
        if (api.Count > 32) { throw new ActionUnavailableException("GpuEnumerationIncomplete"); }
        for (uint i = 0; i < api.Count; i++)
        {
            ct.ThrowIfCancellationRequested(); var gpu = api.Gpu(i);
            try
            {
                var video = api.Video(gpu); _ = api.Read(video);
                items.Add(new(GpuFeature.AmdVideo, api.Key(gpu), api.Name(gpu),
                    "AMD 공식 ADLX 영상 업스케일링입니다. Radeon Image Sharpening이 켜져 있으면 이 설정이 무시될 수 있습니다. 선명도·게임용 RSR·가상 초고해상도는 변경하지 않습니다.", [new(0, "끄기"), new(1, "켜기")]));
            }
            catch (ActionUnavailableException) { }
        }
        return items;
    }
    public GpuSettingState Read(string key) { using var api = new Adlx(); return api.Read(api.Find(key)); }
    public GpuSettingState Desired(string key, GpuSettingState before, int choice)
    {
        if (choice is not (0 or 1)) { throw new ActionUnavailableException("TargetRejected"); }
        return before with { Value = choice, Enabled = choice == 1 };
    }
    public bool CompareExchange(string key, GpuSettingState expected, GpuSettingState desired, Action beforeCommit)
    {
        using var api = new Adlx(); var video = api.Find(key);
        if (api.Read(video) != expected || desired.Stamp != expected.Stamp || desired.SettingId != 0 || desired.Value != (desired.Enabled ? 1 : 0)) { return false; }
        beforeCommit(); api.Set(video, desired.Enabled); return true;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Version(out ulong version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Initialize(ulong version, out nint system);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Terminate();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Release(nint self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetInterface(nint self, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] internal delegate int Query(nint self, string id, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate uint CountItems(nint self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetAt(nint self, uint index, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetString(nint self, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetForGpu(nint self, nint gpu, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetBool(nint self, out byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int SetBool(nint self, byte value);
}
