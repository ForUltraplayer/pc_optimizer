/**
 * @file    : StartupActionTests.cs
 * @author  : rudals252
 * @brief   : 시작 등록의 개별 확인·원문 보존·외부 변경·세션·복구 경계 검증
 */
using System.Text;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Startup;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>대역 레지스트리와 실제 조율기만 사용하며 사용자 시작 항목은 변경하지 않습니다.</summary>
public sealed class StartupActionTests
{
    private static readonly ActionSession Session = ActionCenterTests.Session;
    private static readonly ActionTarget Target = new ActionTarget.Startup(StartupRegistration.Source, "Fixture App");
    internal static RollbackValue Value(string text = "%TEMP%\\fixture.exe --test", int type = 2) => new(true, type, Encoding.Unicode.GetBytes(text + '\0'));
    private static RestoreCoordinator Service(Platform platform, ActionCenterTests.Store store, Func<ActionSession>? session = null)
        => new(new OperationCoordinator(), store, session ?? (() => Session), [new StartupRunActionAdapter(platform, session ?? (() => Session))]);
    /// <summary>Pending을 먼저 저장하고 항목 하나를 제거·정확히 복원합니다. 복원은 앱을 실행하지 않습니다.</summary>
    [Theory]
    [InlineData(1)][InlineData(2)]
    public async Task UnregisterAndRestorePreserveNativeBytes(int type)
    {
        var platform = new Platform { Current = Value(type: type) }; var original = platform.Current; var store = new ActionCenterTests.Store();
        platform.BeforeWrite = () => Assert.True(Assert.Single(store.Catalog.Records).State is RollbackState.Pending or RollbackState.Restoring);
        var service = Service(platform, store); var plan = (await service.PrepareApplyAsync(ActionId.Startup, Target, default)).Plan!;
        Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded); Assert.False(platform.Current.Exists);
        Assert.False(Assert.Single(store.Catalog.Records).CanExpire);
        var undo = (await service.PrepareRestoreAsync(ActionId.Startup, plan.Id, default)).Plan!;
        Assert.Contains("Fixture App", undo.Preview.Summary); Assert.Contains("지금 실행하지", undo.Preview.Impact);
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded);
        Assert.True(platform.Current.SameAs(original)); Assert.Equal(2, platform.Writes);
        Assert.Equal(RollbackState.Restored, Assert.Single(store.Catalog.Records).State);
        Assert.True(Assert.Single(store.Catalog.Records).CanExpire);
    }
    /// <summary>미리보기 뒤 값/형식/세션 변경은 저장·쓰기 전에 거절합니다.</summary>
    [Theory]
    [InlineData("value")][InlineData("type")][InlineData("missing")][InlineData("session")]
    public async Task PreviewChangeNeverWrites(string condition)
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var session = Session;
        var service = Service(platform, store, () => session); var plan = (await service.PrepareApplyAsync(ActionId.Startup, Target, default)).Plan!;
        if (condition == "value") { platform.Current = Value("different"); }
        if (condition == "type") { platform.Current = Value(type: 1); }
        if (condition == "missing") { platform.Current = StartupRegistration.Absent; }
        if (condition == "session") { session = session with { SessionId = 4 }; }
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Succeeded); Assert.False(result.Started); Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }
    /// <summary>삭제 직전 또는 복원 직전 다시 등록한 항목을 덮어쓰지 않습니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ReregistrationProtectedAtCompareExchange(bool restore)
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Startup, Target, default)).Plan!;
        if (restore)
        {
            Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
            plan = (await service.PrepareRestoreAsync(ActionId.Startup, plan.Id, default)).Plan!;
        }
        platform.BeforeWrite = () => platform.Current = Value("app recreated");
        Assert.False((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        Assert.True(platform.Current.SameAs(Value("app recreated"))); Assert.Equal(restore ? 1 : 0, platform.Writes);
    }
    /// <summary>미지원 출처·이름·형식·실행 범위는 준비하지 않습니다.</summary>
    [Theory]
    [InlineData("hklm64.run", "App", 1, true)]
    [InlineData("hkcu.runOnce", "App", 1, true)]
    [InlineData("hkcu.run", "", 1, true)]
    [InlineData("hkcu.run", "App", 3, true)]
    [InlineData("hkcu.run", "App", 1, false)]
    public async Task UnsupportedTargetsCannotPrepare(string source, string name, int kind, bool full)
    {
        var platform = new Platform { Current = Value(type: kind) };
        var service = Service(platform, new(), () => Session with { Scope = full ? ActionUserScope.Full : ActionUserScope.SystemOnly });
        Assert.Null((await service.PrepareApplyAsync(ActionId.Startup, new ActionTarget.Startup(source, name), default)).Plan);
        Assert.Equal(0, platform.Writes);
    }
    /// <summary>잘못된 원문 문자열·이름은 저장 대상이 아닙니다.</summary>
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("x\0y")]
    public void InvalidCommandsAreRejected(string command) => Assert.False(StartupRegistration.ValidValue(Value(command)));
    /// <summary>복구 키는 이름을 손실 없이 보존하며 임의 경로/비정규 인코딩을 거절합니다.</summary>
    [Fact]
    public void KeyIsBoundedCanonicalAndDoesNotContainCommand()
    {
        Assert.Equal("테스트 앱", StartupRegistration.Name(StartupRegistration.Key("테스트 앱")));
        Assert.Null(StartupRegistration.Name("hkcu-run-v1:!!!!"));
        Assert.Null(StartupRegistration.Name("arbitrary"));
        Assert.False(StartupRegistration.ValidName(new string('x', 101)));
        Assert.False(StartupRegistration.ValidName("a\nb"));
        Assert.False(StartupRegistration.ValidName("a\u202Eb"));
        Assert.False(StartupRegistration.ValidValue(new(true, 1, [65, 0])));
        Assert.False(StartupRegistration.ValidValue(new(true, 1, [65, 0, 0])));
    }
    /// <summary>복구 기록의 SID·종류·원문·대상 키가 변조되면 쓰기 대상으로 쓰지 않습니다.</summary>
    [Theory]
    [InlineData("sid")][InlineData("action")][InlineData("key")][InlineData("before")][InlineData("applied")][InlineData("purpose")]
    public async Task TamperedRestoreRecordIsRejected(string field)
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Startup, Target, default)).Plan!;
        Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        var record = Assert.Single(store.Catalog.Records);
        record = field switch
        {
            "sid" => record with { Sid = "other" }, "action" => record with { ActionId = ActionId.Power },
            "key" => record with { TargetKey = "arbitrary" }, "before" => record with { Before = Value(type: 3) },
            "applied" => record with { Applied = Value("injected") }, _ => record with { Purpose = RollbackPurpose.ServiceRecovery },
        };
        store.Catalog = new([record], []);
        Assert.Null((await service.PrepareRestoreAsync(ActionId.Startup, record.Id, default)).Plan); Assert.Equal(1, platform.Writes);
    }
    /// <summary>복구 저장소에 접근할 수 없으면 실제 등록을 변경하지 않습니다.</summary>
    [Fact]
    public async Task JournalFailureBlocksNativeWrite()
    {
        var platform = new Platform(); var store = new ActionCenterTests.Store(); var service = Service(platform, store);
        var plan = (await service.PrepareApplyAsync(ActionId.Startup, Target, default)).Plan!;
        store.Fail = true;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal(0, platform.Writes); Assert.Empty(store.Catalog.Records);
    }
    internal sealed class Platform : IStartupRunPlatform
    {
        internal RollbackValue Current = Value();
        internal int Writes;
        internal Action? BeforeWrite;
        public RollbackValue Read(string name, ActionSession session) => Current;
        public bool CompareExchange(string name, RollbackValue expected, RollbackValue desired, ActionSession session, Action beforeCommit)
        {
            BeforeWrite?.Invoke();
            if (!Current.SameAs(expected)) { return false; }
            beforeCommit(); Current = desired; Writes++; return true;
        }
    }
}
