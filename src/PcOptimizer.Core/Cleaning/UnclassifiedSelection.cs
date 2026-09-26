/**
 * @file    : UnclassifiedSelection.cs
 * @author  : rudals252
 * @brief   : 미분류 대용량 폴더 선정 결과(완전 관측 후보 상위 목록, 부분 관측으로 순위에서 뺀 폴더 목록, 각 개수)와 후보·확장자 분포 레코드
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 확장자 하나의 관측 크기입니다.
/// </summary>
/// <param name="Extension">확장자(소문자, 점 포함, 없으면 빈 문자열).</param>
/// <param name="Bytes">논리 크기 합.</param>
public sealed record ExtensionShare(string Extension, long Bytes);

/// <summary>
/// 미분류 대용량 폴더 후보 하나입니다. 크기·파일 수·확장자·수정 시각은 보고된 하위 후보와 제외 위치를 뺀 나머지 기준입니다.
/// </summary>
/// <param name="Path">폴더 전체 경로(측정값에만 쓰고 화면 문장에는 마지막 이름만 씀).</param>
/// <param name="Bytes">관측 논리 크기(하위 후보·제외 위치 제외).</param>
/// <param name="FileCount">관측 파일 수.</param>
/// <param name="TopExtensions">크기순 상위 확장자.</param>
/// <param name="NewestWriteUtc">관측 파일 중 가장 최근 수정 시각(UTC). 마지막 사용 시각이 아닙니다.</param>
/// <param name="DuplicatesPossible">하드링크 중복이 섞였을 수 있는지 여부(논리 크기 추정).</param>
/// <param name="ExcludesReportedDescendants">하위 폴더가 따로 후보(또는 부분 관측 폴더)로 보고되어 그 크기를 뺐는지 여부.</param>
public sealed record UnclassifiedFolderCandidate(
    string Path,
    long Bytes,
    long FileCount,
    IReadOnlyList<ExtensionShare> TopExtensions,
    DateTimeOffset? NewestWriteUtc,
    bool DuplicatesPossible,
    bool ExcludesReportedDescendants);

/// <summary>
/// 부분 관측이어서 순위에서 뺀 대용량 폴더입니다(스펙 §5.2 "부분 스캔 폴더는 별도 CannotVerify(PartialData)").
/// </summary>
/// <param name="Path">폴더 전체 경로.</param>
/// <param name="ObservedBytes">관측된 부분의 논리 크기(실제 크기는 더 클 수 있음).</param>
public sealed record PartialFolder(string Path, long ObservedBytes);

/// <summary>
/// 미분류 대용량 폴더 선정 결과입니다.
/// </summary>
/// <param name="Candidates">완전 관측 후보(크기 내림차순·경로 오름차순, 최대 상위 개수).</param>
/// <param name="QualifyingCount">잘라내기 전 후보 수.</param>
/// <param name="PartialFolders">부분 관측 대용량 폴더(같은 정렬, 최대 상위 개수).</param>
/// <param name="PartialCount">잘라내기 전 부분 관측 대용량 폴더 수.</param>
public sealed record UnclassifiedSelection(
    IReadOnlyList<UnclassifiedFolderCandidate> Candidates,
    int QualifyingCount,
    IReadOnlyList<PartialFolder> PartialFolders,
    int PartialCount);
