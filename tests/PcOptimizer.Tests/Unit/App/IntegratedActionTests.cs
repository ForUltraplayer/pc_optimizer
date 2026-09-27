/**
 * @file    : IntegratedActionTests.cs
 * @author  : rudals252
 * @brief   : 추가 조치의 세션·원본 보존·수동 위치·효과 필터 연결 회귀
 */
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Files;
using PcOptimizer.Probes.Actions.Startup;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 사용자 경로·시작 등록을 변경하지 않습니다.</summary>
public sealed class IntegratedActionTests
{
    /// <summary>네 가지 출처의 복구 키·범위와 기록 선행 저장을 검증합니다.</summary>
    [Theory]
    [InlineData(StartupRegistration.UserFolder, ActionId.StartupFolder, false, true)]
    [InlineData(StartupRegistration.CommonFolder, ActionId.CommonStartupFolder, true, true)]
    [InlineData(StartupRegistration.Machine32, ActionId.MachineStartup, true, false)]
    [InlineData(StartupRegistration.Machine64, ActionId.MachineStartup, true, false)]
    public async Task SourceSpecificJournalAndRestore(string source, ActionId id, bool machine, bool folder)
    {
        var session = ActionCenterTests.Session with { Scope = machine ? ActionUserScope.SystemOnly : ActionUserScope.Full };
        var platform = new StartupActionTests.Platform { Current = folder ? new(true, 900, new byte[116]) : StartupActionTests.Value() };
        var before = platform.Current; var store = new ActionCenterTests.Store();
        var adapter = new StartupRunActionAdapter(_ => platform, () => session, machine, folder);
        var service = new RestoreCoordinator(new OperationCoordinator(), store, () => session, [adapter]);
        platform.BeforeWrite = () => Assert.True(Assert.Single(store.Catalog.Records).State is RollbackState.Pending or RollbackState.Restoring);
        var plan = (await service.PrepareApplyAsync(id, new ActionTarget.Startup(source, "owned.lnk"), default)).Plan!;
        Assert.NotNull(plan); Assert.True((await service.ExecuteAsync(plan.Id, default)).Succeeded);
        var record = Assert.Single(store.Catalog.Records);
        var decoded = RollbackCodec.Decode(RollbackCodec.Encode(record, session.Sid), session.Sid);
        Assert.Equal(record.TargetKey, decoded.TargetKey); Assert.True(record.Before.SameAs(decoded.Before));
        Assert.Equal(source, StartupRegistration.SourceOfKey(record.TargetKey)); Assert.False(record.CanExpire);
        Assert.Equal(machine ? ActionScope.System : ActionScope.CurrentUser, record.Scope);
        var undo = (await service.PrepareRestoreAsync(id, record.Id, default)).Plan!;
        Assert.True((await service.ExecuteAsync(undo.Id, default)).Succeeded); Assert.True(before.SameAs(platform.Current));
    }
    /// <summary>공용 시작 폴더를 HKLM Run 실행기로 잘못 전달하지 않습니다.</summary>
    [Fact]
    public async Task WrongSourceAdapterCannotPrepare()
    {
        var platform = new StartupActionTests.Platform();
        var adapter = new StartupRunActionAdapter(_ => platform, () => ActionCenterTests.Session, true);
        Assert.Null(await adapter.PrepareAsync(new ActionTarget.Startup(StartupRegistration.CommonFolder, "owned.lnk"), default));
        var userFolder = new StartupRunActionAdapter(_ => platform, () => ActionCenterTests.Session with { Scope = ActionUserScope.SystemOnly }, false, true);
        await Assert.ThrowsAsync<ActionUnavailableException>(() => userFolder.PrepareAsync(new ActionTarget.Startup(StartupRegistration.UserFolder, "owned.lnk"), default));
    }
    /// <summary>효과 필터를 바꿔도 미리보기·결과가 남고 필터만으로 실행하지 않습니다.</summary>
    [Fact]
    public async Task EffectFilterPreservesConfirmationAndResult()
    {
        var adapter = new ActionCenterTests.Adapter();
        var workflow = new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(), () => ActionCenterTests.Session, [adapter], []);
        using var vm = new ActionCenterViewModel(workflow, new ActionCenterTests.Dispatch(), () => Task.CompletedTask,
            [new(ActionId.Power, ActionCenterTests.Target, "전원", "효과")]);
        Assert.Single(vm.ChoiceGroups); await vm.PrepareAsync(ActionId.Power, ActionCenterTests.Target); var preview = vm.Preview;
        vm.SelectEffectCommand.Execute("startup"); Assert.Empty(vm.ChoiceGroups); Assert.Same(preview, vm.Preview); Assert.Equal(0, adapter.Executions);
        vm.SelectEffectCommand.Execute("power"); Assert.Single(vm.ChoiceGroups);
        await vm.ExecuteCommand.ExecuteAsync(null); var result = vm.Result;
        vm.SelectEffectCommand.Execute("space"); Assert.Same(result, vm.Result); Assert.Single(vm.Results);
        vm.SelectEffectCommand.Execute("invalid"); Assert.Equal("all", vm.EffectFilter); Assert.Single(vm.ChoiceGroups);
    }
    /// <summary>선택 경로는 정확한 캐시 루트·현재 사용자 세션에만 묶습니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ManualLocationRegistrationIsBoundedAndSessionBound(bool steam)
    {
        var session = ActionCenterTests.Session; var adobe = new AdobeCacheLocationCatalog(); var games = new SteamCacheLocationCatalog();
        (string? Key, string? Code) Register(string path, ActionSession owner) => steam ? games.TryRegister(path, owner) : adobe.TryRegister(path, owner);
        var path = steam ? @"D:\Owned\steamapps\shadercache" : @"D:\Owned\Media Cache Files";
        var first = Register(path, session); Assert.NotNull(first.Key); Assert.Equal(first.Key, Register(path.ToUpperInvariant(), session).Key);
        Assert.Equal("ScopeExcluded", Register(path, session with { Scope = ActionUserScope.SystemOnly }).Code);
        foreach (var bad in new[] { @"\\server\share" + path[2..], path.Replace("Owned", "OWNED~1"), path + ":ads", path.Replace("Owned", ".."), @"D:\Owned\Projects" })
        { Assert.Null(Register(bad, session).Key); }
        Assert.Throws<ActionUnavailableException>(() => { if (steam) { games.Resolve(first.Key!, session with { Sid = "other" }); } else { adobe.Resolve(first.Key!, session with { Sid = "other" }); } });
    }
    /// <summary>공식 링크 뒤의 쿼리·하위 경로나 외부 입력을 허용하지 않습니다.</summary>
    [Theory]
    [InlineData(CacheSupportLinks.NvidiaShader)][InlineData(CacheSupportLinks.SteamDownload)]
    public void SupportLinksAreExactAndNeverClaimCleanup(string link)
    {
        Assert.True(CacheSupportLinks.IsAllowed(link)); Assert.False(CacheSupportLinks.IsAllowed(link + "?redirect=bad")); Assert.False(CacheSupportLinks.IsAllowed(link + "/other"));
        string? opened = null;
        using var vm = new ActionCenterViewModel(new ActionWorkflow(new OperationCoordinator(), new ActionCenterTests.Store(), () => ActionCenterTests.Session, [], []),
            new ActionCenterTests.Dispatch(), () => Task.CompletedTask, openSupport: url => { opened = url; return true; });
        var choice = Assert.Single(vm.ManualChoices, c => c.SupportUrl == link); vm.OpenSupportCommand.Execute(choice);
        Assert.Equal(link, opened); Assert.False(vm.HasResult); Assert.Empty(vm.Results);
        Assert.False(vm.OpenSupportCommand.CanExecute(new ManualActionChoice("injected", "", null, link + "?bad")));
    }
    /// <summary>잘못된 영상 캐시 선택은 이전 선택을 보존하며 스냅샷을 뒤에서 바꾸지 않습니다.</summary>
    [Theory]
    [InlineData("davinci", "CacheClip")][InlineData("capcut", "Cache")]
    public void VideoLocationSnapshotIsStable(string app, string leaf)
    {
        var locations = new PcOptimizer.Probes.Applications.VideoCacheLocations();
        Assert.True(locations.TrySet(app, @"D:\Owned\" + leaf)); var snapshot = locations.Snapshot();
        Assert.False(locations.TrySet(app, @"D:\Owned\Projects")); Assert.Equal(snapshot[app], locations.Snapshot()[app]);
        Assert.False(locations.TrySet(app, @"D:\OWNED~1\" + leaf));
        Assert.True(locations.TrySet(app, @"E:\Moved\" + leaf)); locations.Clear();
        Assert.Empty(locations.Snapshot()); Assert.Equal(@"D:\Owned\" + leaf, snapshot[app]);
    }
}
