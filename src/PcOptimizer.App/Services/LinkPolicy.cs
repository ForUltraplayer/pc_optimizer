/**
 * @file    : LinkPolicy.cs
 * @author  : rudals252
 * @brief   : 외부 링크의 엄격한 HTTPS·공식 호스트 및 경로 허용 목록을 확인해 정규 URL만 비승격 셸(explorer.exe)로 열기(관리자 권한 브라우저 방지)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.App.Services;

/// <summary>
/// 외부 링크 열기 정책입니다(스펙 §7 OpenLink 허용 목록). 사용자가 카드의 링크 버튼을 누를 때만 쓰며 자동으로 열지 않습니다.
/// </summary>
/// <remarks>
/// 허용 조건: <see cref="OfficialUrl.TryParse"/>를 통과한 HTTPS URL(사용자 정보·비기본 포트·IP·끝 점·공백·역슬래시 없음)이면서
/// (1) 공식 링크 표(vendor-links.json) 항목과 같은 ASCII 호스트이고 경로가 그 항목 경로로 시작하거나,
/// (2) NVIDIA 허용 호스트의 드라이버 페이지 또는 Windows 배포 파일 경로이거나 (3) CacheSupportLinks의 정확한 공식 안내 URL입니다.
/// 이 정책은 출처와 관계없이 호스트와 경로를 다시 확인합니다. 호스트는 퓨니코드로 비교해 유사 문자 도메인을 막습니다.
/// 열 때는 검증한 <see cref="Uri.AbsoluteUri"/>(정규화한 문자열)만 <see cref="UnelevatedShellLauncher"/>에 넘깁니다. 앱이 항상 관리자 권한이므로
/// URL을 직접 셸 실행하지 않고 이미 실행 중인 비승격 데스크톱 셸에 COM으로 위임해 브라우저가 일반 권한으로 열리게 합니다.
/// 열지 못하면 <see cref="LinkOpenResult.Failed"/>를 돌려주고 카드가 주소 복사 안내를 보여 줍니다.
/// </remarks>
public sealed class LinkPolicy
{
    private const string LOG_CATEGORY = nameof(LinkPolicy);

    private readonly VendorLinkCatalog? _catalog;
    private readonly IAppLogger _logger;
    private readonly Action<string> _shellLauncher;

    /// <summary>
    /// 비승격 셸(explorer.exe)로 여는 정책을 만듭니다.
    /// </summary>
    /// <param name="catalog">공식 링크 표(읽지 못했으면 null, 이때 NVIDIA 허용 호스트만 엶).</param>
    /// <param name="logger">공용 로거.</param>
    public LinkPolicy(VendorLinkCatalog? catalog, IAppLogger logger)
        : this(catalog, logger, new UnelevatedShellLauncher())
    {
    }

    /// <summary>
    /// 비승격 셸 실행기를 지정해 정책을 만듭니다(테스트에서 데스크톱 연결을 대역으로 바꾸기 위함).
    /// </summary>
    /// <param name="catalog">공식 링크 표.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="shellLauncher">검증한 URL을 비승격 셸로 여는 실행기.</param>
    public LinkPolicy(VendorLinkCatalog? catalog, IAppLogger logger, UnelevatedShellLauncher shellLauncher)
        : this(catalog, logger, (shellLauncher ?? throw new ArgumentNullException(nameof(shellLauncher))).Launch)
    {
    }

    /// <summary>
    /// 실행기를 지정해 정책을 만듭니다(테스트에서 실제 브라우저 실행을 막기 위함).
    /// </summary>
    /// <param name="catalog">공식 링크 표.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="shellLauncher">검증한 URL을 여는 실행기.</param>
    public LinkPolicy(VendorLinkCatalog? catalog, IAppLogger logger, Action<string> shellLauncher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(shellLauncher);
        _catalog = catalog;
        _logger = logger;
        _shellLauncher = shellLauncher;
    }

    /// <summary>
    /// 링크가 허용되는지 확인합니다.
    /// </summary>
    /// <param name="url">링크 URL.</param>
    /// <returns>허용되면 true.</returns>
    public bool IsAllowed(string? url)
    {
        return TryValidate(url, out _);
    }

    /// <summary>
    /// 허용된 링크면 검증한 정규 URL을 엽니다. 예외를 던지지 않습니다.
    /// </summary>
    /// <param name="url">링크 URL.</param>
    /// <returns>열기 결과.</returns>
    public LinkOpenResult TryOpen(string? url)
    {
        if (!TryValidate(url, out var validated))
        {
            _logger.Warn(LOG_CATEGORY, "LinkRejected");
            return LinkOpenResult.Refused;
        }

        try
        {
            _shellLauncher(validated.AbsoluteUri);
            _logger.Info(LOG_CATEGORY, $"LinkOpened host={OfficialUrl.AsciiHost(validated)}");
            return LinkOpenResult.Opened;
        }
        catch (Exception ex)
        {
            // 셸 실행 실패는 UI를 멈추지 않도록 흡수하고 예외 형식 이름만 남긴다.
            _logger.Warn(LOG_CATEGORY, $"LinkOpenFailed host={OfficialUrl.AsciiHost(validated)} error={ex.GetType().Name}");
            return LinkOpenResult.Failed;
        }
    }

    /// <summary>
    /// 엄격한 URL 해석 후 캐시 안내의 정확한 주소·공식 링크 표·NVIDIA 허용 호스트 조건을 확인한다.
    /// </summary>
    private bool TryValidate(string? url, out Uri validated)
    {
        validated = null!;
        if (!OfficialUrl.TryParse(url, out var uri))
        {
            return false;
        }

        if (PcOptimizer.Core.Actions.CacheSupportLinks.IsAllowed(url))
        {
            validated = uri;
            return true;
        }

        if ((_catalog is not null && _catalog.IsAllowedLink(uri)) || NvidiaUrlAllowlist.IsAllowedLink(uri))
        {
            validated = uri;
            return true;
        }

        return false;
    }
}
