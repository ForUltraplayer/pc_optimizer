/**
 * @file    : IWmiClient.cs
 * @author  : rudals252
 * @brief   : WMI 클래스 조회 계약(제공자 타임아웃 포함). 테스트에서 fixture 행으로 바꿔 끼운다
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// WMI 클래스 조회 계약입니다. 구현은 예외를 던지지 않고 실패를 <see cref="WmiQueryResult"/>로 돌려줍니다.
/// </summary>
public interface IWmiClient
{
    /// <summary>
    /// 클래스의 지정 속성을 조회합니다.
    /// </summary>
    /// <param name="className">WMI 클래스 이름(root\cimv2).</param>
    /// <param name="properties">읽을 속성 이름.</param>
    /// <param name="timeout">제공자 타임아웃.</param>
    /// <param name="ct">취소 토큰(행 사이에서 확인).</param>
    /// <returns>조회 결과.</returns>
    WmiQueryResult Query(string className, IReadOnlyList<string> properties, TimeSpan timeout, CancellationToken ct);
}
