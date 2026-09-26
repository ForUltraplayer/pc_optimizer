/**
 * @file    : IFileIdentityReader.cs
 * @author  : rudals252
 * @brief   : 파일 ID(볼륨 일련번호 + 128비트 파일 ID)와 압축·희소 파일 할당 크기를 읽는 조회 전용 계약과 파일 ID 값. 테스트에서 가짜 ID로 바꿔 끼운다
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 볼륨 일련번호와 128비트 파일 ID로 이루어진 파일 식별자입니다. 같은 값이면 같은 파일(하드링크)입니다.
/// </summary>
/// <param name="VolumeSerialNumber">볼륨 일련번호.</param>
/// <param name="FileIdLow">파일 ID 하위 8바이트.</param>
/// <param name="FileIdHigh">파일 ID 상위 8바이트.</param>
public readonly record struct FileIdentity(ulong VolumeSerialNumber, ulong FileIdLow, ulong FileIdHigh);

/// <summary>
/// 파일 식별자·할당 크기 조회 계약입니다. 파일 내용을 읽지 않으며 실패는 null로 알립니다.
/// </summary>
public interface IFileIdentityReader
{
    /// <summary>
    /// 파일 ID를 읽습니다(속성 읽기 전용 핸들, reparse point를 따라가지 않음).
    /// </summary>
    /// <param name="path">파일 경로.</param>
    /// <returns>파일 ID 또는 null(접근 거부·사용 중·지원 안 함).</returns>
    FileIdentity? TryGetIdentity(string path);

    /// <summary>
    /// 압축·희소 파일의 실제 할당 크기를 읽습니다.
    /// </summary>
    /// <param name="path">파일 경로.</param>
    /// <returns>할당 크기(바이트) 또는 null.</returns>
    long? TryGetAllocatedSize(string path);
}
