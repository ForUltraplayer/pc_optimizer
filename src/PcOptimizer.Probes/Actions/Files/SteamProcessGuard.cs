/**
 * @file    : SteamProcessGuard.cs
 * @author  : rudals252
 * @brief   : Steam 작성자와 등록 라이브러리의 대화형 프로세스를 조회하고 불완전 관측은 거절
 */
using System.Diagnostics;
using System.Management;
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Probes.Actions.Files;

internal static class SteamProcessGuard
{
    private static readonly HashSet<string> Writers = new(["steam.exe", "steamcmd.exe", "steamservice.exe", "steamwebhelper.exe", "GameOverlayUI.exe", "GameOverlayUI64.exe",
        "steamerrorreporter.exe", "steamerrorreporter64.exe", "fossilize_replay.exe", "fossilize_replay64.exe"], StringComparer.OrdinalIgnoreCase);
    internal static string? Check(IReadOnlyList<string> libraries)
    {
        try
        {
            var started = Stopwatch.GetTimestamp();
            var scope = new ManagementScope(@"\\.\root\cimv2", new ConnectionOptions { EnablePrivileges = true });
            using var query = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT ProcessId, SessionId, Name, ExecutablePath FROM Win32_Process"),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = true, Rewindable = false });
            using var rows = query.Get();
            var seen = 0; var unknown = false;
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    if (++seen > 16384 || Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(5)) { return "SteamStateUnavailable"; }
                    if (row["ProcessId"] is not uint || row["SessionId"] is not uint session || row["Name"] is not string name || string.IsNullOrWhiteSpace(name))
                    { unknown = true; continue; }
                    if (Writers.Contains(name)) { return "SteamAppRunning"; }
                    // 시스템 세션의 경로 미제공 항목은 게임 부재의 근거로 사용하지 않는다.
                    // 경로가 제공된 서비스도 라이브러리 아래라면 거절한다.
                    if (session == 0 && row["ExecutablePath"] is not string) { continue; }
                    if (row["ExecutablePath"] is not string path || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)
                        || path.Contains('~')) { unknown = true; continue; }
                    var full = Path.GetFullPath(path);
                    if (libraries.Any(root => PathScope.IsSameOrUnder(full, root))) { return "SteamGameRunning"; }
                }
            }
            return seen == 0 || unknown || Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(5) ? "SteamStateUnavailable" : null;
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or IOException or System.Security.SecurityException)
        { return "SteamStateUnavailable"; }
    }
}
