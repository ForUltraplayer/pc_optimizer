/**
 * @file    : ScanReport.cs
 * @author  : rudals252
 * @brief   : 화면·내보내기용 검사 리포트 모델(스키마 버전, 검사 ID·시각, 결과, 버전, 사용자 컨텍스트, 프로브 요약, Finding)
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 한 번의 리포트입니다. 화면 모델의 원천이며 P2에서 JSON으로 내보냅니다(익명화는 내보내기 단계에서 수행).
/// </summary>
public sealed record ScanReport
{
    /// <summary>리포트 스키마 버전.</summary>
    public const string SCHEMA_VERSION = "1.0";

    /// <summary>리포트 스키마 버전.</summary>
    public string SchemaVersion { get; init; } = SCHEMA_VERSION;

    /// <summary>검사 ID.</summary>
    public required Guid ScanId { get; init; }

    /// <summary>검사 시작 시각(UTC).</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>검사 완료 시각(UTC).</summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>검사 단위 결과.</summary>
    public required ScanOutcome Outcome { get; init; }

    /// <summary>앱 버전.</summary>
    public required string AppVersion { get; init; }

    /// <summary>규칙 데이터 버전.</summary>
    public required string RulesVersion { get; init; }

    /// <summary>검사를 실행한 사용자 컨텍스트.</summary>
    public required UserContext UserContext { get; init; }

    /// <summary>프로브별 실행 요약.</summary>
    public required IReadOnlyList<ProbeSummary> ProbeSummaries { get; init; }

    /// <summary>규칙 판정과 수집 실패 변환으로 만든 Finding 목록.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }
}
