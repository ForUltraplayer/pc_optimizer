/**
 * @file    : NuGetConfigReader.cs
 * @author  : rudals252
 * @brief   : NuGet 전역 패키지 폴더 설정 리더(환경 변수 NUGET_PACKAGES → %APPDATA%\NuGet\NuGet.Config의 config/add[key=globalPackagesFolder]만 읽음, DTD·외부 참조 금지, 패키지 원본 자격 증명 등 다른 요소는 보관·기록하지 않음)
 */

// 기본 패키지
using System.Xml;

// 사용자 패키지
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// NuGet 설정 리더입니다. 우선순위: 환경 변수 <c>NUGET_PACKAGES</c> &gt; 사용자 <c>NuGet.Config</c>의 <c>globalPackagesFolder</c>.
/// 솔루션·프로젝트 폴더의 NuGet.Config와 컴퓨터 전체 설정은 범위 밖입니다.
/// </summary>
/// <remarks>
/// <c>&lt;configuration&gt;&lt;config&gt;&lt;add key="globalPackagesFolder" value="…"/&gt;</c>만 봅니다. 상대 경로·변수 참조는 추측하지 않고 해석 불가로 둡니다.
/// </remarks>
public sealed class NuGetConfigReader : IAppConfigReader
{
    /// <summary>리더 이름.</summary>
    public const string APP = "nuget";

    /// <summary>전역 패키지 폴더 환경 변수.</summary>
    public const string ENVIRONMENT_VARIABLE = "NUGET_PACKAGES";

    /// <summary>%APPDATA% 기준 사용자 설정 파일.</summary>
    public const string USER_CONFIG_RELATIVE = @"NuGet\NuGet.Config";

    /// <summary>읽을 설정 파일 최대 크기(바이트).</summary>
    public const int MAX_CONFIG_BYTES = 256 * 1024;

    private const string APP_DATA_VARIABLE = "APPDATA";
    private const string ROOT_ELEMENT = "configuration";
    private const string CONFIG_ELEMENT = "config";
    private const string ADD_ELEMENT = "add";
    private const string KEY_ATTRIBUTE = "key";
    private const string VALUE_ATTRIBUTE = "value";
    private const string GLOBAL_PACKAGES_KEY = "globalPackagesFolder";
    private const int CONFIG_DEPTH = 1;
    private const int ADD_DEPTH = 2;

    private static readonly XmlReaderSettings READER_SETTINGS = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersInDocument = MAX_CONFIG_BYTES,
    };

    private readonly IPathEnvironment _environment;
    private readonly IDirectoryEntrySource _source;

    /// <summary>
    /// 리더를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경.</param>
    /// <param name="source">설정 파일 존재 확인용 열거 공급자.</param>
    public NuGetConfigReader(IPathEnvironment environment, IDirectoryEntrySource source)
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

        if (!TryFindGlobalPackagesFolder(content.Text, out var value))
        {
            return new AppConfigReading(APP, AppConfigReadState.Invalid, AppConfigValueOrigin.UserFile, []);
        }

        return value is null ? AppConfigReading.NotConfigured(APP) : Result(value, AppConfigValueOrigin.UserFile);
    }

    /// <summary>
    /// configuration/config/add[key=globalPackagesFolder]의 value만 찾는다(마지막 값). XML이 잘못됐거나 DTD가 있으면 false.
    /// </summary>
    private static bool TryFindGlobalPackagesFolder(string xml, out string? value)
    {
        value = null;
        try
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, READER_SETTINGS);
            var inRoot = false;
            var inConfig = false;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    inConfig &= !(reader.Depth == CONFIG_DEPTH && reader.LocalName == CONFIG_ELEMENT);
                    continue;
                }

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Depth == 0)
                {
                    inRoot = reader.LocalName == ROOT_ELEMENT;
                }
                else if (reader.Depth == CONFIG_DEPTH)
                {
                    inConfig = inRoot && reader.LocalName == CONFIG_ELEMENT && !reader.IsEmptyElement;
                }
                else if (inConfig && reader.Depth == ADD_DEPTH && reader.LocalName == ADD_ELEMENT
                    && string.Equals(reader.GetAttribute(KEY_ATTRIBUTE), GLOBAL_PACKAGES_KEY, StringComparison.OrdinalIgnoreCase))
                {
                    value = reader.GetAttribute(VALUE_ATTRIBUTE);
                }
            }

            return true;
        }
        catch (XmlException)
        {
            value = null;
            return false;
        }
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
