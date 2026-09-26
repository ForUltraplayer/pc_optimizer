/**
 * @file    : NativeMethods.FileSystem.cs
 * @author  : rudals252
 * @brief   : 파일 스캔용 조회 전용 kernel32 P/Invoke(속성 읽기 전용 핸들 열기, FILE_ID_INFO 조회, 압축·희소 파일 할당 크기, 8.3 짧은 이름의 긴 이름 변환)
 */

// 기본 패키지
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 파일 시스템 조회 함수 선언입니다. 파일 내용을 읽거나 쓰는 접근 권한은 요청하지 않습니다.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>속성 읽기 권한(내용 읽기 아님).</summary>
    internal const uint FILE_READ_ATTRIBUTES = 0x0080;

    /// <summary>다른 프로세스의 읽기 공유 허용.</summary>
    internal const uint FILE_SHARE_READ = 0x0001;

    /// <summary>다른 프로세스의 쓰기 공유 허용.</summary>
    internal const uint FILE_SHARE_WRITE = 0x0002;

    /// <summary>다른 프로세스의 삭제 공유 허용.</summary>
    internal const uint FILE_SHARE_DELETE = 0x0004;

    /// <summary>기존 파일만 연다.</summary>
    internal const uint OPEN_EXISTING = 3;

    /// <summary>reparse point를 따라가지 않고 그 자체를 연다.</summary>
    internal const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    /// <summary>디렉터리도 열 수 있게 한다.</summary>
    internal const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    /// <summary>FILE_INFO_BY_HANDLE_CLASS.FileIdInfo.</summary>
    internal const int FILE_ID_INFO_CLASS = 18;

    /// <summary>GetCompressedFileSizeW 실패 표시 값.</summary>
    internal const uint INVALID_FILE_SIZE = 0xFFFFFFFF;

    /// <summary>
    /// 파일 핸들을 엽니다(이 앱에서는 FILE_READ_ATTRIBUTES + OPEN_EXISTING으로만 호출).
    /// </summary>
    [LibraryImport(KERNEL32, EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    /// <summary>
    /// 핸들의 FILE_ID_INFO(볼륨 일련번호 + 128비트 파일 ID)를 읽습니다.
    /// </summary>
    [LibraryImport(KERNEL32, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetFileInformationByHandleEx(SafeFileHandle file, int fileInformationClass, out FileIdInfo information, uint bufferSize);

    /// <summary>
    /// 압축·희소 파일의 실제 할당 크기(하위 32비트 반환, 상위 32비트는 out)를 읽습니다.
    /// </summary>
    [LibraryImport(KERNEL32, EntryPoint = "GetCompressedFileSizeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint GetCompressedFileSize(string fileName, out uint fileSizeHigh);

    /// <summary>
    /// 8.3 짧은 이름이 섞인 경로를 긴 이름으로 바꿉니다(반환값은 복사한 문자 수, 버퍼가 작으면 필요한 크기).
    /// </summary>
    [LibraryImport(KERNEL32, EntryPoint = "GetLongPathNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static unsafe partial uint GetLongPathName(string shortPath, char* longPath, uint bufferLength);

    /// <summary>
    /// FILE_ID_INFO 구조체(24바이트)입니다.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileIdInfo
    {
        /// <summary>볼륨 일련번호.</summary>
        public ulong VolumeSerialNumber;

        /// <summary>FILE_ID_128 하위 8바이트.</summary>
        public ulong FileIdLow;

        /// <summary>FILE_ID_128 상위 8바이트.</summary>
        public ulong FileIdHigh;
    }
}
