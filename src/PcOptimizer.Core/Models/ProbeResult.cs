/**
 * @file    : ProbeResult.cs
 * @author  : rudals252
 * @brief   : 프로브 한 번의 수집 결과(상태·측정값·Issue·UTC 시작 시각·소요 시간·사용자 컨텍스트) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 프로브 한 번의 수집 결과입니다. 프로브는 Finding·Verdict를 만들지 않고 이 결과만 반환합니다.
/// 측정값·Issue 목록은 생성(및 with 식) 시 복사해 읽기 전용 보기로 보관하므로 생성 후 바뀌지 않습니다.
/// </summary>
/// <param name="ProbeId">결과를 만든 프로브 ID.</param>
/// <param name="Status">수집 상태.</param>
/// <param name="Measurements">수집한 측정값(null 불가).</param>
/// <param name="Issues">수집 중 발생한 문제(null 불가).</param>
/// <param name="StartedAtUtc">수집 시작 시각(UTC). 실행 조율기가 자신의 시계 값으로 덮어쓴다.</param>
/// <param name="Duration">소요 시간. 실행 조율기가 자신이 잰 값으로 덮어쓴다.</param>
/// <param name="UserContext">수집을 실행한 사용자 컨텍스트.</param>
public sealed record ProbeResult(
    string ProbeId,
    ProbeStatus Status,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyList<Issue> Issues,
    DateTimeOffset StartedAtUtc,
    TimeSpan Duration,
    UserContext UserContext)
{
    private readonly IReadOnlyList<Measurement> _measurements = ReadOnlyListCopy.Of(Measurements, nameof(Measurements));
    private readonly IReadOnlyList<Issue> _issues = ReadOnlyListCopy.Of(Issues, nameof(Issues));

    /// <summary>수집한 측정값(읽기 전용 복사본).</summary>
    public IReadOnlyList<Measurement> Measurements
    {
        get => _measurements;
        init => _measurements = ReadOnlyListCopy.Of(value, nameof(Measurements));
    }

    /// <summary>수집 중 발생한 문제(읽기 전용 복사본).</summary>
    public IReadOnlyList<Issue> Issues
    {
        get => _issues;
        init => _issues = ReadOnlyListCopy.Of(value, nameof(Issues));
    }
}
