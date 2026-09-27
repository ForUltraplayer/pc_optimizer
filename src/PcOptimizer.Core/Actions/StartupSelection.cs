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
    {
        const string probe = StartupItemsProbeContract.PROBE_ID;
        if (!snapshot.TryGetProbe(probe, out var result) || result.Status is not (ProbeStatus.Success or ProbeStatus.Partial)
            || snapshot.GetMeasurement(probe, StartupItemsProbeContract.ITEM_COUNT)?.Value is not IntegerValue { Value: >= 0 and <= 4096 } count) { return []; }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count.Value; i++)
        {
            string? Text(string field) => snapshot.GetMeasurement(probe, StartupItemsProbeContract.ItemMeasurementName(i, field)) is { Quality: MeasurementQuality.Observed, Value: TextValue text } ? text.Value : null;
            var name = Text(StartupItemsProbeContract.FIELD_NAME);
            if (name is null || !StartupRegistration.ValidName(name) || Text(StartupItemsProbeContract.FIELD_SOURCE) != StartupRegistration.Source
                || Text(StartupItemsProbeContract.FIELD_REGISTRY_VIEW) != "Registry64" || Text(StartupItemsProbeContract.FIELD_VALUE_KIND) is not ("String" or "ExpandString")) { continue; }
            names.Add(name);
        }
        return names.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
