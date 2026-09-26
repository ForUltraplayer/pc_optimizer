/**
 * @file    : StartupItemsProbeTests.cs
 * @author  : rudals252
 * @brief   : 시작 프로그램 프로브의 Run/RunOnce·64/32비트 보기 기록, StartupApproved 정확한 이름 연결과 원시 값(0x02·0x03·기타·없음·형식 다름) 기록, 임시 폴더 항목 열거, 접근 거부 시 부분 수집을 가짜 레지스트리로 검증
 */

// 기본 패키지
using System.IO;
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="StartupItemsProbe"/>를 가짜 레지스트리와 테스트별 임시 폴더로 검증합니다. 실제 레지스트리·시작프로그램 폴더를 읽지 않습니다.
/// </summary>
public sealed class StartupItemsProbeTests : IDisposable
{
    private const string KIND_STRING = "String";
    private const string KIND_DWORD = "DWord";

    private static readonly ScanContext CONTEXT = new(Guid.NewGuid(), EngineTestData.USER, false, FakeProbe.OBSERVED_AT);

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "pcoptimizer-startup-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 테스트 임시 폴더를 지운다(테스트가 만든 폴더만).
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    /// 문자열 값 항목.
    /// </summary>
    private static RegistryValueEntry Command(string name, string command)
    {
        return new RegistryValueEntry(name, KIND_STRING, command, null);
    }

    /// <summary>
    /// StartupApproved 이진 값 항목.
    /// </summary>
    private static RegistryValueEntry Approved(string name, params byte[] bytes)
    {
        return new RegistryValueEntry(name, StartupItemsProbeContract.KIND_BINARY, null, bytes);
    }

    /// <summary>
    /// 폴더 없이(레지스트리만) 프로브를 실행한다.
    /// </summary>
    private static Task<ProbeResult> RunAsync(FakeRegistryReader registry, params StartupFolderLocation[] folders)
    {
        return new StartupItemsProbe(registry, new FakeClock { UtcNow = FakeProbe.OBSERVED_AT }, folders).RunAsync(CONTEXT, CancellationToken.None);
    }

    /// <summary>
    /// 이름·출처가 같은 항목의 인덱스를 찾는다.
    /// </summary>
    private static int IndexOf(ProbeResult result, string name, string source)
    {
        var count = (int)((IntegerValue)result.Measurements.Single(m => m.Name == StartupItemsProbeContract.ITEM_COUNT).Value).Value;
        for (var index = 0; index < count; index++)
        {
            if (Field(result, index, StartupItemsProbeContract.FIELD_NAME) == new TextValue(name)
                && Field(result, index, StartupItemsProbeContract.FIELD_SOURCE) == new TextValue(source))
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException($"항목 {source}:{name} 없음");
    }

    /// <summary>
    /// 항목 필드 측정값(없으면 null).
    /// </summary>
    private static MeasurementValue? Field(ProbeResult result, int index, string field)
    {
        return result.Measurements.SingleOrDefault(m => m.Name == StartupItemsProbeContract.ItemMeasurementName(index, field))?.Value;
    }

    /// <summary>
    /// 읽지 못한 위치 목록.
    /// </summary>
    private static IReadOnlyList<string> Unreadable(ProbeResult result)
    {
        return ((TextListValue)result.Measurements.Single(m => m.Name == StartupItemsProbeContract.UNREADABLE_SOURCES).Value).Values;
    }

    /// <summary>시작 항목은 HKCU·HKLM과 사용자 폴더를 읽는 사용자 범위이며 관리자 권한·네트워크가 필요 없다.</summary>
    [Fact]
    public void 사용자_범위_프로브다()
    {
        var probe = new StartupItemsProbe(new FakeRegistryReader(), new FakeClock(), []);

        Assert.Equal(ProbeScope.User, probe.Scope);
        Assert.Equal(FindingCategory.Startup, probe.Category);
        Assert.False(probe.RequiresElevation);
        Assert.False(probe.RequiresNetwork);
    }

    /// <summary>StartupApproved 첫 바이트(0x02·0x03·기타)를 원시 값으로 기록하고 규칙이 활성/비활성/알 수 없음으로 해석한다.</summary>
    [Fact]
    public async Task StartupApproved_원시_값을_기록한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY,
                Command("Chat", @"C:\Apps\chat.exe"), Command("Updater", @"C:\Apps\upd.exe"), Command("Odd", @"C:\Apps\odd.exe"))
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN_SUB_KEY,
                Approved("Chat", 0x02, 0, 0, 0), Approved("Updater", 0x03, 0, 0, 0), Approved("Odd", 0x06, 0, 0, 0));

        var result = await RunAsync(registry);

        Assert.Equal(ProbeStatus.Success, result.Status);
        var chat = IndexOf(result, "Chat", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_FOUND), Field(result, chat, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Equal(new TextValue(StartupItemsProbeContract.KIND_BINARY), Field(result, chat, StartupItemsProbeContract.FIELD_APPROVED_KIND));
        Assert.Equal(new IntegerValue(0x02), Field(result, chat, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        Assert.Equal(new TextValue(@"C:\Apps\chat.exe"), Field(result, chat, StartupItemsProbeContract.FIELD_COMMAND));
        Assert.Equal(new IntegerValue(0x03), Field(result, IndexOf(result, "Updater", StartupItemsProbeContract.SOURCE_HKCU_RUN), StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        Assert.Equal(new IntegerValue(0x06), Field(result, IndexOf(result, "Odd", StartupItemsProbeContract.SOURCE_HKCU_RUN), StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));

        var findings = new StartupItemsRule().Evaluate(EngineTestData.CreateSnapshot(result));
        Assert.Contains(findings, f => f.Title.Contains("Chat", StringComparison.Ordinal) && f.Title.EndsWith(CoreStrings.Startup_State_Enabled, StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Title.Contains("Updater", StringComparison.Ordinal) && f.Title.EndsWith(CoreStrings.Startup_State_Disabled, StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Title.Contains("Odd", StringComparison.Ordinal) && f.Title.EndsWith(CoreStrings.Startup_State_Unknown, StringComparison.Ordinal));
    }

    /// <summary>StartupApproved 값이 없거나(missing) 이진이 아니거나 비어 있으면 첫 바이트를 기록하지 않는다.</summary>
    [Fact]
    public async Task StartupApproved_없음과_형식_다름을_구분한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY,
                Command("NoEntry", "a.exe"), Command("WrongType", "b.exe"), Command("Empty", "c.exe"))
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN_SUB_KEY,
                new RegistryValueEntry("WrongType", KIND_DWORD, null, null), Approved("Empty"));

        var result = await RunAsync(registry);

        var noEntry = IndexOf(result, "NoEntry", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_MISSING), Field(result, noEntry, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Null(Field(result, noEntry, StartupItemsProbeContract.FIELD_APPROVED_KIND));
        var wrongType = IndexOf(result, "WrongType", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        Assert.Equal(new TextValue(KIND_DWORD), Field(result, wrongType, StartupItemsProbeContract.FIELD_APPROVED_KIND));
        Assert.Null(Field(result, wrongType, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        var empty = IndexOf(result, "Empty", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        Assert.Equal(new TextValue(StartupItemsProbeContract.KIND_BINARY), Field(result, empty, StartupItemsProbeContract.FIELD_APPROVED_KIND));
        Assert.Null(Field(result, empty, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
    }

    /// <summary>StartupApproved 연결은 대소문자만 무시한 정확한 이름 일치이며 접두사·부분·확장자 차이는 연결하지 않는다.</summary>
    [Fact]
    public async Task 이름이_정확히_같을_때만_연결한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY,
                Command("Chat", "chat.exe"), Command("OneDrive", "od.exe"))
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN_SUB_KEY,
                Approved("Cha", 0x02), Approved("Chat Helper", 0x02), Approved("chat.exe", 0x02), Approved("onedrive", 0x03));

        var result = await RunAsync(registry);

        var chat = IndexOf(result, "Chat", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_MISSING), Field(result, chat, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Null(Field(result, chat, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        var oneDrive = IndexOf(result, "OneDrive", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        Assert.Equal(new IntegerValue(0x03), Field(result, oneDrive, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
    }

    /// <summary>HKLM은 64비트·32비트 보기를 따로 읽어 보기를 기록하고, 각각 StartupApproved Run·Run32와 연결한다.</summary>
    [Fact]
    public async Task HKLM_64_32비트_보기를_기록한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.LocalMachine, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY, Command("Sync", "sync64.exe"))
            .WithKeyValues(RegistryRoot.LocalMachine, RegistryView.Registry32, StartupItemsProbe.RUN_SUB_KEY, Command("Sync", "sync32.exe"))
            .WithKeyValues(RegistryRoot.LocalMachine, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN_SUB_KEY, Approved("Sync", 0x02))
            .WithKeyValues(RegistryRoot.LocalMachine, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN32_SUB_KEY, Approved("Sync", 0x03));

        var result = await RunAsync(registry);

        var sync64 = IndexOf(result, "Sync", StartupItemsProbeContract.SOURCE_HKLM64_RUN);
        var sync32 = IndexOf(result, "Sync", StartupItemsProbeContract.SOURCE_HKLM32_RUN);
        Assert.Equal(new TextValue(nameof(RegistryView.Registry64)), Field(result, sync64, StartupItemsProbeContract.FIELD_REGISTRY_VIEW));
        Assert.Equal(new TextValue(nameof(RegistryView.Registry32)), Field(result, sync32, StartupItemsProbeContract.FIELD_REGISTRY_VIEW));
        Assert.Equal(new TextValue("sync64.exe"), Field(result, sync64, StartupItemsProbeContract.FIELD_COMMAND));
        Assert.Equal(new TextValue("sync32.exe"), Field(result, sync32, StartupItemsProbeContract.FIELD_COMMAND));
        Assert.Equal(new IntegerValue(0x02), Field(result, sync64, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        Assert.Equal(new IntegerValue(0x03), Field(result, sync32, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
    }

    /// <summary>RunOnce(HKCU·HKLM 64/32) 항목도 수집하며 StartupApproved 관리 대상이 아니라고 기록한다(Run 값과 연결하지 않음).</summary>
    [Fact]
    public async Task RunOnce를_포함한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_ONCE_SUB_KEY, Command("Cleanup", "cleanup.exe"))
            .WithKeyValues(RegistryRoot.LocalMachine, RegistryView.Registry32, StartupItemsProbe.RUN_ONCE_SUB_KEY, Command("Setup", "setup.exe"))
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN_SUB_KEY, Approved("Cleanup", 0x02));

        var result = await RunAsync(registry);

        var cleanup = IndexOf(result, "Cleanup", StartupItemsProbeContract.SOURCE_HKCU_RUN_ONCE);
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_NOT_TRACKED), Field(result, cleanup, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Null(Field(result, cleanup, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        IndexOf(result, "Setup", StartupItemsProbeContract.SOURCE_HKLM32_RUN_ONCE);
        Assert.DoesNotContain(registry.KeyReads, key => key.EndsWith(StartupItemsProbe.APPROVED_RUN_SUB_KEY, StringComparison.Ordinal));
    }

    /// <summary>폴더는 최상위 .lnk/.exe/.bat/.cmd/.url만 이름순으로 열거하고(하위 폴더·기타 확장자 제외), 파일 이름으로 StartupFolder 값과 연결한다.</summary>
    [Fact]
    public async Task 시작프로그램_폴더_항목을_열거한다()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_tempRoot, "Startup")).FullName;
        foreach (var file in new[] { "b.EXE", "a.lnk", "notes.txt", "desktop.ini", "run.cmd" })
        {
            await File.WriteAllTextAsync(Path.Combine(folder, file), string.Empty);
        }

        var nested = Directory.CreateDirectory(Path.Combine(folder, "nested")).FullName;
        await File.WriteAllTextAsync(Path.Combine(nested, "deep.lnk"), string.Empty);
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.APPROVED_FOLDER_SUB_KEY, Approved("A.LNK", 0x03));

        var result = await RunAsync(registry, new StartupFolderLocation(StartupItemsProbeContract.SOURCE_USER_FOLDER, "Startup", folder, RegistryRoot.CurrentUser));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new IntegerValue(3), result.Measurements.Single(m => m.Name == StartupItemsProbeContract.ITEM_COUNT).Value);
        Assert.Equal(new TextValue("a.lnk"), Field(result, 0, StartupItemsProbeContract.FIELD_NAME));
        Assert.Equal(new TextValue("b.EXE"), Field(result, 1, StartupItemsProbeContract.FIELD_NAME));
        Assert.Equal(new TextValue("run.cmd"), Field(result, 2, StartupItemsProbeContract.FIELD_NAME));
        Assert.Equal(new TextValue(Path.Combine(folder, "a.lnk")), Field(result, 0, StartupItemsProbeContract.FIELD_COMMAND));
        Assert.Equal(new TextValue("Startup"), Field(result, 0, StartupItemsProbeContract.FIELD_LOCATION));
        Assert.Equal(new IntegerValue(0x03), Field(result, 0, StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE));
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_MISSING), Field(result, 1, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Null(Field(result, 0, StartupItemsProbeContract.FIELD_REGISTRY_VIEW));
    }

    /// <summary>없는 폴더와 없는 키는 항목 없음(성공)이며 실패로 바꾸지 않는다.</summary>
    [Fact]
    public async Task 없는_폴더와_키는_항목_없음이다()
    {
        var result = await RunAsync(
            new FakeRegistryReader(),
            new StartupFolderLocation(StartupItemsProbeContract.SOURCE_COMMON_FOLDER, "CommonStartup", Path.Combine(_tempRoot, "missing"), RegistryRoot.LocalMachine));

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal(new IntegerValue(0), result.Measurements.Single(m => m.Name == StartupItemsProbeContract.ITEM_COUNT).Value);
        Assert.Empty(Unreadable(result));
        Assert.Empty(result.Issues);
    }

    /// <summary>한 위치의 접근 거부는 그 위치만 AccessDenied Issue(Partial)로 알리고 다른 위치의 항목은 그대로 수집한다.</summary>
    [Fact]
    public async Task 접근_거부는_부분_수집이고_다른_위치는_유지한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY, Command("Chat", "chat.exe"))
            .WithKey(RegistryRoot.LocalMachine, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY,
                new RegistryKeyReading(RegistryReadStatus.AccessDenied, [], "SecurityException"))
            .WithKeyValues(RegistryRoot.LocalMachine, RegistryView.Registry32, StartupItemsProbe.RUN_SUB_KEY, Command("Sync", "sync32.exe"));
        var deniedFolder = new StartupFolderLocation(StartupItemsProbeContract.SOURCE_COMMON_FOLDER, "CommonStartup", @"C:\denied", RegistryRoot.LocalMachine);

        var result = await new StartupItemsProbe(
            registry, new FakeClock(), [deniedFolder], _ => throw new UnauthorizedAccessException("denied"))
            .RunAsync(CONTEXT, CancellationToken.None);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal(CannotVerifyReason.AccessDenied, issue.Reason));
        Assert.All(result.Issues, issue => Assert.DoesNotContain("denied", issue.Summary, StringComparison.Ordinal));
        Assert.Equal([StartupItemsProbeContract.SOURCE_HKLM64_RUN, StartupItemsProbeContract.SOURCE_COMMON_FOLDER], Unreadable(result));
        IndexOf(result, "Chat", StartupItemsProbeContract.SOURCE_HKCU_RUN);
        IndexOf(result, "Sync", StartupItemsProbeContract.SOURCE_HKLM32_RUN);
    }

    /// <summary>StartupApproved 키 접근 거부는 항목을 유지하되 활성 여부를 unreadable로 기록하고 Partial(AccessDenied)이다.</summary>
    [Fact]
    public async Task StartupApproved_접근_거부는_항목을_유지한다()
    {
        var registry = new FakeRegistryReader()
            .WithKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY, Command("Chat", "chat.exe"), Command("Mail", "mail.exe"))
            .WithKey(RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.APPROVED_RUN_SUB_KEY,
                new RegistryKeyReading(RegistryReadStatus.AccessDenied, [], "UnauthorizedAccessException"));

        var result = await RunAsync(registry);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Equal(CannotVerifyReason.AccessDenied, Assert.Single(result.Issues).Reason);
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_UNREADABLE), Field(result, 0, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Equal(new TextValue(StartupItemsProbeContract.LOOKUP_UNREADABLE), Field(result, 1, StartupItemsProbeContract.FIELD_APPROVED_LOOKUP));
        Assert.Empty(Unreadable(result));
    }

    /// <summary>모든 위치를 읽지 못하면 Failed이며 빈 성공으로 바꾸지 않는다. 폴더 위치를 모르면 PartialData다.</summary>
    [Fact]
    public async Task 모든_위치를_읽지_못하면_실패다()
    {
        var registry = new FakeRegistryReader();
        foreach (var (root, view, key) in new[]
        {
            (RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY),
            (RegistryRoot.CurrentUser, RegistryView.Registry64, StartupItemsProbe.RUN_ONCE_SUB_KEY),
            (RegistryRoot.LocalMachine, RegistryView.Registry64, StartupItemsProbe.RUN_SUB_KEY),
            (RegistryRoot.LocalMachine, RegistryView.Registry64, StartupItemsProbe.RUN_ONCE_SUB_KEY),
            (RegistryRoot.LocalMachine, RegistryView.Registry32, StartupItemsProbe.RUN_SUB_KEY),
            (RegistryRoot.LocalMachine, RegistryView.Registry32, StartupItemsProbe.RUN_ONCE_SUB_KEY),
        })
        {
            registry.WithKey(root, view, key, new RegistryKeyReading(RegistryReadStatus.Error, [], "IOException"));
        }

        var result = await RunAsync(registry, new StartupFolderLocation(StartupItemsProbeContract.SOURCE_USER_FOLDER, "Startup", null, RegistryRoot.CurrentUser));

        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Equal(7, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.Reason == CannotVerifyReason.PartialData);
        Assert.Equal(7, Unreadable(result).Count);
    }
}
