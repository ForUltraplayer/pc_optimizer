/**
 * @file    : IProbe.cs
 * @author  : rudals252
 * @brief   : 수집기(프로브) 계약. ProbeResult만 반환하며 Finding·Verdict를 만들지 않는다
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 시스템 정보를 수집하는 프로브 계약입니다. 구현은 Probes 프로젝트에 두며,
/// 결과는 <see cref="ProbeResult"/>로만 반환하고 판정(Finding·Verdict)은 규칙 엔진이 맡습니다.
/// </summary>
/// <remarks>
/// 실행 조율기는 권한·온라인 정책에 맞지 않으면 프로브를 호출하지 않고, 타임아웃·예외·취소를 ProbeResult로 흡수합니다.
/// 구현은 취소 토큰을 존중해야 하며, 취소할 수 없는 호출은 제공자 타임아웃 등으로 스스로 끝나도록 해야 합니다.
/// 반환한 결과의 StartedAtUtc·Duration은 조율기가 측정한 값으로 덮어씁니다.
/// </remarks>
public interface IProbe
{
    /// <summary>프로브 ID(조율기 안에서 고유).</summary>
    string Id { get; }

    /// <summary>이 프로브가 속한 검사 분류.</summary>
    FindingCategory Category { get; }

    /// <summary>관리자 권한이 필요한지 여부. 일반 권한 검사에서는 호출하지 않고 ElevationRequired로 건너뜁니다.</summary>
    bool RequiresElevation { get; }

    /// <summary>네트워크가 필요한지 여부. 온라인 확인을 요청하지 않은 검사에서는 호출하지 않고 NotRequested로 건너뜁니다.</summary>
    bool RequiresNetwork { get; }

    /// <summary>기본 타임아웃. 설정의 프로브별 재정의가 있으면 그 값을 씁니다.</summary>
    TimeSpan DefaultTimeout { get; }

    /// <summary>
    /// 수집을 실행합니다.
    /// </summary>
    /// <param name="context">검사 컨텍스트.</param>
    /// <param name="ct">사용자 취소 또는 타임아웃 시 취소되는 토큰.</param>
    /// <returns>수집 결과.</returns>
    Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct);
}
