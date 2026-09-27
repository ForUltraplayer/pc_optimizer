/**
 * @file    : RestartActionAdapter.cs
 * @author  : rudals252
 * @brief   : 취소 가능한 앱 대기 후 고정 인자로 UEFI·고급 시작 재부팅 요청. 강제 종료 인자는 사용하지 않음
 */
using System.Diagnostics;
using System.Runtime.InteropServices;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions.Advanced;

internal interface IRestartPlatform
{
    bool IsUefi();
    Task<int?> RequestAsync(RestartDestination destination, IActionExecution execution);
}

internal sealed class RestartPlatform : IRestartPlatform
{
    public bool IsUefi() => GetFirmwareType(out var type) && type == 2;
    internal static ProcessStartInfo StartInfo(RestartDestination destination)
    {
        if (!Enum.IsDefined(destination)) { throw new ActionUnavailableException("TargetRejected"); }
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory };
        foreach (var key in start.Environment.Keys.Where(CacheToolProcess.IsStrippedVariable).ToArray()) { start.Environment.Remove(key); }
        foreach (var argument in new[] { "/r", destination == RestartDestination.Firmware ? "/fw" : "/o", "/t", "0" }) { start.ArgumentList.Add(argument); }
        return start;
    }
    public async Task<int?> RequestAsync(RestartDestination destination, IActionExecution execution)
    {
        using var process = new Process { StartInfo = StartInfo(destination) };
        if (!SystemCacheToolBackend.IsPlainPath(process.StartInfo.FileName, directory: false)) { throw new ActionUnavailableException("TargetRejected"); }
        if (!execution.TryStartProcess(process.Start)) { return null; }
        // 요청 전달 이후 취소/timeout을 성공적인 재부팅 취소로 표시하지 않는다. 실제 자식 종료까지 관문 유지.
        var exit = process.WaitForExitAsync(CancellationToken.None);
        execution.Track(exit);
        await exit.ConfigureAwait(false);
        return process.ExitCode;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out uint firmwareType);
}

/// <summary>안전 모드 직행이 아닌 고급 시작 메뉴와 UEFI 진입 요청을 처리합니다.</summary>
public sealed class RestartActionAdapter : IActionAdapter
{
    private readonly Func<ActionSession> _session;
    private readonly IRestartPlatform _platform;
    private readonly Func<CancellationToken, Task> _delay;
    /// <summary>앱 공통 세션과 실행 관문에서 호출합니다.</summary>
    public RestartActionAdapter(Func<ActionSession> session) : this(session, new RestartPlatform(), ct => Task.Delay(TimeSpan.FromSeconds(5), ct)) { }
    internal RestartActionAdapter(Func<ActionSession> session, IRestartPlatform platform, Func<CancellationToken, Task> delay)
    { _session = session; _platform = platform; _delay = delay; }
    /// <inheritdoc />
    public ActionDefinition Definition => new(ActionId.Restart, ActionScope.System);
    private void Check(RestartDestination destination, ActionSession session)
    {
        if (!Enum.IsDefined(destination)) { throw new ActionUnavailableException("TargetRejected"); }
        if (!session.IsKnown || _session() != session) { throw new ActionUnavailableException("SessionChanged"); }
        if (destination == RestartDestination.Firmware && !_platform.IsUefi()) { throw new ActionUnavailableException("FirmwareUnavailable"); }
    }
    /// <inheritdoc />
    public Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (restore || target is not ActionTarget.Restart selected) { return Task.FromResult<ActionPreview?>(null); }
        Check(selected.Destination, _session());
        var firmware = selected.Destination == RestartDestination.Firmware;
        return Task.FromResult<ActionPreview?>(new(target, firmware ? "PC를 다시 시작하고 BIOS/UEFI 설정으로 이동합니다." : "PC를 다시 시작하고 안전 모드를 선택할 수 있는 고급 시작 메뉴로 이동합니다.",
            "작업을 모두 저장하세요. 실행 후 5초 동안 앱에서 취소할 수 있습니다. 요청 전달 이후에는 이 앱에서 취소할 수 없습니다. 앱을 강제로 종료하는 인자는 사용하지 않습니다.",
            new(firmware ? "BIOS/UEFI 설정" : "고급 시작 · 안전 모드는 직접 선택",
                firmware ? "UEFI 여부만 확인했습니다. 펌웨어의 진입 지원을 보장하지 않으며 BIOS 값은 변경하지 않습니다."
                : "WinRE 활성 여부는 확인되지 않았습니다. 복구 환경이 없으면 실패할 수 있습니다. 문제 해결 → 고급 옵션 → 시작 설정 → 다시 시작 → 4(안전 모드)를 선택하세요. 계정 암호와 필요 시 장치 암호화 복구 키를 준비하세요.", RequiresRestart: true)));
    }
    /// <inheritdoc />
    public async Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct)
    {
        if (plan.Preview.Target is not ActionTarget.Restart selected) { return new(plan.Id, false, false, "TargetRejected"); }
        Check(selected.Destination, plan.Session);
        await _delay(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); Check(selected.Destination, plan.Session);
        var code = await _platform.RequestAsync(selected.Destination, execution).ConfigureAwait(false);
        return new(plan.Id, code is not null, code == 0, code == 0 ? "RestartRequested" : code is null ? "RestartNotStarted" : "RestartRejected");
    }
}
