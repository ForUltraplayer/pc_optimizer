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
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Files;
using PcOptimizer.Probes.Actions.Power;
using PcOptimizer.Probes.Actions.Startup;
using PcOptimizer.Probes.Actions.SystemCleanup;
using PcOptimizer.Probes.Actions.Display;
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
        // 일반 보충 규칙은 승격 시 사용자 쓰기 가능한 앱 설정 경로를 적용하지 않는다(AppCacheProbe.ReadConfigs).
        // Resolve 전용 읽기 검사는 별도 예외: 작은 config.dat의 캐시 위치만 해석하며 프로필·보호·링크 경계를 확인하고 실행 대상에는 연결하지 않는다.
        // Steam 전용 검사도 보호 검사 뒤 libraryfolders.vdf만 읽는다. Program Files의 shadercache 읽기 예외는 전용 검사에 한정하며 삭제 권한이 아니다.
        var bundledRules = RuleCatalogLoader.CreateEmbedded();

        // 공식 링크 표(vendor-links.json)도 Probes 어셈블리 포함 리소스만 읽는다. 규칙(링크 생성)과 링크 열기 정책이 같은 표를 쓴다.
        var vendorLinks = VendorLinkCatalogLoader.LoadEmbedded();
        if (vendorLinks.Catalog is null)
        {
            logger.Warn(LOG_CATEGORY, $"VendorLinksUnavailable errors={vendorLinks.Errors.Count}");
        }

        var videoLocations = new PcOptimizer.Probes.Applications.VideoCacheLocations();
        var scanService = ScanService.CreateDefault(
            logger, limitToSystemScope: userScope == UserScopeMode.SystemOnly, rules: bundledRules, vendorLinks: vendorLinks.Catalog, videoLocations: videoLocations);

        // 내 PC 사양은 검사와 같은 프로브 인스턴스를 규칙 없이 직접 실행한다(검사 중에는 새로 고침을 막음). 이미지 저장 대상은 창이 만들어진 뒤 정해진다.
        var dispatcher = new WpfUiDispatcher(Dispatcher);
        var exportPathPicker = new SaveFileDialogExportPathPicker();
        MainWindow? window = null;
        var spec = new PcSpecViewModel(
            new PcSpecService(scanService.Probes, SystemClock.Instance, logger, () => scanService.CreateContext(false), scanService.Operations),
            new PcSpecTextFormatter(),
            new WpfClipboard(),
            exportPathPicker,
            () => window?.SpecCaptureRoot,
            dispatcher,
            logger,
            Environment.MachineName,
            Environment.UserName);

        MainViewModel? viewModel = null;
        var actionScope = userScopeUnresolved ? ActionUserScope.Unknown : userScope == UserScopeMode.SystemOnly ? ActionUserScope.SystemOnly : ActionUserScope.Full;
        ActionSession ReadActionSession() => SystemActionSession.Read(actionScope);
        bool? SelectVideoFolder(string app)
        {
            if (ReadActionSession() is not { IsKnown: true, Scope: ActionUserScope.Full } || app is not ("davinci" or "capcut")) { return false; }
            var picker = new Microsoft.Win32.OpenFolderDialog
            {
                Title = app == "davinci" ? "Resolve 프로젝트 설정에서 확인한 CacheClip 폴더를 선택하세요" : "CapCut 설정에서 확인한 Cache 폴더를 선택하세요",
                Multiselect = false,
            };
            return picker.ShowDialog() == true ? videoLocations.TrySet(app, picker.FolderName) : null;
        }
        var adobeLocations = new AdobeCacheLocationCatalog();
        var steamLocations = new SteamCacheLocationCatalog();
        string? PickSteamFolder()
        {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Steam 라이브러리의 steamapps\\shadercache 폴더를 선택하세요", Multiselect = false };
            return picker.ShowDialog() == true ? picker.FolderName : null;
        }
        (ActionChoice? Choice, string? Code) RegisterSteamFolder(string path)
        {
            var registration = steamLocations.TryRegister(path, ReadActionSession());
            return registration.Key is { } key
                ? (new(ActionId.SteamShaderCache, new ActionTarget.Files(key), "Steam 셰이더 캐시 · " + path,
                    "선택한 라이브러리의 30일 이상 된 파일만 확인합니다. 게임 실행 때 재다운로드·컴파일이 필요할 수 있습니다."), null)
                : (null, registration.Code);
        }
        string? PickAdobeFolder()
        {
            var picker = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Adobe 설정에서 지정한 Media Cache Files 또는 Peak Files 폴더를 선택하세요",
                Multiselect = false,
            };
            return picker.ShowDialog() == true ? picker.FolderName : null;
        }
        (ActionChoice? Choice, string? Code) RegisterAdobeFolder(string path)
        {
            var registration = adobeLocations.TryRegister(path, ReadActionSession());
            return registration.Key is { } key
                ? (new(ActionId.AppFiles, new ActionTarget.Files(key), "사용자 지정 Adobe 캐시 · " + path,
                    "선택한 위치의 90일 이상 된 오디오·파형 파일만 확인합니다. 앱 재시작 후에는 폴더를 다시 선택하세요."), null)
                : (null, registration.Code);
        }
        // 코드 카탈로그만 등록하며 준비/확인 전에는 사용자 파일을 변경하지 않는다.
        var rollbackStore = new RollbackStore();
        var actions = new ActionCenterViewModel(new ActionWorkflow(scanService.Operations, rollbackStore,
            ReadActionSession, [new FileCleanupAdapter(ReadActionSession), FileCleanupAdapter.ForSystemTemp(ReadActionSession), FileCleanupAdapter.ForAdobeCache(ReadActionSession, adobeLocations), FileCleanupAdapter.ForSteamCache(ReadActionSession, steamLocations), FileCleanupAdapter.ForGraphicsCache(ReadActionSession), new DeliveryOptimizationAdapter(ReadActionSession), new WindowsUpdateCleanupAdapter(rollbackStore, ReadActionSession), new OfficialCacheActionAdapter(new SystemCacheToolBackend(logger, actionScope != ActionUserScope.Full), ReadActionSession)], [new PowerActionAdapter(ReadActionSession), new StartupRunActionAdapter(ReadActionSession), StartupRunActionAdapter.ForMachine(ReadActionSession), new UpdateServiceRecoveryAdapter(ReadActionSession), StartupRunActionAdapter.ForFolder(ReadActionSession), StartupRunActionAdapter.ForFolder(ReadActionSession, true), StartupRunActionAdapter.ForApproval(ReadActionSession), StartupRunActionAdapter.ForApproval(ReadActionSession, true)]), dispatcher, async () =>
            {
                if (viewModel?.StartScanCommand.CanExecute(null) != true) { throw new InvalidOperationException("RescanUnavailable"); }
                await viewModel.StartScanCommand.ExecuteAsync(null);
            }, [new(ActionId.UserFiles, new ActionTarget.Files(UserTempTargets.Temp), "오래된 임시 파일 정리", "7일 이상 지난 임시 파일로 차지한 공간을 줄입니다. 폴더와 최근 파일은 남깁니다."),
                new(ActionId.UserFiles, new ActionTarget.Files(UserTempTargets.ExplorerCache), "탐색기 미리보기 캐시 정리", "사용 중이 아닌 오래된 썸네일·아이콘 캐시만 확인합니다. 이후 미리보기를 다시 만들 때 잠시 느릴 수 있습니다."),
                new(ActionId.SystemFiles, new ActionTarget.Files(SystemTempTargets.Temp), "Windows 임시 파일 정리", "7일 이상 지난 사용 중이 아닌 시스템 임시 파일을 확인합니다. 모든 사용자에게 영향을 줄 수 있으며 업데이트 캐시·설치 파일은 포함하지 않습니다."),
                new(ActionId.WindowsUpdateCache, new ActionTarget.Files(WindowsUpdateCleanupAdapter.Download), "Windows 업데이트 다운로드 캐시 정리", "7일 이상 된 Download 파일만 확인합니다. 업데이트·전송이 진행 중이면 실행하지 않습니다. 실행 시 Windows Update·BITS를 잠시 중지한 뒤 복구하며 파일은 필요하면 다시 다운로드합니다."),
                new(ActionId.DeliveryOptimization, new ActionTarget.DeliveryCache(), "Windows 배달 최적화 캐시 정리", "Windows·Store의 다운로드 완료 캐시를 확인합니다. 모든 사용자에게 영향이 있으며 필요하면 다시 다운로드합니다. 고정 보관·진행 중인 다운로드는 제외합니다."),
                new(ActionId.AppFiles, new ActionTarget.Files(AdobeCacheTargets.Media), "Adobe 기본 미디어 캐시 정리", "90일 이상 된 .cfa·.pek만 확인합니다. 편집 앱을 닫아 주세요. 다음 사용 때 다시 만드는 시간이 필요하며 옮긴 캐시 위치는 포함하지 않습니다."),
                new(ActionId.AppFiles, new ActionTarget.Files(AdobeCacheTargets.Peaks), "Adobe 기본 파형 캐시 정리", "Peak Files의 90일 이상 된 .pek만 확인합니다. 미디어 캐시와 별도로 선택하며 원본 미디어·프로젝트는 대상이 아닙니다."),
                .. GraphicsCacheTargets.Keys.Select(key => new ActionChoice(ActionId.GraphicsShaderCache, new ActionTarget.Files(key), GraphicsCacheTargets.Label(key) + " 정리",
                    "30일 이상 쓰이지 않은 캐시 파일만 확인합니다. 드라이버·게임이 열어 둔 파일은 건너뛰며, 지운 캐시는 다음 실행 때 다시 만들어 첫 로딩이 느릴 수 있습니다. NVIDIA 설정은 바꾸지 않습니다.")),
                new(ActionId.Power, new ActionTarget.Power(PowerActionAdapter.Balanced), "균형 조정 전원 계획", "소비 전력과 응답성의 균형을 선택합니다. 이 PC에 설치된 계획인지 먼저 확인하며 이전 계획으로 되돌릴 수 있습니다."),
                new(ActionId.Power, new ActionTarget.Power(PowerActionAdapter.HighPerformance), "고성능 전원 계획", "AC 전원이 연결되고 계획이 설치된 경우에만 선택합니다. 발열·소음·소비 전력이 늘 수 있으며 성능 향상을 보장하지 않습니다."),
                new(ActionId.Power, new ActionTarget.Power(PowerActionAdapter.PowerSaver), "절전 전원 계획", "전력 소비를 줄이는 쪽으로 선택합니다. 작업 응답성이 낮아질 수 있으며 이전 계획으로 되돌릴 수 있습니다."),
                .. (actionScope == ActionUserScope.Full ? Enum.GetValues<OfficialCacheTool>() : []).Where(t => SystemCacheToolBackend.IsToolInProtectedLocation((CacheTool)t))
                    .Select(t => new ActionChoice(ActionId.OfficialCache, new ActionTarget.OfficialTool(t), t + " 공식 캐시 정리", "공식 도구로 다운로드 캐시를 정리합니다. 재다운로드가 필요할 수 있으며 개별 파일 미리보기와 처리 범위가 다릅니다."))],
                    new SettingsUriPolicy(logger).TryOpen, PickAdobeFolder, RegisterAdobeFolder, SelectVideoFolder, videoLocations.Clear,
                    PickSteamFolder, RegisterSteamFolder, url => new LinkPolicy(vendorLinks.Catalog, logger).TryOpen(url) == LinkOpenResult.Opened);
        viewModel = new MainViewModel(
            scanService,
            new ReportExporter(PersonalDataScrubber.FromEnvironment()),
            exportPathPicker,
            new SettingsUriPolicy(logger),
            new LinkPolicy(vendorLinks.Catalog, logger),
            dispatcher,
            logger,
            elevation,
            userScope,
            // 공식 캐시 후보의 도구 가용성이다. 주사율 카드 실행기는 아래 Full 사용자 조건으로 따로 연결한다.
            new CacheToolActionAvailability(SystemCacheToolBackend.IsToolInProtectedLocation, CacheToolActionAvailability.DEFAULT_REVIEWED_APP_IDS, userScope == UserScopeMode.SystemOnly),
            spec,
            // SID를 알아내지 못해 시스템만으로 정한 경우는 "다른 관리자 계정" 배너가 아니라 "확인하지 못함" 배너를 보인다.
            userScopeUnresolved, actions, displayTrialsAvailable: actionScope == ActionUserScope.Full);

        // 일반 실행에서도 진단 카드의 시험 버튼으로 연결한다. 실제 변경은 별도 확인 뒤에만 시작한다.
        DisplayTrialViewModel? displayTrials = null;
        if (actionScope == ActionUserScope.Full)
        {
            displayTrials = new(new DisplayTrialCoordinator(scanService.Operations, new RollbackStore(), ReadActionSession), scanService.Operations, dispatcher, async () =>
            {
                if (!viewModel.StartScanCommand.CanExecute(null)) { throw new InvalidOperationException("RescanUnavailable"); }
                await viewModel.StartScanCommand.ExecuteAsync(null);
            });
        }
        window = new MainWindow(viewModel, logger, displayTrials);
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
