/**
 * @file    : UpdateCleanupGuard.cs
 * @author  : rudals252
 * @brief   : 중지 전 WUA/BITS/DO 및 실행 중 CBS·재부팅·프로세스·서비스 재진입을 확인
 */
using System.Diagnostics;
using Microsoft.Win32;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

internal sealed class UpdateCleanupGuard(IUpdateServicePlatform services)
{
    private static readonly string[] PendingKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\PackagesPending",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
    ];
    private static readonly HashSet<string> Writers = new(["TiWorker", "TrustedInstaller", "MoUsoCoreWorker", "UsoClient", "wuauclt",
        "SetupHost", "SetupPrep", "Windows10UpgraderApp", "UpdateAssistant", "dism", "DismHost"], StringComparer.OrdinalIgnoreCase);
    internal void BeforeStopping(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // COM 조회 때문에 원래 정지된 서비스를 깨우지 않는다. 이 경우 공식 Windows 정리로 안내한다.
        RequireRunning();
        var activity = new UpdateActivityReader(services).Read(ct);
        if (activity.KnownBlock is { } blocked) { throw new ActionUnavailableException(blocked); }
        RequireRunning();
        if (BitsJobReader.CountAll() != 0) { throw new ActionUnavailableException("UpdateBitsJobsPresent"); }
        ct.ThrowIfCancellationRequested();
        CheckNonActivating();
        CheckDelivery(ct);
        RequireRunning();
    }
    private void RequireRunning()
    {
        foreach (var service in new[] { UpdateService.WindowsUpdate, UpdateService.Bits })
        {
            var state = services.Read(service);
            if (state.State != UpdateServiceState.Running || !state.AcceptsStop) { throw new ActionUnavailableException("UpdateServicesNotReady"); }
        }
    }
    internal static void CheckNonActivating()
    {
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (var path in PendingKeys)
            {
                using var key = machine.OpenSubKey(path, false);
                if (key is not null) { throw new ActionUnavailableException("UpdateRebootRequired"); }
            }
            using var session = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager", false);
            if (session is null) { throw new ActionUnavailableException("UpdateStateUnavailable"); }
            var rename = session.GetValue("PendingFileRenameOperations", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (rename is not null && rename is not string[]) { throw new ActionUnavailableException("UpdateStateUnavailable"); }
            if (rename is string[] entries && entries.Any(s => !string.IsNullOrWhiteSpace(s))) { throw new ActionUnavailableException("UpdateRebootRequired"); }
            var processes = Process.GetProcesses();
            try
            {
                if (processes.Length == 0) { throw new ActionUnavailableException("UpdateStateUnavailable"); }
                foreach (var process in processes)
                {
                    var name = process.ProcessName;
                    if (string.IsNullOrWhiteSpace(name)) { throw new ActionUnavailableException("UpdateStateUnavailable"); }
                    if (Writers.Contains(name)) { throw new ActionUnavailableException("UpdateWorkerBusy"); }
                }
            }
            finally { foreach (var process in processes) { process.Dispose(); } }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { throw new ActionUnavailableException("UpdateStateUnavailable"); }
    }
    private static void CheckDelivery(CancellationToken ct)
    {
        var entries = new DeliveryCachePlatform().Read(ct);
        if (entries.Any(e => e.Status > 3)) { throw new ActionUnavailableException("UpdateStateUnavailable"); }
        if (entries.Any(e => e.Status is 0 or 3)) { throw new ActionUnavailableException("UpdateDownloadBusy"); }
    }
    internal string? FileBoundary(bool stopped, CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested) { return "Cancelled"; }
            CheckNonActivating();
            if (stopped)
            {
                if (services.Read(UpdateService.WindowsUpdate).State != UpdateServiceState.Stopped
                    || services.Read(UpdateService.Bits).State != UpdateServiceState.Stopped) { return "UpdateServiceRestarted"; }
                CheckDelivery(ct);
                if (services.Read(UpdateService.WindowsUpdate).State != UpdateServiceState.Stopped
                    || services.Read(UpdateService.Bits).State != UpdateServiceState.Stopped) { return "UpdateServiceRestarted"; }
            }
            return null;
        }
        catch (ActionUnavailableException ex) { return ex.Code; }
        catch (OperationCanceledException) { return "Cancelled"; }
    }
}
