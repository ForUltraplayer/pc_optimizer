/**
 * @file    : AdobeProcessGuard.cs
 * @author  : rudals252
 * @brief   : Adobe 미디어 캐시 작성 프로세스의 실행 여부를 조회하고 조회 실패는 거절
 */
using System.ComponentModel;
using System.Diagnostics;

namespace PcOptimizer.Probes.Actions.Files;

internal static class AdobeProcessGuard
{
    private static readonly string[] Prefixes = ["Adobe Premiere", "Adobe Media Encoder", "Adobe Audition", "Adobe Encore", "Adobe Soundbooth"];
    private static readonly HashSet<string> Names = new(["AfterFX", "aerender", "aerendercore", "Audition", "Encore", "Soundbooth", "dynamiclinkmanager", "dynamiclinkmediaserver", "PProHeadless", "PremiereElements", "ElementsAutoAnalyzer"], StringComparer.OrdinalIgnoreCase);
    // 경로·명령줄·창 제목은 수집하지 않는다. 다른 세션의 관련 프로세스도 보수적으로 거절한다.
    internal static string? Check() => Check(ReadNames);
    internal static string? Check(Func<IReadOnlyList<string>> readNames)
    {
        try
        {
            var names = readNames();
            if (names.Count == 0 || names.Any(string.IsNullOrWhiteSpace)) { return "AdobeStateUnavailable"; }
            return names.Any(IsWriter) ? "AdobeAppRunning" : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        { return "AdobeStateUnavailable"; }
    }
    internal static bool IsWriter(string name) => Prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) || Names.Contains(name);
    private static IReadOnlyList<string> ReadNames()
    {
        var processes = Process.GetProcesses();
        try { return processes.Select(p => p.ProcessName).ToArray(); }
        finally { foreach (var process in processes) { process.Dispose(); } }
    }
}
