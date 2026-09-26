/**
 * @file    : VendorLinkCatalogParser.cs
 * @author  : rudals252
 * @brief   : 공식 링크 표(vendor-links.json) 순수 해석·검증(스키마 버전, 종류, 정규화 키, HTTPS URL, 항목의 공식 도메인 안 호스트, ID·URL·OEM 제조사 이름 중복 거부). 오류가 하나라도 있으면 표 전체를 거부
 */

// 기본 패키지
using System.Globalization;
using System.Text.Json;

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 공식 링크 표를 해석합니다. I/O 없이 문자열만 받습니다. 항목 하나라도 규칙을 어기면 표 전체를 쓰지 않습니다(부분 적용 없음).
/// </summary>
public static class VendorLinkCatalogParser
{
    /// <summary>지원하는 표 스키마 버전.</summary>
    public const int SCHEMA_VERSION = 1;

    private const string PROPERTY_SCHEMA_VERSION = "schemaVersion";
    private const string PROPERTY_ENTRIES = "entries";
    private const string PROPERTY_ID = "id";
    private const string PROPERTY_KIND = "kind";
    private const string PROPERTY_KEY = "key";
    private const string PROPERTY_MANUFACTURERS = "manufacturers";
    private const string PROPERTY_LABEL = "label";
    private const string PROPERTY_URL = "url";
    private const string PROPERTY_DOMAINS = "officialDomains";
    private const char DOMAIN_SEPARATOR = '.';

    /// <summary>JSON 종류 이름 → 종류.</summary>
    private static readonly Dictionary<string, VendorLinkKind> KINDS = new(StringComparer.Ordinal)
    {
        ["gpuVendor"] = VendorLinkKind.GpuVendor,
        ["oem"] = VendorLinkKind.Oem,
        ["tool"] = VendorLinkKind.Tool,
    };

    /// <summary>
    /// 표 JSON을 해석합니다. 예외를 던지지 않습니다.
    /// </summary>
    /// <param name="json">표 JSON 문자열.</param>
    /// <returns>해석 결과(오류가 있으면 표는 null).</returns>
    public static VendorLinkParseResult Parse(string? json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail(errors, "empty");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Fail(errors, "notJson");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(PROPERTY_SCHEMA_VERSION, out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var schema)
                || schema != SCHEMA_VERSION)
            {
                return Fail(errors, "schemaVersion");
            }

            if (!root.TryGetProperty(PROPERTY_ENTRIES, out var entriesElement)
                || entriesElement.ValueKind != JsonValueKind.Array
                || entriesElement.GetArrayLength() == 0)
            {
                return Fail(errors, "entries");
            }

            var entries = new List<VendorLinkEntry>();
            var index = 0;
            foreach (var element in entriesElement.EnumerateArray())
            {
                if (TryParseEntry(element, index, errors) is { } entry)
                {
                    entries.Add(entry);
                }

                index++;
            }

            CheckDuplicates(entries, errors);
            return errors.Count == 0 ? new VendorLinkParseResult(new VendorLinkCatalog(entries), []) : new VendorLinkParseResult(null, errors);
        }
    }

    /// <summary>
    /// 항목 하나를 검증해 만든다. 오류가 있으면 기록하고 null.
    /// </summary>
    private static VendorLinkEntry? TryParseEntry(JsonElement element, int index, List<string> errors)
    {
        string Error(string what) => string.Format(CultureInfo.InvariantCulture, "entries[{0}].{1}", index, what);

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(Error("notObject"));
            return null;
        }

        var id = ReadText(element, PROPERTY_ID);
        var kindText = ReadText(element, PROPERTY_KIND);
        var key = ReadText(element, PROPERTY_KEY);
        var label = ReadText(element, PROPERTY_LABEL);
        var url = ReadText(element, PROPERTY_URL);
        var domains = ReadTextList(element, PROPERTY_DOMAINS);
        var manufacturers = element.TryGetProperty(PROPERTY_MANUFACTURERS, out _) ? ReadTextList(element, PROPERTY_MANUFACTURERS) : [];
        var errorCount = errors.Count;

        if (id is null)
        {
            errors.Add(Error(PROPERTY_ID));
        }

        if (kindText is null || !KINDS.TryGetValue(kindText, out var kind))
        {
            errors.Add(Error(PROPERTY_KIND));
            kind = default;
        }

        if (key is null || !string.Equals(ManufacturerNameNormalizer.Normalize(key), key, StringComparison.Ordinal))
        {
            errors.Add(Error(PROPERTY_KEY));
        }

        if (label is null)
        {
            errors.Add(Error(PROPERTY_LABEL));
        }

        if (domains is null || domains.Count == 0 || domains.Any(domain => !IsValidDomain(domain)))
        {
            errors.Add(Error(PROPERTY_DOMAINS));
        }

        if (manufacturers is null || manufacturers.Any(name => ManufacturerNameNormalizer.Normalize(name).Length == 0))
        {
            errors.Add(Error(PROPERTY_MANUFACTURERS));
        }

        if (!OfficialUrl.TryParse(url, out var uri)
            || domains is null
            || !domains.Any(domain => OfficialUrl.IsHostInDomain(OfficialUrl.AsciiHost(uri), domain)))
        {
            errors.Add(Error(PROPERTY_URL));
        }

        if (errors.Count != errorCount)
        {
            return null;
        }

        string[] names = [.. new[] { key! }.Concat(manufacturers!.Select(ManufacturerNameNormalizer.Normalize)).Distinct(StringComparer.Ordinal)];
        return new VendorLinkEntry(id!, kind, key!, names, label!, url!, domains!);
    }

    /// <summary>
    /// ID(대소문자 무시)·URL·OEM 제조사 이름 중복을 오류로 기록한다.
    /// </summary>
    private static void CheckDuplicates(List<VendorLinkEntry> entries, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manufacturers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!ids.Add(entry.Id))
            {
                errors.Add("duplicateId:" + entry.Id);
            }

            if (!urls.Add(entry.Url))
            {
                errors.Add("duplicateUrl:" + entry.Id);
            }

            if (entry.Kind == VendorLinkKind.Oem && entry.ManufacturerNames.Any(name => !manufacturers.Add(name)))
            {
                errors.Add("duplicateManufacturer:" + entry.Id);
            }
        }
    }

    /// <summary>
    /// 공백이 아닌 문자열 속성을 앞뒤 공백 없이 읽는다(없거나 형식이 다르면 null).
    /// </summary>
    private static string? ReadText(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
    }

    /// <summary>
    /// 문자열 배열 속성을 읽는다(없거나 문자열이 아닌 항목이 있으면 null).
    /// </summary>
    private static List<string>? ReadTextList(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                return null;
            }

            list.Add(item.GetString()!.Trim());
        }

        return list;
    }

    /// <summary>
    /// 공식 도메인 표기 검사: ASCII 소문자·숫자·'-'·'.'만, 점을 하나 이상 포함하고 점으로 시작·끝나지 않는다.
    /// </summary>
    private static bool IsValidDomain(string domain)
    {
        return domain.Contains(DOMAIN_SEPARATOR, StringComparison.Ordinal)
            && !domain.StartsWith(DOMAIN_SEPARATOR)
            && !domain.EndsWith(DOMAIN_SEPARATOR)
            && domain.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '-' or DOMAIN_SEPARATOR);
    }

    /// <summary>
    /// 오류 하나로 실패 결과를 만든다.
    /// </summary>
    private static VendorLinkParseResult Fail(List<string> errors, string error)
    {
        errors.Add(error);
        return new VendorLinkParseResult(null, errors);
    }
}
