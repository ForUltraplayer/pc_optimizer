/**
 * @file    : SystemCacheToolBackend.cs
 * @author  : rudals252
 * @brief   : 설치된 npm·pip·dotnet HTTP 캐시만 찾고 보호 경계를 검사하여 공식 명령으로 정리
 */
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions;

/// <summary>1차 공식 도구 통합 구현입니다. 자동 설치·승격·임의 명령·직접 파일 삭제는 없습니다.</summary>
public sealed class SystemCacheToolBackend : ICacheToolBackend
{
    private const int MAX_ENTRIES = 100000;
    private static readonly TimeSpan INSPECTION_BUDGET = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    public async Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct)
    {
        if (IsElevated()) { return null; }
        foreach (var executable in FindExecutables(tool))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsPlainPath(executable, directory: false)) { continue; }
            var script = tool == CacheTool.Npm ? Path.Combine(Path.GetDirectoryName(executable)!, "node_modules", "npm", "bin", "npm-cli.js") : null;
            if (script is not null && !IsPlainPath(script, directory: false)) { continue; }
            var fingerprint = await FingerprintAsync(executable, script, ct).ConfigureAwait(false);
            var candidate = new CacheToolLocation(tool, executable, string.Empty, fingerprint, script);
            var result = await CacheToolProcess.RunAsync(candidate, clear: false, ct).ConfigureAwait(false);
            if (!result.Success) { continue; }
            var value = result.Output.Trim();
            if (tool == CacheTool.NuGetHttp)
            {
                const string PREFIX = "http-cache: ";
                if (!value.StartsWith(PREFIX, StringComparison.Ordinal)) { continue; }
                value = value[PREFIX.Length..].Trim();
            }
            if (value.Contains('\n') || value.Contains('\r') || !Path.IsPathFullyQualified(value) || value.StartsWith("\\\\", StringComparison.Ordinal)) { continue; }
            return candidate with { CachePath = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar) };
        }
        return null;
    }

    /// <inheritdoc />
    public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct)
        => Task.Run(() => Inspect(location, ct), ct);

    /// <inheritdoc />
    public async Task<bool> ClearAsync(CacheToolLocation location, CancellationToken ct)
    {
        if (IsElevated() || !Inspect(location, ct).Allowed) { return false; }
        if (await FingerprintAsync(location.Executable, location.Script, ct).ConfigureAwait(false) != location.Fingerprint) { return false; }
        return (await CacheToolProcess.RunAsync(location, clear: true, ct).ConfigureAwait(false)).Success;
    }

    /// <summary>현재 사용자 권한을 재확인합니다.</summary>
    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>실행 파일과 고정 npm 진입 파일이 바뀌면 계획을 무효로 만드는 지문입니다.</summary>
    private static async Task<string> FingerprintAsync(string executable, string? script, CancellationToken ct)
    {
        var values = new List<string>();
        foreach (var path in new[] { executable, script }.OfType<string>())
        {
            using var stream = File.OpenRead(path);
            values.Add(Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)));
        }
        return string.Join(":", values);
    }

    /// <summary>공식 실행 파일 이름만 절대 경로로 찾습니다. 현재 폴더·배치 파일·스토어 실행 별칭은 제외합니다.</summary>
    private static IEnumerable<string> FindExecutables(CacheTool tool)
    {
        var name = tool switch { CacheTool.Npm => "node.exe", CacheTool.Pip => "python.exe", CacheTool.NuGetHttp => "dotnet.exe", _ => throw new ArgumentOutOfRangeException(nameof(tool)) };
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var standard = tool switch { CacheTool.Npm => Path.Combine(programFiles, "nodejs"), CacheTool.NuGetHttp => Path.Combine(programFiles, "dotnet"), _ => string.Empty };
        return new[] { standard }.Concat((Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';'))
            .Where(path => Path.IsPathFullyQualified(path) && !path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.Combine(path, name)).Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists).Take(3);
    }

    /// <summary>중간 폴더와 마지막 항목 모두 일반 로컬 항목이어야 합니다.</summary>
    internal static bool IsPlainPath(string path, bool directory)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal)) { return false; }
        var source = FileSystemDirectoryEntrySource.Instance;
        var parents = new Stack<string>();
        for (var parent = Path.GetDirectoryName(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent)) { parents.Push(parent); }
        foreach (var parent in parents) { if (source.ProbeRoot(parent) != RootPresence.Directory) { return false; } }
        return source.ProbeRoot(path) == (directory ? RootPresence.Directory : RootPresence.NotDirectory);
    }

    /// <summary>보호 경로·다른 사용자·링크·부분 관측은 실행 불가입니다. 사용자 데이터 본문은 읽지 않습니다.</summary>
    private static CacheInspection Inspect(CacheToolLocation location, CancellationToken ct)
    {
        try
        {
            if (IsElevated()) { return new(false, 0, "NormalUserRequired"); }
            var environment = SystemPathEnvironment.Instance;
            var policy = ProtectionPolicyParser.Parse(FileScanService.ReadBundledPolicy() ?? string.Empty);
            if (policy.Policy is null) { return new(false, 0, "ProtectionUnavailable"); }
            var protection = new ProtectionPolicyResolver(environment, Win32RegistryReader.Instance).Resolve(policy.Policy);
            using var identity = WindowsIdentity.GetCurrent();
            var otherUsers = OtherUserLocationGuard.Create(Win32RegistryReader.Instance, environment, identity.User?.Value);
            var root = location.CachePath;
            if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal) || root.IndexOf(':', 2) >= 0
                || Path.GetPathRoot(root)?.TrimEnd('\\') == root || root.Length < 4) { return new(false, 0, "UnsafePath"); }
            var profile = environment.GetUserProfilePath();
            if (profile is null || string.Equals(root, profile, StringComparison.OrdinalIgnoreCase)
                || protection.IsProtected(root) || otherUsers.IsOtherUserLocation(root)
                || protection.Roots.Any(item => item.Path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))) { return new(false, 0, "ProtectedPath"); }
            // 기존 도구 실행은 공유·시스템 영역까지 확장하지 않는다. 1차는 현재 프로필의 캐시만 지원한다.
            if (!root.StartsWith(profile.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { return new(false, 0, "OutsideUserProfile"); }
            if (!IsPlainPath(location.Executable, false) || (location.Script is not null && !IsPlainPath(location.Script, false))) { return new(false, 0, "ToolChanged"); }
            if (!Directory.Exists(root))
            {
                var parent = Path.GetDirectoryName(root);
                return parent is not null && IsPlainPath(parent, true) && FileSystemDirectoryEntrySource.Instance.ProbeRoot(root) == RootPresence.Missing
                    ? new(true, 0, null) : new(false, 0, "UnreadablePath");
            }
            if (!IsPlainPath(root, true)) { return new(false, 0, "LinkOrPlaceholder"); }
            var watch = Stopwatch.StartNew();
            long bytes = 0;
            var count = 0;
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.TryPop(out var directory))
            {
                if (!IsPlainPath(directory, true) || protection.IsProtected(directory)) { return new(false, 0, "ProtectedOrLinkedChild"); }
                foreach (var entry in FileSystemDirectoryEntrySource.Instance.Enumerate(directory))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++count > MAX_ENTRIES || watch.Elapsed > INSPECTION_BUDGET) { return new(false, 0, "InspectionIncomplete"); }
                    if ((entry.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x40000 | (FileAttributes)0x400000)) != 0) { return new(false, 0, "LinkOrPlaceholder"); }
                    if (entry.IsDirectory) { stack.Push(Path.Combine(directory, entry.Name)); }
                    else
                    {
                        // HTTP 캐시를 전역 패키지로 잘못 설정한 경우에도 정리하지 않는다.
                        if (location.Tool == CacheTool.NuGetHttp && !entry.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) { return new(false, 0, "UnexpectedHttpCacheContent"); }
                        bytes = checked(bytes + entry.Length);
                    }
                }
            }
            return new(true, bytes, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or OverflowException)
        { return new(false, 0, "InspectionFailed"); }
    }
}
