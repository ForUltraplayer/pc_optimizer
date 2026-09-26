/**
 * @file    : IFileScanService.cs
 * @author  : rudals252
 * @brief   : 검사 ID마다 파일 순회를 한 번만 실행하고 결과를 여러 프로브(저장소·앱 캐시·미분류)가 재사용하게 하는 공유 파일 스캔 서비스 계약
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 공유 파일 스캔 서비스 계약입니다(스펙 §4 "앱 캐시·저장소·미분류는 공유 스캔 결과를 재사용").
/// </summary>
public interface IFileScanService
{
    /// <summary>볼륨당 시간 예산.</summary>
    TimeSpan BudgetPerVolume { get; }

    /// <summary>
    /// 순회 예정 루트가 들어 있을 볼륨 수(파일 시스템 접근 없음, 타임아웃 계산용).
    /// </summary>
    /// <returns>볼륨 수(최소 1).</returns>
    int CountPlannedVolumes();

    /// <summary>
    /// 이 검사 ID의 스캔 결과를 돌려줍니다. 같은 검사 ID로 처음 부르면 순회를 시작하고, 다시 부르면 같은 결과(진행 중이면 같은 작업)를 돌려줍니다.
    /// 다른 검사 ID가 오면 이전 결과를 버립니다.
    /// </summary>
    /// <param name="context">검사 컨텍스트.</param>
    /// <param name="ct">이 호출의 취소 토큰(처음 호출의 토큰이 순회를 취소함).</param>
    /// <returns>스캔 결과.</returns>
    Task<DirectoryScanResult> GetOrScanAsync(ScanContext context, CancellationToken ct);
}
