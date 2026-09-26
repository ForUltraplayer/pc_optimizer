/**
 * @file    : App.xaml.cs
 * @author  : rudals252
 * @brief   : 애플리케이션 진입점. 관리자 재검사 고정 인자·권한·SID로 시작 방식을 정하고 공용 로거·검사 서비스·내보내기·설정 URI 정책·공식 링크 정책·재검사 시작기·메인 화면 모델을 조립하며 처리되지 않은 예외를 형식 이름만 기록
 */

// 기본 패키지
using System.Windows;
using System.Windows.Threading;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Drivers;

namespace PcOptimizer.App;

/// <summary>
/// 애플리케이션 진입점 클래스입니다. 구성 요소를 조립해 메인 창을 띄웁니다(일반 권한 시작, 조회 전용).
/// 관리자 권한 재검사로 시작된 인스턴스는 고정 인자만 해석하고, 원래 사용자와 SID가 다르면 시스템 범위만 검사합니다.
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
        var launchMode = ScanLaunchModeResolver.Resolve(ElevatedRescanArguments.Parse(e.Args), elevation.IsElevated, elevation.CurrentUserSid);

        // SID·인자 원문은 기록하지 않는다(시작 방식만).
        logger.Info(LOG_CATEGORY, $"AppStarted elevated={elevation.IsElevated} mode={launchMode}");

        // 앱 캐시 규칙은 실행 모드와 관계없이 Probes 어셈블리에 포함된 리소스만 읽는다(RuleCatalogLoader.CreateEmbedded, 출력 폴더에는 규칙 파일이
        // 없고 파일 시스템에서 읽지도 않음). 포함 sources.json과 SHA-256이 맞을 때만 쓰며 다운로드·사용자 규칙 경로는 없다. 관리자 권한으로 실행 중인
        // 검사(재검사·직접 승격)에서는 사용자 쓰기 가능한 앱 설정 경로(npm·pip·NuGet·Steam)를 적용하지 않고 기본 위치만 본다(AppCacheProbe, ScanContext.IsElevated 기준).
        var bundledRules = RuleCatalogLoader.CreateEmbedded();

        // 공식 링크 표(vendor-links.json)도 Probes 어셈블리 포함 리소스만 읽는다. 규칙(링크 생성)과 링크 열기 정책이 같은 표를 쓴다.
        var vendorLinks = VendorLinkCatalogLoader.LoadEmbedded();
        if (vendorLinks.Catalog is null)
        {
            logger.Warn(LOG_CATEGORY, $"VendorLinksUnavailable errors={vendorLinks.Errors.Count}");
        }

        var scanService = ScanService.CreateDefault(
            logger, limitToSystemScope: launchMode == ScanLaunchMode.ElevatedDifferentUser, rules: bundledRules, vendorLinks: vendorLinks.Catalog);

        var viewModel = new MainViewModel(
            scanService,
            new ReportExporter(PersonalDataScrubber.FromEnvironment()),
            new SaveFileDialogExportPathPicker(),
            new SettingsUriPolicy(logger),
            new LinkPolicy(vendorLinks.Catalog, logger),
            new WpfUiDispatcher(Dispatcher),
            logger,
            elevation,
            ElevationRelauncher.CreateDefault(elevation, logger),
            launchMode);

        var window = new MainWindow(viewModel, logger);
        MainWindow = window;
        window.Show();

        if (launchMode != ScanLaunchMode.Normal)
        {
            // 사용자가 원래 창에서 재검사를 요청해 UAC를 승인했으므로 새 검사 ID로 바로 검사한다(원래 창 결과와 합치지 않음).
            viewModel.StartScanCommand.Execute(null);
        }
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
