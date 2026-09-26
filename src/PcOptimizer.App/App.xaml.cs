/**
 * @file    : App.xaml.cs
 * @author  : rudals252
 * @brief   : 애플리케이션 진입점. 공용 로거·검사 서비스·내보내기·설정 URI 정책·메인 화면 모델을 조립하고 처리되지 않은 예외를 형식 이름만 기록
 */

// 기본 패키지
using System.Windows;
using System.Windows.Threading;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.App.Views;

namespace PcOptimizer.App;

/// <summary>
/// 애플리케이션 진입점 클래스입니다. 구성 요소를 조립해 메인 창을 띄웁니다(일반 권한, 조회 전용).
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
        logger.Info(LOG_CATEGORY, "AppStarted");

        var viewModel = new MainViewModel(
            ScanService.CreateDefault(logger),
            new ReportExporter(PersonalDataScrubber.FromEnvironment()),
            new SaveFileDialogExportPathPicker(),
            new SettingsUriPolicy(logger),
            new WpfUiDispatcher(Dispatcher),
            logger);

        var window = new MainWindow(viewModel);
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
