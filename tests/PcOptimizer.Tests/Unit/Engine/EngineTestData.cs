/**
 * @file    : EngineTestData.cs
 * @author  : rudals252
 * @brief   : 엔진 단위 테스트에서 공통으로 쓰는 ProbeResult·스냅샷 생성 도우미
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.Engine;

/// <summary>
/// 엔진 단위 테스트용 데이터 생성 도우미입니다.
/// </summary>
internal static class EngineTestData
{
    /// <summary>테스트용 사용자 컨텍스트.</summary>
    public static readonly UserContext USER = new("anon-user", IsElevated: false);

    /// <summary>
    /// 지정한 상태·측정값·Issue로 ProbeResult를 만든다.
    /// </summary>
    public static ProbeResult CreateResult(
        string probeId,
        ProbeStatus status,
        IReadOnlyList<Measurement>? measurements = null,
        IReadOnlyList<Issue>? issues = null)
    {
        return new ProbeResult(
            probeId,
            status,
            measurements ?? [],
            issues ?? [],
            FakeProbe.OBSERVED_AT,
            TimeSpan.Zero,
            USER);
    }

    /// <summary>
    /// 결과 목록으로 스냅샷을 만든다.
    /// </summary>
    public static ScanSnapshot CreateSnapshot(params ProbeResult[] results)
    {
        return new ScanSnapshot(Guid.NewGuid(), results);
    }
}
