/**
 * @file    : SystemCacheToolBackend.cs
 * @author  : rudals252
 * @brief   : 설치된 npm·pip·dotnet HTTP 캐시만 찾고 보호 경계를 검사하여 공식 명령으로 정리. 관리자 권한 앱이므로 보호 위치(Program Files 계열) 도구만 실행하고, SystemOnly 인스턴스는 관측 전에 거절한다. 정리 창 노출 판정은 실행 규칙과 같은 후보·보호 위치 판정으로 존재만 확인한다(프로세스 실행 없음)
 */

// 기본 패키지
using System.Security.Cryptography;
using System.Security.Principal;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Actions;

/// <summary>
/// 1차 공식 도구 통합 구현입니다. 자동 설치·승격·임의 명령·직접 파일 삭제는 없습니다.
/// 앱은 항상 관리자 권한으로 실행되므로 사용자 쓰기 가능 위치의 도구는 실행하지 않습니다(PATH 탐색 결과 포함).
/// </summary>
public sealed class SystemCacheToolBackend : ICacheToolBackend
{
    /// <inheritdoc />
    public Task WaitForDrainAsync() => CacheToolProcess.WaitForDrainAsync(_logger);
    /// <summary>다른 관리자 계정으로 승격된(SystemOnly) 인스턴스라 사용자별 캐시(npm·pip·NuGet 모두 해당)를 다루지 않는다는 거절 코드입니다.</summary>
    public const string USER_SCOPE_EXCLUDED = "UserScopeExcluded";

    /// <summary>도구 실행 파일(또는 npm 진입 파일)이 보호 위치(Program Files 계열) 밖에 있어 관리자 권한 앱이 실행하지 않는다는 거절 코드입니다.</summary>
    public const string TOOL_NOT_IN_PROTECTED_LOCATION = "ToolNotInProtectedLocation";

    private const int MAX_EXECUTABLE_CANDIDATES = 3;
    private const string PATH_VARIABLE = "PATH";
    private static readonly TimeSpan INSPECTION_BUDGET = TimeSpan.FromSeconds(15);

    private readonly IAppLogger _logger;
    private readonly bool _limitToSystemScope;
    private readonly IPathEnvironment _environment;
    private readonly IDirectoryEntrySource _entries;
    private readonly CachePathInspector _inspector;
    private readonly Func<CacheTool, IEnumerable<string>> _findExecutables;
    private readonly Func<CacheToolLocation, bool, CancellationToken, Task<CacheProcessResult>> _run;
    private readonly Func<string, string?, CancellationToken, Task<string>> _fingerprint;

    /// <summary>시스템 공급자와 개인정보를 기록하지 않는 로거를 연결합니다.</summary>
    /// <param name="logger">앱 로거(없으면 기록하지 않음).</param>
    /// <param name="limitToSystemScope">true면(SystemOnly) 사용자별 캐시의 조회·정리를 관측 전에 <see cref="USER_SCOPE_EXCLUDED"/>로 거절합니다.</param>
    public SystemCacheToolBackend(IAppLogger? logger = null, bool limitToSystemScope = false)
        : this(logger ?? NullAppLogger.Instance, limitToSystemScope, SystemPathEnvironment.Instance, FileSystemDirectoryEntrySource.Instance,
            new CachePathInspector(SystemPathEnvironment.Instance, Win32RegistryReader.Instance, FileSystemDirectoryEntrySource.Instance,
                ProtectedProgramRoot, CurrentSid, FileScanService.ReadBundledPolicy, TimeProvider.System, INSPECTION_BUDGET),
            FindExecutables, (location, clear, ct) => CacheToolProcess.RunAsync(location, clear, ct, logger), FingerprintAsync)
    {
    }

    /// <summary>테스트가 파일 시스템·도구 실행·지문 계산을 가짜로 바꾸는 생성자입니다.</summary>
    internal SystemCacheToolBackend(IAppLogger logger, bool limitToSystemScope, IPathEnvironment environment,
        IDirectoryEntrySource entries, CachePathInspector inspector,
        Func<CacheTool, IEnumerable<string>> findExecutables,
        Func<CacheToolLocation, bool, CancellationToken, Task<CacheProcessResult>> run,
        Func<string, string?, CancellationToken, Task<string>> fingerprint)
    {
        _logger = logger;
        _limitToSystemScope = limitToSystemScope;
        _environment = environment;
        _entries = entries;
        _inspector = inspector;
        _findExecutables = findExecutables;
        _run = run;
        _fingerprint = fingerprint;
    }

    /// <summary>
    /// 보호 위치 이름(<see cref="CachePathInspector.PROTECTED_PROGRAM_ROOTS"/>)별 실제 경로입니다. 프로세스 환경 변수는 사용자 환경(HKCU)으로
    /// 바꿀 수 있으므로 읽지 않고, 같은 폴더를 Known Folder API로 읽습니다. %ProgramW6432%는 64비트 프로세스의 Program Files와 같습니다.
    /// </summary>
    /// <param name="name">보호 위치 이름.</param>
    /// <returns>경로. 알 수 없으면 null(검사에서 건너뜀).</returns>
    internal static string? ProtectedProgramRoot(string name) => name switch
    {
        CachePathInspector.PROGRAM_FILES => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        CachePathInspector.PROGRAM_FILES_X86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        CachePathInspector.PROGRAM_W6432 => Environment.Is64BitProcess ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) : null,
        _ => null,
    };

    /// <summary>호출 시점의 현재 사용자 SID를 읽습니다.</summary>
    private static string? CurrentSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SystemOnly이면 도구 탐색·파일 관측·프로세스 실행 전에 <see cref="USER_SCOPE_EXCLUDED"/>로 거절합니다.
    /// 보호 위치 밖의 후보(PATH 탐색 결과 포함)는 조회 명령도 실행하지 않고 건너뛰며, 보호 위치 후보가 하나도 없이 그런 후보만 있으면
    /// <see cref="TOOL_NOT_IN_PROTECTED_LOCATION"/>로 거절합니다. 후보 상한(<see cref="MAX_EXECUTABLE_CANDIDATES"/>)은 보호 위치 판정 뒤에 적용해
    /// 사용자 폴더 후보가 상한을 소모하지 않습니다.
    /// </remarks>
    public async Task<CacheToolLocation?> LocateAsync(CacheTool tool, CancellationToken ct)
    {
        if (_limitToSystemScope)
        {
            _logger.Info(nameof(SystemCacheToolBackend), $"CacheToolRefused tool={tool} code={USER_SCOPE_EXCLUDED}");
            throw new CacheToolUnavailableException(USER_SCOPE_EXCLUDED);
        }
        var outsideProtected = false;
        var protectedCandidates = 0;
        foreach (var executable in _findExecutables(tool))
        {
            ct.ThrowIfCancellationRequested();
            var script = ScriptFor(tool, executable);
            if (!_inspector.IsProtectedProgramLocation(executable) || (script is not null && !_inspector.IsProtectedProgramLocation(script)))
            {
                outsideProtected = true;
                continue;
            }
            if (++protectedCandidates > MAX_EXECUTABLE_CANDIDATES) { break; }
            if (!IsPlainPath(executable, directory: false, _entries)) { continue; }
            if (script is not null && !IsPlainPath(script, directory: false, _entries)) { continue; }
            var fingerprint = await _fingerprint(executable, script, ct).ConfigureAwait(false);
            var candidate = new CacheToolLocation(tool, executable, string.Empty, fingerprint, script);
            var result = await _run(candidate, false, ct).ConfigureAwait(false);
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
            return candidate with { CachePath = CachePathInspector.CanonicalPath(_environment, value) };
        }
        if (outsideProtected && protectedCandidates == 0)
        {
            _logger.Info(nameof(SystemCacheToolBackend), $"CacheToolRefused tool={tool} code={TOOL_NOT_IN_PROTECTED_LOCATION}");
            throw new CacheToolUnavailableException(TOOL_NOT_IN_PROTECTED_LOCATION);
        }
        return null;
    }

    /// <inheritdoc />
    /// <remarks>SystemOnly이면 파일을 열거하지 않고 <see cref="USER_SCOPE_EXCLUDED"/>로 거절합니다.</remarks>
    public Task<CacheInspection> InspectAsync(CacheToolLocation location, CancellationToken ct)
        => _limitToSystemScope
            ? Task.FromResult(new CacheInspection(false, 0, USER_SCOPE_EXCLUDED))
            : Task.Run(() => _inspector.Inspect(location, ct), ct);

    /// <inheritdoc />
    /// <remarks>
    /// SystemOnly이면 실행 직전 관측·지문 계산·프로세스 시작 없이 Started=false로 거절합니다.
    /// 실행 직전 검사(<see cref="CachePathInspector.Inspect"/>)가 도구 보호 위치를 다시 확인하므로 보호 위치 밖이면
    /// <see cref="TOOL_NOT_IN_PROTECTED_LOCATION"/>로 시작하지 않습니다.
    /// </remarks>
    public async Task<CacheToolExecution> ClearAsync(CacheToolLocation location, CancellationToken ct)
    {
        if (_limitToSystemScope) { return new(false, false, USER_SCOPE_EXCLUDED); }
        var inspection = _inspector.Inspect(location, ct);
        if (!inspection.Allowed) { return new(false, false, inspection.Reason ?? "Blocked"); }
        if (await _fingerprint(location.Executable, location.Script, ct).ConfigureAwait(false) != location.Fingerprint) { return new(false, false, "ToolChanged"); }
        var result = await _run(location, true, ct).ConfigureAwait(false);
        return new(result.Started, result.Success, result.Code);
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

    /// <summary>
    /// 정리 창을 보여 줄지 판단합니다. 보호 위치(Program Files 계열)에 npm·pip·dotnet 중 하나라도 실행 가능한 상태로 있으면 true입니다.
    /// 실행 규칙(<see cref="LocateAsync"/>)과 같은 후보 목록(표준 위치 + PATH)과 같은 보호 위치 판정을 쓰며, 존재 확인만 하고 도구 프로세스는 실행하지 않습니다.
    /// </summary>
    /// <returns>하나라도 있으면 true.</returns>
    public static bool AnyToolInProtectedLocation() => AnyToolInProtectedLocation(SystemPathEnvironment.Instance, ProtectedProgramRoot,
        Environment.GetEnvironmentVariable(PATH_VARIABLE), FileSystemDirectoryEntrySource.Instance);

    /// <summary>도구 하나의 보호 위치 설치 여부를 프로세스 실행 없이 확인합니다.</summary>
    public static bool IsToolInProtectedLocation(CacheTool tool) => IsToolInProtectedLocation(tool, SystemPathEnvironment.Instance, ProtectedProgramRoot,
        Environment.GetEnvironmentVariable(PATH_VARIABLE), FileSystemDirectoryEntrySource.Instance);


    /// <summary>주입 가능한 <see cref="AnyToolInProtectedLocation()"/>입니다.</summary>
    /// <param name="environment">경로 정규화 환경.</param>
    /// <param name="programRoot">보호 위치 이름별 경로(없으면 null).</param>
    /// <param name="pathVariable">PATH 값(없으면 null).</param>
    /// <param name="entries">디렉터리 항목 공급자.</param>
    /// <returns>하나라도 있으면 true.</returns>
    internal static bool AnyToolInProtectedLocation(IPathEnvironment environment, Func<string, string?> programRoot, string? pathVariable,
        IDirectoryEntrySource entries)
        => Enum.GetValues<CacheTool>().Any(tool => IsToolInProtectedLocation(tool, environment, programRoot, pathVariable, entries));

    /// <summary>
    /// 도구 하나가 보호 위치에 실행 가능한 상태로 있는지 확인합니다. 실행 파일과(npm은) 고정 진입 파일이 모두 보호 위치 아래의 일반 로컬 파일이어야 합니다.
    /// </summary>
    /// <param name="tool">도구.</param>
    /// <param name="environment">경로 정규화 환경.</param>
    /// <param name="programRoot">보호 위치 이름별 경로(없으면 null).</param>
    /// <param name="pathVariable">PATH 값(없으면 null).</param>
    /// <param name="entries">디렉터리 항목 공급자.</param>
    /// <returns>하나라도 있으면 true.</returns>
    internal static bool IsToolInProtectedLocation(CacheTool tool, IPathEnvironment environment, Func<string, string?> programRoot,
        string? pathVariable, IDirectoryEntrySource entries)
        => CandidateExecutables(tool, programRoot, pathVariable).Any(executable =>
        {
            var script = ScriptFor(tool, executable);
            return CachePathInspector.IsProtectedProgramLocation(environment, programRoot, executable)
                && (script is null || CachePathInspector.IsProtectedProgramLocation(environment, programRoot, script))
                && IsPlainPath(executable, directory: false, entries)
                && (script is null || IsPlainPath(script, directory: false, entries));
        });

    /// <summary>
    /// 실행 규칙과 노출 판정이 함께 쓰는 후보 실행 파일 목록입니다. 각 보호 위치의 표준 폴더(nodejs·dotnet, pip은 없음)를 먼저,
    /// 그다음 PATH의 절대 경로를 순서대로 둡니다. 스토어 실행 별칭(WindowsApps)·상대 경로는 제외하고 중복을 없앱니다.
    /// 보호 위치 판정과 후보 상한은 사용하는 쪽이 이 목록에 적용합니다(상한이 판정보다 앞서지 않음).
    /// </summary>
    /// <param name="tool">도구.</param>
    /// <param name="programRoot">보호 위치 이름별 경로(없으면 null).</param>
    /// <param name="pathVariable">PATH 값(없으면 null).</param>
    /// <returns>후보 실행 파일 경로(존재 여부는 확인하지 않음).</returns>
    internal static IEnumerable<string> CandidateExecutables(CacheTool tool, Func<string, string?> programRoot, string? pathVariable)
    {
        var name = ExecutableName(tool);
        var standard = StandardFolder(tool) is { } folder
            ? CachePathInspector.PROTECTED_PROGRAM_ROOTS.Select(programRoot).OfType<string>()
                .Where(Path.IsPathFullyQualified).Select(root => Path.Combine(root, folder))
            : [];
        return standard.Concat((pathVariable ?? string.Empty).Split(';'))
            .Where(path => Path.IsPathFullyQualified(path) && !path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.Combine(path, name)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>도구의 공식 실행 파일 이름입니다.</summary>
    private static string ExecutableName(CacheTool tool) => tool switch
    {
        CacheTool.Npm => "node.exe",
        CacheTool.Pip => "python.exe",
        CacheTool.NuGetHttp => "dotnet.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };

    /// <summary>보호 위치 아래 표준 설치 폴더 이름입니다. 표준 폴더가 없는 도구(pip, 버전별 폴더)는 null.</summary>
    private static string? StandardFolder(CacheTool tool) => tool switch { CacheTool.Npm => "nodejs", CacheTool.NuGetHttp => "dotnet", _ => null };

    /// <summary>npm은 node.exe 옆의 고정 npm-cli.js로 실행합니다. 다른 도구는 null.</summary>
    private static string? ScriptFor(CacheTool tool, string executable) => tool == CacheTool.Npm
        ? Path.Combine(Path.GetDirectoryName(executable)!, "node_modules", "npm", "bin", "npm-cli.js")
        : null;

    /// <summary>실제 파일 시스템에 있는 후보만 돌려줍니다. 보호 위치 판정과 상한은 <see cref="LocateAsync"/>가 적용합니다.</summary>
    private static IEnumerable<string> FindExecutables(CacheTool tool)
        => CandidateExecutables(tool, ProtectedProgramRoot, Environment.GetEnvironmentVariable(PATH_VARIABLE)).Where(File.Exists);

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
