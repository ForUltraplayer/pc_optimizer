/**
 * @file    : DisplayFindingTarget.cs
 * @author  : rudals252
 * @brief   : 표시 문구 대신 같은 대상의 형식 있는 측정값으로 카드와 시험 대상을 연결
 */
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.Services;

internal sealed record DisplayFindingTarget(string DeviceKey, int Hz)
{
    internal static DisplayFindingTarget? Read(Finding finding)
    {
        if (finding.Category != FindingCategory.Display || finding.Verdict != Verdict.Candidate) { return null; }
        var paths = finding.Measured.Where(m => m.Name.StartsWith(DisplayProbeContract.TARGET_PREFIX + "[", StringComparison.Ordinal)
            && m.Name.EndsWith("." + DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH, StringComparison.Ordinal)).ToArray();
        if (paths.Length != 1 || paths[0].Value is not TextValue { Value: var path } || string.IsNullOrWhiteSpace(path)
            || finding.Id != DisplayRefreshRule.FINDING_ID_PREFIX + path) { return null; }
        var prefix = paths[0].Name[..^DisplayProbeContract.FIELD_MONITOR_DEVICE_PATH.Length];
        var rates = finding.Measured.Where(m => m.Name == prefix + DisplayProbeContract.FIELD_MAX_SAME_MODE_REFRESH_HZ).ToArray();
        return rates.Length == 1 && rates[0].Value is IntegerValue { Value: > 1 and <= int.MaxValue } hz
            ? new(path, (int)hz.Value) : null;
    }
}
