/**
 * @file    : TrimPolicyProbe.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 검사에서만 System32의 fsutil.exe를 셸 없이 읽기 전용(behavior query DisableDeleteNotify)으로 실행해 파일 시스템별 OS 삭제 알림(TRIM) 정책 값을 수집하는 프로브
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// TRIM(삭제 알림) OS 정책을 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="TrimPolicyProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// fsutil은 관리자 권한을 요구하므로 <see cref="RequiresElevation"/>이 true이며, 일반 권한 검사에서는 조율기가 실행하지 않고 ElevationRequired로 건너뜁니다.
/// 이 작업에서 허용된 유일한 자식 프로세스이며, 시스템 폴더의 fsutil.exe 전체 경로만 셸 없이 "behavior query DisableDeleteNotify" 인수로 실행합니다(설정 변경 명령 없음).
/// 제한 시간·시작 실패·0이 아닌 종료 코드·해석 실패는 Failed입니다. 출력 원문은 측정값·로그에 남기지 않습니다.
/// </remarks>
public sealed class TrimPolicyProbe : IProbe
{
    /// <summary>fsutil 실행 파일 이름.</summary>
    public const string FSUTIL_FILE_NAME = "fsutil.exe";

    /// <summary>fsutil 실행 제한 시간.</summary>
    public static readonly TimeSpan FSUTIL_TIMEOUT = TimeSpan.FromSeconds(15);

    /// <summary>fsutil 읽기 전용 조회 인수("behavior query DisableDeleteNotify").</summary>
    public static readonly IReadOnlyList<string> QUERY_ARGUMENTS = ["behavior", "query", "DisableDeleteNotify"];

    private const string SOURCE = "fsutil behavior query DisableDeleteNotify";
    private const int EXIT_CODE_SUCCESS = 0;

    /// <summary>프로브 제한 시간 여유(fsutil 제한 시간이 먼저 끝나도록).</summary>
    private static readonly TimeSpan PROBE_TIMEOUT_MARGIN = TimeSpan.FromSeconds(5);

    private readonly IProcessRunner _runner;
    private readonly IClock _clock;
    private readonly Func<string> _systemDirectory;

    /// <summary>
    /// 실제 프로세스 실행기·시스템 폴더·시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public TrimPolicyProbe()
        : this(ProcessRunner.Instance, SystemClock.Instance, () => Environment.SystemDirectory)
    {
    }

    /// <summary>
    /// 의존성을 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="runner">프로세스 실행기.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="systemDirectory">시스템 폴더(%SystemRoot%\System32) 전체 경로를 돌려주는 함수.</param>
    public TrimPolicyProbe(IProcessRunner runner, IClock clock, Func<string> systemDirectory)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(systemDirectory);
        _runner = runner;
        _clock = clock;
        _systemDirectory = systemDirectory;
    }

    /// <inheritdoc />
    public string Id => TrimPolicyProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Storage;

    /// <inheritdoc />
    public bool RequiresElevation => true;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => FSUTIL_TIMEOUT + PROBE_TIMEOUT_MARGIN;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var observedAt = _clock.UtcNow;
        var fsutilPath = Path.Combine(_systemDirectory(), FSUTIL_FILE_NAME);
        var run = _runner.Run(fsutilPath, QUERY_ARGUMENTS, FSUTIL_TIMEOUT, ct);

        var failure = run.Status switch
        {
            ProcessRunStatus.NotFound => new Issue(CannotVerifyReason.Unsupported, ProbeStrings.Trim_ToolMissing),
            ProcessRunStatus.StartFailed => new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Trim_StartFailed, run.ErrorCode ?? string.Empty)),
            ProcessRunStatus.TimedOut => new Issue(CannotVerifyReason.Timeout, ProbeStrings.Trim_Timeout),
            _ when run.ExitCode != EXIT_CODE_SUCCESS => new Issue(
                CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Trim_ExitCode, run.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)),
            _ => null,
        };

        if (failure is not null)
        {
            return Task.FromResult(CreateResult(context, ProbeStatus.Failed, [], [failure], observedAt));
        }

        var values = FsutilDeleteNotifyParser.Parse(run.StandardOutput);
        if (values.Count == 0)
        {
            return Task.FromResult(CreateResult(
                context, ProbeStatus.Failed, [], [new Issue(CannotVerifyReason.ProbeError, ProbeStrings.Trim_ParseFailed)], observedAt));
        }

        Measurement[] measurements =
        [
            .. TrimPolicyProbeContract.FileSystems
                .Where(values.ContainsKey)
                .Select(fileSystem => new Measurement(
                    TrimPolicyProbeContract.DisableDeleteNotifyName(fileSystem),
                    new IntegerValue(values[fileSystem]),
                    null,
                    SOURCE,
                    observedAt,
                    MeasurementQuality.Reported)),
        ];
        return Task.FromResult(CreateResult(context, ProbeStatus.Success, measurements, [], observedAt));
    }

    /// <summary>
    /// 이 프로브의 결과를 만든다. 시작 시각·소요 시간은 실행 조율기가 덮어쓴다.
    /// </summary>
    private ProbeResult CreateResult(
        ScanContext context,
        ProbeStatus status,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<Issue> issues,
        DateTimeOffset startedAt)
    {
        return new ProbeResult(Id, status, measurements, issues, startedAt, TimeSpan.Zero, context.UserContext);
    }
}
