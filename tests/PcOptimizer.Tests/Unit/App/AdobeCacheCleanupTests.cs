/**
 * @file    : AdobeCacheCleanupTests.cs
 * @author  : rudals252
 * @brief   : Adobe 고정 캐시 범위·앱 실행 중 거절·부분 처리·사용자 범위·카드 연결 회귀
 */
using System.ComponentModel;
using System.IO;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Files;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>소유한 메모리 파일 시스템으로 검증하며 실제 Adobe 파일을 열지 않습니다.</summary>
public sealed class AdobeCacheCleanupTests
{
    private static readonly ActionTarget.Files Media = new(AdobeCacheTargets.Media);
    private static ActionCoordinator Service(FileCleanupTests.Platform fs, Func<string?>? idle = null, string key = AdobeCacheTargets.Media,
        ActionSession? session = null, Func<string, bool>? protection = null)
    {
        var current = session ?? FileCleanupTests.Session;
        return new(new OperationCoordinator(), () => current, [new FileCleanupAdapter(
            (_, _) => AdobeCacheTargets.Target(key, FileCleanupTests.Root, protection ?? (_ => false), idle ?? (() => null)),
            () => current, fs, definition: new(ActionId.AppFiles, ActionScope.CurrentUser))]);
    }

    /// <summary>실제 조율기·화면 모델을 통해 준비와 실행을 분리하고 결과·재검사를 연결합니다.</summary>
    [Fact] public async Task ActionCenterRequiresConfirmationThenRescansAndKeepsCounts()
    {
        var fs = new FileCleanupTests.Platform(); fs.Add("old.cfa");
        var session = FileCleanupTests.Session; var gate = new OperationCoordinator(); var rescans = 0;
        var adapter = new FileCleanupAdapter((_, _) => AdobeCacheTargets.Target(AdobeCacheTargets.Media, FileCleanupTests.Root, _ => false, () => null),
            () => session, fs, definition: new(ActionId.AppFiles, ActionScope.CurrentUser));
        var workflow = new ActionWorkflow(gate, new ActionCenterTests.Store(), () => session, [adapter], []);
        var choice = new ActionChoice(ActionId.AppFiles, Media, "Adobe 기본 미디어 캐시", "오디오·파형을 다시 만듭니다.");
        using var vm = new ActionCenterViewModel(workflow, new ActionCenterTests.Dispatch(), () => { Assert.False(gate.State.IsBusy); rescans++; return Task.CompletedTask; }, [choice]);
        await vm.PrepareChoiceCommand.ExecuteAsync(choice);
        Assert.True(vm.HasPreview); Assert.Equal(0, fs.Deletes); Assert.Contains("90일", vm.Preview!.Target);
        Assert.Contains("되돌릴 수 없는", vm.Preview.SafetyLabel);
        await vm.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(1, fs.Deletes); Assert.Equal(1, rescans); Assert.Single(vm.Results);
        Assert.Contains("삭제 1개", vm.Result!.FileCounts);
    }

    /// <summary>고정 패턴·90일 기준 밖인 프로젝트·렌더·DB·새 파일은 남깁니다.</summary>
    [Theory] [InlineData(AdobeCacheTargets.Media, 2)] [InlineData(AdobeCacheTargets.Peaks, 1)]
    public async Task DeletesOnlyOldReviewedExtensions(string key, int count)
    {
        var fs = new FileCleanupTests.Platform();
        foreach (var name in new[] { "old.cfa", "old.PEK", "movie.mp4", "edit.prproj", "cache.db", "index.ims", "render.wav", "cache.cfa.bak" }) { fs.Add(name); }
        var recent = fs.Add("recent.cfa"); fs.Nodes[recent] = fs.Nodes[recent] with { Created = DateTime.UtcNow.AddDays(-89).ToFileTimeUtc() };
        var edited = fs.Add("edited.pek"); fs.Nodes[edited] = fs.Nodes[edited] with { Written = DateTime.UtcNow.AddDays(-89).ToFileTimeUtc() };
        var service = Service(fs, key: key);
        var plan = (await service.PrepareAsync(ActionId.AppFiles, new ActionTarget.Files(key), false, default)).Plan!;
        Assert.Equal(count * 100, plan.Preview.Details!.EstimatedLogicalBytes);
        Assert.Contains("다시 만들어", plan.Preview.Impact);
        var added = fs.Add("after-preview.cfa");
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Succeeded); Assert.Equal(count, result.Effect!.ChangedFiles);
        foreach (var file in new[] { "movie.mp4", "edit.prproj", "cache.db", "index.ims", "render.wav", "cache.cfa.bak" })
        { Assert.True(fs.Nodes.ContainsKey(FileCleanupTests.Root + "\\" + file)); }
        Assert.True(fs.Nodes.ContainsKey(recent)); Assert.True(fs.Nodes.ContainsKey(edited)); Assert.True(fs.Nodes.ContainsKey(added));
    }

    /// <summary>준비 전과 후에 실행 중 상태를 확인하고 계획 발급을 거절합니다.</summary>
    [Theory] [InlineData("AdobeAppRunning", 0)] [InlineData("AdobeStateUnavailable", 0)]
    [InlineData("AdobeAppRunning", 1)] [InlineData("AdobeStateUnavailable", 1)]
    public async Task BusyDuringPreparationNeverIssuesPlan(string reason, int allowedCalls)
    {
        var fs = new FileCleanupTests.Platform(); fs.Add("old.cfa"); var calls = 0;
        var service = Service(fs, () => calls++ >= allowedCalls ? reason : null);
        var result = await service.PrepareAsync(ActionId.AppFiles, Media, false, default);
        Assert.Null(result.Plan); Assert.Equal(reason, result.Code); Assert.Equal(0, fs.Deletes);
    }

    /// <summary>미리보기 후 앱 실행 또는 조회 실패는 삭제 전에 거절합니다.</summary>
    [Theory] [InlineData("AdobeAppRunning")] [InlineData("AdobeStateUnavailable")]
    public async Task BusyBeforeExecutionLeavesEverything(string reason)
    {
        var fs = new FileCleanupTests.Platform(); fs.Add("old.cfa"); string? state = null;
        var service = Service(fs, () => state);
        var plan = (await service.PrepareAsync(ActionId.AppFiles, Media, false, default)).Plan!;
        state = reason; var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.False(result.Succeeded); Assert.Equal(reason, result.Code); Assert.Equal(0, fs.Deletes);
        Assert.DoesNotContain("일부 파일을 정리한 뒤", new ActionResultViewModel(result, ActionId.AppFiles, false, "").Detail);
    }

    /// <summary>실행 초반 재조회 뒤 첫 쓰기 직전 앱 실행도 관문을 우회하지 않습니다.</summary>
    [Fact] public async Task AppStartingJustBeforeFirstWriteIsNotPartialSuccess()
    {
        var fs = new FileCleanupTests.Platform(); fs.Add("old.cfa"); var calls = 0;
        var service = Service(fs, () => ++calls >= 4 ? "AdobeAppRunning" : null);
        var plan = (await service.PrepareAsync(ActionId.AppFiles, Media, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal("AdobeAppRunning", result.Code); Assert.Equal(0, fs.Deletes);
        Assert.Equal(1, result.Effect!.SkippedFiles);
    }

    /// <summary>중간 앱 실행·조회 실패는 이미 삭제된 수와 나머지 수를 보존합니다.</summary>
    [Theory] [InlineData("AdobeAppRunning")] [InlineData("AdobeStateUnavailable")]
    public async Task AppStartingAfterOneDeletePreservesPartialEvidence(string reason)
    {
        var fs = new FileCleanupTests.Platform(); fs.Add("a.cfa"); var keep = fs.Add("b.pek"); string? state = null;
        fs.AfterDelete = () => state = reason;
        var service = Service(fs, () => state);
        var plan = (await service.PrepareAsync(ActionId.AppFiles, Media, false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Started); Assert.False(result.Succeeded); Assert.Equal(reason, result.Code);
        Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.SkippedFiles); Assert.True(fs.Nodes.ContainsKey(keep));
        Assert.Contains("일부 파일을 정리한 뒤", new ActionResultViewModel(result, ActionId.AppFiles, false, "").Detail);
    }

    /// <summary>공유 사용자·링크·보호 파일은 Adobe 확장자여도 정리 권한이 없습니다.</summary>
    [Fact] public async Task ProtectedLinkedAndLockedCachesStay()
    {
        var fs = new FileCleanupTests.Platform(); var protectedFile = fs.Add("protected.cfa"); var link = fs.Add("link.pek");
        fs.Nodes[link] = fs.Nodes[link] with { Attributes = FileAttributes.ReparsePoint };
        var hard = fs.Add("hard.cfa"); fs.Nodes[hard] = fs.Nodes[hard] with { Links = 2 };
        var locked = fs.Add("locked.cfa"); fs.Locked.Add(locked); fs.Add("free.pek");
        var service = Service(fs, protection: p => p == protectedFile);
        var plan = (await service.PrepareAsync(ActionId.AppFiles, Media, false, default)).Plan!;
        Assert.Equal(200, plan.Preview.Details!.EstimatedLogicalBytes);
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal("Partial", result.Code); Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.FailedFiles);
        Assert.True(fs.Nodes.ContainsKey(protectedFile)); Assert.True(fs.Nodes.ContainsKey(link)); Assert.True(fs.Nodes.ContainsKey(hard));
    }

    /// <summary>시스템 전용/미확인 계정에서는 앱 캐시 계획을 만들지 않습니다.</summary>
    [Theory] [InlineData(ActionUserScope.SystemOnly)] [InlineData(ActionUserScope.Unknown)]
    public async Task ExcludedScopeCannotPrepare(ActionUserScope scope)
    {
        var fs = new FileCleanupTests.Platform();
        var service = Service(fs, () => throw new Exception("must not observe"), session: FileCleanupTests.Session with { Scope = scope });
        Assert.Null((await service.PrepareAsync(ActionId.AppFiles, Media, false, default)).Plan); Assert.Equal(0, fs.Deletes);
    }

    /// <summary>기본 등록 프로필 외 경로·임의 카탈로그는 추측하지 않습니다.</summary>
    [Fact] public void DefaultLocationsAndCatalogAreClosed()
    {
        const string profile = @"C:\Users\Fixture"; const string roaming = profile + @"\AppData\Roaming";
        Assert.Equal(roaming + @"\Adobe\Common\Media Cache Files", AdobeCacheTargets.DefaultRoot(AdobeCacheTargets.Media, profile, roaming, Path.GetFullPath));
        Assert.Equal(roaming + @"\Adobe\Common\Peak Files", AdobeCacheTargets.DefaultRoot(AdobeCacheTargets.Peaks, profile, roaming, Path.GetFullPath));
        Assert.Throws<ActionUnavailableException>(() => AdobeCacheTargets.DefaultRoot(AdobeCacheTargets.Media, profile, @"D:\Moved", Path.GetFullPath));
        Assert.Throws<ActionUnavailableException>(() => AdobeCacheTargets.DefaultRoot("../Media Cache", profile, roaming, Path.GetFullPath));
        Assert.Throws<ActionUnavailableException>(() => AdobeCacheTargets.Target("unknown", roaming, _ => false, () => null));
    }

    /// <summary>편집·렌더·공유 캐시 보조 프로세스 이름은 대소문자와 무관하게 거절합니다.</summary>
    [Theory] [InlineData("Adobe Premiere Pro")] [InlineData("Adobe Premiere Pro (Beta)")] [InlineData("AfterFX")]
    [InlineData("Adobe Media Encoder")] [InlineData("aerender")] [InlineData("Adobe Audition")]
    [InlineData("DYNAMICLINKMANAGER")] [InlineData("dynamiclinkmediaserver")] [InlineData("PProHeadless")]
    public void KnownWritersAreBlocked(string name) => Assert.Equal("AdobeAppRunning", AdobeProcessGuard.Check(() => ["explorer", name]));

    /// <summary>조회 실패·빈 결과를 실행 중인 앱 없음으로 해석하지 않습니다.</summary>
    [Fact] public void ProcessQueryFailureIsClosed()
    {
        Assert.Null(AdobeProcessGuard.Check(() => ["explorer", "PcOptimizer"]));
        Assert.Equal("AdobeStateUnavailable", AdobeProcessGuard.Check(() => []));
        Assert.Equal("AdobeStateUnavailable", AdobeProcessGuard.Check(() => [""]));
        Assert.Equal("AdobeStateUnavailable", AdobeProcessGuard.Check(() => throw new Win32Exception()));
        Assert.Equal("AdobeStateUnavailable", AdobeProcessGuard.Check(() => throw new InvalidOperationException()));
    }

    /// <summary>검토된 Adobe 앱 카드만 기본 캐시 재확인 버튼으로 연결합니다.</summary>
    [Theory] [InlineData("Adobe 미디어 캐시", true, true)] [InlineData("Adobe 미디어 캐시", false, false)]
    [InlineData("Adobe", true, false)] [InlineData("Games", true, false)]
    public void CardUsesExactReviewedAppAndAvailability(string app, bool available, bool expected)
    {
        var finding = new Finding(AppCacheRule.FINDING_ID_PREFIX + app, FindingCategory.AppCache, "fixture", [], "fixture", Verdict.Info, null, null, null, null, []);
        var card = new FindingCardViewModel(finding, new SettingsUriPolicy(NullAppLogger.Instance, _ => { }),
            new LinkPolicy(null, NullAppLogger.Instance, _ => { }), adobeCleanupAvailable: available);
        Assert.Equal(expected, card.CanPrepareAdobe);
    }
}
