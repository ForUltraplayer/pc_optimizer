/**
 * @file    : TroubleshootingCatalogParser.cs
 * @author  : rudals252
 * @brief   : 문제 해결 카탈로그(troubleshooting-tools.json) 순수 해석·검증. 실행 방식별 참조(명령 ID·내장 도구 ID·링크 ID)와 문구 길이를 검사하고 오류가 하나라도 있으면 전체 거부
 */
using System.Text.Json;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Troubleshooting;

/// <summary>I/O 없이 JSON 문자열만 받습니다. 부분 적용은 없습니다.</summary>
public static class TroubleshootingCatalogParser
{
    /// <summary>지원하는 스키마 버전.</summary>
    public const int SCHEMA_VERSION = 1;
    /// <summary>한 줄 설명 최대 길이.</summary>
    public const int MAX_LINE_LENGTH = 200;
    /// <summary>절차 한 단계 최대 길이.</summary>
    public const int MAX_STEP_LENGTH = 300;
    /// <summary>도구당 절차 단계 상한.</summary>
    public const int MAX_STEPS = 12;
    /// <summary>도구·증상 개수 상한.</summary>
    public const int MAX_ITEMS = 200;
    private const int MAX_ID_LENGTH = 64;

    private static readonly Dictionary<string, ToolMode> MODES = new(StringComparer.Ordinal)
    { ["direct"] = ToolMode.DirectCommand, ["builtin"] = ToolMode.BuiltInTool, ["external"] = ToolMode.ExternalGuide };
    private static readonly Dictionary<string, SafetyLevel> SAFETY = new(StringComparer.Ordinal)
    { ["safe"] = SafetyLevel.Safe, ["caution"] = SafetyLevel.Caution, ["irreversible"] = SafetyLevel.Irreversible };

    /// <summary>카탈로그 JSON을 해석합니다. 예외를 던지지 않습니다.</summary>
    /// <param name="json">JSON 문자열.</param>
    /// <param name="linkExists">외부 도구 링크 ID가 공식 링크 표에 있는지 판정하는 함수.</param>
    public static TroubleshootingParseResult Parse(string? json, Func<string, bool> linkExists)
    {
        ArgumentNullException.ThrowIfNull(linkExists);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) { return Fail(errors, "empty"); }
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { return Fail(errors, "notJson"); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { return Fail(errors, "notObject"); }
            if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != SCHEMA_VERSION)
            { return Fail(errors, "schemaVersion"); }
            if (!root.TryGetProperty("tools", out var toolsElement) || toolsElement.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("symptoms", out var symptomsElement) || symptomsElement.ValueKind != JsonValueKind.Array)
            { return Fail(errors, "missingArrays"); }
            if (toolsElement.GetArrayLength() is 0 or > MAX_ITEMS || symptomsElement.GetArrayLength() is 0 or > MAX_ITEMS) { return Fail(errors, "itemCount"); }

            var tools = new List<TroubleshootingTool>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var element in toolsElement.EnumerateArray())
            {
                var tool = ParseTool(element, index, errors, linkExists);
                if (tool is not null && !ids.Add(tool.Id)) { errors.Add($"tools[{index}].id:duplicate"); }
                if (tool is not null) { tools.Add(tool); }
                index++;
            }
            var symptoms = new List<Symptom>();
            var symptomIds = new HashSet<string>(StringComparer.Ordinal);
            index = 0;
            foreach (var element in symptomsElement.EnumerateArray())
            {
                var symptom = ParseSymptom(element, index, errors, ids);
                if (symptom is not null && !symptomIds.Add(symptom.Id)) { errors.Add($"symptoms[{index}].id:duplicate"); }
                if (symptom is not null) { symptoms.Add(symptom); }
                index++;
            }
            return errors.Count > 0 ? new(null, errors) : new(new TroubleshootingCatalog(tools, symptoms), []);
        }
    }

    private static TroubleshootingTool? ParseTool(JsonElement element, int index, List<string> errors, Func<string, bool> linkExists)
    {
        var prefix = $"tools[{index}]";
        if (element.ValueKind != JsonValueKind.Object) { errors.Add(prefix + ":notObject"); return null; }
        var id = Id(element, "id", prefix, errors);
        var category = Line(element, "category", prefix, errors, MAX_LINE_LENGTH);
        var name = Line(element, "name", prefix, errors, MAX_LINE_LENGTH);
        var when = Line(element, "when", prefix, errors, MAX_LINE_LENGTH);
        var what = Line(element, "what", prefix, errors, MAX_LINE_LENGTH);
        var caution = Line(element, "caution", prefix, errors, MAX_LINE_LENGTH);
        var modeText = Line(element, "mode", prefix, errors, MAX_ID_LENGTH);
        var safetyText = Line(element, "safety", prefix, errors, MAX_ID_LENGTH);
        var warning = Optional(element, "warning", prefix, errors, MAX_STEP_LENGTH);
        var command = Optional(element, "command", prefix, errors, MAX_ID_LENGTH);
        var openTarget = Optional(element, "openTarget", prefix, errors, MAX_ID_LENGTH);
        var linkId = Optional(element, "linkId", prefix, errors, MAX_ID_LENGTH);
        var reboot = element.TryGetProperty("rebootRequired", out var rebootElement) && rebootElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? rebootElement.GetBoolean() : Missing<bool>(prefix + ".rebootRequired", errors);
        var steps = Steps(element, prefix, errors);
        if (modeText is null || !MODES.TryGetValue(modeText, out var mode)) { errors.Add(prefix + ".mode:unknown"); return null; }
        if (safetyText is null || !SAFETY.TryGetValue(safetyText, out var safety)) { errors.Add(prefix + ".safety:unknown"); return null; }
        var references = new[] { command, openTarget, linkId }.Count(v => v is not null);
        if (references != 1) { errors.Add(prefix + ":referenceCount"); }
        switch (mode)
        {
            case ToolMode.DirectCommand when command is null || !RepairCommandCatalog.IsKnown(command): errors.Add(prefix + ".command:unknown"); break;
            case ToolMode.BuiltInTool when openTarget is null || BuiltInToolCatalog.Find(openTarget) is null: errors.Add(prefix + ".openTarget:unknown"); break;
            case ToolMode.ExternalGuide when linkId is null || !linkExists(linkId): errors.Add(prefix + ".linkId:unknown"); break;
        }
        if (mode == ToolMode.ExternalGuide && safety == SafetyLevel.Irreversible && warning is null) { errors.Add(prefix + ".warning:requiredForIrreversible"); }
        if (id is null || category is null || name is null || when is null || what is null || caution is null || steps is null) { return null; }
        return new(id, category, name, when, what, caution, mode, safety, steps, warning, command, openTarget, linkId, reboot);
    }

    private static Symptom? ParseSymptom(JsonElement element, int index, List<string> errors, HashSet<string> toolIds)
    {
        var prefix = $"symptoms[{index}]";
        if (element.ValueKind != JsonValueKind.Object) { errors.Add(prefix + ":notObject"); return null; }
        var id = Id(element, "id", prefix, errors);
        var title = Line(element, "title", prefix, errors, MAX_LINE_LENGTH);
        var summary = Line(element, "summary", prefix, errors, MAX_STEP_LENGTH);
        if (!element.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array || stepsElement.GetArrayLength() is 0 or > MAX_STEPS)
        { errors.Add(prefix + ".steps:invalid"); return null; }
        var steps = new List<SymptomStep>();
        var stepIndex = 0;
        foreach (var step in stepsElement.EnumerateArray())
        {
            var stepPrefix = $"{prefix}.steps[{stepIndex++}]";
            if (step.ValueKind != JsonValueKind.Object) { errors.Add(stepPrefix + ":notObject"); continue; }
            var tool = Id(step, "tool", stepPrefix, errors);
            var note = Line(step, "note", stepPrefix, errors, MAX_STEP_LENGTH);
            if (tool is not null && !toolIds.Contains(tool)) { errors.Add(stepPrefix + ".tool:unknown"); }
            if (tool is not null && note is not null) { steps.Add(new(tool, note)); }
        }
        if (steps.Select(s => s.ToolId).Distinct(StringComparer.Ordinal).Count() != steps.Count) { errors.Add(prefix + ".steps:duplicateTool"); }
        return id is null || title is null || summary is null ? null : new(id, title, summary, steps);
    }

    private static IReadOnlyList<string>? Steps(JsonElement element, string prefix, List<string> errors)
    {
        if (!element.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() is 0 or > MAX_STEPS)
        { errors.Add(prefix + ".steps:invalid"); return null; }
        var list = new List<string>();
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.String || !ValidText(step.GetString(), MAX_STEP_LENGTH)) { errors.Add(prefix + ".steps:text"); return null; }
            list.Add(step.GetString()!);
        }
        return list;
    }

    private static string? Id(JsonElement element, string property, string prefix, List<string> errors)
    {
        var value = Line(element, property, prefix, errors, MAX_ID_LENGTH);
        if (value is not null && !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')) { errors.Add($"{prefix}.{property}:format"); return null; }
        return value;
    }

    private static string? Line(JsonElement element, string property, string prefix, List<string> errors, int max)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || !ValidText(value.GetString(), max))
        { errors.Add($"{prefix}.{property}:invalid"); return null; }
        return value.GetString();
    }

    private static string? Optional(JsonElement element, string property, string prefix, List<string> errors, int max)
    {
        if (!element.TryGetProperty(property, out var value)) { return null; }
        if (value.ValueKind != JsonValueKind.String || !ValidText(value.GetString(), max)) { errors.Add($"{prefix}.{property}:invalid"); return null; }
        return value.GetString();
    }

    private static T Missing<T>(string what, List<string> errors) { errors.Add(what + ":invalid"); return default!; }

    private static bool ValidText(string? text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max && !text.Any(c => char.IsControl(c) || char.IsSurrogate(c));

    private static TroubleshootingParseResult Fail(List<string> errors, string code) { errors.Add(code); return new(null, errors); }
}
