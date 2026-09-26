/**
 * @file    : SystemCacheToolBackend.cs
 * @author  : rudals252
 * @brief   : 설치된 npm·pip·dotnet HTTP 캐시만 찾고 보호 경계를 검사하여 공식 명령으로 정리
 */
using PcOptimizer.Core.Abstractions;
using System.Security.Cryptography;
using System.Security.Principal;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions;

/// <summary>1차 공식 도구 통합 구현입니다. 자동 설치·승격·임의 명령·직접 파일 삭제는 없습니다.</summary>
public sealed class SystemCacheToolBackend : ICacheToolBackend
{
    private readonly IAppLogger _logger;
    private readonly CachePathInspector _inspector;

    /// <summary>시스템 공급자와 개인정보를 기록하지 않는 로거를 연결합니다.</summary>
    public SystemCacheToolBackend(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
        _inspector = new(SystemPathEnvironment.Instance, Win32RegistryReader.Instance,
            FileSystemDirectoryEntrySource.Instance, IsElevated, CurrentSid,
            FileScanService.ReadBundledPolicy, TimeProvider.System, TimeSpan.FromSeconds(15));
    }

    /// <summary>호출 시점의 현재 사용자 SID를 읽습니다.</summary>
    private static string? CurrentSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value;
    }

    /// <inheritdoc />
    public async Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct)
    {
        if (IsElevated()) { throw new CacheToolUnavailableException("NormalUserRequired"); }
        foreach (var executable in FindExecutables(tool))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsPlainPath(executable, directory: false)) { continue; }
            var script = tool == CacheTool.Npm ? Path.Combine(Path.GetDirectoryName(executable)!, "node_modules", "npm", "bin", "npm-cli.js") : null;
            if (script is not null && !IsPlainPath(script, directory: false)) { continue; }
            var fingerprint = await FingerprintAsync(executable, script, ct).ConfigureAwait(false);
            var candidate = new CacheToolLocation(tool, executable, string.Empty, fingerprint, script);
            var result = await CacheToolProcess.RunAsync(candidate, clear: false, ct, _logger).ConfigureAwait(false);
            if (result.Code is "ProcessStillRunning" or "Busy") { throw new CacheToolUnavailableException(result.Code); }
            if (!result.Success) { continue; }
            var value = result.Output.Trim();
            if (tool == CacheTool.NuGetHttp)
            {
                const string PREFIX = "http-cache: ";
                if (!value.StartsWith(PREFIX, StringComparison.Ordinal)) { continue; }
                value = value[PREFIX.Length..].Trim();
            }
            if (value.Contains('\n') || value.Contains('\r') || !Path.IsPathFullyQualified(value) || value.StartsWith("\\\\", StringComparison.Ordinal)) { continue; }
            return candidate with { CachePath = CachePathInspector.CanonicalPath(SystemPathEnvironment.Instance, value) };
        }
        return null;
    }

    /// <inheritdoc />
    public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct)
        => Task.Run(() => _inspector.Inspect(location, ct), ct);

    /// <inheritdoc />
    public async Task<CacheToolExecution> ClearAsync(CacheToolLocation location, CancellationToken ct)
    {
        var inspection = _inspector.Inspect(location, ct);
        if (!inspection.Allowed) { return new(false, false, inspection.Reason ?? "Blocked"); }
        if (await FingerprintAsync(location.Executable, location.Script, ct).ConfigureAwait(false) != location.Fingerprint) { return new(false, false, "ToolChanged"); }
        var result = await CacheToolProcess.RunAsync(location, clear: true, ct, _logger).ConfigureAwait(false);
        return new(result.Started, result.Success, result.Code);
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
    internal static bool IsPlainPath(string path, bool directory, IDirectoryEntrySource? entries = null)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal)) { return false; }
        var source = entries ?? FileSystemDirectoryEntrySource.Instance;
        var parents = new Stack<string>();
        for (var parent = Path.GetDirectoryName(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent)) { parents.Push(parent); }
        foreach (var parent in parents) { if (source.ProbeRoot(parent) != RootPresence.Directory) { return false; } }
        return source.ProbeRoot(path) == (directory ? RootPresence.Directory : RootPresence.NotDirectory);
    }

}
