/**
 * @file    : ProtectionPolicyParser.cs
 * @author  : rudals252
 * @brief   : 보호 정책 JSON 문자열을 파싱·검증하는 순수 함수(스키마 버전·알 수 없는 종류·필수 값·경로 형태 검사, 하나라도 틀리면 정책 전체 무효)
 */

// 기본 패키지
using System.Text.Json;

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 보호 정책(protect.json) 파서입니다. 파일을 읽지 않고 문자열만 검증합니다.
/// 알 수 없는 종류나 잘못된 항목을 건너뛰고 나머지로 계속하지 않고, 정책 전체를 무효로 돌려줍니다(보호 없이 순회하는 일을 막기 위함).
/// </summary>
/// <remarks>
/// 형식:
/// <code>
/// { "schemaVersion": 1, "notes": "...", "protectedRoots": [
///   { "kind": "knownFolder", "folder": "Documents" },
///   { "kind": "environmentPath", "path": "%SystemRoot%\\System32" },
///   { "kind": "cloudSyncEnvironment", "provider": "OneDrive", "variable": "OneDrive" },
///   { "kind": "cloudSyncRegistry", "provider": "OneDrive", "hive": "HKCU", "key": "Software\\...\\Accounts", "value": "UserFolder" },
///   { "kind": "cloudSyncJsonFile", "provider": "Dropbox", "file": "%LocalAppData%\\Dropbox\\info.json", "property": "path" } ] }
/// </code>
/// 경로 템플릿은 %이름%으로 시작하거나 드라이브 절대 경로여야 하며, UNC·상대 경로·드라이브 루트 전체는 무효입니다.
/// </remarks>
public static class ProtectionPolicyParser
{
    /// <summary>지원하는 스키마 버전.</summary>
    public const int SUPPORTED_SCHEMA_VERSION = 1;

    /// <summary>종류: Known Folder.</summary>
    public const string KIND_KNOWN_FOLDER = "knownFolder";

    /// <summary>종류: 환경 변수 경로.</summary>
    public const string KIND_ENVIRONMENT_PATH = "environmentPath";

    /// <summary>종류: 동기화 루트 환경 변수.</summary>
    public const string KIND_CLOUD_SYNC_ENVIRONMENT = "cloudSyncEnvironment";

    /// <summary>종류: 동기화 루트 레지스트리.</summary>
    public const string KIND_CLOUD_SYNC_REGISTRY = "cloudSyncRegistry";

    /// <summary>종류: 동기화 루트 JSON 설정 파일.</summary>
    public const string KIND_CLOUD_SYNC_JSON_FILE = "cloudSyncJsonFile";

    /// <summary>지원하는 레지스트리 하이브(현재 사용자만).</summary>
    public const string HIVE_CURRENT_USER = "HKCU";

    private const string PROPERTY_SCHEMA_VERSION = "schemaVersion";
    private const string PROPERTY_ROOTS = "protectedRoots";
    private const string PROPERTY_KIND = "kind";
    private const string PROPERTY_FOLDER = "folder";
    private const string PROPERTY_PATH = "path";
    private const string PROPERTY_PROVIDER = "provider";
    private const string PROPERTY_VARIABLE = "variable";
    private const string PROPERTY_HIVE = "hive";
    private const string PROPERTY_KEY = "key";
    private const string PROPERTY_VALUE = "value";
    private const string PROPERTY_FILE = "file";
    private const string PROPERTY_PROPERTY = "property";
    private const char ENVIRONMENT_MARK = '%';
    private const int MIN_ENVIRONMENT_TEMPLATE_LENGTH = 3;

    private static readonly JsonDocumentOptions DOCUMENT_OPTIONS = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// 정책 JSON을 파싱·검증합니다. 예외를 던지지 않고 결과 코드로 알립니다.
    /// </summary>
    /// <param name="json">정책 JSON(null·빈 문자열은 Missing).</param>
    /// <returns>파싱 결과.</returns>
    public static ProtectionPolicyParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.Missing);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DOCUMENT_OPTIONS);
        }
        catch (JsonException)
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.MalformedJson);
        }

        using (document)
        {
            return ParseRoot(document.RootElement);
        }
    }

    /// <summary>
    /// 최상위 객체를 검증한다.
    /// </summary>
    private static ProtectionPolicyParseResult ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.NotAnObject);
        }

        if (!root.TryGetProperty(PROPERTY_SCHEMA_VERSION, out var version))
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.MissingSchemaVersion);
        }

        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != SUPPORTED_SCHEMA_VERSION)
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.UnsupportedSchemaVersion);
        }

        if (!root.TryGetProperty(PROPERTY_ROOTS, out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.MissingRoots);
        }

        if (roots.GetArrayLength() == 0)
        {
            return ProtectionPolicyParseResult.Invalid(ProtectionPolicyError.EmptyRoots);
        }

        var specs = new List<ProtectedRootSpec>();
        var index = 0;
        foreach (var entry in roots.EnumerateArray())
        {
            var (spec, error) = ParseEntry(entry);
            if (spec is null)
            {
                return ProtectionPolicyParseResult.Invalid(error, index);
            }

            specs.Add(spec);
            index++;
        }

        return ProtectionPolicyParseResult.Valid(new ProtectionPolicy(SUPPORTED_SCHEMA_VERSION, specs));
    }

    /// <summary>
    /// 보호 루트 항목 하나를 검증한다.
    /// </summary>
    private static (ProtectedRootSpec? Spec, ProtectionPolicyError Error) ParseEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return (null, ProtectionPolicyError.InvalidEntry);
        }

        var kind = RequiredText(entry, PROPERTY_KIND);
        return kind switch
        {
            null => (null, ProtectionPolicyError.MissingField),
            KIND_KNOWN_FOLDER => ParseKnownFolder(entry),
            KIND_ENVIRONMENT_PATH => ParseEnvironmentPath(entry),
            KIND_CLOUD_SYNC_ENVIRONMENT => ParseCloudSyncEnvironment(entry),
            KIND_CLOUD_SYNC_REGISTRY => ParseCloudSyncRegistry(entry),
            KIND_CLOUD_SYNC_JSON_FILE => ParseCloudSyncJsonFile(entry),
            _ => (null, ProtectionPolicyError.UnknownKind),
        };
    }

    /// <summary>
    /// Known Folder 항목. 이름은 열거형 이름과 정확히(대소문자 포함) 같아야 한다(숫자 금지).
    /// </summary>
    private static (ProtectedRootSpec? Spec, ProtectionPolicyError Error) ParseKnownFolder(JsonElement entry)
    {
        var folder = RequiredText(entry, PROPERTY_FOLDER);
        if (folder is null)
        {
            return (null, ProtectionPolicyError.MissingField);
        }

        var known = Enum.GetValues<ProtectedKnownFolder>()
            .Where(value => string.Equals(value.ToString(), folder, StringComparison.Ordinal))
            .Select(value => (ProtectedKnownFolder?)value)
            .FirstOrDefault();
        return known is { } value
            ? (new KnownFolderRootSpec(value), ProtectionPolicyError.None)
            : (null, ProtectionPolicyError.UnknownKnownFolder);
    }

    /// <summary>
    /// 환경 변수 경로 항목.
    /// </summary>
    private static (ProtectedRootSpec? Spec, ProtectionPolicyError Error) ParseEnvironmentPath(JsonElement entry)
    {
        var path = RequiredText(entry, PROPERTY_PATH);
        if (path is null)
        {
            return (null, ProtectionPolicyError.MissingField);
        }

        return IsAbsoluteTemplate(path)
            ? (new EnvironmentPathRootSpec(path), ProtectionPolicyError.None)
            : (null, ProtectionPolicyError.InvalidPath);
    }

    /// <summary>
    /// 동기화 루트 환경 변수 항목.
    /// </summary>
    private static (ProtectedRootSpec? Spec, ProtectionPolicyError Error) ParseCloudSyncEnvironment(JsonElement entry)
    {
        var provider = RequiredText(entry, PROPERTY_PROVIDER);
        var variable = RequiredText(entry, PROPERTY_VARIABLE);
        return provider is null || variable is null
            ? (null, ProtectionPolicyError.MissingField)
            : (new CloudSyncEnvironmentRootSpec(provider, variable), ProtectionPolicyError.None);
    }

    /// <summary>
    /// 동기화 루트 레지스트리 항목(HKCU만).
    /// </summary>
    private static (ProtectedRootSpec? Spec, ProtectionPolicyError Error) ParseCloudSyncRegistry(JsonElement entry)
    {
        var provider = RequiredText(entry, PROPERTY_PROVIDER);
        var hive = RequiredText(entry, PROPERTY_HIVE);
        var key = RequiredText(entry, PROPERTY_KEY);
        var value = RequiredText(entry, PROPERTY_VALUE);
        if (provider is null || hive is null || key is null || value is null)
        {
            return (null, ProtectionPolicyError.MissingField);
        }

        return string.Equals(hive, HIVE_CURRENT_USER, StringComparison.Ordinal)
            ? (new CloudSyncRegistryRootSpec(provider, key, value), ProtectionPolicyError.None)
            : (null, ProtectionPolicyError.UnsupportedHive);
    }

    /// <summary>
    /// 동기화 루트 JSON 설정 파일 항목.
    /// </summary>
    private static (ProtectedRootSpec? Spec, ProtectionPolicyError Error) ParseCloudSyncJsonFile(JsonElement entry)
    {
        var provider = RequiredText(entry, PROPERTY_PROVIDER);
        var file = RequiredText(entry, PROPERTY_FILE);
        var property = RequiredText(entry, PROPERTY_PROPERTY);
        if (provider is null || file is null || property is null)
        {
            return (null, ProtectionPolicyError.MissingField);
        }

        return IsAbsoluteTemplate(file)
            ? (new CloudSyncJsonFileRootSpec(provider, file, property), ProtectionPolicyError.None)
            : (null, ProtectionPolicyError.InvalidPath);
    }

    /// <summary>
    /// 공백이 아닌 문자열 속성을 읽는다(없거나 문자열이 아니거나 공백이면 null).
    /// </summary>
    private static string? RequiredText(JsonElement entry, string name)
    {
        return entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
    }

    /// <summary>
    /// "%이름%…" 형태이거나 드라이브 루트가 아닌 드라이브 절대 경로인지 확인한다.
    /// </summary>
    private static bool IsAbsoluteTemplate(string path)
    {
        if (path.Length >= MIN_ENVIRONMENT_TEMPLATE_LENGTH && path[0] == ENVIRONMENT_MARK)
        {
            return path.IndexOf(ENVIRONMENT_MARK, 1) > 1;
        }

        return PathScope.IsDriveAbsolute(path) && !PathScope.IsDriveRoot(path);
    }
}
