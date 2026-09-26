/**
 * @file    : FakeFileIdentityReader.cs
 * @author  : rudals252
 * @brief   : 경로별 고정 파일 ID·할당 크기를 돌려주고 조회 호출을 기록하는 테스트용 파일 식별자 읽기
 */

// 기본 패키지
using System.Collections.Concurrent;

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 파일 식별자 읽기입니다. 등록하지 않은 경로는 null(조회 실패)입니다.
/// </summary>
internal sealed class FakeFileIdentityReader : IFileIdentityReader
{
    private readonly Dictionary<string, FileIdentity> _identities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _allocated = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ID 조회 호출 경로.</summary>
    public ConcurrentQueue<string> IdentityCalls { get; } = new();

    /// <summary>
    /// 파일 ID를 등록한다(같은 번호면 같은 파일 = 하드링크).
    /// </summary>
    public FakeFileIdentityReader WithIdentity(string path, ulong fileId, ulong volume = 1)
    {
        _identities[path] = new FileIdentity(volume, fileId, 0);
        return this;
    }

    /// <summary>
    /// 할당 크기를 등록한다.
    /// </summary>
    public FakeFileIdentityReader WithAllocated(string path, long bytes)
    {
        _allocated[path] = bytes;
        return this;
    }

    /// <inheritdoc />
    public FileIdentity? TryGetIdentity(string path)
    {
        IdentityCalls.Enqueue(path);
        return _identities.TryGetValue(path, out var identity) ? identity : null;
    }

    /// <inheritdoc />
    public long? TryGetAllocatedSize(string path)
    {
        return _allocated.TryGetValue(path, out var bytes) ? bytes : null;
    }
}
