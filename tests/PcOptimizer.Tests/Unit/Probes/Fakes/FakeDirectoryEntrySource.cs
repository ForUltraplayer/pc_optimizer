/**
 * @file    : FakeDirectoryEntrySource.cs
 * @author  : rudals252
 * @brief   : 경로별 고정 항목 목록·접근 거부·열거 도중 실패·열거 시점 훅을 가진 테스트용 디렉터리 열거 공급자와 가짜 항목 생성 도우미
 */

// 기본 패키지
using System.Collections.Concurrent;
using System.IO;

// 사용자 패키지
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 디렉터리 열거 공급자입니다. 실제 파일 시스템에 접근하지 않으며, 큰 크기는 메타데이터 값일 뿐입니다.
/// </summary>
internal sealed class FakeDirectoryEntrySource : IDirectoryEntrySource
{
    /// <summary>파일 기본 수정 시각.</summary>
    public static readonly DateTimeOffset DEFAULT_WRITE = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Dictionary<string, List<DirectoryEntry>> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _denied = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _failAfter = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>열거를 시작한 디렉터리(순서대로).</summary>
    public ConcurrentQueue<string> Enumerated { get; } = new();

    /// <summary>열거 시작 시 호출되는 훅(동시성 테스트용, 호출 스레드를 막을 수 있음).</summary>
    public Action<string>? OnEnumerate { get; set; }

    /// <summary>항목 하나를 돌려주기 직전에 호출되는 훅(디렉터리 경로, 항목). 예산이 N번째 항목 뒤에 넘어가는 경우를 만든다.</summary>
    public Action<string, DirectoryEntry>? OnEntry { get; set; }

    /// <summary>루트 존재 확인 시 호출되는 훅(루트 경로).</summary>
    public Action<string>? OnProbeRoot { get; set; }

    /// <summary>
    /// 디렉터리와 그 직접 항목을 등록한다. 하위 디렉터리 항목은 따로 <see cref="Dir"/>로 등록해야 내용이 생긴다.
    /// </summary>
    public FakeDirectoryEntrySource Dir(string path, params DirectoryEntry[] entries)
    {
        _directories[path] = [.. entries];
        return this;
    }

    /// <summary>
    /// 디렉터리를 접근 거부로 만든다(루트이면 ProbeRoot도 AccessDenied).
    /// </summary>
    public FakeDirectoryEntrySource Deny(string path)
    {
        _denied.Add(path);
        return this;
    }

    /// <summary>
    /// 디렉터리 열거가 항목 n개를 돌려준 뒤 IOException으로 실패하게 한다.
    /// </summary>
    public FakeDirectoryEntrySource FailAfter(string path, int count)
    {
        _failAfter[path] = count;
        return this;
    }

    /// <summary>
    /// 가짜 파일 항목.
    /// </summary>
    public static DirectoryEntry File(string name, long bytes, FileAttributes attributes = FileAttributes.Normal, DateTimeOffset? written = null)
    {
        return new DirectoryEntry(name, attributes, bytes, written ?? DEFAULT_WRITE, IsDirectory: false);
    }

    /// <summary>
    /// 가짜 디렉터리 항목.
    /// </summary>
    public static DirectoryEntry Folder(string name, FileAttributes extra = 0)
    {
        return new DirectoryEntry(name, FileAttributes.Directory | extra, 0, DEFAULT_WRITE, IsDirectory: true);
    }

    /// <inheritdoc />
    public RootPresence ProbeRoot(string path)
    {
        OnProbeRoot?.Invoke(path);
        if (_denied.Contains(path))
        {
            return RootPresence.AccessDenied;
        }

        return _directories.ContainsKey(path) ? RootPresence.Directory : RootPresence.Missing;
    }

    /// <inheritdoc />
    public IEnumerable<DirectoryEntry> Enumerate(string directoryPath)
    {
        Enumerated.Enqueue(directoryPath);
        OnEnumerate?.Invoke(directoryPath);
        if (_denied.Contains(directoryPath))
        {
            throw new UnauthorizedAccessException("fake denied");
        }

        if (!_directories.TryGetValue(directoryPath, out var entries))
        {
            throw new DirectoryNotFoundException("fake missing");
        }

        return Iterate(directoryPath, entries);
    }

    /// <summary>
    /// 항목을 돌려주다가 지정한 개수 뒤에 실패한다.
    /// </summary>
    private IEnumerable<DirectoryEntry> Iterate(string directoryPath, List<DirectoryEntry> entries)
    {
        var limit = _failAfter.TryGetValue(directoryPath, out var count) ? count : int.MaxValue;
        for (var index = 0; index < entries.Count; index++)
        {
            if (index == limit)
            {
                throw new IOException("fake in use");
            }

            OnEntry?.Invoke(directoryPath, entries[index]);
            yield return entries[index];
        }

        if (limit == entries.Count)
        {
            throw new IOException("fake in use");
        }
    }
}
