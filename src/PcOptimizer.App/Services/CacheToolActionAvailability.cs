/**
 * @file    : CacheToolActionAvailability.cs
 * @author  : rudals252
 * @brief   : SP4 시점의 유일한 앱 내 실행(npm·pip·NuGet 도구 캐시 정리) 판정. 검토 규칙 앱 카드이고 도구가 보호 위치에 있을 때만 true
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.Services;

/// <summary>도구 캐시 정리 카드의 앱 내 실행 가능 여부를 판정합니다.</summary>
public sealed class CacheToolActionAvailability : IActionAvailability
{
    /// <summary>앱 캐시 앱 카드 ID 접두(<see cref="AppCacheRule.FINDING_ID_PREFIX"/>, 뒤에 앱 이름).</summary>
    public const string APP_CARD_PREFIX = AppCacheRule.FINDING_ID_PREFIX;

    /// <summary>
    /// 공식 도구로 정리하는 검토 규칙(supplement) 앱 식별자. rules/rule-metadata.json의 appLabel(`npm`, `pip`, `NuGet`)과 같고 대소문자를 무시해 비교합니다.
    /// </summary>
    public static readonly IReadOnlyList<string> DEFAULT_REVIEWED_APP_IDS = ["npm", "pip", "nuget"];

    private readonly Func<CacheTool, bool> _toolInProtectedLocation;
    private readonly bool _limitToSystemScope;
    private HashSet<CacheTool> _available = [];
    private readonly HashSet<string> _reviewedAppIds;

    /// <summary>판정기를 만듭니다.</summary>
    /// <param name="toolInProtectedLocation">보호 위치(Program Files)에 있는 npm/pip/dotnet 중 하나라도 찾았는지 돌려주는 함수(새로 고침 때만 평가).</param>
    /// <param name="reviewedAppIds">검토 규칙 앱 식별자(예: npm, pip, nuget). 대소문자 무시.</param>
    /// <param name="limitToSystemScope">사용자별 조치를 거절하는 시스템 전용 범위인지 여부.</param>
    public CacheToolActionAvailability(Func<CacheTool, bool> toolInProtectedLocation, IEnumerable<string> reviewedAppIds, bool limitToSystemScope = false)
    {
        ArgumentNullException.ThrowIfNull(toolInProtectedLocation);
        ArgumentNullException.ThrowIfNull(reviewedAppIds);
        _toolInProtectedLocation = toolInProtectedLocation;
        _reviewedAppIds = new HashSet<string>(reviewedAppIds, StringComparer.OrdinalIgnoreCase);
        _limitToSystemScope = limitToSystemScope;
        Refresh();
    }

    /// <inheritdoc />
    public bool CacheToolsAvailable => _available.Count > 0;

    /// <inheritdoc />
    public void Refresh() => _available = _limitToSystemScope ? [] : Enum.GetValues<CacheTool>().Where(_toolInProtectedLocation).ToHashSet();

    /// <inheritdoc />
    public bool CanExecuteInApp(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (finding.Verdict != Verdict.Candidate || !finding.Id.StartsWith(APP_CARD_PREFIX, StringComparison.Ordinal))
        {
            return false;
        }

        var appId = finding.Id[APP_CARD_PREFIX.Length..];
        var tool = appId.ToLowerInvariant() switch { "npm" => CacheTool.Npm, "pip" => CacheTool.Pip, "nuget" => CacheTool.NuGetHttp, _ => (CacheTool?)null };
        return _reviewedAppIds.Contains(appId) && tool is { } value && _available.Contains(value);
    }
}
