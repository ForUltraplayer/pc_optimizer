/**
 * @file    : UpdateActivityReader.cs
 * @author  : rudals252
 * @brief   : 로컬 WUA 설치 진행·재부팅 상태와 허용 서비스 상태를 분리 조회
 */
using System.Reflection;
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

internal sealed record UpdateActivity(bool? Installing, bool? RebootRequired, UpdateServiceStatus? WindowsUpdate, UpdateServiceStatus? Bits)
{
    internal bool Complete => Installing is not null && RebootRequired is not null && WindowsUpdate is not null && Bits is not null;
    // 유휴/삭제 가능 판정이 아니다. 이 관측으로 명백하게 거절할 이유만 반환한다.
    internal string? KnownBlock => RebootRequired == true ? "UpdateRebootRequired" : Installing == true ? "UpdateInstallBusy"
        : !Complete ? "UpdateStateUnavailable"
        : new[] { WindowsUpdate!, Bits! }.Any(s => s.State is not (UpdateServiceState.Stopped or UpdateServiceState.Running)) ? "UpdateServiceTransitionFailed" : null;
}

internal sealed class UpdateActivityReader(IUpdateServicePlatform? services = null)
{
    private readonly IUpdateServicePlatform _services = services ?? new UpdateServicePlatform();
    internal UpdateActivity Read(CancellationToken ct)
    {
        var busy = ReadBoolean("Microsoft.Update.Installer", "IsBusy", ct);
        var reboot = ReadBoolean("Microsoft.Update.SystemInfo", "RebootRequired", ct);
        UpdateServiceStatus? ReadService(UpdateService service)
        {
            ct.ThrowIfCancellationRequested();
            try { return _services.Read(service); }
            catch (ActionUnavailableException) { return null; }
        }
        return new(busy, reboot, ReadService(UpdateService.WindowsUpdate), ReadService(UpdateService.Bits));
    }
    private static bool? ReadBoolean(string progId, string property, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        object? instance = null;
        try
        {
            var type = Type.GetTypeFromProgID(progId, false);
            if (type is null || (instance = Activator.CreateInstance(type)) is null) { return null; }
            var value = type.InvokeMember(property, BindingFlags.GetProperty, null, instance, null);
            ct.ThrowIfCancellationRequested();
            return value is bool boolean ? boolean : null;
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or TypeLoadException or MissingMemberException or UnauthorizedAccessException)
        { return null; }
        finally
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                try { Marshal.FinalReleaseComObject(instance); } catch (InvalidComObjectException) { }
            }
        }
    }
}
