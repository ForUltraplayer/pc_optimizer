/**
 * @file    : StartupSelection.cs
 * @author  : rudals252
 * @brief   : 시작 항목 스냅샷에서 지원하는 현재 사용자 Run 이름만 선택
 */
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Core.Actions;

/// <summary>추천/자동 선택 없이 사용자가 판단할 등록 이름만 반환합니다.</summary>
public static class StartupSelection
{
    /// <summary>HKLM·RunOnce·폴더·잘못된 관측은 제외합니다. 실행 직전에는 네이티브 원문을 다시 확인합니다.</summary>
    public static IReadOnlyList<string> Names(ScanSnapshot snapshot)
        => Targets(snapshot).Where(t => t.SourceKey == StartupRegistration.Source).Select(t => t.ValueName).ToArray();
    /// <summary>허용한 Run 출처와 보기를 정확히 짝지어 이름 충돌 없이 반환합니다.</summary>
    public static IReadOnlyList<ActionTarget.Startup> Targets(ScanSnapshot snapshot)
    {
        const string probe = StartupItemsProbeContract.PROBE_ID;
        if (!snapshot.TryGetProbe(probe, out var result) || result.Status is not (ProbeStatus.Success or ProbeStatus.Partial)
            || snapshot.GetMeasurement(probe, StartupItemsProbeContract.ITEM_COUNT)?.Value is not IntegerValue { Value: >= 0 and <= 4096 } count) { return []; }
        var targets = new Dictionary<string, ActionTarget.Startup>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count.Value; i++)
        {
            string? Text(string field) => snapshot.GetMeasurement(probe, StartupItemsProbeContract.ItemMeasurementName(i, field)) is { Quality: MeasurementQuality.Observed, Value: TextValue text } ? text.Value : null;
            var name = Text(StartupItemsProbeContract.FIELD_NAME);
            var source = Text(StartupItemsProbeContract.FIELD_SOURCE);
            if (source is StartupRegistration.UserFolder or StartupRegistration.CommonFolder)
            {
                if (name is not null && StartupRegistration.ValidName(name) && name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                    && name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|', '~']) < 0)
                { targets.TryAdd(source + ":" + name, new(source, name)); }
                continue;
            }
            var view = source switch { StartupRegistration.Source or StartupRegistration.Machine64 => "Registry64", StartupRegistration.Machine32 => "Registry32", _ => null };
            if (name is null || !StartupRegistration.ValidName(name) || view is null
                || Text(StartupItemsProbeContract.FIELD_REGISTRY_VIEW) != view || Text(StartupItemsProbeContract.FIELD_VALUE_KIND) is not ("String" or "ExpandString")) { continue; }
            targets.TryAdd(source + ":" + name, new(source!, name));
        }
        return targets.Values.OrderBy(t => t.SourceKey, StringComparer.Ordinal).ThenBy(t => t.ValueName, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
