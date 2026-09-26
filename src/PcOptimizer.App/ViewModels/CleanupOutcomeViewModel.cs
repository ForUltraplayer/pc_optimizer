using System.Globalization;
using PcOptimizer.App.Resources;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>외부 도구 종료와 크기 관측을 구분하며 디스크 확보량으로 표현하지 않습니다.</summary>
public sealed class CleanupOutcomeViewModel(string tool, CacheCleanupResult result)
{
    public string Tool { get; } = tool;
    public string BeforeText => FormatBytes(result.BeforeBytes);
    public string AfterText => FormatBytes(result.RemainingBytes);
    public string Title => !result.ToolSucceeded ? Strings.Cleanup_OutcomeFailed
        : result.BeforeBytes is null || result.RemainingBytes is null ? Strings.Cleanup_OutcomeUnverified
        : result.RemainingBytes < result.BeforeBytes ? Strings.Cleanup_OutcomeReduced
        : result.RemainingBytes == result.BeforeBytes ? Strings.Cleanup_OutcomeUnchanged : Strings.Cleanup_OutcomeIncreased;
    public string ChangeText => !result.ToolSucceeded ? Strings.Cleanup_Failed
        : result.BeforeBytes is not { } before || result.RemainingBytes is not { } after ? Strings.Cleanup_OutcomeUnknownHelp
        : before > after ? DisplayText.Format(Strings.Cleanup_Reduction, FormatBytes(before - after))
        : before == after ? Strings.Cleanup_NoReduction : Strings.Cleanup_IncreaseHelp;
    public static string FormatBytes(long? bytes) => bytes is not { } value ? Strings.Cleanup_Unverified
        : value < 1000 ? value.ToString("N0", CultureInfo.CurrentCulture) + " B"
        : value < 1_000_000 ? (value / 1000d).ToString("N1", CultureInfo.CurrentCulture) + " KB"
        : value < 1_000_000_000 ? (value / 1_000_000d).ToString("N1", CultureInfo.CurrentCulture) + " MB"
        : (value / 1_000_000_000d).ToString("N1", CultureInfo.CurrentCulture) + " GB";
}
