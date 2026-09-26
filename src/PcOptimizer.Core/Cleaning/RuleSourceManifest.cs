/**
 * @file    : RuleSourceManifest.cs
 * @author  : rudals252
 * @brief   : 포함 규칙 파일 출처 목록(sources.json: 파일별 종류·SHA-256·라이선스·원본 URL·커밋·버전·저작자 표시) 레코드와 순수 파서(필수 파일 종류가 빠지거나 형식이 틀리면 전체 무효)
 */

// 기본 패키지
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 포함 규칙 파일 하나의 출처 정보입니다.
/// </summary>
/// <param name="Path">rules 폴더 기준 파일 이름(하위 폴더·상위 이동 없음).</param>
/// <param name="Kind">종류(<c>winapp2</c>·<c>supplement</c>·<c>metadata</c>).</param>
/// <param name="Sha256">포함한 파일 바이트의 SHA-256(소문자 16진수).</param>
/// <param name="License">라이선스 식별자.</param>
/// <param name="UpstreamCommit">원본 커밋(커뮤니티 스냅샷만).</param>
/// <param name="UpstreamVersion">원본 버전 줄 값(커뮤니티 스냅샷만).</param>
public sealed record RuleSourceFile(string Path, string Kind, string Sha256, string License, string? UpstreamCommit, string? UpstreamVersion);

/// <summary>
/// 포함 규칙 파일 출처 목록입니다.
/// </summary>
/// <param name="Files">파일 목록.</param>
public sealed partial record RuleSourceManifest(IReadOnlyList<RuleSourceFile> Files)
{
    /// <summary>종류: 커뮤니티 winapp2 스냅샷.</summary>
    public const string KIND_WINAPP2 = "winapp2";

    /// <summary>종류: 검토한 보충 규칙.</summary>
    public const string KIND_SUPPLEMENT = "supplement";

    /// <summary>종류: 규칙 메타데이터.</summary>
    public const string KIND_METADATA = "metadata";

    /// <summary>지원하는 스키마 버전.</summary>
    public const int SUPPORTED_SCHEMA_VERSION = 1;

    private const string PROPERTY_SCHEMA_VERSION = "schemaVersion";
    private const string PROPERTY_FILES = "files";
    private const string PROPERTY_PATH = "path";
    private const string PROPERTY_KIND = "kind";
    private const string PROPERTY_SHA256 = "sha256";
    private const string PROPERTY_LICENSE = "license";
    private const string PROPERTY_COMMIT = "upstreamCommit";
    private const string PROPERTY_VERSION = "upstreamVersion";
    private const int REGEX_TIMEOUT_MILLISECONDS = 1000;
    private static readonly string[] REQUIRED_KINDS = [KIND_WINAPP2, KIND_SUPPLEMENT, KIND_METADATA];

    /// <summary>
    /// 종류로 파일을 찾습니다.
    /// </summary>
    /// <param name="kind">종류.</param>
    /// <returns>파일(없으면 null).</returns>
    public RuleSourceFile? Find(string kind)
    {
        return Files.FirstOrDefault(file => string.Equals(file.Kind, kind, StringComparison.Ordinal));
    }

    /// <summary>
    /// sources.json을 해석합니다. 필수 종류(winapp2·supplement·metadata)가 하나씩 없거나, 파일 이름이 단순 이름이 아니거나, 해시 형식이 틀리면 null입니다.
    /// </summary>
    /// <param name="json">JSON 문자열.</param>
    /// <returns>출처 목록 또는 null.</returns>
    public static RuleSourceManifest? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(PROPERTY_SCHEMA_VERSION, out var version) || !version.TryGetInt32(out var number) || number != SUPPORTED_SCHEMA_VERSION
                || !root.TryGetProperty(PROPERTY_FILES, out var files) || files.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var list = new List<RuleSourceFile>();
            foreach (var element in files.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || Text(element, PROPERTY_PATH) is not { } path || !SimpleFileName().IsMatch(path)
                    || Text(element, PROPERTY_KIND) is not { } kind
                    || Text(element, PROPERTY_SHA256) is not { } sha || !Sha256Hex().IsMatch(sha)
                    || Text(element, PROPERTY_LICENSE) is not { } license)
                {
                    return null;
                }

                list.Add(new RuleSourceFile(path, kind, sha.ToLowerInvariant(), license, Text(element, PROPERTY_COMMIT), Text(element, PROPERTY_VERSION)));
            }

            return REQUIRED_KINDS.All(kind => list.Count(file => file.Kind == kind) == 1) ? new RuleSourceManifest(list.AsReadOnly()) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 공백이 아닌 문자열 속성을 읽는다.
    /// </summary>
    private static string? Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// 폴더 구분자·상위 이동이 없는 단순 파일 이름.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex SimpleFileName();

    /// <summary>
    /// 64자리 16진수.
    /// </summary>
    [GeneratedRegex(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex Sha256Hex();
}
