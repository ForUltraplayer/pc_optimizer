/**
 * @file    : RuleMetadata.cs
 * @author  : rudals252
 * @brief   : 검토한 규칙의 메타데이터(영향: 기대 효과·부작용·재생성, 지원 앱 버전 메모, 참조 출처, 관측 방식, 연결된 앱 설정 리더) 레코드와 rule-metadata.json 순수 파서(형식이 틀리면 전체 무효)
 */

// 기본 패키지
using System.Text.Json;

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 규칙 관측 방식입니다.
/// </summary>
public enum ObservationKind
{
    /// <summary>파일 크기를 관측합니다.</summary>
    Size,

    /// <summary>폴더 이름만 나열합니다(크기·판정 없음, 예: Squirrel 버전 폴더).</summary>
    FolderNamesOnly,
}

/// <summary>
/// 검토한 영향 설명입니다.
/// </summary>
/// <param name="Benefit">기대 효과.</param>
/// <param name="SideEffect">부작용.</param>
/// <param name="Regeneration">다시 생기는지에 대한 설명.</param>
public sealed record RuleImpact(string Benefit, string SideEffect, string Regeneration);

/// <summary>
/// 검토한 규칙 메타데이터입니다. 메타데이터가 없는 커뮤니티 규칙은 "영향 미확인"으로 표시합니다.
/// </summary>
/// <param name="RuleId">규칙 ID.</param>
/// <param name="Impact">영향.</param>
/// <param name="AppVersionNotes">지원 앱 버전 메모.</param>
/// <param name="Sources">참조 출처(URL 또는 문서 이름).</param>
/// <param name="Observation">관측 방식.</param>
/// <param name="ConfigReader">연결된 앱 설정 리더 이름(npm·pip·nuget·steam·adobe, 없으면 null).</param>
public sealed record RuleMetadata(
    string RuleId,
    RuleImpact Impact,
    string AppVersionNotes,
    IReadOnlyList<string> Sources,
    ObservationKind Observation,
    string? ConfigReader);

/// <summary>
/// rule-metadata.json 파서입니다. 파일을 읽지 않고 문자열만 검증합니다.
/// </summary>
/// <remarks>
/// 형식: <c>{ "schemaVersion": 1, "rules": [ { "ruleId", "impact": { "benefit", "sideEffect", "regeneration" }, "appVersionNotes", "sources": [..],
/// "observation": "size"|"folderNamesOnly", "configReader": "npm"|… } ] }</c>. 필수 값이 없거나 규칙 ID가 겹치면 전체 무효(null)입니다.
/// </remarks>
public static class RuleMetadataParser
{
    /// <summary>지원하는 스키마 버전.</summary>
    public const int SUPPORTED_SCHEMA_VERSION = 1;

    /// <summary>관측 방식 값: 크기.</summary>
    public const string OBSERVATION_SIZE = "size";

    /// <summary>관측 방식 값: 폴더 이름만.</summary>
    public const string OBSERVATION_FOLDER_NAMES = "folderNamesOnly";

    private const string PROPERTY_SCHEMA_VERSION = "schemaVersion";
    private const string PROPERTY_RULES = "rules";
    private const string PROPERTY_RULE_ID = "ruleId";
    private const string PROPERTY_IMPACT = "impact";
    private const string PROPERTY_BENEFIT = "benefit";
    private const string PROPERTY_SIDE_EFFECT = "sideEffect";
    private const string PROPERTY_REGENERATION = "regeneration";
    private const string PROPERTY_APP_VERSION_NOTES = "appVersionNotes";
    private const string PROPERTY_SOURCES = "sources";
    private const string PROPERTY_OBSERVATION = "observation";
    private const string PROPERTY_CONFIG_READER = "configReader";

    /// <summary>
    /// 메타데이터 JSON을 해석합니다. 예외를 던지지 않고 무효이면 null을 돌려줍니다.
    /// </summary>
    /// <param name="json">JSON 문자열.</param>
    /// <returns>규칙 ID별 메타데이터 또는 null(무효).</returns>
    public static IReadOnlyDictionary<string, RuleMetadata>? Parse(string? json)
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
                || !root.TryGetProperty(PROPERTY_SCHEMA_VERSION, out var version)
                || !version.TryGetInt32(out var number) || number != SUPPORTED_SCHEMA_VERSION
                || !root.TryGetProperty(PROPERTY_RULES, out var rules) || rules.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new Dictionary<string, RuleMetadata>(StringComparer.Ordinal);
            foreach (var element in rules.EnumerateArray())
            {
                var entry = ParseEntry(element);
                if (entry is null || !result.TryAdd(entry.RuleId, entry))
                {
                    return null;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 항목 하나를 해석한다(필수 값이 없으면 null).
    /// </summary>
    private static RuleMetadata? ParseEntry(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || Text(element, PROPERTY_RULE_ID) is not { } ruleId
            || !element.TryGetProperty(PROPERTY_IMPACT, out var impact) || impact.ValueKind != JsonValueKind.Object
            || Text(impact, PROPERTY_BENEFIT) is not { } benefit
            || Text(impact, PROPERTY_SIDE_EFFECT) is not { } sideEffect
            || Text(impact, PROPERTY_REGENERATION) is not { } regeneration
            || Text(element, PROPERTY_APP_VERSION_NOTES) is not { } notes
            || !element.TryGetProperty(PROPERTY_SOURCES, out var sources) || sources.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sourceList = sources.EnumerateArray().Select(source => source.ValueKind == JsonValueKind.String ? source.GetString() : null).ToList();
        if (sourceList.Count == 0 || sourceList.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        ObservationKind? observation = Text(element, PROPERTY_OBSERVATION) switch
        {
            null or OBSERVATION_SIZE => ObservationKind.Size,
            OBSERVATION_FOLDER_NAMES => ObservationKind.FolderNamesOnly,
            _ => null,
        };
        return observation is null
            ? null
            : new RuleMetadata(ruleId, new RuleImpact(benefit, sideEffect, regeneration), notes, [.. sourceList!], observation.Value, Text(element, PROPERTY_CONFIG_READER));
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
}
