/**
 * @file    : FileScanService.cs
 * @author  : rudals252
 * @brief   : 검사 ID마다 한 번만 보호 정책 검증→보호 루트 해석→스캔 계획→볼륨별 메타데이터 순회→임시 위치 관측을 실행하고 결과를 캐시해 여러 프로브가 공유하게 하는 서비스(정책 무효면 순회하지 않음)
 */

// 기본 패키지
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 공유 파일 스캔 서비스입니다. 캐시는 가장 최근 검사 ID 하나만 보관하고, 새 검사 ID가 오면 버립니다.
/// </summary>
/// <remarks>
/// 포함 보호 정책(Probes 어셈블리 리소스 <c>rules\protect.json</c>)이 없거나 무효이면 아무 것도 순회하지 않고 <see cref="DirectoryScanResult.InvalidPolicy"/>를 돌려줍니다.
/// 순회는 처음 호출한 쪽의 취소 토큰으로 취소되며, 이후 호출은 자기 토큰으로 기다리기만 취소할 수 있습니다.
/// </remarks>
public sealed class FileScanService : IFileScanService
{
    /// <summary>포함 보호 정책의 어셈블리 리소스 이름.</summary>
    public const string POLICY_RESOURCE_NAME = "PcOptimizer.Rules.protect.json";

    /// <summary>보호 정책 파일 최대 크기(바이트). 넘으면 무효로 봅니다.</summary>
    public const int MAX_POLICY_BYTES = 256 * 1024;

    private readonly Func<string?> _readPolicy;
    private readonly ProtectionPolicyResolver _resolver;
    private readonly ScanRootCatalog _catalog;
    private readonly FileSystemScanner _scanner;
    private readonly IDirectoryEntrySource _source;
    private readonly TimeProvider _time;
    private readonly Lock _cacheLock = new();
    private Guid _cachedScanId;
    private Task<DirectoryScanResult>? _cachedTask;

    /// <summary>
    /// 서비스를 만듭니다.
    /// </summary>
    /// <param name="readPolicy">보호 정책 JSON을 읽는 함수(없으면 null).</param>
    /// <param name="resolver">보호 정책 해석기.</param>
    /// <param name="catalog">스캔 루트 계획기.</param>
    /// <param name="scanner">파일 순회기.</param>
    /// <param name="source">임시 위치 존재 확인용 열거 공급자.</param>
    /// <param name="budgetPerVolume">볼륨당 시간 예산.</param>
    /// <param name="time">시간 공급자.</param>
    public FileScanService(
        Func<string?> readPolicy,
        ProtectionPolicyResolver resolver,
        ScanRootCatalog catalog,
        FileSystemScanner scanner,
        IDirectoryEntrySource source,
        TimeSpan budgetPerVolume,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(readPolicy);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budgetPerVolume, TimeSpan.Zero);
        _readPolicy = readPolicy;
        _resolver = resolver;
        _catalog = catalog;
        _scanner = scanner;
        _source = source;
        BudgetPerVolume = budgetPerVolume;
        _time = time;
    }

    /// <inheritdoc />
    public TimeSpan BudgetPerVolume { get; }

    /// <summary>
    /// 실제 환경·파일 시스템과 어셈블리에 포함된 보호 정책을 쓰는 서비스를 만듭니다.
    /// </summary>
    /// <returns>서비스.</returns>
    public static FileScanService CreateDefault()
    {
        var environment = SystemPathEnvironment.Instance;
        return new FileScanService(
            ReadBundledPolicy,
            new ProtectionPolicyResolver(environment, Win32RegistryReader.Instance),
            new ScanRootCatalog(environment),
            FileSystemScanner.CreateDefault(),
            FileSystemDirectoryEntrySource.Instance,
            ScanOptions.DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME,
            TimeProvider.System);
    }

    /// <summary>
    /// 포함 보호 정책을 Probes 어셈블리 리소스에서 읽습니다(없거나 너무 크거나 읽지 못하면 null → 무효).
    /// 출력 폴더(<see cref="AppContext.BaseDirectory"/>)의 파일은 읽지 않으므로 사용자 쓰기 가능한 폴더의 파일을 바꿔 보호 정책을 약하게 만들 수 없습니다.
    /// </summary>
    /// <returns>정책 JSON 또는 null.</returns>
    public static string? ReadBundledPolicy()
    {
        try
        {
            using var stream = typeof(FileScanService).Assembly.GetManifestResourceStream(POLICY_RESOURCE_NAME);
            if (stream is null || stream.Length > MAX_POLICY_BYTES)
            {
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or DecoderFallbackException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public int CountPlannedVolumes()
    {
        return _catalog.CountPlannedVolumes();
    }

    /// <inheritdoc />
    public Task<DirectoryScanResult> GetOrScanAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        Task<DirectoryScanResult> task;
        lock (_cacheLock)
        {
            if (_cachedTask is null || _cachedScanId != context.ScanId)
            {
                _cachedScanId = context.ScanId;
                _cachedTask = ScanAsync(context.ScanId, ct);
            }

            task = _cachedTask;
        }

        return task.WaitAsync(ct);
    }

    /// <summary>
    /// 정책 검증부터 임시 위치 관측까지 한 번 실행한다.
    /// </summary>
    private async Task<DirectoryScanResult> ScanAsync(Guid scanId, CancellationToken ct)
    {
        await Task.Yield();
        var start = _time.GetTimestamp();
        var parsed = ProtectionPolicyParser.Parse(_readPolicy());
        if (!parsed.IsValid)
        {
            return DirectoryScanResult.InvalidPolicy(scanId, parsed.Error, _time.GetElapsedTime(start));
        }

        var protection = _resolver.Resolve(parsed.Policy!);
        var plan = _catalog.Build(protection);
        var targets = plan.Roots
            .Where(root => root.State == ScanRootPlanState.Planned)
            .Select(root => new ScanTarget(root.Id, root.Path!))
            .ToList();
        var patterns = plan.Locations
            .Where(location => location.Path is not null && location.FilePatterns.Count > 0)
            .Select(location => new FilePatternLocation(location.Id, location.Path!, location.FilePatterns))
            .ToList();

        var traversal = await _scanner.ScanAsync(targets, protection, patterns, BudgetPerVolume, ct).ConfigureAwait(false);
        var locations = LocationObserver.Observe(plan, traversal, protection, _source);
        return new DirectoryScanResult(scanId, ProtectionPolicyError.None, protection, plan, traversal, locations, _time.GetElapsedTime(start));
    }
}
