/**
 * @file    : ScanService.cs
 * @author  : rudals252
 * @brief   : 검사 실행 조율 서비스. 프로브·규칙을 등록하고 검사 컨텍스트(권한·온라인 요청·검사 단위 익명 ID·시스템 범위 제한)를 만들어 ScanCoordinator를 실행
 */

// 기본 패키지
using System.Reflection;
using System.Security.Principal;
using PcOptimizer.Core.Actions;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Engine;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Hardware;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.App.Services;

/// <summary>
/// 검사 한 번을 실행하는 App 서비스입니다. 수집·판정은 <see cref="ScanCoordinator"/>에 맡기고,
/// 여기서는 컨텍스트 구성과 등록 목록 관리만 합니다. 검사에서는 설정을 변경하지 않으며, 네트워크 프로브(NVIDIA·Windows Update)는
/// 사용자가 온라인 확인을 켠 검사에서만 실행 조율기가 호출합니다(그 밖에는 NotRequested로 건너뜀).
/// 대화형 사용자와 다른 관리자 계정으로 실행된 인스턴스는 <c>limitToSystemScope</c>로 만들어 사용자별 프로브를 건너뜁니다.
/// </summary>
public sealed class ScanService
{
    /// <summary>종료 중 목록의 변경을 전달합니다.</summary>
    public event EventHandler? DrainingChanged
    {
        add => _coordinator.DrainingChanged += value;
        remove => _coordinator.DrainingChanged -= value;
    }
    /// <summary>내장 판정 규칙 버전. 앱 캐시 규칙 스냅샷(winapp2) 버전·커밋은 규칙 목록 요약 Finding에 따로 기록합니다.</summary>
    public const string BUILTIN_RULES_VERSION = "builtin-p6";

    private const string LOG_CATEGORY = nameof(ScanService);
    private const string ANONYMOUS_ID_PREFIX = "scan-user-";
    private const string GUID_COMPACT_FORMAT = "N";
    private const string UNKNOWN_APP_VERSION = "0.0.0";

    private readonly ScanCoordinator _coordinator;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly Func<bool> _isElevated;
    private readonly bool _limitToSystemScope;

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
    /// <param name="limitToSystemScope">
    /// 시스템 범위 프로브만 실행할지 여부(기본 false). 대화형 로그온 사용자와 다른 관리자 계정으로 실행 중이면(또는 확인 불가) true이며,
    /// 사용자별 프로브는 호출하지 않고 관리자 계정으로 로그인해서 실행하라는 안내와 함께 건너뜁니다.
    /// </param>
    /// <param name="operations">사양·조치와 공유할 실행 관문. 생략하면 이 서비스가 하나를 소유합니다.</param>
    public ScanService(
        IEnumerable<IProbe> probes,
        IEnumerable<IRule> rules,
        ScanOptions options,
        ScanReportVersions versions,
        IClock clock,
        IAppLogger logger,
        Func<bool> isElevated,
        bool limitToSystemScope = false, IOperationCoordinator? operations = null)
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
        _limitToSystemScope = limitToSystemScope;
        Operations = operations ?? new OperationCoordinator(logger);
    }

    /// <summary>사양·조치 서비스와 공유해야 하는 실행 관문입니다.</summary>
    public IOperationCoordinator Operations { get; }

    /// <summary>등록한 프로브(등록 순서).</summary>
    public IReadOnlyList<IProbe> Probes { get; }

    /// <summary>등록한 프로브의 분류(등록 순서, 중복 없음).</summary>
    public IReadOnlyList<FindingCategory> Categories { get; }

    /// <summary>시스템 범위 프로브만 실행하는지 여부(다른 관리자 계정으로 실행됨).</summary>
    public bool LimitsToSystemScope => _limitToSystemScope;

    /// <summary>타임아웃·취소 뒤 아직 끝나지 않은(종료 중인) 프로브 ID.</summary>
    public IReadOnlyCollection<string> DrainingProbeIds => _coordinator.DrainingProbeIds;

    /// <summary>
    /// 기본 구성으로 서비스를 만듭니다. 프로브: 메모리·전원·디스플레이·시스템 정보·시스템 상세(OS·CPU·BIOS 배포일·메인보드·네트워크 어댑터)·그래픽 설정(HAGS)·게임 모드·보안 상태·설치 GPU·NVIDIA 온라인 조회·
    /// Windows Update 드라이버 검색·볼륨·물리 디스크·TRIM 정책·시작 프로그램·파일 스캔·앱 캐시 (TRIM 정책만 관리자 권한 필요, 일반 권한에서는 ElevationRequired로 건너뜀).
    /// 네트워크 프로브는 NVIDIA 온라인 조회와 Windows Update 드라이버 검색 둘이며 온라인 확인을 요청한 검사에서만 실행됩니다.
    /// 드라이버 링크 규칙(NVIDIA 비교·AMD/Intel·제조사 지원)은 포함 리소스의 공식 링크 표만 씁니다.
    /// 사용자 범위 프로브는 게임 모드·시작 프로그램·파일 스캔·앱 캐시이며 나머지는 시스템 범위입니다.
    /// 파일 스캔과 앱 캐시는 공유 서비스(<see cref="FileScanService"/>) 하나를 함께 씁니다. 앱 캐시 규칙은 Probes 어셈블리 포함 리소스만 읽고 SHA-256 일관성을 확인합니다(승격 여부와 무관, 파일 시스템의 규칙 파일은 읽지 않음).
    /// </summary>
    /// <param name="logger">공용 로거.</param>
    /// <param name="limitToSystemScope">시스템 범위 프로브만 실행할지 여부(다른 관리자 계정으로 실행됨).</param>
    /// <param name="rules">앱 캐시 규칙 로더(없으면 어셈블리 포함 리소스만 읽는 로더).</param>
    /// <param name="vendorLinks">공식 링크 표(없으면 포함 리소스에서 읽고, 그것도 실패하면 링크 없이 확인 불가로 표시).</param>
    /// <returns>검사 서비스.</returns>
    public static ScanService CreateDefault(
        IAppLogger logger, bool limitToSystemScope = false, RuleCatalogLoader? rules = null, VendorLinkCatalog? vendorLinks = null)
    {
        var fileScan = FileScanService.CreateDefault();
        var links = vendorLinks ?? VendorLinkCatalogLoader.LoadEmbedded().Catalog;
        var appVersion = typeof(ScanService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? UNKNOWN_APP_VERSION;

        return new ScanService(
            [
                new MemoryProbe(),
                new PowerProbe(),
                new DisplayProbe(),
                new SystemInfoProbe(),
                new SystemDetailsProbe(),
                new GraphicsSettingsProbe(),
                new GameModeSettingsProbe(),
                new SecurityStatusProbe(),
                new InstalledGpuProbe(),
                new NvidiaDriverLookupProbe(),
                new WindowsUpdateDriverProbe(),
                new VolumeProbe(),
                new PhysicalDiskProbe(),
                new TrimPolicyProbe(),
                new StartupItemsProbe(),
                new FileScanProbe(fileScan, SystemClock.Instance),
                AppCacheProbe.CreateDefault(fileScan, rules ?? RuleCatalogLoader.CreateEmbedded()),
            ],
            [
                new MemorySpeedRule(),
                new PowerPlanRule(),
                new DisplayRefreshRule(),
                new SystemInfoRule(),
                new GraphicsSettingsRule(),
                new SecurityStatusRule(),
                new InstalledDriverRule(),
                new DriverUpdateRule(links),
                new GpuVendorLinkRule(links),
                new OemSupportRule(links),
                new WindowsUpdateDriverRule(),
                new StorageSpaceRule(),
                new DiskHealthRule(),
                new TrimPolicyRule(),
                new StartupItemsRule(),
                new FileScanSummaryRule(),
                new TempLocationsRule(),
                new UnclassifiedFolderRule(),
                new RuleCatalogSummaryRule(),
                new AppCacheRule(),
                new SquirrelVersionFoldersRule(),
            ],
            new ScanOptions(),
            new ScanReportVersions(appVersion, BUILTIN_RULES_VERSION),
            SystemClock.Instance,
            logger,
            IsCurrentProcessElevated,
            limitToSystemScope);
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
    /// 새 검사 ID·검사 단위 익명 사용자 ID·현재 권한·시스템 범위 제한으로 검사 컨텍스트를 만듭니다(검사 실행과 사양 스냅샷이 함께 씀).
    /// </summary>
    /// <param name="onlineCheckRequested">사용자가 온라인 업데이트 확인을 켰는지 여부.</param>
    /// <returns>새 검사 컨텍스트.</returns>
    public ScanContext CreateContext(bool onlineCheckRequested)
    {
        var userContext = new UserContext(ANONYMOUS_ID_PREFIX + Guid.NewGuid().ToString(GUID_COMPACT_FORMAT), _isElevated());
        return new ScanContext(Guid.NewGuid(), userContext, onlineCheckRequested, _clock.UtcNow) { LimitToSystemScope = _limitToSystemScope };
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
        ct.ThrowIfCancellationRequested();
        using var lease = Operations.TryAcquire(OperationKind.Scan) ?? throw new InvalidOperationException("다른 작업이 실행 중이거나 종료를 기다리고 있습니다.");
        var context = CreateContext(onlineCheckRequested);

        _logger.Info(
            LOG_CATEGORY,
            $"ScanStarted scan={context.ScanId} elevated={context.IsElevated} online={onlineCheckRequested} systemScopeOnly={context.LimitToSystemScope}");
        var result = await _coordinator.RunScanAsync(context, ct, lease).ConfigureAwait(false);
        _logger.Info(
            LOG_CATEGORY,
            $"ScanFinished scan={context.ScanId} outcome={result.Report.Outcome} findings={result.Report.Findings.Count} draining={_coordinator.DrainingProbeIds.Count}");
        return result;
    }
}
