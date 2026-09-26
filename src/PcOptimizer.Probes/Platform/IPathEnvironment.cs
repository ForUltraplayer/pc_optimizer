/**
 * @file    : IPathEnvironment.cs
 * @author  : rudals252
 * @brief   : 파일 스캔 경로 해석에 필요한 환경 조회 계약(환경 변수, 리디렉션을 반영한 Known Folder, 사용자 프로필, 경로 정규화, 작은 설정 파일 읽기). 테스트에서 가짜 값으로 바꿔 끼운다
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 경로 해석용 환경 조회 계약입니다. 쓰기 메서드는 없습니다.
/// </summary>
public interface IPathEnvironment
{
    /// <summary>
    /// 환경 변수 값을 읽습니다(없거나 공백이면 null).
    /// </summary>
    /// <param name="name">변수 이름.</param>
    /// <returns>값 또는 null.</returns>
    string? GetEnvironmentVariable(string name);

    /// <summary>
    /// Known Folder의 현재 위치(리디렉션 반영)를 읽습니다(알 수 없으면 null).
    /// </summary>
    /// <param name="folder">Known Folder 종류.</param>
    /// <returns>경로 또는 null.</returns>
    string? GetKnownFolderPath(ProtectedKnownFolder folder);

    /// <summary>
    /// 현재 사용자 프로필 경로를 읽습니다(알 수 없으면 null).
    /// </summary>
    /// <returns>경로 또는 null.</returns>
    string? GetUserProfilePath();

    /// <summary>
    /// 절대 경로를 비교용으로 정규화합니다(전체 경로, 존재하면 8.3 짧은 이름을 긴 이름으로, 끝 구분자 제거).
    /// </summary>
    /// <param name="path">드라이브 절대 경로.</param>
    /// <returns>정규화 경로.</returns>
    string NormalizePath(string path);

    /// <summary>
    /// 정해진 작은 설정 파일을 텍스트로 읽습니다(없거나 최대 크기를 넘거나 읽지 못하면 null).
    /// </summary>
    /// <param name="path">파일 경로.</param>
    /// <param name="maxBytes">허용 최대 크기(바이트).</param>
    /// <returns>파일 내용 또는 null.</returns>
    string? ReadSmallTextFile(string path, int maxBytes);
}
