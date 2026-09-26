/**
 * @file    : ConfigFileText.cs
 * @author  : rudals252
 * @brief   : 앱 설정 리더가 정해진 작은 설정 파일 하나를 크기 제한 안에서 읽고, 파일이 있는데 읽지 못한 경우를 없음과 구분하는 공용 도우미
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// 설정 파일 읽기 결과입니다.
/// </summary>
/// <param name="Text">파일 내용(없거나 읽지 못하면 null). 호출한 리더가 필요한 키만 뽑고 버립니다.</param>
/// <param name="Unreadable">파일은 있는데 읽지 못했는지(크기 초과·접근 거부 등) 여부.</param>
internal readonly record struct ConfigFileContent(string? Text, bool Unreadable);

/// <summary>
/// 설정 파일 읽기 도우미입니다.
/// </summary>
internal static class ConfigFileText
{
    /// <summary>
    /// 설정 파일을 읽습니다.
    /// </summary>
    /// <param name="environment">경로 환경.</param>
    /// <param name="source">존재 확인용 열거 공급자.</param>
    /// <param name="path">파일 경로.</param>
    /// <param name="maxBytes">최대 크기.</param>
    /// <returns>읽기 결과.</returns>
    public static ConfigFileContent Read(IPathEnvironment environment, IDirectoryEntrySource source, string path, int maxBytes)
    {
        var text = environment.ReadSmallTextFile(path, maxBytes);
        return text is not null
            ? new ConfigFileContent(text, false)
            : new ConfigFileContent(null, source.ProbeRoot(path) == RootPresence.NotDirectory);
    }
}
