/**
 * @file    : DirectoryAggregate.cs
 * @author  : rudals252
 * @brief   : 디렉터리 하나의 직접 포함 파일(하위 폴더 제외) 관측 집계(논리 크기·파일 수·건너뛴 항목 수·확장자별 크기·최근 수정 시각·중복 가능 여부) 레코드
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 디렉터리 하나에 직접 들어 있는 파일의 관측 집계입니다(하위 폴더 내용은 포함하지 않음). 파일 본문은 읽지 않은 메타데이터 값입니다.
/// </summary>
/// <param name="Path">디렉터리 전체 경로.</param>
/// <param name="Bytes">직접 포함 파일의 논리 크기 합(하드링크로 이미 센 파일 제외).</param>
/// <param name="FileCount">직접 포함 파일 수.</param>
/// <param name="SkippedEntryCount">
/// 이 디렉터리에서 건너뛴 항목 수(보호 제외·접근 거부·사용 중·reparse·placeholder·시간 초과). 0보다 크면 이 디렉터리는 완전 관측이 아닙니다.
/// </param>
/// <param name="ExtensionBytes">확장자(소문자, 점 포함, 없으면 빈 문자열)별 논리 크기.</param>
/// <param name="NewestWriteUtc">직접 포함 파일 중 가장 최근 수정 시각(UTC, 파일이 없으면 null). 마지막 사용 시각이 아닙니다.</param>
/// <param name="DuplicatesPossible">파일 ID로 중복을 확인하지 못한 파일이 있어 하드링크 중복이 섞였을 수 있는지 여부.</param>
public sealed record DirectoryAggregate(
    string Path,
    long Bytes,
    long FileCount,
    long SkippedEntryCount,
    IReadOnlyDictionary<string, long> ExtensionBytes,
    DateTimeOffset? NewestWriteUtc,
    bool DuplicatesPossible);
