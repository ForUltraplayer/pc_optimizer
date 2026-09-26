/**
 * @file    : ScanSnapshot.cs
 * @author  : rudals252
 * @brief   : 규칙 입력용 불변 스냅샷(검사 ID와 프로브별 결과, 조회 도우미)
 */

// 기본 패키지
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PcOptimizer.Core.Models;

/// <summary>
/// 한 검사에서 수집한 프로브 결과의 불변 스냅샷입니다. 규칙은 이 스냅샷만 입력으로 받습니다.
/// 측정값뿐 아니라 프로브 상태·Issues를 담아 미실행·실패·빈 성공을 구분할 수 있게 합니다.
/// </summary>
public sealed class ScanSnapshot
{
    private readonly FrozenDictionary<string, ProbeResult> _resultsById;

    /// <summary>
    /// 프로브 결과 목록으로 스냅샷을 만듭니다. 목록은 복사되므로 이후 원본을 바꿔도 영향이 없습니다.
    /// </summary>
    /// <param name="scanId">검사 ID.</param>
    /// <param name="probeResults">프로브 결과 목록(ProbeId 중복 불가).</param>
    /// <exception cref="ArgumentException">ProbeId가 중복된 경우.</exception>
    public ScanSnapshot(Guid scanId, IEnumerable<ProbeResult> probeResults)
    {
        ArgumentNullException.ThrowIfNull(probeResults);

        ProbeResult[] results = [.. probeResults];
        var byId = new Dictionary<string, ProbeResult>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            if (!byId.TryAdd(result.ProbeId, result))
            {
                throw new ArgumentException($"ProbeId '{result.ProbeId}'가 중복되었습니다.", nameof(probeResults));
            }
        }

        ScanId = scanId;
        ProbeResults = results;
        _resultsById = byId.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>검사 ID.</summary>
    public Guid ScanId { get; }

    /// <summary>프로브 결과(등록 순서).</summary>
    public IReadOnlyList<ProbeResult> ProbeResults { get; }

    /// <summary>
    /// 프로브 ID로 결과를 찾습니다.
    /// </summary>
    /// <param name="probeId">프로브 ID.</param>
    /// <param name="result">찾은 결과.</param>
    /// <returns>결과가 있으면 true.</returns>
    public bool TryGetProbe(string probeId, [MaybeNullWhen(false)] out ProbeResult result)
    {
        return _resultsById.TryGetValue(probeId, out result);
    }

    /// <summary>
    /// 프로브 ID와 측정 이름으로 측정값을 찾습니다. 없으면 null입니다(null은 "값 없음"이며 0·false와 다릅니다).
    /// </summary>
    /// <param name="probeId">프로브 ID.</param>
    /// <param name="name">측정 항목 이름.</param>
    /// <returns>찾은 측정값 또는 null.</returns>
    public Measurement? GetMeasurement(string probeId, string name)
    {
        if (!_resultsById.TryGetValue(probeId, out var result))
        {
            return null;
        }

        return result.Measurements.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));
    }
}
