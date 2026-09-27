/**
 * @file    : UpdateServicePlatform.cs
 * @author  : rudals252
 * @brief   : wuauserv·BITS만 조회/정지/복원하는 네이티브 서비스 경계
 */
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

internal enum UpdateService { WindowsUpdate, Bits }
internal enum UpdateServiceState : uint { Stopped = 1, StartPending = 2, StopPending = 3, Running = 4, ContinuePending = 5, PausePending = 6, Paused = 7 }
internal sealed record UpdateServiceStatus(UpdateServiceState State, bool AcceptsStop);
internal interface IUpdateServicePlatform
{
    UpdateServiceStatus Read(UpdateService service);
    Task<UpdateServiceStatus> ReadStableAsync(UpdateService service, CancellationToken ct);
    Task<bool> ChangeAsync(UpdateService service, UpdateServiceState expected, UpdateServiceState desired, Action beforeWrite, CancellationToken ct);
}

internal sealed class UpdateServicePlatform : IUpdateServicePlatform
{
    internal static string Name(UpdateService service) => service switch
    { UpdateService.WindowsUpdate => "wuauserv", UpdateService.Bits => "BITS", _ => throw new ActionUnavailableException("TargetRejected") };
    private static ServiceHandle Open(UpdateService service, uint access)
    {
        using var manager = OpenSCManagerW(null, null, 1); // CONNECT만 요청
        if (manager.IsInvalid) { throw new ActionUnavailableException("UpdateServiceUnavailable"); }
        var handle = OpenServiceW(manager, Name(service), access);
        if (handle.IsInvalid) { handle.Dispose(); throw new ActionUnavailableException("UpdateServiceUnavailable"); }
        return handle;
    }
    private static UpdateServiceStatus Read(ServiceHandle handle)
    {
        if (!QueryServiceStatusEx(handle, 0, out var status, (uint)Marshal.SizeOf<ServiceStatusProcess>(), out _)
            || status.State is < 1 or > 7) { throw new ActionUnavailableException("UpdateServiceUnavailable"); }
        return new((UpdateServiceState)status.State, (status.Controls & 1) != 0);
    }
    public UpdateServiceStatus Read(UpdateService service) { using var handle = Open(service, 4); return Read(handle); }
    public async Task<UpdateServiceStatus> ReadStableAsync(UpdateService service, CancellationToken ct)
    {
        using var handle = Open(service, 4);
        return await WaitAsync(handle, null, ct).ConfigureAwait(false);
    }
    private static async Task<UpdateServiceStatus> WaitAsync(ServiceHandle handle, UpdateServiceState? desired, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = Read(handle);
            if (desired is { } state ? status.State == state : status.State is UpdateServiceState.Running or UpdateServiceState.Stopped) { return status; }
            if (status.State == UpdateServiceState.Paused || Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(30))
            { throw new ActionUnavailableException("UpdateServiceTransitionFailed"); }
            await Task.Delay(250, ct).ConfigureAwait(false);
        }
    }
    public async Task<bool> ChangeAsync(UpdateService service, UpdateServiceState expected, UpdateServiceState desired, Action beforeWrite, CancellationToken ct)
    {
        if (expected is not (UpdateServiceState.Running or UpdateServiceState.Stopped)
            || desired is not (UpdateServiceState.Running or UpdateServiceState.Stopped) || desired == expected) { return false; }
        using var handle = Open(service, 4 | (desired == UpdateServiceState.Stopped ? 0x20u : 0x10u));
        ct.ThrowIfCancellationRequested();
        var status = Read(handle);
        if (status.State != expected || (desired == UpdateServiceState.Stopped && !status.AcceptsStop)) { return false; }
        beforeWrite(); ct.ThrowIfCancellationRequested();
        if (Read(handle).State != expected) { return false; }
        // 종속 서비스 중지·시작 유형 변경·프로세스 강제 종료는 하지 않는다.
        var accepted = desired == UpdateServiceState.Stopped ? ControlService(handle, 1, out _) : StartServiceW(handle, 0, IntPtr.Zero);
        if (!accepted) { throw new ActionUnavailableException("UpdateServiceChangeFailed"); }
        return (await WaitAsync(handle, desired, ct).ConfigureAwait(false)).State == desired;
    }
    private sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ServiceHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess { public uint Type, State, Controls, Win32Exit, ServiceExit, CheckPoint, WaitHint, ProcessId, Flags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus { public uint Type, State, Controls, Win32Exit, ServiceExit, CheckPoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern ServiceHandle OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern ServiceHandle OpenServiceW(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(ServiceHandle handle, int level, out ServiceStatusProcess status, uint size, out uint needed);
    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(ServiceHandle handle, uint control, out ServiceStatus status);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartServiceW(ServiceHandle handle, uint argc, IntPtr argv);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
