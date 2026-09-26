/**
 * @file    : OtherUserLocationGuard.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 탐지·확장·관측이 다른 사용자의 데이터(ProfileList의 프로필 루트·다른 SID 프로필 경로, 휴지통의 다른 SID 폴더)에 들어가지 않도록 보호 정책과 함께 쓰는 경로 판정기(현재 프로필·Public·Default는 허용, 드라이브 루트는 기준으로 쓰지 않음)
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 다른 사용자 위치 판정기입니다(스펙 §5.2 "다른 사용자 프로필을 자동 탐색하지 않는다").
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>프로필 루트: <c>HKLM\...\ProfileList</c>의 <c>ProfilesDirectory</c>와 <c>%SystemDrive%\Users</c>. 드라이브 루트는 프로필 루트로 쓰지 않습니다.</item>
/// <item>다른 프로필: ProfileList의 다른 SID <c>ProfileImagePath</c>(시스템 서비스 계정 S-1-5-18/19/20 제외). 프로필 루트 밖(예: 다른 드라이브)이어도 막습니다.</item>
/// <item>현재 사용자 프로필은 항상 허용하고, 프로필 루트 바로 아래의 <c>Public</c>·<c>Default</c>는 사용자 개인 프로필이 아니므로 허용합니다(규칙 결과는 자기 상태로 보고).</item>
/// <item>프로필 루트 아래의 그 밖의 폴더와 휴지통(<c>$Recycle.Bin</c>)의 다른 SID 폴더는 막습니다.</item>
/// </list>
/// </remarks>
public sealed class OtherUserLocationGuard
{
    /// <summary>프로필 목록 키(HKLM, 64비트 보기).</summary>
    public const string PROFILE_LIST_KEY = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    /// <summary>프로필 루트 값 이름.</summary>
    public const string PROFILES_DIRECTORY_VALUE = "ProfilesDirectory";

    /// <summary>SID별 프로필 경로 값 이름.</summary>
    public const string PROFILE_IMAGE_PATH_VALUE = "ProfileImagePath";

    /// <summary>휴지통 폴더 이름.</summary>
    public const string RECYCLE_BIN = "$Recycle.Bin";

    /// <summary>프로필 루트 아래에서 허용하는 공용 폴더 이름.</summary>
    public static readonly IReadOnlySet<string> SHARED_PROFILE_FOLDERS = new HashSet<string>(["Public", "Default"], StringComparer.OrdinalIgnoreCase);

    private const string SID_PREFIX = "S-1-";
    private const string SYSTEM_DRIVE_VARIABLE = "SystemDrive";
    private const string DEFAULT_PROFILES_FOLDER = "Users";

    /// <summary>프로필 경로가 시스템 폴더에 있는 서비스 계정 SID(다른 사용자로 보지 않음).</summary>
    private static readonly HashSet<string> SERVICE_SIDS = new(["S-1-5-18", "S-1-5-19", "S-1-5-20"], StringComparer.OrdinalIgnoreCase);

    private readonly string? _profile;
    private readonly string? _currentSid;
    private readonly IReadOnlyList<string> _profilesRoots;
    private readonly IReadOnlyList<string> _otherProfiles;

    /// <summary>
    /// 판정기를 만듭니다.
    /// </summary>
    /// <param name="profile">현재 사용자 프로필(모르면 null).</param>
    /// <param name="currentSid">현재 사용자 SID(모르면 null이며, 그때 휴지통의 모든 SID 폴더를 다른 사용자로 봄).</param>
    /// <param name="profilesRoots">프로필 루트(드라이브 루트는 무시).</param>
    /// <param name="otherProfiles">다른 사용자 프로필 경로(드라이브 루트·현재 프로필은 무시).</param>
    public OtherUserLocationGuard(string? profile, string? currentSid, IEnumerable<string> profilesRoots, IEnumerable<string> otherProfiles)
    {
        ArgumentNullException.ThrowIfNull(profilesRoots);
        ArgumentNullException.ThrowIfNull(otherProfiles);
        _profile = string.IsNullOrWhiteSpace(profile) ? null : PathScope.Normalize(profile);
        _currentSid = string.IsNullOrWhiteSpace(currentSid) ? null : currentSid;
        _profilesRoots = [.. Usable(profilesRoots)];
        _otherProfiles = [.. Usable(otherProfiles).Where(path => _profile is null || !PathScope.IsSameOrUnder(_profile, path))];
    }

    /// <summary>
    /// 레지스트리 ProfileList와 환경으로 판정기를 만듭니다(값 하나씩만 읽음).
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="environment">경로 환경.</param>
    /// <param name="currentSid">현재 사용자 SID.</param>
    /// <returns>판정기.</returns>
    public static OtherUserLocationGuard Create(IRegistryReader registry, IPathEnvironment environment, string? currentSid)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(environment);

        var roots = new List<string>();
        if (Expand(registry.ReadStringValue(RegistryRoot.LocalMachine, RegistryView.Registry64, PROFILE_LIST_KEY, PROFILES_DIRECTORY_VALUE), environment) is { } directory)
        {
            roots.Add(directory);
        }

        if (environment.GetEnvironmentVariable(SYSTEM_DRIVE_VARIABLE) is { } systemDrive)
        {
            roots.Add(Path.Join(systemDrive.TrimEnd(PathScope.SEPARATOR) + PathScope.SEPARATOR, DEFAULT_PROFILES_FOLDER));
        }

        var others = new List<string>();
        var sids = registry.ReadSubKeyNames(RegistryRoot.LocalMachine, RegistryView.Registry64, PROFILE_LIST_KEY);
        if (sids.Status == RegistryReadStatus.Found)
        {
            foreach (var sid in sids.Names.Where(sid => !SERVICE_SIDS.Contains(sid) && !string.Equals(sid, currentSid, StringComparison.OrdinalIgnoreCase)))
            {
                var reading = registry.ReadStringValue(RegistryRoot.LocalMachine, RegistryView.Registry64, PROFILE_LIST_KEY + PathScope.SEPARATOR + sid, PROFILE_IMAGE_PATH_VALUE);
                if (Expand(reading, environment) is { } path)
                {
                    others.Add(path);
                }
            }
        }

        return new OtherUserLocationGuard(environment.GetUserProfilePath(), currentSid, roots, others);
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
        if (_profile is not null && PathScope.IsSameOrUnder(normalized, _profile))
        {
            return false;
        }

        if (_otherProfiles.Any(other => PathScope.IsSameOrUnder(normalized, other)))
        {
            return true;
        }

        foreach (var root in _profilesRoots.Where(root => PathScope.IsStrictlyUnder(normalized, root)))
        {
            var first = Winapp2PathSyntax.Segments(normalized[root.Length..]).FirstOrDefault();
            if (first is not null && !SHARED_PROFILE_FOLDERS.Contains(first))
            {
                return true;
            }
        }

        return IsOtherUserRecycleBin(normalized);
    }

    /// <summary>
    /// 휴지통의 다른 SID 폴더(또는 그 아래)인지 확인한다.
    /// </summary>
    private bool IsOtherUserRecycleBin(string normalized)
    {
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

    /// <summary>
    /// 드라이브 절대 경로(드라이브 루트 제외)만 정규화해 남긴다.
    /// </summary>
    private static IEnumerable<string> Usable(IEnumerable<string> paths)
    {
        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path) && PathScope.IsDriveAbsolute(path.Trim()) && !PathScope.IsDriveRoot(path.Trim()))
            .Select(path => PathScope.Normalize(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 레지스트리 문자열의 %변수%를 펼친다(없거나 펼치지 못하면 null).
    /// </summary>
    private static string? Expand(RegistryStringReading reading, IPathEnvironment environment)
    {
        return reading.Status == RegistryReadStatus.Found && !string.IsNullOrWhiteSpace(reading.Text)
            ? PathTemplate.ExpandOnly(reading.Text, environment)
            : null;
    }
}
