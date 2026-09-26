/**
 * @file    : ScanService.cs
 * @author  : rudals252
 * @brief   : 검사 실행 조율 서비스. 프로브·규칙을 등록하고 검사 컨텍스트(권한·온라인 요청·검사 단위 익명 ID)를 만들어 ScanCoordinator를 실행
 */

// 기본 패키지
using System.Reflection;
using System.Security.Principal;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Hardware;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.App.Services;

/// <summary>
/// 검사 한 번을 실행하는 App 서비스입니다. 수집·판정은 <see cref="ScanCoordinator"/>에 맡기고,
/// 여기서는 컨텍스트 구성과 등록 목록 관리만 합니다. 네트워크·관리자 권한 동작은 하지 않습니다.
/// </summary>
public sealed class ScanService
{
    /// <summary>내장 규칙 데이터 버전(외부 규칙 파일이 생기기 전까지 코드 내장 규칙만 사용).</summary>
    public const string BUILTIN_RULES_VERSION = "builtin-p3a";

    private const string LOG_CATEGORY = nameof(ScanService);
    private const string ANONYMOUS_ID_PREFIX = "scan-user-";
    private const string GUID_COMPACT_FORMAT = "N";
    private const string UNKNOWN_APP_VERSION = "0.0.0";

    private readonly ScanCoordinator _coordinator;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly Func<bool> _isElevated;

    /// <summary>
    /// 검사 서비스를 만듭니다.
    /// </summary>
    /// <param name="probes">프로브 목록.</param>
    /// <param name="rules">판정 규칙 목록.</param>
    /// <param name="options">실행 설정.</param>
    /// <param name="versions">리포트 버전.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="isElevated">현재 프로세스가 관리자 권한인지 판정하는 함수.</param>
    public ScanService(
        IEnumerable<IProbe> probes,
        IEnumerable<IRule> rules,
        ScanOptions options,
        ScanReportVersions versions,
        IClock clock,
        IAppLogger logger,
        Func<bool> isElevated)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(isElevated);

        Probes = [.. probes];
        Categories = [.. Probes.Select(probe => probe.Category).Distinct()];
        _coordinator = new ScanCoordinator(Probes, rules, options, versions, clock, logger);
        _clock = clock;
        _logger = logger;
        _isElevated = isElevated;
    }

    /// <summary>등록한 프로브(등록 순서).</summary>
    public IReadOnlyList<IProbe> Probes { get; }

    /// <summary>등록한 프로브의 분류(등록 순서, 중복 없음).</summary>
    public IReadOnlyList<FindingCategory> Categories { get; }

    /// <summary>타임아웃·취소 뒤 아직 끝나지 않은(종료 중인) 프로브 ID.</summary>
    public IReadOnlyCollection<string> DrainingProbeIds => _coordinator.DrainingProbeIds;

    /// <summary>
    /// 기본 구성으로 서비스를 만듭니다. 프로브: 메모리·전원·디스플레이·시스템 정보·그래픽 설정·보안 상태·설치 GPU·볼륨·물리 디스크·TRIM 정책
    /// (TRIM 정책만 관리자 권한 필요, 일반 권한에서는 ElevationRequired로 건너뜀). 네트워크 프로브는 없습니다.
    /// </summary>
    /// <param name="logger">공용 로거.</param>
    /// <returns>검사 서비스.</returns>
    public static ScanService CreateDefault(IAppLogger logger)
    {
        var appVersion = typeof(ScanService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? UNKNOWN_APP_VERSION;

        return new ScanService(
            [
                new MemoryProbe(),
                new PowerProbe(),
                new DisplayProbe(),
                new SystemInfoProbe(),
                new GraphicsSettingsProbe(),
                new SecurityStatusProbe(),
                new InstalledGpuProbe(),
                new VolumeProbe(),
                new PhysicalDiskProbe(),
                new TrimPolicyProbe(),
            ],
            [
                new MemorySpeedRule(),
                new PowerPlanRule(),
                new DisplayRefreshRule(),
                new SystemInfoRule(),
                new GraphicsSettingsRule(),
                new SecurityStatusRule(),
                new InstalledDriverRule(),
                new StorageSpaceRule(),
                new DiskHealthRule(),
                new TrimPolicyRule(),
            ],
            new ScanOptions(),
            new ScanReportVersions(appVersion, BUILTIN_RULES_VERSION),
            SystemClock.Instance,
            logger,
            IsCurrentProcessElevated);
    }

    /// <summary>
    /// 현재 프로세스가 관리자 권한(승격된 토큰)으로 실행 중인지 확인합니다.
    /// </summary>
    /// <returns>관리자 권한이면 true.</returns>
    public static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 새 검사 ID·검사 단위 익명 사용자 ID로 검사를 한 번 실행합니다.
    /// </summary>
    /// <param name="onlineCheckRequested">사용자가 온라인 업데이트 확인을 켰는지 여부.</param>
    /// <param name="ct">사용자 취소 토큰.</param>
    /// <returns>리포트와 스냅샷.</returns>
    /// <exception cref="InvalidOperationException">다른 검사가 진행 중인 경우.</exception>
    public async Task<ScanResult> RunScanAsync(bool onlineCheckRequested, CancellationToken ct)
    {
        var userContext = new UserContext(ANONYMOUS_ID_PREFIX + Guid.NewGuid().ToString(GUID_COMPACT_FORMAT), _isElevated());
        var context = new ScanContext(Guid.NewGuid(), userContext, onlineCheckRequested, _clock.UtcNow);

        _logger.Info(LOG_CATEGORY, $"ScanStarted scan={context.ScanId} elevated={context.IsElevated} online={onlineCheckRequested}");
        var result = await _coordinator.RunScanAsync(context, ct).ConfigureAwait(false);
        _logger.Info(
            LOG_CATEGORY,
            $"ScanFinished scan={context.ScanId} outcome={result.Report.Outcome} findings={result.Report.Findings.Count} draining={_coordinator.DrainingProbeIds.Count}");
        return result;
    }
}
