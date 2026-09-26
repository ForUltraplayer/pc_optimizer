/**
 * @file    : CleanupOutcomeViewModel.cs
 * @author  : rudals252
 * @brief   : 실제 캐시 정리 실행의 전후 논리 크기와 확인 한계 표시
 */
using System.Globalization;
using PcOptimizer.App.Resources;
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>외부 도구 종료와 크기 관측을 구분하며 디스크 확보량으로 표현하지 않습니다.</summary>
/// <param name="tool">실행한 공식 도구 이름.</param>
/// <param name="result">도구 종료와 전후 관측 결과.</param>
public sealed class CleanupOutcomeViewModel(string tool, CacheCleanupResult result)
{
    /// <summary>실행한 공식 도구 이름.</summary>
    public string Tool { get; } = tool;
    /// <summary>실행 직전 관측한 논리 크기.</summary>
    public string BeforeText => FormatBytes(result.BeforeBytes);
    /// <summary>실행 후 논리 크기 또는 확인 불가 표시.</summary>
    public string AfterText => FormatBytes(result.RemainingBytes);
    /// <summary>도구 종료와 크기 확인을 구분하는 제목.</summary>
    public string Title => !result.ToolSucceeded ? Strings.Cleanup_OutcomeFailed
        : result.BeforeBytes is null || result.RemainingBytes is null ? Strings.Cleanup_OutcomeUnverified
        : result.RemainingBytes < result.BeforeBytes ? Strings.Cleanup_OutcomeReduced
        : result.RemainingBytes == result.BeforeBytes ? Strings.Cleanup_OutcomeUnchanged : Strings.Cleanup_OutcomeIncreased;
    /// <summary>논리 크기 차이와 관측 한계 설명.</summary>
    public string ChangeText => !result.ToolSucceeded ? Strings.Cleanup_Failed
        : result.BeforeBytes is not { } before || result.RemainingBytes is not { } after ? Strings.Cleanup_OutcomeUnknownHelp
        : before > after ? DisplayText.Format(Strings.Cleanup_Reduction, FormatBytes(before - after))
        : before == after ? Strings.Cleanup_NoReduction : Strings.Cleanup_IncreaseHelp;
    /// <summary>논리 바이트 수를 표시 단위로 바꿉니다.</summary>
    public static string FormatBytes(long? bytes) => bytes is not { } value ? Strings.Cleanup_Unverified
        : value < 1000 ? value.ToString("N0", CultureInfo.CurrentCulture) + " B"
        : value < 1_000_000 ? (value / 1000d).ToString("N1", CultureInfo.CurrentCulture) + " KB"
        : value < 1_000_000_000 ? (value / 1_000_000d).ToString("N1", CultureInfo.CurrentCulture) + " MB"
        : (value / 1_000_000_000d).ToString("N1", CultureInfo.CurrentCulture) + " GB";
}
