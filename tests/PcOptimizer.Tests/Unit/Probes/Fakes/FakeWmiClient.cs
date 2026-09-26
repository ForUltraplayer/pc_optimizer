/**
 * @file    : FakeWmiClient.cs
 * @author  : rudals252
 * @brief   : 클래스 이름별 고정 결과(비식별 fixture 행 또는 실패 상태)를 돌려주고 조회한 네임스페이스를 기록하는 테스트용 WMI 클라이언트
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 클래스 이름별로 미리 정한 결과를 돌려주는 가짜 WMI 클라이언트입니다. 등록하지 않은 클래스는 ClassUnavailable입니다.
/// </summary>
internal sealed class FakeWmiClient : IWmiClient
{
    private readonly Dictionary<string, WmiQueryResult> _results = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 클래스의 성공 결과 행을 등록한다.
    /// </summary>
    public FakeWmiClient WithRows(string className, params Dictionary<string, object?>[] rows)
    {
        _results[className] = WmiQueryResult.Succeeded([.. rows]);
        return this;
    }

    /// <summary>
    /// 클래스의 실패 결과를 등록한다.
    /// </summary>
    public FakeWmiClient WithFailure(string className, WmiQueryStatus status)
    {
        _results[className] = WmiQueryResult.Failed(status, status.ToString());
        return this;
    }

    /// <summary>조회한 (네임스페이스, 클래스) 기록(호출 순서).</summary>
    public List<(string Namespace, string ClassName)> Queries { get; } = [];

    /// <inheritdoc />
    public WmiQueryResult Query(string className, IReadOnlyList<string> properties, TimeSpan timeout, CancellationToken ct)
    {
        return Query(WmiNamespaces.CIMV2, className, properties, timeout, ct);
    }

    /// <inheritdoc />
    public WmiQueryResult Query(string wmiNamespace, string className, IReadOnlyList<string> properties, TimeSpan timeout, CancellationToken ct)
    {
        Queries.Add((wmiNamespace, className));
        return _results.TryGetValue(className, out var result)
            ? result
            : WmiQueryResult.Failed(WmiQueryStatus.ClassUnavailable, nameof(WmiQueryStatus.ClassUnavailable));
    }
}
