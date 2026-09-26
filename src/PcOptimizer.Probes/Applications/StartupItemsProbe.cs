/**
 * @file    : StartupItemsProbe.cs
 * @author  : rudals252
 * @brief   : HKCU·HKLM(64/32비트 보기) Run·RunOnce 값과 사용자·공용 시작프로그램 폴더 항목을 읽고, 같은 이름의 StartupApproved 원시 값을 연결해 수집하는 사용자 범위 시작 프로그램 프로브(조회 전용)
 */

// 기본 패키지
using System.Security;
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 시작 프로그램 항목을 수집하는 프로브입니다. 판정은 하지 않으며 측정 이름은 <see cref="StartupItemsProbeContract"/>를 따릅니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>레지스트리: HKCU Run·RunOnce, HKLM Run·RunOnce를 64비트·32비트(WOW6432Node) 보기로 각각 읽고 보기를 항목마다 기록합니다.</item>
/// <item>폴더: 사용자·모든 사용자 시작프로그램 폴더의 최상위 .lnk/.exe/.bat/.cmd/.url 파일 이름만 열거합니다. 바로 가기를 해석하거나 실행하지 않습니다.</item>
/// <item>활성 상태: Explorer\StartupApproved의 Run(HKCU·HKLM 64비트)·Run32(HKLM 32비트)·StartupFolder 값 중 이름이 대소문자 무시로
/// 정확히 같은 값만 연결하고, 형식과 첫 바이트 원시 값을 기록합니다(해석은 규칙). RunOnce는 StartupApproved 관리 대상이 아닙니다.</item>
/// <item>예약 작업·서비스·패키지 앱 StartupTask는 읽지 않습니다.</item>
/// <item>키가 없으면 항목 없음(성공), 접근 거부·오류는 그 위치만 Issue로 알리고 다른 위치는 계속 읽습니다(Partial). 모든 위치를 못 읽으면 Failed.</item>
/// </list>
/// HKCU와 사용자 폴더를 읽으므로 사용자 범위입니다. 레지스트리·파일에 쓰지 않습니다.
/// </remarks>
public sealed class StartupItemsProbe : IProbe
{
    /// <summary>Run 키.</summary>
    public const string RUN_SUB_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>RunOnce 키.</summary>
    public const string RUN_ONCE_SUB_KEY = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    /// <summary>StartupApproved Run 키(HKCU Run, HKLM 64비트 Run 항목용).</summary>
    public const string APPROVED_RUN_SUB_KEY = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>StartupApproved Run32 키(HKLM 32비트 Run 항목용).</summary>
    public const string APPROVED_RUN32_SUB_KEY = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";

    /// <summary>StartupApproved StartupFolder 키(시작프로그램 폴더 항목용).</summary>
    public const string APPROVED_FOLDER_SUB_KEY = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    /// <summary>폴더에서 시작 항목으로 보는 확장자.</summary>
    public static readonly IReadOnlySet<string> STARTUP_FILE_EXTENSIONS =
        new HashSet<string>([".lnk", ".exe", ".bat", ".cmd", ".url"], StringComparer.OrdinalIgnoreCase);

    private const string SOURCE_REGISTRY_PREFIX = "Registry ";
    private const string SOURCE_FOLDER_PREFIX = "Folder ";
    private const string SOURCE_COUNT = "StartupItemsProbe";
    private const int FIRST_BYTE_INDEX = 0;

    /// <summary>읽을 레지스트리 위치(읽기 순서 = 항목 순서).</summary>
    private static readonly RegistrySource[] REGISTRY_SOURCES =
    [
        new(StartupItemsProbeContract.SOURCE_HKCU_RUN, RegistryRoot.CurrentUser, RegistryView.Registry64, RUN_SUB_KEY,
            new StartupApprovedKey(RegistryRoot.CurrentUser, APPROVED_RUN_SUB_KEY)),
        new(StartupItemsProbeContract.SOURCE_HKCU_RUN_ONCE, RegistryRoot.CurrentUser, RegistryView.Registry64, RUN_ONCE_SUB_KEY, null),
        new(StartupItemsProbeContract.SOURCE_HKLM64_RUN, RegistryRoot.LocalMachine, RegistryView.Registry64, RUN_SUB_KEY,
            new StartupApprovedKey(RegistryRoot.LocalMachine, APPROVED_RUN_SUB_KEY)),
        new(StartupItemsProbeContract.SOURCE_HKLM64_RUN_ONCE, RegistryRoot.LocalMachine, RegistryView.Registry64, RUN_ONCE_SUB_KEY, null),
        new(StartupItemsProbeContract.SOURCE_HKLM32_RUN, RegistryRoot.LocalMachine, RegistryView.Registry32, RUN_SUB_KEY,
            new StartupApprovedKey(RegistryRoot.LocalMachine, APPROVED_RUN32_SUB_KEY)),
        new(StartupItemsProbeContract.SOURCE_HKLM32_RUN_ONCE, RegistryRoot.LocalMachine, RegistryView.Registry32, RUN_ONCE_SUB_KEY, null),
    ];

    private readonly IRegistryReader _registry;
    private readonly IClock _clock;
    private readonly IReadOnlyList<StartupFolderLocation> _folders;
    private readonly Func<string, IEnumerable<string>> _enumerateFiles;

    /// <summary>
    /// 실제 레지스트리·시작프로그램 폴더와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public StartupItemsProbe()
        : this(Win32RegistryReader.Instance, SystemClock.Instance, DefaultFolders())
    {
    }

    /// <summary>
    /// 레지스트리·시계·폴더 목록을 지정해 프로브를 만듭니다(폴더는 실제 파일 시스템으로 열거).
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="folders">열거할 시작프로그램 폴더.</param>
    public StartupItemsProbe(IRegistryReader registry, IClock clock, IReadOnlyList<StartupFolderLocation> folders)
        : this(registry, clock, folders, EnumerateTopLevelFiles)
    {
    }

    /// <summary>
    /// 모든 의존성을 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="folders">열거할 시작프로그램 폴더.</param>
    /// <param name="enumerateFiles">폴더의 최상위 파일 경로를 열거하는 함수(조회 전용).</param>
    public StartupItemsProbe(
        IRegistryReader registry,
        IClock clock,
        IReadOnlyList<StartupFolderLocation> folders,
        Func<string, IEnumerable<string>> enumerateFiles)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(enumerateFiles);
        _registry = registry;
        _clock = clock;
        _folders = [.. folders];
        _enumerateFiles = enumerateFiles;
    }

    /// <inheritdoc />
    public string Id => StartupItemsProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Startup;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.User;

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
    /// 현재 사용자·모든 사용자 시작프로그램 폴더 위치를 돌려준다(폴더가 없어도 경로는 돌려받는다).
    /// </summary>
    private static StartupFolderLocation[] DefaultFolders()
    {
        return
        [
            new(StartupItemsProbeContract.SOURCE_USER_FOLDER, nameof(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.Startup, Environment.SpecialFolderOption.DoNotVerify), RegistryRoot.CurrentUser),
            new(StartupItemsProbeContract.SOURCE_COMMON_FOLDER, nameof(Environment.SpecialFolder.CommonStartup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup, Environment.SpecialFolderOption.DoNotVerify), RegistryRoot.LocalMachine),
        ];
    }

    /// <summary>
    /// 폴더의 최상위 파일만 열거한다(하위 폴더를 따라가지 않음).
    /// </summary>
    private static IEnumerable<string> EnumerateTopLevelFiles(string path)
    {
        return Directory.EnumerateFiles(path);
    }

    /// <summary>
    /// 모든 위치를 읽고 StartupApproved를 연결해 측정값·Issue를 만든다.
    /// </summary>
    private ProbeResult Collect(ScanContext context, CancellationToken ct)
    {
        var observedAt = _clock.UtcNow;
        var items = new List<CollectedItem>();
        var issues = new List<Issue>();
        var unreadable = new List<string>();

        foreach (var source in REGISTRY_SOURCES)
        {
            ct.ThrowIfCancellationRequested();
            ReadRegistrySource(source, items, issues, unreadable);
        }

        foreach (var folder in _folders)
        {
            ct.ThrowIfCancellationRequested();
            ReadFolder(folder, items, issues, unreadable);
        }

        var measurements = new List<Measurement>
        {
            new(StartupItemsProbeContract.ITEM_COUNT, new IntegerValue(items.Count), null, SOURCE_COUNT, observedAt, MeasurementQuality.Observed),
            new(StartupItemsProbeContract.UNREADABLE_SOURCES, new TextListValue(unreadable), null, SOURCE_COUNT, observedAt, MeasurementQuality.Observed),
        };

        var approved = new StartupApprovedReader(_registry, issues);
        for (var index = 0; index < items.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            AddItemMeasurements(index, items[index], approved, measurements, observedAt);
        }

        var status = unreadable.Count == REGISTRY_SOURCES.Length + _folders.Count
            ? ProbeStatus.Failed
            : issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext);
    }

    /// <summary>
    /// 레지스트리 위치 하나의 값을 항목으로 모은다. 키가 없으면 항목 없음, 읽기 실패는 Issue.
    /// </summary>
    private void ReadRegistrySource(RegistrySource source, List<CollectedItem> items, List<Issue> issues, List<string> unreadable)
    {
        var location = StartupApprovedReader.HiveName(source.Root) + @"\" + source.SubKey;
        var reading = _registry.ReadKeyValues(source.Root, source.View, source.SubKey);
        switch (reading.Status)
        {
            case RegistryReadStatus.Found:
                foreach (var value in reading.Values)
                {
                    items.Add(new CollectedItem(
                        value.Name,
                        source.SourceCode,
                        location,
                        SOURCE_REGISTRY_PREFIX + location + " (" + source.View + ")",
                        source.View.ToString(),
                        value.Kind,
                        value.Text,
                        source.Approved));
                }

                break;
            case RegistryReadStatus.KeyMissing:
            case RegistryReadStatus.ValueMissing:
                break;
            case RegistryReadStatus.AccessDenied:
                issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.Startup_SourceAccessDenied, location + " (" + source.View + ")")));
                unreadable.Add(source.SourceCode);
                break;
            case RegistryReadStatus.Error:
            default:
                issues.Add(new Issue(
                    CannotVerifyReason.ProbeError,
                    ProbeText.Format(ProbeStrings.Startup_SourceError, location + " (" + source.View + ")", reading.ErrorCode ?? string.Empty)));
                unreadable.Add(source.SourceCode);
                break;
        }
    }

    /// <summary>
    /// 시작프로그램 폴더의 최상위 시작 파일을 이름순으로 모은다. 폴더가 없으면 항목 없음, 위치 불명·읽기 실패는 Issue.
    /// </summary>
    private void ReadFolder(StartupFolderLocation folder, List<CollectedItem> items, List<Issue> issues, List<string> unreadable)
    {
        if (string.IsNullOrWhiteSpace(folder.Path))
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Startup_FolderUnknown, folder.LocationName)));
            unreadable.Add(folder.SourceCode);
            return;
        }

        List<string> files;
        try
        {
            files = [.. _enumerateFiles(folder.Path)
                .Where(file => STARTUP_FILE_EXTENSIONS.Contains(Path.GetExtension(file)))
                .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)];
        }
        catch (DirectoryNotFoundException)
        {
            // 폴더가 없으면 등록 항목이 없는 것이다(읽기 실패 아님).
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.Startup_SourceAccessDenied, folder.LocationName)));
            unreadable.Add(folder.SourceCode);
            return;
        }
        catch (IOException ex)
        {
            issues.Add(new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Startup_SourceError, folder.LocationName, ex.GetType().Name)));
            unreadable.Add(folder.SourceCode);
            return;
        }

        var approved = new StartupApprovedKey(folder.ApprovedRoot, APPROVED_FOLDER_SUB_KEY);
        foreach (var file in files)
        {
            items.Add(new CollectedItem(
                Path.GetFileName(file),
                folder.SourceCode,
                folder.LocationName,
                SOURCE_FOLDER_PREFIX + folder.LocationName,
                RegistryView: null,
                ValueKind: null,
                Command: file,
                approved));
        }
    }

    /// <summary>
    /// 항목 하나의 측정값을 추가한다. StartupApproved는 이름이 정확히 같은 값만 연결하고 원시 형식·첫 바이트를 기록한다.
    /// </summary>
    private static void AddItemMeasurements(
        int index,
        CollectedItem item,
        StartupApprovedReader approved,
        List<Measurement> measurements,
        DateTimeOffset observedAt)
    {
        void Add(string field, MeasurementValue value, string source) => measurements.Add(new Measurement(
            StartupItemsProbeContract.ItemMeasurementName(index, field), value, null, source, observedAt, MeasurementQuality.Observed));

        Add(StartupItemsProbeContract.FIELD_NAME, new TextValue(item.Name), item.Source);
        Add(StartupItemsProbeContract.FIELD_SOURCE, new TextValue(item.SourceCode), item.Source);
        Add(StartupItemsProbeContract.FIELD_LOCATION, new TextValue(item.Location), item.Source);
        if (item.RegistryView is { } view)
        {
            Add(StartupItemsProbeContract.FIELD_REGISTRY_VIEW, new TextValue(view), item.Source);
        }

        if (item.ValueKind is { } kind)
        {
            Add(StartupItemsProbeContract.FIELD_VALUE_KIND, new TextValue(kind), item.Source);
        }

        if (item.Command is { } command)
        {
            Add(StartupItemsProbeContract.FIELD_COMMAND, new TextValue(command), item.Source);
        }

        if (item.Approved is not { } approvedKey)
        {
            Add(StartupItemsProbeContract.FIELD_APPROVED_LOOKUP, new TextValue(StartupItemsProbeContract.LOOKUP_NOT_TRACKED), item.Source);
            return;
        }

        var approvedSource = SOURCE_REGISTRY_PREFIX + StartupApprovedReader.HiveName(approvedKey.Root) + @"\" + approvedKey.SubKey;
        var (lookup, entry) = approved.Find(approvedKey, item.Name);
        Add(StartupItemsProbeContract.FIELD_APPROVED_LOOKUP, new TextValue(lookup), approvedSource);
        if (entry is null)
        {
            return;
        }

        Add(StartupItemsProbeContract.FIELD_APPROVED_KIND, new TextValue(entry.Kind), approvedSource);
        if (entry.Binary is { Count: > 0 } bytes)
        {
            Add(StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE, new IntegerValue(bytes[FIRST_BYTE_INDEX]), approvedSource);
        }
    }

    /// <summary>
    /// 읽을 레지스트리 위치 하나입니다.
    /// </summary>
    private sealed record RegistrySource(string SourceCode, RegistryRoot Root, RegistryView View, string SubKey, StartupApprovedKey? Approved);

    /// <summary>
    /// 수집한 항목 하나입니다.
    /// </summary>
    private sealed record CollectedItem(
        string Name,
        string SourceCode,
        string Location,
        string Source,
        string? RegistryView,
        string? ValueKind,
        string? Command,
        StartupApprovedKey? Approved);
}
