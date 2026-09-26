/**
 * @file    : TestDirectory.cs
 * @author  : rudals252
 * @brief   : 테스트 전용 임시 루트(%TEMP%\pcoptimizer-p4-tests\<GUID>)를 만들고, 정리할 때 그 루트 안인지 검증한 뒤 정션을 먼저 링크만 제거하고 루트만 지우는 테스트 도우미
 */

// 기본 패키지
using System.IO;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 테스트가 만든 임시 폴더입니다. 정리는 검증된 테스트 루트 안에서만 합니다.
/// </summary>
internal sealed class TestDirectory : IDisposable
{
    /// <summary>모든 P4 테스트 폴더의 상위 폴더 이름.</summary>
    public const string BASE_NAME = "pcoptimizer-p4-tests";

    private static readonly string BASE = Path.Combine(Path.GetTempPath(), BASE_NAME);

    /// <summary>
    /// 새 테스트 폴더를 만든다.
    /// </summary>
    public TestDirectory()
    {
        Root = Path.GetFullPath(Path.Combine(BASE, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
    }

    /// <summary>테스트 루트 전체 경로.</summary>
    public string Root { get; }

    /// <summary>
    /// 루트 아래 상대 경로의 전체 경로.
    /// </summary>
    public string PathOf(string relative)
    {
        return Path.Combine(Root, relative);
    }

    /// <summary>
    /// 지정한 크기의 파일을 만든다(상위 폴더 포함).
    /// </summary>
    public string File(string relative, int bytes, FileAttributes attributes = FileAttributes.Normal)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
        if (attributes != FileAttributes.Normal)
        {
            System.IO.File.SetAttributes(path, attributes);
        }

        return path;
    }

    /// <summary>
    /// 테스트 루트를 지운다. 루트가 테스트 상위 폴더 안인지 확인하고, 정션·심볼릭 링크는 대상이 아닌 링크만 먼저 지운다.
    /// </summary>
    public void Dispose()
    {
        var baseWithSeparator = Path.GetFullPath(BASE) + Path.DirectorySeparatorChar;
        if (!Root.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
        {
            System.IO.File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var link in Directory.EnumerateDirectories(Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .Where(path => new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint))
            .ToList())
        {
            Directory.Delete(link);
        }

        Directory.Delete(Root, recursive: true);
    }
}
