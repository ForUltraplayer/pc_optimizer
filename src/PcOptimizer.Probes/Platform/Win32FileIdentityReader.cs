/**
 * @file    : Win32FileIdentityReader.cs
 * @author  : rudals252
 * @brief   : CreateFileW(FILE_READ_ATTRIBUTES, OPEN_REPARSE_POINT)와 GetFileInformationByHandleEx(FileIdInfo)로 파일 ID를, GetCompressedFileSizeW로 할당 크기를 읽는 조회 구현(내용 읽기·쓰기 권한 없음)
 */

// 기본 패키지
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 파일 ID·할당 크기 조회 구현입니다. 핸들은 속성 읽기 권한만 요청하고 다른 프로세스의 읽기·쓰기·삭제를 막지 않습니다.
/// MAX_PATH 이상 경로는 <c>\\?\</c> 접두사를 붙여 엽니다.
/// </summary>
public sealed class Win32FileIdentityReader : IFileIdentityReader
{
    private const int MAX_PATH = 260;
    private const string LONG_PATH_PREFIX = @"\\?\";
    private const int HIGH_PART_SHIFT = 32;

    /// <summary>공유 인스턴스.</summary>
    public static Win32FileIdentityReader Instance { get; } = new();

    /// <inheritdoc />
    public FileIdentity? TryGetIdentity(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var handle = NativeMethods.CreateFile(
            ToWin32Path(path),
            NativeMethods.FILE_READ_ATTRIBUTES,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_BACKUP_SEMANTICS | NativeMethods.FILE_FLAG_OPEN_REPARSE_POINT,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        if (!NativeMethods.GetFileInformationByHandleEx(
            handle, NativeMethods.FILE_ID_INFO_CLASS, out var info, (uint)Unsafe.SizeOf<NativeMethods.FileIdInfo>()))
        {
            return null;
        }

        return new FileIdentity(info.VolumeSerialNumber, info.FileIdLow, info.FileIdHigh);
    }

    /// <inheritdoc />
    public long? TryGetAllocatedSize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Marshal.SetLastPInvokeError(0);
        var low = NativeMethods.GetCompressedFileSize(ToWin32Path(path), out var high);
        if (low == NativeMethods.INVALID_FILE_SIZE && Marshal.GetLastPInvokeError() != 0)
        {
            return null;
        }

        return (long)(((ulong)high << HIGH_PART_SHIFT) | low);
    }

    /// <summary>
    /// 긴 경로에 확장 길이 접두사를 붙인다.
    /// </summary>
    private static string ToWin32Path(string path)
    {
        return path.Length >= MAX_PATH && !path.StartsWith(LONG_PATH_PREFIX, StringComparison.Ordinal)
            ? LONG_PATH_PREFIX + path
            : path;
    }
}
