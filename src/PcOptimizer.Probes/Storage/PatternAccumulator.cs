/**
 * @file    : PatternAccumulator.cs
 * @author  : rudals252
 * @brief   : 이름 패턴 위치(예: 탐색기 thumbcache_*.db) 한 곳의 순회 중 가변 집계(열거 여부·실패 사유·패턴 일치 파일 크기·수·중복 가능)
 */

// 기본 패키지
using System.IO.Enumeration;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 이름 패턴 위치의 순회 중 집계입니다. 해당 폴더를 순회하는 볼륨 스레드 하나만 수정합니다.
/// </summary>
internal sealed class PatternAccumulator(FilePatternLocation location)
{
    /// <summary>위치 ID.</summary>
    public string Id { get; } = location.Id;

    /// <summary>정규화 경로.</summary>
    public string Path { get; } = PathScope.Normalize(location.Path);

    /// <summary>폴더를 열거했는지 여부.</summary>
    public bool Enumerated { get; private set; }

    /// <summary>패턴 일치 파일 논리 크기.</summary>
    public long Bytes { get; private set; }

    /// <summary>패턴 일치 파일 수.</summary>
    public long FileCount { get; private set; }

    /// <summary>폴더 열거 실패 사유.</summary>
    public ScanSkipReason? Failure { get; private set; }

    /// <summary>중복 가능 여부.</summary>
    public bool DuplicatesPossible { get; private set; }

    /// <summary>
    /// 폴더 열거를 시작했음을 기록한다.
    /// </summary>
    public void MarkEnumerated()
    {
        Enumerated = true;
    }

    /// <summary>
    /// 폴더 열거 실패를 기록한다(첫 사유만).
    /// </summary>
    public void Fail(ScanSkipReason reason)
    {
        Failure ??= reason;
    }

    /// <summary>
    /// 파일 이름이 패턴 중 하나와 맞는지 확인한다(대소문자 무시).
    /// </summary>
    public bool Matches(string fileName)
    {
        return location.Patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));
    }

    /// <summary>
    /// 일치 파일 하나를 더한다.
    /// </summary>
    public void Add(long bytes, bool unverified)
    {
        Bytes += bytes;
        FileCount++;
        DuplicatesPossible |= unverified;
    }

    /// <summary>
    /// 결과 값으로 바꾼다.
    /// </summary>
    public PatternTally ToTally()
    {
        return new PatternTally(Id, Enumerated, Bytes, FileCount, Failure, DuplicatesPossible);
    }
}
