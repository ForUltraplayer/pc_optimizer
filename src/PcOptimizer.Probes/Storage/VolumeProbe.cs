/**
 * @file    : VolumeProbe.cs
 * @author  : rudals252
 * @brief   : root\Microsoft\Windows\Storage의 MSFT_Volume 볼륨별 드라이브 문자·레이블·파일 시스템·종류·크기·남은 공간·상태와 시스템 드라이브 Windows.old 존재 여부(크기 측정 없음)를 수집하는 볼륨 프로브
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 볼륨 정보를 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="VolumeProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// MSFT_Volume 조회 실패·빈 결과는 Failed입니다. 크기 값이 빠진 고정 볼륨이나 시스템 드라이브 불명은 Partial입니다
/// (매체 없는 이동식·광학 드라이브의 크기 누락은 부분 수집으로 보지 않습니다).
/// Windows.old는 존재 여부만 확인하며 폴더 내용을 순회하거나 크기를 재지 않습니다(크기 집계는 파일 스캔 단계의 일).
/// </remarks>
public sealed class VolumeProbe : IProbe
{
    /// <summary>볼륨 WMI 클래스.</summary>
    public const string VOLUME_CLASS = "MSFT_Volume";

    /// <summary>Windows.old 폴더 이름.</summary>
    public const string WINDOWS_OLD_FOLDER = "Windows.old";

    /// <summary>WMI 제공자 타임아웃. 프로브 기본 타임아웃보다 짧게 두어 호출이 스스로 끝나게 한다.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(10);

    private const string PROPERTY_DRIVE_LETTER = "DriveLetter";
    private const string PROPERTY_LABEL = "FileSystemLabel";
    private const string PROPERTY_FILE_SYSTEM = "FileSystem";
    private const string PROPERTY_DRIVE_TYPE = "DriveType";
    private const string PROPERTY_SIZE = "Size";
    private const string PROPERTY_SIZE_REMAINING = "SizeRemaining";
    private const string PROPERTY_HEALTH_STATUS = "HealthStatus";
    private const string SOURCE_PREFIX = "WMI " + VOLUME_CLASS + ".";
    private const string SOURCE_SYSTEM_DRIVE = "Environment SystemDirectory";
    private const string SOURCE_WINDOWS_OLD = "Directory.Exists(<SystemDrive>\\" + WINDOWS_OLD_FOLDER + ")";
    private const string DRIVE_SUFFIX = ":";
    private const char NULL_DRIVE_LETTER = '\0';

    private static readonly string[] PROPERTIES =
    [
        PROPERTY_DRIVE_LETTER,
        PROPERTY_LABEL,
        PROPERTY_FILE_SYSTEM,
        PROPERTY_DRIVE_TYPE,
        PROPERTY_SIZE,
        PROPERTY_SIZE_REMAINING,
        PROPERTY_HEALTH_STATUS,
    ];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;
    private readonly Func<string?> _systemDrive;
    private readonly Func<string, bool> _directoryExists;

    /// <summary>
    /// 실제 WMI·파일 시스템과 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public VolumeProbe()
        : this(WmiClient.Instance, SystemClock.Instance, DefaultSystemDrive, Directory.Exists)
    {
    }

    /// <summary>
    /// 의존성을 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="systemDrive">시스템 드라이브 루트(예: "C:\")를 돌려주는 함수(모르면 null).</param>
    /// <param name="directoryExists">폴더 존재 확인 함수(조회 전용).</param>
    public VolumeProbe(IWmiClient wmi, IClock clock, Func<string?> systemDrive, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(systemDrive);
        ArgumentNullException.ThrowIfNull(directoryExists);
        _wmi = wmi;
        _clock = clock;
        _systemDrive = systemDrive;
        _directoryExists = directoryExists;
    }

    /// <inheritdoc />
    public string Id => VolumeProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Storage;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_LOCAL_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Collect(context, ct));
    }

    /// <summary>
    /// 시스템 폴더 경로의 루트(예: "C:\")를 돌려준다. 알 수 없으면 null.
    /// </summary>
    private static string? DefaultSystemDrive()
    {
        return Path.GetPathRoot(Environment.SystemDirectory);
    }

    /// <summary>
    /// 볼륨 정보와 Windows.old 존재 여부를 조회해 측정값과 Issue를 만든다.
    /// </summary>
    private ProbeResult Collect(ScanContext context, CancellationToken ct)
    {
        var observedAt = _clock.UtcNow;
        var volumes = _wmi.Query(WmiNamespaces.STORAGE, VOLUME_CLASS, PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (volumes.Status != WmiQueryStatus.Success)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(VOLUME_CLASS, volumes)], observedAt);
        }

        if (volumes.Rows.Count == 0)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.EmptyIssue(VOLUME_CLASS)], observedAt);
        }

        var measurements = new List<Measurement>
        {
            new(VolumeProbeContract.VOLUME_COUNT, new IntegerValue(volumes.Rows.Count), null, SOURCE_PREFIX + "Count", observedAt, MeasurementQuality.Observed),
        };
        var issues = new List<Issue>();

        var missingSize = false;
        for (var index = 0; index < volumes.Rows.Count; index++)
        {
            var row = volumes.Rows[index];
            if (DriveLetterOf(row) is { } letter)
            {
                Add(measurements, index, VolumeProbeContract.FIELD_DRIVE_LETTER, new TextValue(letter), null, PROPERTY_DRIVE_LETTER, observedAt);
            }

            AddText(measurements, index, VolumeProbeContract.FIELD_LABEL, row, PROPERTY_LABEL, observedAt);
            AddText(measurements, index, VolumeProbeContract.FIELD_FILE_SYSTEM, row, PROPERTY_FILE_SYSTEM, observedAt);
            AddInteger(measurements, index, VolumeProbeContract.FIELD_DRIVE_TYPE, row, PROPERTY_DRIVE_TYPE, null, observedAt);
            AddInteger(measurements, index, VolumeProbeContract.FIELD_HEALTH_STATUS, row, PROPERTY_HEALTH_STATUS, null, observedAt);
            var hasSize = AddInteger(measurements, index, VolumeProbeContract.FIELD_SIZE, row, PROPERTY_SIZE, VolumeProbeContract.UNIT_BYTES, observedAt);
            var hasRemaining = AddInteger(
                measurements, index, VolumeProbeContract.FIELD_SIZE_REMAINING, row, PROPERTY_SIZE_REMAINING, VolumeProbeContract.UNIT_BYTES, observedAt);
            var isFixed = WmiResultInterpreter.GetInt64(row, PROPERTY_DRIVE_TYPE) == VolumeProbeContract.DRIVE_TYPE_FIXED;
            missingSize |= isFixed && (!hasSize || !hasRemaining);
        }

        if (missingSize)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Storage_MissingVolumeValues));
        }

        ReadWindowsOld(measurements, issues, observedAt);
        var status = issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return CreateResult(context, status, measurements, issues, observedAt);
    }

    /// <summary>
    /// 시스템 드라이브의 Windows.old 폴더 존재 여부만 확인한다(내용 순회·크기 측정 없음).
    /// </summary>
    private void ReadWindowsOld(List<Measurement> measurements, List<Issue> issues, DateTimeOffset observedAt)
    {
        var root = _systemDrive();
        if (string.IsNullOrWhiteSpace(root))
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Storage_SystemDriveUnknown));
            return;
        }

        var drive = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        measurements.Add(new Measurement(VolumeProbeContract.SYSTEM_DRIVE, new TextValue(drive), null, SOURCE_SYSTEM_DRIVE, observedAt, MeasurementQuality.Observed));
        measurements.Add(new Measurement(
            VolumeProbeContract.WINDOWS_OLD_EXISTS,
            new BooleanValue(_directoryExists(Path.Combine(root, WINDOWS_OLD_FOLDER))),
            null,
            SOURCE_WINDOWS_OLD,
            observedAt,
            MeasurementQuality.Observed));
    }

    /// <summary>
    /// DriveLetter 속성(char, 없으면 NUL)을 "C" 같은 문자열로 읽는다. 없으면 null.
    /// </summary>
    private static string? DriveLetterOf(IReadOnlyDictionary<string, object?> row)
    {
        if (!row.TryGetValue(PROPERTY_DRIVE_LETTER, out var raw))
        {
            return null;
        }

        return raw switch
        {
            char letter when letter != NULL_DRIVE_LETTER && char.IsLetter(letter) => letter.ToString().ToUpperInvariant(),
            string text when !string.IsNullOrWhiteSpace(text) => text.Trim().TrimEnd(DRIVE_SUFFIX[0]).ToUpperInvariant(),
            _ => null,
        };
    }

    /// <summary>
    /// 문자열 속성이 있으면 볼륨 측정값으로 추가한다.
    /// </summary>
    private static void AddText(List<Measurement> measurements, int index, string field, IReadOnlyDictionary<string, object?> row, string property, DateTimeOffset observedAt)
    {
        if (WmiResultInterpreter.GetText(row, property) is { } text)
        {
            Add(measurements, index, field, new TextValue(text), null, property, observedAt);
        }
    }

    /// <summary>
    /// 정수 속성이 있으면 볼륨 측정값으로 추가한다. 값이 없으면 false.
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

        Add(measurements, index, field, new IntegerValue(value), unit, property, observedAt);
        return true;
    }

    /// <summary>
    /// 볼륨 필드 측정값을 추가한다.
    /// </summary>
    private static void Add(
        List<Measurement> measurements,
        int index,
        string field,
        MeasurementValue value,
        string? unit,
        string property,
        DateTimeOffset observedAt)
    {
        measurements.Add(new Measurement(
            VolumeProbeContract.VolumeMeasurementName(index, field), value, unit, SOURCE_PREFIX + property, observedAt, MeasurementQuality.Reported));
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
}
