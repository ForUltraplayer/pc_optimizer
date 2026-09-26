/**
 * @file    : PcSpecService.cs
 * @author  : rudals252
 * @brief   : 하드웨어 프로브를 규칙 없이 직접 실행해 내 PC 사양 스냅샷(9개 섹션)을 만든다(실행·조립·공용 형식 도우미). 섹션 빌더는 PcSpecService.Sections.cs. 검사와 무관하게 열 때마다 새로 읽는다. 타임아웃 뒤에도 살아 있는 프로브 실행을 추적해 끝날 때까지 재호출하지 않는다(REV-017)
 */

// 기본 패키지
using System.Collections.Concurrent;
using System.Globalization;

// 사용자 패키지
using PcOptimizer.App.Models;
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.Services;

/// <summary>
/// 내 PC 사양 스냅샷을 만드는 서비스입니다.
/// 사용자 요구(2026-09-27)에 따라 CPU·메인보드·GPU·RAM 모듈·디스크·볼륨·모니터·네트워크 어댑터를 요약하지 않고 장치마다 한 줄(라벨: 값)로 나열합니다.
/// 값을 알 수 없으면 <see cref="PcSpecItem.Value"/>를 null로 두며 0·빈 문자열로 바꾸지 않습니다.
/// 프로브 인스턴스는 일반 검사와 공유하므로, 기다리기를 멈춘(타임아웃·취소) 뒤에도 끝나지 않은 실행은 프로브 ID별로 보관하고
/// 그 실행이 끝나기 전에는 같은 프로브를 다시 호출하지 않습니다(<see cref="HasLiveProbes"/>, <see cref="WaitForDrainAsync"/>).
/// </summary>
public sealed partial class PcSpecService
{
    /// <summary>사양 섹션에 쓰는 프로브 ID(실행 순서).</summary>
    public static readonly string[] PROBE_IDS =
    [
        SystemDetailsProbeContract.PROBE_ID, SystemInfoProbeContract.PROBE_ID, MemoryProbeContract.PROBE_ID, GpuProbeContract.PROBE_ID,
        DisplayProbeContract.PROBE_ID, PhysicalDiskProbeContract.PROBE_ID, VolumeProbeContract.PROBE_ID, PowerProbeContract.PROBE_ID,
    ];

    private const long BYTES_PER_GIB = 1024L * 1024 * 1024;
    private const string LOG_CATEGORY = nameof(PcSpecService);
    private const string VALUE_SEPARATOR = " · ";
    private const string FALLBACK_FORMAT = "{0} {1}";
    private const string MEMORY_SIZE_FORMAT = "0";
    private const string STORAGE_SIZE_FORMAT = "0.0";
    private const string DATE_FORMAT = "yyyy-MM-dd";
    private const int FIRST_ORDINAL = 1;

    /// <summary>단위가 보고되지 않은 메모리 속도에 쓰는 단위(SMBIOS 3.x 이후 Type 17 속도 단위).</summary>
    private const string DEFAULT_MEMORY_SPEED_UNIT = MemoryProbeContract.UNIT_MEGATRANSFERS;

    /// <summary>알려진 버스 종류 이름(MSFT_PhysicalDisk.BusType).</summary>
    private static readonly Dictionary<long, string> BUS_TYPE_NAMES = new()
    {
        [1] = "SCSI",
        [2] = "ATAPI",
        [3] = "ATA",
        [4] = "IEEE 1394",
        [5] = "SSA",
        [6] = "Fibre Channel",
        [7] = "USB",
        [8] = "RAID",
        [9] = "iSCSI",
        [10] = "SAS",
        [11] = "SATA",
        [12] = "SD",
        [13] = "MMC",
        [14] = "Virtual",
        [15] = "File Backed Virtual",
        [16] = "Storage Spaces",
        [17] = "NVMe",
        [18] = "SCM",
        [19] = "UFS",
    };

    private readonly IReadOnlyList<IProbe> _probes;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly Func<ScanContext> _contextFactory;
    private readonly ConcurrentDictionary<string, Task> _liveProbes = new(StringComparer.Ordinal);

    /// <summary>서비스를 만듭니다.</summary>
    /// <param name="probes">등록된 프로브 전체(이 중 <see cref="PROBE_IDS"/>만 실행).</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="contextFactory">검사 컨텍스트 생성기(새 ScanId).</param>
    public PcSpecService(IReadOnlyList<IProbe> probes, IClock clock, IAppLogger logger, Func<ScanContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(contextFactory);

        _probes = probes;
        _clock = clock;
        _logger = logger;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 사양 수집이 타임아웃·취소로 기다리기를 멈췄지만 아직 끝나지 않은 프로브 실행이 있는지 여부입니다.
    /// true인 동안 같은 프로브를 공유하는 검사·사양 새로 고침을 시작하지 않아야 합니다.
    /// </summary>
    public bool HasLiveProbes => LiveTasks().Length > 0;

    /// <summary>
    /// 살아 있는 프로브 실행이 모두 끝날 때까지 기다립니다. 늦게 끝난 실행의 실패는 던지지 않고 형식 이름만 기록합니다.
    /// 끝나지 않는 실행이 있으면 완료되지 않으므로 UI 스레드에서 동기로 기다리지 않습니다.
    /// </summary>
    /// <param name="ct">대기 취소 토큰.</param>
    /// <returns>살아 있는 실행이 없어지면 완료되는 작업.</returns>
    /// <exception cref="OperationCanceledException">대기가 취소된 경우.</exception>
    public async Task WaitForDrainAsync(CancellationToken ct)
    {
        while (LiveTasks() is { Length: > 0 } live)
        {
            foreach (var running in live)
            {
                await Task.WhenAny(running).WaitAsync(ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 프로브를 순서대로 실행해 스냅샷을 만듭니다. 프로브마다 <see cref="IProbe.DefaultTimeout"/>을 적용하고,
    /// 타임아웃·예외는 형식 이름만 기록한 뒤 해당 섹션을 확인 불가로 둡니다. 사용자 취소는 호출자에게 전달합니다.
    /// </summary>
    /// <param name="ct">사용자 취소 토큰.</param>
    /// <returns>사양 스냅샷.</returns>
    /// <exception cref="OperationCanceledException">사용자가 취소한 경우.</exception>
    public async Task<PcSpecSnapshot> CaptureAsync(CancellationToken ct)
    {
        var context = _contextFactory();
        var results = new Dictionary<string, ProbeResult>(StringComparer.Ordinal);
        foreach (var id in PROBE_IDS)
        {
            ct.ThrowIfCancellationRequested();
            var probe = _probes.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
            if (probe is null)
            {
                _logger.Warn(LOG_CATEGORY, $"SpecProbeMissing probe={id}");
                continue;
            }

            if (!CanRun(probe, context))
            {
                _logger.Info(LOG_CATEGORY, $"SpecProbeSkipped probe={id}");
                continue;
            }

            if (_liveProbes.TryGetValue(id, out var live) && !live.IsCompleted)
            {
                _logger.Warn(LOG_CATEGORY, $"SpecProbeStillRunning probe={id}");
                continue;
            }

            if (await RunProbeAsync(probe, context, ct).ConfigureAwait(false) is { } result)
            {
                results[id] = result;
            }
        }

        return BuildSnapshot(results, _clock.UtcNow);
    }

    /// <summary>
    /// 프로브 결과에서 스냅샷을 만듭니다(순수). 없는 프로브나 실패·건너뜀·취소 결과는 해당 항목을 확인 불가(null)로 두고,
    /// 모든 항목이 null인 섹션은 <see cref="PcSpecSnapshot.UnavailableSections"/>에 넣습니다.
    /// </summary>
    /// <param name="results">프로브 ID별 결과.</param>
    /// <param name="capturedAtUtc">캡처 시각(UTC).</param>
    /// <returns>9개 섹션 스냅샷.</returns>
    public static PcSpecSnapshot BuildSnapshot(IReadOnlyDictionary<string, ProbeResult> results, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(results);

        var details = new ProbeReader(results, SystemDetailsProbeContract.PROBE_ID);
        var systemInfo = new ProbeReader(results, SystemInfoProbeContract.PROBE_ID);
        var sections = new List<PcSpecSection>();
        var unavailable = new List<string>();
        AddSection(sections, unavailable, Strings.Spec_Section_Os, Os(details));
        AddSection(sections, unavailable, Strings.Spec_Section_Cpu, Cpu(details));
        AddSection(sections, unavailable, Strings.Spec_Section_Memory, Memory(new ProbeReader(results, MemoryProbeContract.PROBE_ID)));
        AddSection(sections, unavailable, Strings.Spec_Section_Gpu, Gpu(new ProbeReader(results, GpuProbeContract.PROBE_ID)));
        AddSection(sections, unavailable, Strings.Spec_Section_Display, Displays(new ProbeReader(results, DisplayProbeContract.PROBE_ID)));
        AddSection(
            sections,
            unavailable,
            Strings.Spec_Section_Storage,
            [.. Disks(new ProbeReader(results, PhysicalDiskProbeContract.PROBE_ID)), .. Volumes(new ProbeReader(results, VolumeProbeContract.PROBE_ID))]);
        AddSection(sections, unavailable, Strings.Spec_Section_Board, Board(details, systemInfo));
        AddSection(sections, unavailable, Strings.Spec_Section_Network, Network(details));
        AddSection(sections, unavailable, Strings.Spec_Section_Power, Power(new ProbeReader(results, PowerProbeContract.PROBE_ID), systemInfo));
        return new PcSpecSnapshot(capturedAtUtc, sections, unavailable);
    }

    /// <summary>
    /// 이 컨텍스트에서 프로브를 실행할 수 있는지 판단한다(검사 조율기와 같은 권한·네트워크·범위 조건).
    /// </summary>
    private static bool CanRun(IProbe probe, ScanContext context)
    {
        return !(probe.RequiresNetwork && !context.OnlineCheckRequested)
            && !(probe.RequiresElevation && !context.IsElevated)
            && !(context.LimitToSystemScope && probe.Scope == ProbeScope.User);
    }

    /// <summary>
    /// 프로브 하나를 기본 타임아웃 안에서 실행한다. 취소 토큰을 무시하는 프로브도 기다리지 않도록 대기 자체에 시간 제한을 두고,
    /// 기다리기를 멈춘 뒤에도 끝나지 않은 실행은 <see cref="TrackIfLive"/>로 보관한다.
    /// </summary>
    private async Task<ProbeResult?> RunProbeAsync(IProbe probe, ScanContext context, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(probe.DefaultTimeout);
        Task<ProbeResult>? running = null;
        try
        {
            running = probe.RunAsync(context, timeout.Token);
            var result = await running.WaitAsync(probe.DefaultTimeout, ct).ConfigureAwait(false);
            if (!string.Equals(result.ProbeId, probe.Id, StringComparison.Ordinal))
            {
                _logger.Warn(LOG_CATEGORY, $"SpecProbeWrongId probe={probe.Id}");
                return null;
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            _logger.Warn(LOG_CATEGORY, $"SpecProbeTimeout probe={probe.Id} type={ex.GetType().Name}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(LOG_CATEGORY, $"SpecProbeFailed probe={probe.Id} type={ex.GetType().Name}");
            return null;
        }
        finally
        {
            TrackIfLive(probe.Id, running);
        }
    }

    /// <summary>
    /// 기다리기를 멈춘 프로브 실행이 아직 끝나지 않았으면 프로브 ID로 보관하고, 끝나면 제거한다(완료 전 재호출 차단).
    /// 대기가 끝난 직후 이미 실패로 끝난 실행도 예외를 읽어 관측한다(미관측 예외로 남기지 않음).
    /// </summary>
    private void TrackIfLive(string probeId, Task? running)
    {
        if (running is null)
        {
            return;
        }

        if (running.IsCompleted)
        {
            if (running.IsFaulted)
            {
                _logger.Warn(LOG_CATEGORY, $"SpecProbeLateFailure probe={probeId} type={running.Exception!.GetBaseException().GetType().Name}");
            }

            return;
        }

        _liveProbes[probeId] = running;
        _logger.Warn(LOG_CATEGORY, $"SpecProbeLive probe={probeId}");
        _ = running.ContinueWith(
            completed => OnLiveProbeCompleted(probeId, completed),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 보관한 실행이 끝나면 사전에서 제거하고 결과를 기록한다(실패는 형식 이름만, 예외를 관측해 미관측 예외로 남기지 않음).
    /// </summary>
    private void OnLiveProbeCompleted(string probeId, Task completed)
    {
        _liveProbes.TryRemove(KeyValuePair.Create(probeId, completed));
        if (completed.Exception is { } failure)
        {
            _logger.Warn(LOG_CATEGORY, $"SpecProbeLateFailure probe={probeId} type={failure.GetBaseException().GetType().Name}");
            return;
        }

        _logger.Info(LOG_CATEGORY, $"SpecProbeDrained probe={probeId}");
    }

    /// <summary>
    /// 아직 끝나지 않은 보관 실행 목록(완료됐지만 제거 전인 항목은 제외).
    /// </summary>
    private Task[] LiveTasks()
    {
        return [.. _liveProbes.Values.Where(task => !task.IsCompleted)];
    }

    /// <summary>
    /// 섹션을 추가하고, 모든 항목 값이 null이면 확인 불가 섹션으로 기록한다.
    /// </summary>
    private static void AddSection(List<PcSpecSection> sections, List<string> unavailable, string title, IReadOnlyList<PcSpecItem> items)
    {
        sections.Add(new PcSpecSection(title, items));
        if (items.All(item => item.Value is null))
        {
            unavailable.Add(title);
        }
    }

    /// <summary>
    /// 매체 종류 원시 값의 디스크 라벨(SSD/HDD/SCM, 그 밖에는 원시 코드, 없으면 "디스크").
    /// </summary>
    private static string DiskLabel(long? mediaType)
    {
        return mediaType switch
        {
            PhysicalDiskProbeContract.MEDIA_TYPE_SSD => Strings.Spec_LabelSsd,
            PhysicalDiskProbeContract.MEDIA_TYPE_HDD => Strings.Spec_LabelHdd,
            PhysicalDiskProbeContract.MEDIA_TYPE_SCM => Strings.Spec_LabelScm,
            null => Strings.Spec_Disk,
            { } code => Format(Strings.Spec_DiskMediaCode, code),
        };
    }

    /// <summary>
    /// 버스 종류 원시 값의 인터페이스 이름(없으면 null, 모르는 코드는 원시 코드 표기).
    /// </summary>
    private static string? BusName(long? busType)
    {
        if (busType is not { } code)
        {
            return null;
        }

        return BUS_TYPE_NAMES.TryGetValue(code, out var name) ? name : Format(Strings.Spec_DiskBusCode, code);
    }

    /// <summary>
    /// Windows가 보고한 디스크 상태 원시 값의 표시 이름(알 수 없으면 null).
    /// </summary>
    private static string? HealthName(long? health)
    {
        return health switch
        {
            PhysicalDiskProbeContract.HEALTH_HEALTHY => Strings.Spec_DiskHealthy,
            PhysicalDiskProbeContract.HEALTH_WARNING => Strings.Spec_DiskWarning,
            PhysicalDiskProbeContract.HEALTH_UNHEALTHY => Strings.Spec_DiskUnhealthy,
            _ => null,
        };
    }

    /// <summary>
    /// 메모리 속도 측정값을 읽는다(0 이하는 값 없음). 단위가 보고되지 않았으면 MT/s로 표시한다.
    /// </summary>
    private static (long Value, string Unit)? MemorySpeed(ProbeReader memory, string name)
    {
        var measurement = memory.Measurement(name);
        if (measurement?.Value is not IntegerValue { Value: > 0 } speed)
        {
            return null;
        }

        return (speed.Value, string.IsNullOrWhiteSpace(measurement.Unit) ? DEFAULT_MEMORY_SPEED_UNIT : measurement.Unit.Trim());
    }

    /// <summary>
    /// 메모리 용량(바이트)을 GiB 정수 반올림 "N GB"로 바꾼다.
    /// </summary>
    private static string MemorySize(long bytes)
    {
        return Format(Strings.Spec_Gigabytes, ((double)bytes / BYTES_PER_GIB).ToString(MEMORY_SIZE_FORMAT, CultureInfo.CurrentCulture));
    }

    /// <summary>
    /// 디스크·볼륨 용량(바이트)을 GiB 소수 첫째 자리 "N.N GB"로 바꾼다.
    /// </summary>
    private static string StorageSize(long bytes)
    {
        return Format(Strings.Spec_Gigabytes, StorageNumber(bytes));
    }

    /// <summary>
    /// 디스크·볼륨 용량(바이트)을 GiB 소수 첫째 자리 수치 문자열로 바꾼다.
    /// </summary>
    private static string StorageNumber(long bytes)
    {
        return ((double)bytes / BYTES_PER_GIB).ToString(STORAGE_SIZE_FORMAT, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 날짜 문자열(yyyy-MM-dd 또는 ISO 8601 오프셋 포함)을 yyyy-MM-dd로 바꾼다. 해석할 수 없으면 보고된 문자열을 그대로 쓴다.
    /// </summary>
    private static string? DateText(string? text)
    {
        if (text is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)
            : text;
    }

    /// <summary>
    /// 대체 값에 대체 표기를 붙인다(값이 없으면 null).
    /// </summary>
    private static string? WithMarker(string? value, string marker)
    {
        return value is null ? null : string.Format(CultureInfo.CurrentCulture, FALLBACK_FORMAT, value, marker);
    }

    /// <summary>
    /// 값이 있는 부분만 " · "로 잇는다. 모두 없으면 null(빈 문자열로 바꾸지 않음).
    /// </summary>
    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return present.Length == 0 ? null : string.Join(VALUE_SEPARATOR, present);
    }

    /// <summary>
    /// 양수만 값으로 인정한다(0·음수는 WMI의 "알 수 없음" 표기로 보고 null).
    /// </summary>
    private static int? Positive(long? value)
    {
        return value is > 0 and <= int.MaxValue ? (int)value.Value : null;
    }

    /// <summary>
    /// 0 이상만 값으로 인정한다(개수 0은 "없음"으로 구분할 때 쓴다).
    /// </summary>
    private static int? NonNegative(long? value)
    {
        return value is >= 0 and <= int.MaxValue ? (int)value.Value : null;
    }

    /// <summary>
    /// 양수 바이트 수만 값으로 인정한다(용량용, int 범위 제한 없음. 0은 WMI의 "알 수 없음"으로 보고 null).
    /// </summary>
    private static long? PositiveBytes(long? value)
    {
        return value is > 0 ? value : null;
    }

    /// <summary>
    /// 0 이상 바이트 수만 값으로 인정한다(볼륨 남은 공간은 0이 실제 값일 수 있음).
    /// </summary>
    private static long? NonNegativeBytes(long? value)
    {
        return value is >= 0 ? value : null;
    }

    /// <summary>
    /// 현재 문화권으로 리소스 형식 문자열을 채운다.
    /// </summary>
    private static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }

    /// <summary>
    /// 프로브 결과 하나의 측정값을 이름으로 읽는 도우미입니다. 결과가 없거나 성공·부분 성공이 아니면 모든 값이 null입니다.
    /// </summary>
    private sealed class ProbeReader
    {
        private readonly Dictionary<string, Measurement> _measurements = new(StringComparer.Ordinal);

        /// <summary>결과 사전에서 프로브 결과를 찾아 읽기 도우미를 만든다.</summary>
        public ProbeReader(IReadOnlyDictionary<string, ProbeResult> results, string probeId)
        {
            if (!results.TryGetValue(probeId, out var result)
                || (result.Status != ProbeStatus.Success && result.Status != ProbeStatus.Partial))
            {
                return;
            }

            foreach (var measurement in result.Measurements)
            {
                _measurements.TryAdd(measurement.Name, measurement);
            }
        }

        /// <summary>측정값(없으면 null).</summary>
        public Measurement? Measurement(string name) => _measurements.GetValueOrDefault(name);

        /// <summary>문자열 측정값(없거나 공백이면 null, 앞뒤 공백 제거).</summary>
        public string? Text(string name)
        {
            return Measurement(name)?.Value is TextValue text && !string.IsNullOrWhiteSpace(text.Value) ? text.Value.Trim() : null;
        }

        /// <summary>정수 측정값(없으면 null).</summary>
        public long? Integer(string name) => Measurement(name)?.Value is IntegerValue value ? value.Value : null;
    }
}
