/**
 * @file    : OtherUserLocationGuard.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 탐지·확장·관측이 다른 사용자의 데이터(사용자 폴더 모음 아래의 다른 프로필, 휴지통의 다른 SID 폴더)에 들어가지 않도록 보호 정책과 함께 쓰는 경로 판정기
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 다른 사용자 위치 판정기입니다(스펙 §5.2 "다른 사용자 프로필을 자동 탐색하지 않는다").
/// 커뮤니티 규칙의 <c>%SystemDrive%\Users\*</c>나 휴지통(<c>$Recycle.Bin</c>) 전체 같은 경로가 관리자 권한 검사에서 다른 계정의 폴더로 들어가지 않게 합니다.
/// </summary>
public sealed class OtherUserLocationGuard
{
    /// <summary>휴지통 폴더 이름.</summary>
    public const string RECYCLE_BIN = "$Recycle.Bin";

    private const string SID_PREFIX = "S-1-";

    private readonly string? _profile;
    private readonly string? _usersFolder;
    private readonly string? _currentSid;

    /// <summary>
    /// 판정기를 만듭니다.
    /// </summary>
    /// <param name="profile">현재 사용자 프로필(모르면 null이며, 그때 사용자 폴더 모음 판정은 하지 않음).</param>
    /// <param name="currentSid">현재 사용자 SID(모르면 null이며, 그때 휴지통의 모든 SID 폴더를 다른 사용자로 봄).</param>
    public OtherUserLocationGuard(string? profile, string? currentSid)
    {
        _profile = string.IsNullOrWhiteSpace(profile) ? null : PathScope.Normalize(profile);
        _usersFolder = _profile is null ? null : PathScope.GetParent(_profile);
        _currentSid = string.IsNullOrWhiteSpace(currentSid) ? null : currentSid;
    }

    /// <summary>
    /// 경로가 다른 사용자의 위치(또는 그 아래)인지 확인합니다.
    /// </summary>
    /// <param name="path">절대 경로.</param>
    /// <returns>다른 사용자 위치이면 true.</returns>
    public bool IsOtherUserLocation(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = PathScope.Normalize(path);
        if (_usersFolder is not null && _profile is not null
            && PathScope.IsStrictlyUnder(normalized, _usersFolder) && !PathScope.IsSameOrUnder(normalized, _profile))
        {
            return true;
        }

        var segments = Winapp2PathSyntax.Segments(normalized);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], RECYCLE_BIN, StringComparison.OrdinalIgnoreCase))
            {
                var owner = segments[index + 1];
                return owner.StartsWith(SID_PREFIX, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(owner, _currentSid, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
