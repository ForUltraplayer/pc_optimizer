/**
 * @file    : ElevationRelauncher.cs
 * @author  : rudals252
 * @brief   : 같은 실행 파일을 runas(UAC)로 고정 인자(--elevated-rescan --origin-sid --context)만 붙여 별도 창으로 시작하고, UAC 취소(1223)·그 밖의 실패를 결과로 돌려주는 관리자 권한 재검사 시작기
 */

// 기본 패키지
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.App.Services;

/// <summary>
/// 관리자 권한 재검사 시작기입니다. 현재 창과 결과는 건드리지 않으며, 승격된 인스턴스는 별도 창·별도 검사 ID로 조회만 합니다.
/// </summary>
/// <remarks>
/// 전달 인자는 <see cref="ElevatedRescanArguments"/>의 고정 형태뿐이며 그 밖의 값(경로·규칙 파일·명령)은 넘기지 않습니다.
/// 원래 사용자 SID는 로그에 남기지 않습니다.
/// </remarks>
public sealed class ElevationRelauncher
{
    /// <summary>UAC 승격 동사.</summary>
    public const string RUNAS_VERB = "runas";

    /// <summary>사용자가 UAC를 취소했을 때의 Win32 오류 코드(ERROR_CANCELLED).</summary>
    public const int ERROR_CANCELLED = 1223;

    /// <summary>실패 코드: 이미 관리자 권한.</summary>
    public const string ERROR_ALREADY_ELEVATED = "AlreadyElevated";

    /// <summary>실패 코드: 실행 파일 경로를 알 수 없음.</summary>
    public const string ERROR_PROCESS_PATH_UNAVAILABLE = "ProcessPathUnavailable";

    /// <summary>실패 코드: 앱 실행 파일이 아닌 호스트(dotnet)로 실행 중이라 같은 인자로 다시 시작할 수 없음.</summary>
    public const string ERROR_UNSUPPORTED_HOST = "UnsupportedHost";

    /// <summary>실패 코드: 현재 사용자 SID를 알 수 없음.</summary>
    public const string ERROR_SID_UNAVAILABLE = "SidUnavailable";

    private const string LOG_CATEGORY = nameof(ElevationRelauncher);
    private const string DOTNET_HOST_NAME = "dotnet";

    private readonly IProcessStarter _starter;
    private readonly IElevationState _elevationState;
    private readonly Func<string?> _processPath;
    private readonly IAppLogger _logger;

    /// <summary>
    /// 재검사 시작기를 만듭니다.
    /// </summary>
    /// <param name="starter">프로세스 시작기.</param>
    /// <param name="elevationState">현재 권한 상태.</param>
    /// <param name="processPath">현재 실행 파일 경로를 돌려주는 함수(보통 <see cref="Environment.ProcessPath"/>).</param>
    /// <param name="logger">공용 로거.</param>
    public ElevationRelauncher(IProcessStarter starter, IElevationState elevationState, Func<string?> processPath, IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentNullException.ThrowIfNull(elevationState);
        ArgumentNullException.ThrowIfNull(processPath);
        ArgumentNullException.ThrowIfNull(logger);
        _starter = starter;
        _elevationState = elevationState;
        _processPath = processPath;
        _logger = logger;
    }

    /// <summary>
    /// 셸 실행기와 현재 실행 파일 경로를 쓰는 시작기를 만듭니다.
    /// </summary>
    /// <param name="elevationState">현재 권한 상태.</param>
    /// <param name="logger">공용 로거.</param>
    /// <returns>재검사 시작기.</returns>
    public static ElevationRelauncher CreateDefault(IElevationState elevationState, IAppLogger logger)
    {
        return new ElevationRelauncher(new ShellProcessStarter(), elevationState, () => Environment.ProcessPath, logger);
    }

    /// <summary>
    /// 같은 실행 파일을 관리자 권한(runas)으로 별도 창에 시작합니다. UAC 대화 상자가 닫힐 때까지 반환하지 않을 수 있으므로
    /// UI 스레드가 아닌 곳에서 호출하세요. 예외를 던지지 않습니다.
    /// </summary>
    /// <returns>시작 결과.</returns>
    public ElevationRelaunchResult Relaunch()
    {
        if (_elevationState.IsElevated)
        {
            return Fail(ERROR_ALREADY_ELEVATED);
        }

        var path = _processPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail(ERROR_PROCESS_PATH_UNAVAILABLE);
        }

        if (string.Equals(Path.GetFileNameWithoutExtension(path), DOTNET_HOST_NAME, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(ERROR_UNSUPPORTED_HOST);
        }

        var sid = _elevationState.CurrentUserSid;
        if (!ElevatedRescanArguments.IsValidSid(sid))
        {
            return Fail(ERROR_SID_UNAVAILABLE);
        }

        var arguments = new ElevatedRescanArguments(sid!, Guid.NewGuid());
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = RUNAS_VERB,
            Arguments = arguments.ToCommandLine(),
        };

        try
        {
            _starter.Start(startInfo);
            _logger.Info(LOG_CATEGORY, $"ElevatedRescanLaunched context={arguments.ContextId}");
            return new ElevationRelaunchResult(ElevationRelaunchOutcome.Started, null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_CANCELLED)
        {
            _logger.Info(LOG_CATEGORY, $"ElevatedRescanCancelled context={arguments.ContextId}");
            return new ElevationRelaunchResult(ElevationRelaunchOutcome.Cancelled, null);
        }
        catch (Exception ex)
        {
            // 시작 실패는 현재 창을 멈추지 않도록 흡수하고 예외 형식 이름만 남긴다(메시지 원문 없음).
            return Fail(ex.GetType().Name);
        }
    }

    /// <summary>
    /// 실패 결과를 만들고 코드만 기록한다.
    /// </summary>
    private ElevationRelaunchResult Fail(string errorCode)
    {
        _logger.Warn(LOG_CATEGORY, $"ElevatedRescanLaunchFailed error={errorCode}");
        return new ElevationRelaunchResult(ElevationRelaunchOutcome.Failed, errorCode);
    }
}
