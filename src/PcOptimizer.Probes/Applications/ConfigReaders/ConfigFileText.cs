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
        // 디렉터리 공급자는 설정 파일 가상 공급자와 별개일 수 있다. 알려진 위험 경계는
        // 본문 요청 전에 차단하며, 실제 파일 읽기 공급자도 모든 경로 속성을 재검증한다.
        var parents = new Stack<string>();
        for (var parent = Path.GetDirectoryName(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
        {
            parents.Push(parent);
        }

        foreach (var parent in parents)
        {
            if (source.ProbeRoot(parent) is not (RootPresence.Directory or RootPresence.Missing))
            {
                return new ConfigFileContent(null, true);
            }
        }

        var presence = source.ProbeRoot(path);
        if (presence is not (RootPresence.NotDirectory or RootPresence.Missing))
        {
            return new ConfigFileContent(null, true);
        }

        var text = environment.ReadSmallTextFile(path, maxBytes);
        return text is not null
            ? new ConfigFileContent(text, false)
            : new ConfigFileContent(null, source.ProbeRoot(path) != RootPresence.Missing);
    }
}
