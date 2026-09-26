/**
 * @file    : ScanOptions.cs
 * @author  : rudals252
 * @brief   : 검사 실행 설정(최대 병렬도, 프로브별 타임아웃 재정의)과 기본 타임아웃 상수
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 실행 설정입니다. 환경별로 달라질 수 있는 값이며 App이 설정 파일에서 읽어 채웁니다.
/// </summary>
public sealed record ScanOptions
{
    /// <summary>기본 최대 동시 실행 프로브 수.</summary>
    public const int DEFAULT_MAX_PARALLELISM = 4;

    /// <summary>로컬 정보 조회 기본 타임아웃.</summary>
    public static readonly TimeSpan DEFAULT_LOCAL_TIMEOUT = TimeSpan.FromSeconds(15);

    /// <summary>네트워크 요청(NVIDIA 등) 기본 타임아웃.</summary>
    public static readonly TimeSpan DEFAULT_NETWORK_TIMEOUT = TimeSpan.FromSeconds(30);

    /// <summary>Windows Update Agent 검색 기본 타임아웃.</summary>
    public static readonly TimeSpan DEFAULT_WUA_SEARCH_TIMEOUT = TimeSpan.FromSeconds(120);

    /// <summary>볼륨당 파일 순회 기본 타임아웃.</summary>
    public static readonly TimeSpan DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME = TimeSpan.FromSeconds(120);

    /// <summary>
    /// 최대 동시 실행 프로브 수(1 이상).
    /// </summary>
    public int MaxParallelism { get; init; } = DEFAULT_MAX_PARALLELISM;

    /// <summary>
    /// 프로브 ID별 타임아웃 재정의. 없으면 프로브의 DefaultTimeout을 쓴다.
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> ProbeTimeoutOverrides { get; init; } =
        new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
}
