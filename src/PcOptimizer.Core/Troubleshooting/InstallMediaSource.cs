/**
 * @file    : InstallMediaSource.cs
 * @author  : rudals252
 * @brief   : 사용자가 고른 Windows 설치 미디어 폴더(ISO 마운트·USB)에서 DISM /Source 인자를 만드는 순수 검증기. sources\install.wim 또는 install.esd만 인정한다
 */
namespace PcOptimizer.Core.Troubleshooting;

/// <summary>폴더 경로를 명령으로 해석하지 않고, 정해진 파일 이름과 고정 인덱스만 써서 인자를 만듭니다.</summary>
public static class InstallMediaSource
{
    /// <summary>설치 미디어의 이미지 폴더 이름.</summary>
    public const string SOURCES_FOLDER = "sources";
    /// <summary>WIM 이미지 파일 이름.</summary>
    public const string WIM_FILE = "install.wim";
    /// <summary>ESD 이미지 파일 이름.</summary>
    public const string ESD_FILE = "install.esd";
    /// <summary>복구에 쓰는 이미지 인덱스(첫 번째 에디션). 다른 에디션이라도 구성 요소 저장소 복구에는 보통 충분하다.</summary>
    public const int IMAGE_INDEX = 1;
    private const int MAX_PATH_LENGTH = 240;

    /// <summary>
    /// 폴더가 설치 미디어 루트(또는 sources 폴더)이면 <c>WIM:&lt;경로&gt;:1</c> / <c>ESD:&lt;경로&gt;:1</c> 형식의 /Source 값을 만듭니다.
    /// </summary>
    /// <param name="folder">사용자가 고른 폴더.</param>
    /// <param name="fileExists">파일 존재 확인 함수(테스트 주입).</param>
    /// <param name="source">만든 /Source 값.</param>
    /// <returns>인정되면 true.</returns>
    public static bool TryResolve(string? folder, Func<string, bool> fileExists, out string source)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        source = string.Empty;
        if (string.IsNullOrWhiteSpace(folder) || folder.Length > MAX_PATH_LENGTH || folder.Any(char.IsControl)
            || folder.IndexOfAny(['%', '~', '*', '?', '"', '<', '>', '|']) >= 0 || !Path.IsPathFullyQualified(folder) || folder.StartsWith(@"\\", StringComparison.Ordinal))
        { return false; }
        var root = Path.GetFullPath(folder).TrimEnd('\\');
        // sources 폴더를 직접 골라도 인정한다.
        var sources = Path.GetFileName(root).Equals(SOURCES_FOLDER, StringComparison.OrdinalIgnoreCase) ? root : Path.Combine(root, SOURCES_FOLDER);
        var wim = Path.Combine(sources, WIM_FILE);
        var esd = Path.Combine(sources, ESD_FILE);
        if (fileExists(wim)) { source = "WIM:" + wim + ":" + IMAGE_INDEX; return true; }
        if (fileExists(esd)) { source = "ESD:" + esd + ":" + IMAGE_INDEX; return true; }
        return false;
    }
}
