/**
 * @file    : FakePathEnvironment.cs
 * @author  : rudals252
 * @brief   : 고정 환경 변수·Known Folder·프로필 경로·작은 설정 파일 내용을 돌려주고 경로는 문자열 정규화만 하는 테스트용 경로 환경
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 경로 환경입니다. 실제 환경 변수·파일을 읽지 않습니다.
/// </summary>
internal sealed class FakePathEnvironment : IPathEnvironment
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ProtectedKnownFolder, string> _knownFolders = [];
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>사용자 프로필 경로.</summary>
    public string? Profile { get; set; }

    /// <summary>파일 읽기 호출 기록.</summary>
    public List<string> FileReads { get; } = [];

    /// <summary>
    /// 환경 변수를 등록한다.
    /// </summary>
    public FakePathEnvironment WithVariable(string name, string value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// Known Folder 위치를 등록한다.
    /// </summary>
    public FakePathEnvironment WithKnownFolder(ProtectedKnownFolder folder, string path)
    {
        _knownFolders[folder] = path;
        return this;
    }

    /// <summary>
    /// 작은 설정 파일 내용을 등록한다.
    /// </summary>
    public FakePathEnvironment WithFile(string path, string content)
    {
        _files[path] = content;
        return this;
    }

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name)
    {
        return _variables.TryGetValue(name, out var value) ? value : null;
    }

    /// <inheritdoc />
    public string? GetKnownFolderPath(ProtectedKnownFolder folder)
    {
        return _knownFolders.TryGetValue(folder, out var path) ? path : null;
    }

    /// <inheritdoc />
    public string? GetUserProfilePath()
    {
        return Profile;
    }

    /// <inheritdoc />
    public string NormalizePath(string path)
    {
        return PathScope.Normalize(path);
    }

    /// <inheritdoc />
    public string? ReadSmallTextFile(string path, int maxBytes)
    {
        FileReads.Add(path);
        return _files.TryGetValue(path, out var content) && content.Length <= maxBytes ? content : null;
    }
}
