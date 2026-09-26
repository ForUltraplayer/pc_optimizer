/**
 * @file    : PipConfigReader.cs
 * @author  : rudals252
 * @brief   : pip 캐시 위치 설정 리더(환경 변수 PIP_CACHE_DIR → %APPDATA%\pip\pip.ini의 [global] cache-dir만 읽음, 색인 URL의 자격 증명 등 다른 키는 보관·기록하지 않음, 사이트·가상 환경 설정과 실행 인수는 범위 밖)
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// pip 설정 리더입니다. 우선순위: 환경 변수 <c>PIP_CACHE_DIR</c> &gt; <c>%APPDATA%\pip\pip.ini</c>의 <c>[global] cache-dir</c>.
/// </summary>
/// <remarks>
/// INI 절 이름은 대소문자를 구분하지 않고, 키와 값은 '=' 또는 ':'로 나눕니다('#'·';'로 시작하는 줄은 주석). 다른 절의 cache-dir은 읽지 않습니다.
/// </remarks>
public sealed class PipConfigReader : IAppConfigReader
{
    /// <summary>리더 이름.</summary>
    public const string APP = "pip";

    /// <summary>캐시 위치 환경 변수.</summary>
    public const string ENVIRONMENT_VARIABLE = "PIP_CACHE_DIR";

    /// <summary>%APPDATA% 기준 사용자 설정 파일.</summary>
    public const string USER_CONFIG_RELATIVE = @"pip\pip.ini";

    /// <summary>읽을 설정 파일 최대 크기(바이트).</summary>
    public const int MAX_CONFIG_BYTES = 64 * 1024;

    private const string APP_DATA_VARIABLE = "APPDATA";
    private const string GLOBAL_SECTION = "global";
    private const string CACHE_KEY = "cache-dir";
    private const char SECTION_START = '[';
    private const char SECTION_END = ']';
    private static readonly char[] KEY_SEPARATORS = ['=', ':'];
    private static readonly char[] COMMENT_MARKS = [';', '#'];
    private static readonly char[] LINE_BREAKS = ['\r', '\n'];

    private readonly IPathEnvironment _environment;
    private readonly IDirectoryEntrySource _source;

    /// <summary>
    /// 리더를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경.</param>
    /// <param name="source">설정 파일 존재 확인용 열거 공급자.</param>
    public PipConfigReader(IPathEnvironment environment, IDirectoryEntrySource source)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(source);
        _environment = environment;
        _source = source;
    }

    /// <inheritdoc />
    public string App => APP;

    /// <inheritdoc />
    public AppConfigReading Read()
    {
        if (_environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE) is { } fromEnvironment)
        {
            return Result(fromEnvironment, AppConfigValueOrigin.Environment);
        }

        if (_environment.GetEnvironmentVariable(APP_DATA_VARIABLE) is not { } appData)
        {
            return AppConfigReading.NotConfigured(APP);
        }

        var content = ConfigFileText.Read(_environment, _source, Path.Join(appData, USER_CONFIG_RELATIVE), MAX_CONFIG_BYTES);
        if (content.Text is null)
        {
            return content.Unreadable
                ? new AppConfigReading(APP, AppConfigReadState.Unreadable, AppConfigValueOrigin.UserFile, [])
                : AppConfigReading.NotConfigured(APP);
        }

        var value = FindGlobalCacheDir(content.Text);
        return value is null ? AppConfigReading.NotConfigured(APP) : Result(value, AppConfigValueOrigin.UserFile);
    }

    /// <summary>
    /// [global] 절의 cache-dir 값만 찾는다.
    /// </summary>
    private static string? FindGlobalCacheDir(string text)
    {
        string? value = null;
        var inGlobal = false;
        foreach (var raw in text.Split(LINE_BREAKS, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.IndexOfAny(COMMENT_MARKS) == 0)
            {
                continue;
            }

            if (line[0] == SECTION_START && line[^1] == SECTION_END)
            {
                inGlobal = string.Equals(line[1..^1].Trim(), GLOBAL_SECTION, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOfAny(KEY_SEPARATORS);
            if (inGlobal && separator > 0 && string.Equals(line[..separator].Trim(), CACHE_KEY, StringComparison.OrdinalIgnoreCase))
            {
                value = line[(separator + 1)..].Trim();
            }
        }

        return value;
    }

    /// <summary>
    /// 값을 경로로 바꿔 결과를 만든다.
    /// </summary>
    private static AppConfigReading Result(string value, AppConfigValueOrigin origin)
    {
        return ConfigPathValue.Parse(value) is { } path
            ? new AppConfigReading(APP, AppConfigReadState.Configured, origin, [path])
            : new AppConfigReading(APP, AppConfigReadState.Invalid, origin, []);
    }
}
