/**
 * @file    : AppConfigPathClassifier.cs
 * @author  : rudals252
 * @brief   : 앱 설정 리더 결과를 측정 상태로 분류하는 도우미(설정 경로가 UNC·보호 루트 안·연결되지 않은 드라이브면 순회하지 않음, 적용할 수 있는 경로만 관측 후보로 돌려줌)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// 앱 설정 분류 결과입니다.
/// </summary>
/// <param name="Reading">리더 결과.</param>
/// <param name="State">측정 상태(<c>AppCacheProbeContract.CONFIG_STATE_*</c>).</param>
/// <param name="AppliedPaths">관측 후보로 쓸 경로(순회해도 되는 것만).</param>
public sealed record ClassifiedAppConfig(AppConfigReading Reading, string State, IReadOnlyList<string> AppliedPaths);

/// <summary>
/// 앱 설정 경로 분류기입니다(스펙 §5.1 "설정된 경로가 UNC/보호/오프라인 위치이면 상태를 표시하고 순회하지 않는다").
/// </summary>
public static class AppConfigPathClassifier
{
    private const string UNC_PREFIX = @"\\";

    /// <summary>
    /// 리더 결과를 분류합니다. 여러 경로(Steam 라이브러리)는 하나라도 적용할 수 있으면 적용 상태이고, 아니면 첫 경로의 사유를 씁니다.
    /// </summary>
    /// <param name="reading">리더 결과.</param>
    /// <param name="isProtected">보호 루트 확인 함수.</param>
    /// <param name="source">드라이브 연결 확인용 열거 공급자.</param>
    /// <returns>분류 결과.</returns>
    public static ClassifiedAppConfig Classify(AppConfigReading reading, Func<string, bool> isProtected, IDirectoryEntrySource source)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(isProtected);
        ArgumentNullException.ThrowIfNull(source);

        var simple = reading.State switch
        {
            AppConfigReadState.NotConfigured => AppCacheProbeContract.CONFIG_STATE_NOT_CONFIGURED,
            AppConfigReadState.Invalid => AppCacheProbeContract.CONFIG_STATE_INVALID,
            AppConfigReadState.Unreadable => AppCacheProbeContract.CONFIG_STATE_UNREADABLE,
            AppConfigReadState.CannotVerify => AppCacheProbeContract.CONFIG_STATE_CANNOT_VERIFY,
            _ => null,
        };
        if (simple is not null)
        {
            return new ClassifiedAppConfig(reading, simple, []);
        }

        var states = reading.Paths.Select(path => (Path: path, State: ClassifyPath(path, isProtected, source))).ToList();
        var applied = states.Where(item => item.State == AppCacheProbeContract.CONFIG_STATE_APPLIED).Select(item => item.Path).ToList();
        var state = applied.Count > 0
            ? AppCacheProbeContract.CONFIG_STATE_APPLIED
            : states.Select(item => item.State).FirstOrDefault() ?? AppCacheProbeContract.CONFIG_STATE_INVALID;
        return new ClassifiedAppConfig(reading, state, applied.AsReadOnly());
    }

    /// <summary>
    /// 경로 하나를 분류한다.
    /// </summary>
    private static string ClassifyPath(string path, Func<string, bool> isProtected, IDirectoryEntrySource source)
    {
        if (path.StartsWith(UNC_PREFIX, StringComparison.Ordinal))
        {
            return AppCacheProbeContract.CONFIG_STATE_UNC;
        }

        if (!PathScope.IsDriveAbsolute(path) || PathScope.IsDriveRoot(path))
        {
            return AppCacheProbeContract.CONFIG_STATE_INVALID;
        }

        if (isProtected(path))
        {
            return AppCacheProbeContract.CONFIG_STATE_PROTECTED;
        }

        var volume = Path.GetPathRoot(path);
        return volume is null || source.ProbeRoot(volume) is RootPresence.Missing or RootPresence.Error
            ? AppCacheProbeContract.CONFIG_STATE_OFFLINE
            : AppCacheProbeContract.CONFIG_STATE_APPLIED;
    }
}
