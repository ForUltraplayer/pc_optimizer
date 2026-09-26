/**
 * @file    : SteamLibraryReader.cs
 * @author  : rudals252
 * @brief   : Steam 셰이더 캐시 위치 리더(HKCU SteamPath → HKLM 32비트 InstallPath, 그 아래 steamapps\libraryfolders.vdf의 라이브러리 경로를 읽어 각 라이브러리의 steamapps\shadercache만 돌려줌, 라이브러리·게임 폴더 자체는 돌려주지 않음)
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// Steam 라이브러리 리더입니다(스펙 §5.1 "Steam 라이브러리 경로 전체가 아니라 그 아래 확인된 steamapps/shadercache만 캐시 후보로 다룬다").
/// </summary>
/// <remarks>
/// 레지스트리 키에서는 설치 경로 값 하나만 꺼내고 다른 값(자동 로그인 계정 등)은 보관하지 않습니다.
/// libraryfolders.vdf가 없으면 설치 폴더만 라이브러리로 보고, 형식이 깨졌으면 해석 불가로 둡니다(기본 위치 규칙은 따로 적용).
/// </remarks>
public sealed class SteamLibraryReader : IAppConfigReader
{
    /// <summary>리더 이름.</summary>
    public const string APP = "steam";

    /// <summary>HKCU Steam 키.</summary>
    public const string USER_KEY = @"Software\Valve\Steam";

    /// <summary>HKCU 설치 경로 값 이름.</summary>
    public const string USER_VALUE = "SteamPath";

    /// <summary>HKLM(32비트 보기) Steam 키.</summary>
    public const string MACHINE_KEY = @"SOFTWARE\Valve\Steam";

    /// <summary>HKLM 설치 경로 값 이름.</summary>
    public const string MACHINE_VALUE = "InstallPath";

    /// <summary>Steam 폴더 기준 라이브러리 목록 파일.</summary>
    public const string LIBRARY_FILE_RELATIVE = @"steamapps\libraryfolders.vdf";

    /// <summary>라이브러리 기준 셰이더 캐시 폴더.</summary>
    public const string SHADER_CACHE_RELATIVE = @"steamapps\shadercache";

    /// <summary>읽을 라이브러리 목록 파일 최대 크기(바이트).</summary>
    public const int MAX_LIBRARY_FILE_BYTES = 1024 * 1024;

    private readonly IPathEnvironment _environment;
    private readonly IRegistryReader _registry;
    private readonly IDirectoryEntrySource _source;

    /// <summary>
    /// 리더를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경(작은 설정 파일 읽기).</param>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="source">설정 파일 존재 확인용 열거 공급자.</param>
    public SteamLibraryReader(IPathEnvironment environment, IRegistryReader registry, IDirectoryEntrySource source)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(source);
        _environment = environment;
        _registry = registry;
        _source = source;
    }

    /// <inheritdoc />
    public string App => APP;

    /// <inheritdoc />
    public AppConfigReading Read()
    {
        var raw = ReadInstallPath(RegistryRoot.CurrentUser, RegistryView.Default, USER_KEY, USER_VALUE)
            ?? ReadInstallPath(RegistryRoot.LocalMachine, RegistryView.Registry32, MACHINE_KEY, MACHINE_VALUE);
        if (raw is null)
        {
            return AppConfigReading.NotConfigured(APP);
        }

        if (ConfigPathValue.Parse(raw) is not { } root)
        {
            return new AppConfigReading(APP, AppConfigReadState.Invalid, AppConfigValueOrigin.Registry, []);
        }

        var libraries = new List<string> { root };
        var content = ConfigFileText.Read(_environment, _source, Path.Join(root, LIBRARY_FILE_RELATIVE), MAX_LIBRARY_FILE_BYTES);
        if (content.Text is not null)
        {
            if (SteamLibraryFoldersParser.Parse(content.Text) is not { } parsed)
            {
                return new AppConfigReading(APP, AppConfigReadState.Invalid, AppConfigValueOrigin.Registry, []);
            }

            foreach (var library in parsed.Select(ConfigPathValue.Parse).OfType<string>())
            {
                if (!libraries.Contains(library, StringComparer.OrdinalIgnoreCase))
                {
                    libraries.Add(library);
                }
            }
        }

        return new AppConfigReading(
            APP,
            AppConfigReadState.Configured,
            AppConfigValueOrigin.Registry,
            [.. libraries.Select(library => Path.Join(library, SHADER_CACHE_RELATIVE))],
            libraries.Count);
    }

    /// <summary>
    /// 키에서 설치 경로 값 하나만 꺼낸다(다른 값은 버림).
    /// </summary>
    private string? ReadInstallPath(RegistryRoot root, RegistryView view, string subKey, string valueName)
    {
        var reading = _registry.ReadKeyValues(root, view, subKey);
        return reading.Status != RegistryReadStatus.Found
            ? null
            : reading.Values.FirstOrDefault(value => string.Equals(value.Name, valueName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value.Text))?.Text;
    }
}
