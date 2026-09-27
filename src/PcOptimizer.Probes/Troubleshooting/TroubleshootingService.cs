/**
 * @file    : TroubleshootingService.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함의 실행 창구. 공용 작업 관문(검사·조치와 상호 배제) 안에서 복구 명령 실행, 시스템 복원 지점 생성(WMI SystemRestore), 내장 도구 열기를 담당
 */
using System.Diagnostics;
using System.Management;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Troubleshooting;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.Probes.Troubleshooting;

/// <summary>사용자 입력을 명령으로 해석하지 않으며 카탈로그의 닫힌 ID만 받습니다.</summary>
public sealed class TroubleshootingService
{
    /// <summary>복원 지점 설명(Windows 시스템 복원 화면에 보이는 이름).</summary>
    public const string RESTORE_POINT_DESCRIPTION = "PC Optimizer 조치 전";
    /// <summary>WMI CreateRestorePoint 반환: 성공.</summary>
    public const uint RESTORE_OK = 0;
    /// <summary>WMI CreateRestorePoint 반환: 시스템 보호(서비스) 꺼짐.</summary>
    public const uint RESTORE_SERVICE_DISABLED = 1058;
    /// <summary>시스템 보호 꺼짐 결과 코드.</summary>
    public const string CODE_PROTECTION_DISABLED = "ProtectionDisabled";
    /// <summary>고른 폴더가 Windows 설치 미디어가 아님.</summary>
    public const string CODE_SOURCE_INVALID = "SourceInvalid";
    private const uint RESTORE_TYPE_MODIFY_SETTINGS = 12;
    private const uint RESTORE_EVENT_BEGIN_SYSTEM_CHANGE = 100;
    private static readonly TimeSpan RESTORE_TIMEOUT = TimeSpan.FromMinutes(3);
    private readonly IOperationCoordinator _operations;
    private readonly IAppLogger _logger;
    private readonly RepairCommandRunner _runner;
    private readonly Func<uint> _createRestorePoint;
    private readonly Func<ProcessStartInfo, bool> _startProcess;
    private readonly string _systemDirectory;

    /// <summary>실제 실행기·WMI·프로세스를 씁니다.</summary>
    public TroubleshootingService(IOperationCoordinator operations, IAppLogger? logger = null)
        : this(operations, logger, new RepairCommandRunner(logger), CreateRestorePointWithWmi, start => { using var p = Process.Start(start); return p is not null; },
              Environment.GetFolderPath(Environment.SpecialFolder.System)) { }

    internal TroubleshootingService(IOperationCoordinator operations, IAppLogger? logger, RepairCommandRunner runner, Func<uint> createRestorePoint,
        Func<ProcessStartInfo, bool> startProcess, string systemDirectory)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _logger = logger ?? NullAppLogger.Instance;
        _runner = runner; _createRestorePoint = createRestorePoint; _startProcess = startProcess; _systemDirectory = systemDirectory;
    }

    /// <summary>카탈로그의 직접 실행 명령 ID를 실행합니다. 검사·조치가 진행 중이면 Busy로 거절합니다. 설치 미디어 원본이 필요한 명령은 <paramref name="sourceFolder"/>를 검증해 /Source 인자로 바꿉니다.</summary>
    public async Task<RepairCommandResult> RunAsync(string commandId, IProgress<string>? progress, CancellationToken ct, string? sourceFolder = null)
    {
        if (!RepairCommandCatalog.IsKnown(commandId)) { return new(commandId, false, null, "Unsupported", string.Empty, false); }
        using var lease = _operations.TryAcquire(OperationKind.Apply);
        if (lease is null) { return new(commandId, false, null, RepairCommandRunner.CODE_BUSY, string.Empty, false); }
        if (commandId == RepairCommandCatalog.RESTORE_POINT) { return await CreateRestorePointAsync(progress, ct).ConfigureAwait(false); }
        var command = RepairCommandCatalog.Find(commandId)!;
        string? source = null;
        if (command.RequiresSource)
        {
            if (!InstallMediaSource.TryResolve(sourceFolder, File.Exists, out source)) { return new(commandId, false, null, CODE_SOURCE_INVALID, string.Empty, false); }
            progress?.Report("설치 미디어의 " + (source.StartsWith("WIM:", StringComparison.Ordinal) ? InstallMediaSource.WIM_FILE : InstallMediaSource.ESD_FILE) + "을(를) 원본으로 씁니다. Windows Update에는 접속하지 않습니다.");
        }
        return await _runner.RunAsync(command, progress, ct, source).ConfigureAwait(false);
    }

    /// <summary>
    /// WMI <c>SystemRestore.CreateRestorePoint</c>로 복원 지점을 만듭니다. Windows는 24시간 안에 만든 지점이 있으면 새로 만들지 않고 성공을 돌려줄 수 있습니다.
    /// </summary>
    private async Task<RepairCommandResult> CreateRestorePointAsync(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("시스템 복원 지점을 만드는 중… (보통 1분 안에 끝납니다)");
        try
        {
            var code = await Task.Run(_createRestorePoint, ct).WaitAsync(RESTORE_TIMEOUT, ct).ConfigureAwait(false);
            _logger.Info(nameof(TroubleshootingService), $"RestorePoint result={code}");
            if (code == RESTORE_OK)
            {
                progress?.Report("시스템 복원 지점을 만들었어요. 24시간 안에 이미 만든 지점이 있으면 Windows가 새로 만들지 않을 수 있어요.");
                return new(RepairCommandCatalog.RESTORE_POINT, true, 0, RepairCommandRunner.CODE_COMPLETED, string.Empty, false);
            }
            var reason = code == RESTORE_SERVICE_DISABLED ? CODE_PROTECTION_DISABLED : RepairCommandRunner.CODE_FAILED;
            progress?.Report(reason == CODE_PROTECTION_DISABLED ? "시스템 보호가 꺼져 있어요. '복원 지점 만들기'에서 C: 드라이브 보호를 켠 뒤 다시 실행하세요." : $"복원 지점을 만들지 못했어요 (코드 {code}).");
            return new(RepairCommandCatalog.RESTORE_POINT, true, (int)code, reason, string.Empty, false);
        }
        catch (OperationCanceledException) { return new(RepairCommandCatalog.RESTORE_POINT, true, null, ct.IsCancellationRequested ? RepairCommandRunner.CODE_CANCELLED : RepairCommandRunner.CODE_TIMED_OUT, string.Empty, false); }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            _logger.Warn(nameof(TroubleshootingService), $"RestorePointFailure type={ex.GetType().Name}");
            progress?.Report("복원 지점을 만들지 못했어요. 시스템 보호 설정을 확인하세요.");
            return new(RepairCommandCatalog.RESTORE_POINT, true, null, RepairCommandRunner.CODE_FAILED, string.Empty, false);
        }
    }

    private static uint CreateRestorePointWithWmi()
    {
        var scope = new ManagementScope(@"\\.\root\default");
        scope.Connect();
        using var systemRestore = new ManagementClass(scope, new ManagementPath("SystemRestore"), new ObjectGetOptions());
        using var parameters = systemRestore.GetMethodParameters("CreateRestorePoint");
        parameters["Description"] = RESTORE_POINT_DESCRIPTION;
        parameters["RestorePointType"] = RESTORE_TYPE_MODIFY_SETTINGS;
        parameters["EventType"] = RESTORE_EVENT_BEGIN_SYSTEM_CHANGE;
        using var result = systemRestore.InvokeMethod("CreateRestorePoint", parameters, null);
        return Convert.ToUInt32(result["ReturnValue"], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>내장 GUI 도구를 System32 절대 경로·고정 인자로 엽니다. 설정 URI 대상은 <paramref name="openSettings"/>에 맡깁니다.</summary>
    /// <returns>열었으면 true. 카탈로그에 없거나 실행 파일이 없으면 false.</returns>
    public bool OpenBuiltIn(string toolId, Func<string, bool> openSettings)
    {
        ArgumentNullException.ThrowIfNull(openSettings);
        if (BuiltInToolCatalog.Find(toolId) is not { } tool) { return false; }
        if (tool.SettingsUri is { } uri) { return openSettings(uri); }
        var path = Path.Combine(_systemDirectory, tool.Executable!);
        if (!File.Exists(path) || !SystemCacheToolBackend.IsPlainPath(path, directory: false)) { _logger.Warn(nameof(TroubleshootingService), $"BuiltInMissing id={toolId}"); return false; }
        var start = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = _systemDirectory };
        foreach (var argument in tool.Arguments) { start.ArgumentList.Add(argument); }
        try
        {
            var opened = _startProcess(start);
            _logger.Info(nameof(TroubleshootingService), $"BuiltInOpened id={toolId} ok={opened}");
            return opened;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { _logger.Warn(nameof(TroubleshootingService), $"BuiltInOpenFailed id={toolId} type={ex.GetType().Name}"); return false; }
    }
}
