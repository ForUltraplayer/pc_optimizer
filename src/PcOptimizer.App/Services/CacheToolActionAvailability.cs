/**
 * @file    : CacheToolActionAvailability.cs
 * @author  : rudals252
 * @brief   : SP4 시점의 유일한 앱 내 실행(npm·pip·NuGet 도구 캐시 정리) 판정. 검토 규칙 앱 카드이고 도구가 보호 위치에 있을 때만 true
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

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

    private readonly Func<bool> _anyToolInProtectedLocation;
    private readonly HashSet<string> _reviewedAppIds;

    /// <summary>판정기를 만듭니다.</summary>
    /// <param name="anyToolInProtectedLocation">보호 위치(Program Files)에 있는 npm/pip/dotnet 중 하나라도 찾았는지 돌려주는 함수(실행 시점마다 평가).</param>
    /// <param name="reviewedAppIds">검토 규칙 앱 식별자(예: npm, pip, nuget). 대소문자 무시.</param>
    public CacheToolActionAvailability(Func<bool> anyToolInProtectedLocation, IEnumerable<string> reviewedAppIds)
    {
        ArgumentNullException.ThrowIfNull(anyToolInProtectedLocation);
        ArgumentNullException.ThrowIfNull(reviewedAppIds);
        _anyToolInProtectedLocation = anyToolInProtectedLocation;
        _reviewedAppIds = new HashSet<string>(reviewedAppIds, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool CacheToolsAvailable => _anyToolInProtectedLocation();

    /// <inheritdoc />
    public bool CanExecuteInApp(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (finding.Verdict != Verdict.Candidate || !finding.Id.StartsWith(APP_CARD_PREFIX, StringComparison.Ordinal))
        {
            return false;
        }

        var appId = finding.Id[APP_CARD_PREFIX.Length..];
        return _reviewedAppIds.Contains(appId) && _anyToolInProtectedLocation();
    }
}
