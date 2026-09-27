/**
 * @file    : DisplaySettingsPlatform.cs
 * @author  : rudals252
 * @brief   : 물리 출력 재식별·동일 형태 지원 모드·주사율 전용 Win32 쓰기
 */
using System.Runtime.InteropServices;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Actions.Display;

internal interface IDisplayModeApi
{
    DisplayFrame Read(string device, uint index);
    int Change(string device, DisplayFrame current, int hz, uint flags);
}

internal sealed class DisplaySettingsPlatform(IDisplayPlatform topology, IDisplayModeApi modes, Func<bool>? interactive = null) : IDisplaySettingsPlatform
{
    internal DisplaySettingsPlatform() : this(Win32DisplayPlatform.Instance, new DisplayModeApi()) { }
    internal const uint CurrentSettings = 0xffffffff, RegisteredSettings = 0xfffffffe;
    public IReadOnlyList<DisplayTrialChoice> Choices()
    {
        var topologyReading = topology.QueryActivePaths();
        if (topologyReading.Error != 0) { throw new ActionUnavailableException("DisplayReadFailed"); }
        var choices = new List<DisplayTrialChoice>();
        var number = 0;
        foreach (var path in topologyReading.Paths)
        {
            number++;
            try
            {
                var identity = Identify(path.MonitorDevicePath ?? "");
                var observed = Observe(identity);
                if (!SameShape(observed.Current.Mode, observed.Registered.Mode)) { continue; }
                foreach (var hz in DisplayModeMatcher.SameShapeRefreshRates(observed.Current.Mode, topology.EnumerateModes(identity.GdiName).Modes))
                {
                    if (hz == observed.Current.Mode.RefreshHz) { continue; }
                    choices.Add(new(identity.MonitorPath, $"화면 {number} · {observed.Current.Mode.Width} × {observed.Current.Mode.Height} · {observed.Current.Mode.RefreshHz} → {hz} Hz", observed.Current.Mode.RefreshHz, hz));
                }
            }
            catch (ActionUnavailableException) { /* 지원하지 않는 출력은 시험 후보로 만들지 않는다. */ }
        }
        return choices;
    }
    public DisplayChange Capture(string monitorPath, int hz)
    {
        var identity = Identify(monitorPath);
        var observed = Observe(identity);
        if (!SameShape(observed.Current.Mode, observed.Registered.Mode)) { throw Reject(); }
        if (hz == observed.Current.Mode.RefreshHz) { throw new ActionUnavailableException("AlreadyApplied"); }
        var desiredMode = observed.Current.Mode with { RefreshHz = hz };
        RequireSupported(identity, desiredMode);
        // 선택 모드는 드라이버 열거 목록과 일치해야 하며 원문의 주사율 필드만 바꾼다.
        var bytes = observed.Current.Native.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(184, 4), hz);
        return new(identity, observed.Current, observed.Registered, new(desiredMode, bytes));
    }
    public DisplayObservation Observe(DisplayIdentity identity)
    {
        if (Identify(identity.MonitorPath) != identity) { throw new ActionUnavailableException("DisplayDeviceChanged"); }
        var current = modes.Read(identity.GdiName, CurrentSettings);
        var registered = modes.Read(identity.GdiName, RegisteredSettings);
        ValidateFrame(current); ValidateFrame(registered);
        return new(current, registered);
    }
    public int Test(DisplayIdentity identity, DisplayFrame frame)
        => Change(identity, frame, 2, null, null);
    public int Apply(DisplayIdentity identity, DisplayFrame frame, bool persist, DisplayObservation expected, Action beforeWrite)
        => Change(identity, frame, persist ? 1u : 0u, expected, beforeWrite);
    public int SaveProfileOnly(DisplayIdentity identity, DisplayFrame frame, DisplayObservation expected, Action beforeWrite)
        => Change(identity, frame, 0x10000001, expected, beforeWrite);
    private int Change(DisplayIdentity identity, DisplayFrame frame, uint flags, DisplayObservation? expected, Action? beforeWrite)
    {
        ValidateFrame(frame);
        RequireSupported(identity, frame.Mode);
        var actual = Observe(identity);
        if (!SameShape(actual.Current.Mode, frame.Mode)) { throw Reject(); }
        if (expected is not null && (!actual.Current.SameMode(expected.Current) || !actual.Registered.SameMode(expected.Registered)))
        { throw new ActionUnavailableException("CurrentValueChanged"); }
        // OS API에는 compare-exchange가 없으므로 최종 관측과 호출 사이 외부 변경의 원자성은 보장하지 않는다.
        beforeWrite?.Invoke();
        return modes.Change(identity.GdiName, actual.Current, frame.Mode.RefreshHz, flags);
    }
    private DisplayIdentity Identify(string monitorPath)
    {
        if (!(interactive?.Invoke() ?? Environment.UserInteractive) || topology.IsRemoteSession() || string.IsNullOrWhiteSpace(monitorPath)) { throw Reject(); }
        var paths = topology.QueryActivePaths();
        var matches = paths.Paths.Where(p => string.Equals(p.MonitorDevicePath, monitorPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (paths.Error != 0 || matches.Length != 1) { throw new ActionUnavailableException("DisplayDeviceChanged"); }
        var path = matches[0];
        // VGA/DVI/HDMI/LVDS/DP/내장 출력만 허용. 간접·무선·가상·복제 출력은 지원하지 않는다.
        if (path.OutputTechnology is not (0 or 4 or 5 or 6 or 10 or 11 or unchecked((int)0x80000000))
            || path.TargetNameError != 0 || path.SourceNameError != 0 || path.AdapterNameError != 0
            || string.IsNullOrWhiteSpace(path.GdiDeviceName) || string.IsNullOrWhiteSpace(path.AdapterDevicePath)
            || paths.Paths.Count(p => string.Equals(p.GdiDeviceName, path.GdiDeviceName, StringComparison.OrdinalIgnoreCase)) != 1
            || paths.Paths.Count(p => p.AdapterLuid == path.AdapterLuid && p.SourceId == path.SourceId) != 1)
        { throw Reject(); }
        var adapter = topology.GetAdapterName(path.GdiDeviceName);
        if (string.IsNullOrWhiteSpace(adapter) || new[] { "virtual", "indirect", "remote", "displaylink" }.Any(s => adapter.Contains(s, StringComparison.OrdinalIgnoreCase)))
        { throw Reject(); }
        return new(path.MonitorDevicePath!, path.AdapterDevicePath, path.GdiDeviceName, path.AdapterLuid, path.SourceId, path.TargetId, path.OutputTechnology);
    }
    private void RequireSupported(DisplayIdentity identity, DisplayMode mode)
    {
        if (!topology.EnumerateModes(identity.GdiName).Modes.Contains(mode)) { throw Reject(); }
    }
    internal static bool SameShape(DisplayMode a, DisplayMode b) => a with { RefreshHz = b.RefreshHz } == b;
    internal static void ValidateFrame(DisplayFrame frame)
    {
        if (frame?.Native is not { Length: 220 } || frame.Mode is null) { throw Reject(); }
        var raw = MemoryMarshal.Read<DevModeW>(frame.Native);
        if (raw.Size != 220 || raw.DriverExtra != 0 || ToMode(raw) != frame.Mode || frame.Mode.Interlaced
            || frame.Mode.Width <= 0 || frame.Mode.Height <= 0 || frame.Mode.BitsPerPixel != 32
            || frame.Mode.Orientation is < 0 or > 3 || frame.Mode.RefreshHz <= 1) { throw Reject(); }
    }
    internal static DisplayMode ToMode(DevModeW raw) => new((int)raw.PelsWidth, (int)raw.PelsHeight,
        (raw.Fields & NativeMethods.DM_DISPLAYORIENTATION) != 0 ? (int)raw.DisplayOrientation : 0,
        (int)raw.BitsPerPel, (raw.DisplayFlags & NativeMethods.DM_INTERLACED) != 0, (int)raw.DisplayFrequency);
    private static ActionUnavailableException Reject() => new("DisplayUnsupported");
}

internal sealed class DisplayModeApi : IDisplayModeApi
{
    public unsafe DisplayFrame Read(string device, uint index)
    {
        if (!DisplayStructSizes.AllMatch()) { throw new ActionUnavailableException("DisplayUnsupported"); }
        var raw = new DevModeW { Size = 220 };
        if (!NativeMethods.EnumDisplaySettingsEx(device, index, &raw, 0)) { throw new ActionUnavailableException("DisplayReadFailed"); }
        var bytes = new byte[220]; MemoryMarshal.Write(bytes, in raw);
        return new(DisplaySettingsPlatform.ToMode(raw), bytes);
    }
    public int Change(string device, DisplayFrame current, int hz, uint flags)
    {
        DisplaySettingsPlatform.ValidateFrame(current);
        var raw = MemoryMarshal.Read<DevModeW>(current.Native);
        raw.Fields = 0x00400000; // DM_DISPLAYFREQUENCY: 위치/해상도/색심도 등은 쓰지 않는다.
        raw.DisplayFrequency = checked((uint)hz);
        return ChangeDisplaySettingsEx(device, ref raw, 0, flags, 0);
    }
    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ChangeDisplaySettingsEx(string device, ref DevModeW mode, nint window, uint flags, nint parameter);
}
