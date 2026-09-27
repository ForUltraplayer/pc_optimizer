/**
 * @file    : App.xaml.cs
 * @author  : rudals252
 * @brief   : 애플리케이션 진입점(항상 관리자 권한). 프로세스 토큰 SID와 대화형 로그온 사용자 SID를 비교해 사용자 범위(전체/시스템만)와 판정 불가 여부(배너 문구 구분)를 정하고 공용 로거·검사 서비스·내보내기·설정 URI 정책·공식 링크 정책·앱 내 실행 판정(보호 위치 도구 캐시 정리)·내 PC 사양 화면 모델(검사와 프로브 공유)·메인 화면 모델을 조립하며 처리되지 않은 예외를 형식 이름만 기록
 */

// 기본 패키지
using System.Windows;
using System.Windows.Threading;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.App;

/// <summary>
/// 애플리케이션 진입점 클래스입니다. 구성 요소를 조립해 메인 창을 띄웁니다(매니페스트 requireAdministrator로 항상 관리자 권한, 단일 프로세스).
/// 표준 계정이 다른 관리자 계정의 자격 증명으로 승격해 실행했으면(토큰 SID ≠ 대화형 사용자 SID, 또는 확인 불가) 시스템 범위만 검사합니다.
/// </summary>
public partial class App : Application
{
    private const string LOG_CATEGORY = nameof(App);

    private FileAppLogger? _logger;

    /// <summary>
    /// 시작 시 구성 요소를 조립하고 메인 창을 표시합니다.
    /// </summary>
    /// <param name="e">시작 인자.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logger = FileAppLogger.CreateDefault();
        _logger = logger;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        var elevation = WindowsElevationState.Capture();
        var sessionUserResolved = InteractiveSessionUser.TryGetSid(out var sid);
        var interactiveSid = sessionUserResolved ? sid : null;
        var userScope = UserScopeResolver.Resolve(elevation.CurrentUserSid, interactiveSid);
        var userScopeUnresolved = UserScopeResolver.IsUnresolved(elevation.CurrentUserSid, interactiveSid);

        // SID·계정명은 기록하지 않는다(권한과 범위·판정 성공 여부만).
        logger.Info(LOG_CATEGORY, $"AppStarted elevated={elevation.IsElevated} scope={userScope} sessionUserResolved={sessionUserResolved}");

        // 앱 캐시 규칙은 실행 모드와 관계없이 Probes 어셈블리에 포함된 리소스만 읽는다(RuleCatalogLoader.CreateEmbedded, 출력 폴더에는 규칙 파일이
        // 없고 파일 시스템에서 읽지도 않음). 포함 sources.json과 SHA-256이 맞을 때만 쓰며 다운로드·사용자 규칙 경로는 없다. 관리자 권한으로 실행 중인
        // 검사(매니페스트상 항상)에서는 사용자 쓰기 가능한 앱 설정 경로(npm·pip·NuGet·Steam)를 적용하지 않고 기본 위치만 본다(AppCacheProbe, ScanContext.IsElevated 기준).
        var bundledRules = RuleCatalogLoader.CreateEmbedded();

        // 공식 링크 표(vendor-links.json)도 Probes 어셈블리 포함 리소스만 읽는다. 규칙(링크 생성)과 링크 열기 정책이 같은 표를 쓴다.
        var vendorLinks = VendorLinkCatalogLoader.LoadEmbedded();
        if (vendorLinks.Catalog is null)
        {
            logger.Warn(LOG_CATEGORY, $"VendorLinksUnavailable errors={vendorLinks.Errors.Count}");
        }

        var scanService = ScanService.CreateDefault(
            logger, limitToSystemScope: userScope == UserScopeMode.SystemOnly, rules: bundledRules, vendorLinks: vendorLinks.Catalog);

        // 내 PC 사양은 검사와 같은 프로브 인스턴스를 규칙 없이 직접 실행한다(검사 중에는 새로 고침을 막음). 이미지 저장 대상은 창이 만들어진 뒤 정해진다.
        var dispatcher = new WpfUiDispatcher(Dispatcher);
        var exportPathPicker = new SaveFileDialogExportPathPicker();
        MainWindow? window = null;
        var spec = new PcSpecViewModel(
            new PcSpecService(scanService.Probes, SystemClock.Instance, logger, () => scanService.CreateContext(false)),
            new PcSpecTextFormatter(),
            new WpfClipboard(),
            exportPathPicker,
            () => window?.SpecCaptureRoot,
            dispatcher,
            logger,
            Environment.MachineName,
            Environment.UserName);

        var viewModel = new MainViewModel(
            scanService,
            new ReportExporter(PersonalDataScrubber.FromEnvironment()),
            exportPathPicker,
            new SettingsUriPolicy(logger),
            new LinkPolicy(vendorLinks.Catalog, logger),
            dispatcher,
            logger,
            elevation,
            userScope,
            // 앱 안에서 바로 실행하는 조치는 보호 위치(Program Files) 도구의 npm·pip·NuGet 캐시 정리뿐이다. 도구 위치는 존재 확인만 하며 프로세스를 실행하지 않는다.
            new CacheToolActionAvailability(SystemCacheToolBackend.IsToolInProtectedLocation, CacheToolActionAvailability.DEFAULT_REVIEWED_APP_IDS, userScope == UserScopeMode.SystemOnly),
            spec,
            // SID를 알아내지 못해 시스템만으로 정한 경우는 "다른 관리자 계정" 배너가 아니라 "확인하지 못함" 배너를 보인다.
            userScopeUnresolved);

        window = new MainWindow(viewModel, logger);
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// 종료를 기록합니다.
    /// </summary>
    /// <param name="e">종료 인자.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info(LOG_CATEGORY, "AppExited");
        base.OnExit(e);
    }

    /// <summary>
    /// UI 스레드의 처리되지 않은 예외를 형식 이름만 기록한다(메시지·스택은 기록하지 않음).
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error(LOG_CATEGORY, "DispatcherUnhandledException", e.Exception);
    }

    /// <summary>
    /// 앱 도메인의 처리되지 않은 예외를 형식 이름만 기록한다.
    /// </summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.Error(LOG_CATEGORY, "DomainUnhandledException", e.ExceptionObject as Exception);
    }

    /// <summary>
    /// 관찰되지 않은 작업 예외를 형식 이름만 기록한다.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Warn(LOG_CATEGORY, "UnobservedTaskException", e.Exception);
    }
}
