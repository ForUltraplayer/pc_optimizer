/**
 * @file    : Winapp2Variables.cs
 * @author  : rudals252
 * @brief   : winapp2/CCleaner 형식 경로에서 지원하는 %이름% 환경 변수 표(32/64비트 두 위치로 펼치는 변수 포함)와 템플릿의 변수 이름 추출 도우미(순수 문자열 처리)
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 지원 환경 변수 표입니다. 표에 없는 변수가 든 규칙은 "해석되지 않은 변수"로 미지원 처리합니다(스펙 §5.1).
/// 실제 값 해석은 Probes의 경로 해석기가 합니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>%ProgramFiles%·%CommonProgramFiles%는 CCleaner 형식 관례대로 64비트·32비트(x86) 두 위치로 펼칩니다.</item>
/// <item>%ProgramData%는 %CommonAppData%와 같은 위치입니다(스냅샷에서 널리 쓰여 표에 포함).</item>
/// <item>%UserName% 등 사용자 식별 값을 경로에 넣는 변수는 지원하지 않습니다.</item>
/// </list>
/// </remarks>
public static class Winapp2Variables
{
    /// <summary>%AppData%(로밍 앱 데이터).</summary>
    public const string APP_DATA = "AppData";

    /// <summary>%LocalAppData%.</summary>
    public const string LOCAL_APP_DATA = "LocalAppData";

    /// <summary>%LocalLowAppData%(AppData\LocalLow).</summary>
    public const string LOCAL_LOW_APP_DATA = "LocalLowAppData";

    /// <summary>%UserProfile%.</summary>
    public const string USER_PROFILE = "UserProfile";

    /// <summary>%ProgramFiles%(64비트·x86 두 위치).</summary>
    public const string PROGRAM_FILES = "ProgramFiles";

    /// <summary>%CommonProgramFiles%(64비트·x86 두 위치).</summary>
    public const string COMMON_PROGRAM_FILES = "CommonProgramFiles";

    /// <summary>%CommonAppData%(ProgramData).</summary>
    public const string COMMON_APP_DATA = "CommonAppData";

    /// <summary>%ProgramData%.</summary>
    public const string PROGRAM_DATA = "ProgramData";

    /// <summary>%WinDir%.</summary>
    public const string WIN_DIR = "WinDir";

    /// <summary>%SystemDrive%(예: "C:").</summary>
    public const string SYSTEM_DRIVE = "SystemDrive";

    /// <summary>%Documents%(Known Folder).</summary>
    public const string DOCUMENTS = "Documents";

    /// <summary>%Pictures%(Known Folder).</summary>
    public const string PICTURES = "Pictures";

    /// <summary>%Music%(Known Folder).</summary>
    public const string MUSIC = "Music";

    /// <summary>%Video%(Known Folder).</summary>
    public const string VIDEO = "Video";

    /// <summary>%Public%.</summary>
    public const string PUBLIC = "Public";

    /// <summary>%Temp%.</summary>
    public const string TEMP = "Temp";

    /// <summary>변수 표시 문자.</summary>
    public const char MARK = '%';

    /// <summary>지원 변수 이름(대소문자 무시).</summary>
    public static readonly IReadOnlySet<string> SUPPORTED = new HashSet<string>(
        [APP_DATA, LOCAL_APP_DATA, LOCAL_LOW_APP_DATA, USER_PROFILE, PROGRAM_FILES, COMMON_PROGRAM_FILES, COMMON_APP_DATA, PROGRAM_DATA,
            WIN_DIR, SYSTEM_DRIVE, DOCUMENTS, PICTURES, MUSIC, VIDEO, PUBLIC, TEMP],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 템플릿의 %이름% 변수 이름을 순서대로 추출합니다.
    /// </summary>
    /// <param name="template">경로 템플릿.</param>
    /// <param name="names">변수 이름 목록.</param>
    /// <returns>%가 짝이 맞고 이름이 비어 있지 않으면 true.</returns>
    public static bool TryGetNames(string template, out IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(template);
        var found = new List<string>();
        names = found;
        var index = 0;
        while (index < template.Length)
        {
            var start = template.IndexOf(MARK, index);
            if (start < 0)
            {
                return true;
            }

            var end = template.IndexOf(MARK, start + 1);
            if (end <= start + 1)
            {
                return false;
            }

            found.Add(template[(start + 1)..end]);
            index = end + 1;
        }

        return true;
    }

    /// <summary>
    /// 변수 이름이 지원 표에 있는지 확인합니다.
    /// </summary>
    /// <param name="name">변수 이름.</param>
    /// <returns>지원하면 true.</returns>
    public static bool IsSupported(string name)
    {
        return SUPPORTED.Contains(name);
    }
}
