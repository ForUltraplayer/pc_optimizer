/**
 * @file    : NpmConfigReader.cs
 * @author  : rudals252
 * @brief   : npm 캐시 위치 설정 리더(환경 변수 npm_config_cache → 사용자 %USERPROFILE%\.npmrc의 cache 키만 읽음, 인증 토큰 등 다른 키는 보관·기록하지 않음, 전역·프로젝트 npmrc와 실행 인수는 범위 밖)
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// npm 설정 리더입니다. 우선순위: 환경 변수 <c>npm_config_cache</c> &gt; 사용자 <c>.npmrc</c>의 <c>cache=</c>.
/// </summary>
/// <remarks>
/// .npmrc는 한 줄에 "키=값"이며 ';'·'#'로 시작하는 줄은 주석입니다. 같은 키가 여러 번 나오면 마지막 값을 씁니다.
/// 큰따옴표로 감싼 값은 JSON 문자열처럼 역슬래시 이스케이프를 풉니다. 변수 참조(${…})는 추측하지 않고 해석 불가로 둡니다.
/// </remarks>
public sealed class NpmConfigReader : IAppConfigReader
{
    /// <summary>리더 이름.</summary>
    public const string APP = "npm";

    /// <summary>캐시 위치 환경 변수.</summary>
    public const string ENVIRONMENT_VARIABLE = "npm_config_cache";

    /// <summary>프로필 기준 사용자 설정 파일 이름.</summary>
    public const string USER_CONFIG_FILE = ".npmrc";

    /// <summary>읽을 설정 파일 최대 크기(바이트).</summary>
    public const int MAX_CONFIG_BYTES = 64 * 1024;

    private const string CACHE_KEY = "cache";
    private const char KEY_SEPARATOR = '=';
    private const char QUOTE = '"';
    private const string ESCAPED_BACKSLASH = @"\\";
    private const string BACKSLASH = @"\";
    private static readonly char[] COMMENT_MARKS = [';', '#'];
    private static readonly char[] LINE_BREAKS = ['\r', '\n'];

    private readonly IPathEnvironment _environment;
    private readonly IDirectoryEntrySource _source;

    /// <summary>
    /// 리더를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경(환경 변수·작은 설정 파일 읽기).</param>
    /// <param name="source">설정 파일 존재 확인용 열거 공급자.</param>
    public NpmConfigReader(IPathEnvironment environment, IDirectoryEntrySource source)
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

        if (_environment.GetUserProfilePath() is not { } profile)
        {
            return AppConfigReading.NotConfigured(APP);
        }

        var content = ConfigFileText.Read(_environment, _source, Path.Join(profile, USER_CONFIG_FILE), MAX_CONFIG_BYTES);
        if (content.Text is null)
        {
            return content.Unreadable
                ? new AppConfigReading(APP, AppConfigReadState.Unreadable, AppConfigValueOrigin.UserFile, [])
                : AppConfigReading.NotConfigured(APP);
        }

        var value = FindCacheValue(content.Text);
        return value is null ? AppConfigReading.NotConfigured(APP) : Result(value, AppConfigValueOrigin.UserFile);
    }

    /// <summary>
    /// cache 키의 마지막 값을 찾는다(다른 키의 값은 보관하지 않음).
    /// </summary>
    private static string? FindCacheValue(string text)
    {
        string? value = null;
        foreach (var raw in text.Split(LINE_BREAKS, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.IndexOfAny(COMMENT_MARKS) == 0)
            {
                continue;
            }

            var separator = line.IndexOf(KEY_SEPARATOR);
            if (separator > 0 && string.Equals(line[..separator].Trim(), CACHE_KEY, StringComparison.OrdinalIgnoreCase))
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
        var unquoted = value.Length > 1 && value[0] == QUOTE && value[^1] == QUOTE
            ? value[1..^1].Replace(ESCAPED_BACKSLASH, BACKSLASH, StringComparison.Ordinal)
            : value;
        return ConfigPathValue.Parse(unquoted) is { } path
            ? new AppConfigReading(APP, AppConfigReadState.Configured, origin, [path])
            : new AppConfigReading(APP, AppConfigReadState.Invalid, origin, []);
    }
}
