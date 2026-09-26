/**
 * @file    : PhysicalDiskProbe.cs
 * @author  : rudals252
 * @brief   : root\Microsoft\Windows\Storage의 MSFT_PhysicalDisk 디스크별 이름·매체·버스·Windows 보고 상태·크기·회전 속도와 제공자 디스크 GUID를 수집하는 물리 디스크 프로브(일련번호 미수집)
 */

// 기본 패키지
using System.Text.RegularExpressions;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 물리 디스크 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="PhysicalDiskProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// 상태 값은 저장소 제공자가 보고한 요약이며 SMART 원시 항목을 읽지 않습니다.
/// 영속 식별자로 ObjectId의 물리 디스크 GUID("PD:{...}")만 뽑아 쓰고, ObjectId 전체(PC 이름 포함)·UniqueId·SerialNumber는 수집하지 않습니다.
/// 조회 실패·빈 결과는 Failed, 상태 값이 빠진 디스크가 있으면 Partial입니다.
/// </remarks>
public sealed partial class PhysicalDiskProbe : IProbe
{
    /// <summary>물리 디스크 WMI 클래스.</summary>
    public const string PHYSICAL_DISK_CLASS = "MSFT_PhysicalDisk";

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(10);

    private const string PROPERTY_DEVICE_ID = "DeviceId";
    private const string PROPERTY_OBJECT_ID = "ObjectId";
    private const string PROPERTY_FRIENDLY_NAME = "FriendlyName";
    private const string PROPERTY_MEDIA_TYPE = "MediaType";
    private const string PROPERTY_BUS_TYPE = "BusType";
    private const string PROPERTY_HEALTH_STATUS = "HealthStatus";
    private const string PROPERTY_SIZE = "Size";
    private const string PROPERTY_SPINDLE_SPEED = "SpindleSpeed";
    private const string SOURCE_PREFIX = "WMI " + PHYSICAL_DISK_CLASS + ".";
    private const string SOURCE_PROVIDER_DISK_ID = SOURCE_PREFIX + PROPERTY_OBJECT_ID + " (PD GUID)";
    private const string DISK_GUID_GROUP = "guid";
    private const int REGEX_TIMEOUT_MILLISECONDS = 100;

    private static readonly string[] PROPERTIES =
    [
        PROPERTY_DEVICE_ID,
        PROPERTY_OBJECT_ID,
        PROPERTY_FRIENDLY_NAME,
        PROPERTY_MEDIA_TYPE,
        PROPERTY_BUS_TYPE,
        PROPERTY_HEALTH_STATUS,
        PROPERTY_SIZE,
        PROPERTY_SPINDLE_SPEED,
    ];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WMI와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public PhysicalDiskProbe()
        : this(WmiClient.Instance, SystemClock.Instance)
    {
    }

    /// <summary>
    /// WMI 클라이언트와 시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    public PhysicalDiskProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => PhysicalDiskProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Storage;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_LOCAL_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var observedAt = _clock.UtcNow;
        var disks = _wmi.Query(WmiNamespaces.STORAGE, PHYSICAL_DISK_CLASS, PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (disks.Status != WmiQueryStatus.Success)
        {
            return Task.FromResult(CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(PHYSICAL_DISK_CLASS, disks)], observedAt));
        }

        if (disks.Rows.Count == 0)
        {
            return Task.FromResult(CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.EmptyIssue(PHYSICAL_DISK_CLASS)], observedAt));
        }

        var measurements = new List<Measurement>
        {
            new(PhysicalDiskProbeContract.DISK_COUNT, new IntegerValue(disks.Rows.Count), null, SOURCE_PREFIX + "Count", observedAt, MeasurementQuality.Observed),
        };

        var missingHealth = false;
        for (var index = 0; index < disks.Rows.Count; index++)
        {
            var row = disks.Rows[index];
            if (ProviderDiskId(WmiResultInterpreter.GetText(row, PROPERTY_OBJECT_ID)) is { } diskGuid)
            {
                measurements.Add(new Measurement(
                    PhysicalDiskProbeContract.DiskMeasurementName(index, PhysicalDiskProbeContract.FIELD_PROVIDER_DISK_ID),
                    new TextValue(diskGuid),
                    null,
                    SOURCE_PROVIDER_DISK_ID,
                    observedAt,
                    MeasurementQuality.Reported));
            }

            AddText(measurements, index, PhysicalDiskProbeContract.FIELD_DEVICE_ID, row, PROPERTY_DEVICE_ID, observedAt);
            AddText(measurements, index, PhysicalDiskProbeContract.FIELD_FRIENDLY_NAME, row, PROPERTY_FRIENDLY_NAME, observedAt);
            AddInteger(measurements, index, PhysicalDiskProbeContract.FIELD_MEDIA_TYPE, row, PROPERTY_MEDIA_TYPE, null, observedAt);
            AddInteger(measurements, index, PhysicalDiskProbeContract.FIELD_BUS_TYPE, row, PROPERTY_BUS_TYPE, null, observedAt);
            missingHealth |= !AddInteger(measurements, index, PhysicalDiskProbeContract.FIELD_HEALTH_STATUS, row, PROPERTY_HEALTH_STATUS, null, observedAt);
            AddInteger(measurements, index, PhysicalDiskProbeContract.FIELD_SIZE, row, PROPERTY_SIZE, PhysicalDiskProbeContract.UNIT_BYTES, observedAt);
            AddInteger(measurements, index, PhysicalDiskProbeContract.FIELD_SPINDLE_SPEED, row, PROPERTY_SPINDLE_SPEED, PhysicalDiskProbeContract.UNIT_RPM, observedAt);
        }

        IReadOnlyList<Issue> issues = missingHealth ? [new Issue(CannotVerifyReason.PartialData, ProbeStrings.Storage_MissingDiskHealth)] : [];
        var status = issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(CreateResult(context, status, measurements, issues, observedAt));
    }

    /// <summary>
    /// ObjectId에서 물리 디스크 GUID("PD:{...}"의 GUID)만 뽑는다. 없으면 null.
    /// </summary>
    private static string? ProviderDiskId(string? objectId)
    {
        if (objectId is null)
        {
            return null;
        }

        var match = PhysicalDiskGuidPattern().Match(objectId);
        return match.Success ? match.Groups[DISK_GUID_GROUP].Value.ToLowerInvariant() : null;
    }

    /// <summary>
    /// 문자열 속성이 있으면 디스크 측정값으로 추가한다.
    /// </summary>
    private static void AddText(List<Measurement> measurements, int index, string field, IReadOnlyDictionary<string, object?> row, string property, DateTimeOffset observedAt)
    {
        if (WmiResultInterpreter.GetText(row, property) is { } text)
        {
            measurements.Add(new Measurement(
                PhysicalDiskProbeContract.DiskMeasurementName(index, field), new TextValue(text), null, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
        }
    }

    /// <summary>
    /// 정수 속성이 있으면 디스크 측정값으로 추가한다. 값이 없으면 false.
    /// </summary>
    private static bool AddInteger(
        List<Measurement> measurements,
        int index,
        string field,
        IReadOnlyDictionary<string, object?> row,
        string property,
        string? unit,
        DateTimeOffset observedAt)
    {
        if (WmiResultInterpreter.GetInt64(row, property) is not { } value)
        {
            return false;
        }

        measurements.Add(new Measurement(
            PhysicalDiskProbeContract.DiskMeasurementName(index, field), new IntegerValue(value), unit, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
        return true;
    }

    /// <summary>
    /// 이 프로브의 결과를 만든다. 시작 시각·소요 시간은 실행 조율기가 덮어쓴다.
    /// </summary>
    private ProbeResult CreateResult(
        ScanContext context,
        ProbeStatus status,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<Issue> issues,
        DateTimeOffset startedAt)
    {
        return new ProbeResult(Id, status, measurements, issues, startedAt, TimeSpan.Zero, context.UserContext);
    }

    /// <summary>
    /// ObjectId 안의 ":PD:{GUID}"를 찾는 정규식.
    /// </summary>
    [GeneratedRegex(@":PD:(?<guid>\{[0-9A-Fa-f-]{36}\})", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex PhysicalDiskGuidPattern();
}
