/**
 * @file    : SystemPathEnvironment.cs
 * @author  : rudals252
 * @brief   : 실제 환경 변수·Known Folder(SHGetKnownFolderPath 기반 Environment.GetFolderPath, 리디렉션 반영)·경로 정규화(GetFullPath + GetLongPathNameW)·크기 제한 설정 파일 읽기 구현
 */

// 기본 패키지
using System.Security;
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 현재 프로세스 사용자의 환경으로 경로를 해석합니다. 쓰기 동작은 없습니다.
/// </summary>
public sealed class SystemPathEnvironment : IPathEnvironment
{
    private const int LONG_PATH_BUFFER_LENGTH = 32768;

    /// <summary>공유 인스턴스.</summary>
    public static SystemPathEnvironment Instance { get; } = new();

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <inheritdoc />
    public string? GetKnownFolderPath(ProtectedKnownFolder folder)
    {
        var special = folder switch
        {
            ProtectedKnownFolder.Documents => Environment.SpecialFolder.MyDocuments,
            ProtectedKnownFolder.Pictures => Environment.SpecialFolder.MyPictures,
            ProtectedKnownFolder.Desktop => Environment.SpecialFolder.DesktopDirectory,
            ProtectedKnownFolder.Videos => Environment.SpecialFolder.MyVideos,
            ProtectedKnownFolder.Music => Environment.SpecialFolder.MyMusic,
            _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, null),
        };

        // Windows에서 GetFolderPath는 SHGetKnownFolderPath를 호출하므로 폴더 리디렉션(OneDrive 백업 포함)이 반영된다.
        var path = Environment.GetFolderPath(special, Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <inheritdoc />
    public string? GetUserProfilePath()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <inheritdoc />
    public string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        return PathScope.Normalize(ToLongPath(full));
    }

    /// <inheritdoc />
    public string? ReadSmallTextFile(string path, int maxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        try
        {
            var fullPath = Path.GetFullPath(path);
            var parents = new Stack<string>();
            for (var parent = Path.GetDirectoryName(fullPath); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
            {
                parents.Push(parent);
            }

            foreach (var parent in parents)
            {
                if (FileSystemDirectoryEntrySource.Instance.ProbeRoot(parent) != RootPresence.Directory)
                {
                    return null;
                }
            }

            if (FileSystemDirectoryEntrySource.Instance.ProbeRoot(fullPath) != RootPresence.NotDirectory)
            {
                return null;
            }

            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > maxBytes)
            {
                return null;
            }

            // 길이 검사 이후 파일이 증가해도 본문을 무제한 읽지 않는다.
            var bytes = new byte[checked(maxBytes + 1)];
            var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            if (count > maxBytes)
            {
                return null;
            }

            using var reader = new StreamReader(new MemoryStream(bytes, 0, count), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// 존재하는 경로의 8.3 짧은 이름을 긴 이름으로 바꾼다. 실패하면(경로 없음 등) 원래 값을 돌려준다.
    /// </summary>
    private static unsafe string ToLongPath(string path)
    {
        var buffer = new char[LONG_PATH_BUFFER_LENGTH];
        fixed (char* pointer = buffer)
        {
            var length = NativeMethods.GetLongPathName(path, pointer, (uint)buffer.Length);
            return length == 0 || length >= buffer.Length ? path : new string(buffer, 0, (int)length);
        }
    }
}
