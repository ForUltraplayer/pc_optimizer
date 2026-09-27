/**
 * @file    : FileCleanupTests.cs
 * @author  : rudals252
 * @brief   : 고정 파일 집합·변조·보호·부분 삭제·예산·취소를 메모리 파일 시스템으로 검증
 */
using System.IO;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Files;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Tests.Unit.Probes.Fakes;
using Microsoft.Win32;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 사용자 경로를 읽거나 삭제하지 않습니다.</summary>
public sealed class FileCleanupTests
{
    internal static readonly ActionSession Session = new("fixture", 1, ActionUserScope.Full);
    internal const string Root = @"C:\owned-fixture";
    internal static CleanupTarget Target(string root = Root) => new("fixture", root, "fixture", true, ["*"], TimeSpan.FromDays(7), _ => false);

    /// <summary>현재 프로필 안에 중첩된 다른 SID도 삭제 보호 목록에서 빠지지 않습니다.</summary>
    [Fact]
    public void NestedOtherProfileIsRetainedAndUnreadableEntryFailsClosed()
    {
        var registry = new FakeRegistryReader().WithSubKeys(RegistryRoot.LocalMachine, RegistryView.Registry64,
            OtherUserLocationGuard.PROFILE_LIST_KEY, new(RegistryReadStatus.Found, ["self", "other", "S-1-5-18"], null));
        var env = new FakePathEnvironment { Profile = @"C:\Users\Self" };
        Assert.Throws<UnauthorizedAccessException>(() => UserTempTargets.OtherProfiles(registry, env, "self"));
        registry.WithString(RegistryRoot.LocalMachine, RegistryView.Registry64, OtherUserLocationGuard.PROFILE_LIST_KEY + "\\other",
            OtherUserLocationGuard.PROFILE_IMAGE_PATH_VALUE, @"C:\Users\Self\AppData\Local\Temp\Other");
        Assert.Equal(@"C:\Users\Self\AppData\Local\Temp\Other", Assert.Single(UserTempTargets.OtherProfiles(registry, env, "self")));
    }

    /// <summary>탐색기 카탈로그는 지정 패턴만 선택하고 하위 폴더는 순회하지 않습니다.</summary>
    [Fact]
    public async Task ExplorerPatternsDoNotAuthorizeOtherFiles()
    {
        var fs = new Platform(); fs.Add("thumbcache_32.db"); fs.Add("iconcache_32.db"); fs.Add("private.db");
        var adapter = new FileCleanupAdapter((_, _) => Target() with { Recursive = false, Patterns = ["thumbcache_*.db", "iconcache_*.db"] }, () => Session, fs);
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session, [adapter]);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        Assert.Equal(200, plan.Preview.Details!.EstimatedLogicalBytes);
        Assert.Equal(2, (await service.ExecuteAsync(plan.Id, default)).Effect!.ChangedFiles);
        Assert.True(fs.Nodes.ContainsKey(Root + @"\private.db"));
    }

    /// <summary>전체 재검증 후 다음 파일이 바뀌어도 삭제 직전 핸들 검증에서 제외됩니다.</summary>
    [Fact]
    public async Task ReplacementDuringExecutionIsSkipped()
    {
        var fs = new Platform(); fs.Add("a.tmp"); var next = fs.Add("b.tmp");
        fs.AfterDelete = () => fs.Nodes[next] = fs.Nodes[next] with { Identity = new(1, 999, 0) };
        var service = Service(fs);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal("Partial", result.Code); Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.SkippedFiles);
        Assert.True(fs.Nodes.ContainsKey(next));
    }

    /// <summary>미리보기 후 생긴 파일은 실행 대상으로 추가되지 않습니다.</summary>
    [Fact]
    public async Task OnlyApprovedSnapshotFilesAreDeleted()
    {
        var fs = new Platform(); fs.Add("old.tmp");
        var service = Service(fs);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        fs.Add("new.tmp");
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Effect!.ChangedFiles);
        Assert.False(fs.Nodes.ContainsKey(Root + @"\old.tmp"));
        Assert.True(fs.Nodes.ContainsKey(Root + @"\new.tmp"));
        Assert.True(fs.Nodes.ContainsKey(Root));
        Assert.Equal("PlanExpired", (await service.ExecuteAsync(plan.Id, default)).Code);
    }
    /// <summary>같은 경로의 파일 교체/수정/부모 교체/링크 변경은 첫 삭제 전에 거절합니다.</summary>
    [Theory]
    [InlineData("id")][InlineData("size")][InlineData("write")][InlineData("parent")][InlineData("link")][InlineData("placeholder")]
    public async Task ChangedSnapshotCannotAuthorizeDeletion(string change)
    {
        var fs = new Platform(); var path = fs.Add("old.tmp");
        var service = Service(fs);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        var file = fs.Nodes[path];
        fs.Nodes[path] = change switch
        {
            "id" => file with { Identity = new(1, 999, 0) }, "size" => file with { Length = 200 },
            "write" => file with { Written = file.Written + 1 }, "parent" => file with { Parents = [new(Root, new(1, 99, 0))] },
            "link" => file with { Links = 2 }, _ => file with { Attributes = FileAttributes.Offline },
        };
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started);
        Assert.Equal("TargetChanged", result.Code);
        Assert.Equal(0, fs.Deletes);
    }
    /// <summary>실행 직전 보호 정책 변화는 허가를 폐기합니다.</summary>
    [Fact]
    public async Task NewlyProtectedFileStopsBeforeDeletion()
    {
        var fs = new Platform(); fs.Add("old.tmp"); var protect = false;
        var adapter = new FileCleanupAdapter((_, _) => Target() with { Protected = p => protect && p.EndsWith("old.tmp", StringComparison.Ordinal) }, () => Session, fs);
        var service = new ActionCoordinator(new OperationCoordinator(), () => Session, [adapter]);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        protect = true;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.False(result.Started); Assert.Equal(0, fs.Deletes);
    }
    /// <summary>사용 중인 파일은 강제로 해제하지 않으며 다른 승인 파일만 처리합니다.</summary>
    [Fact]
    public async Task LockedFileProducesPartialCounts()
    {
        var fs = new Platform(); var locked = fs.Add("locked.tmp"); fs.Add("free.tmp"); fs.Locked.Add(locked);
        var service = Service(fs);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal("Partial", result.Code); Assert.False(result.Succeeded);
        Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.FailedFiles);
        Assert.True(fs.Nodes.ContainsKey(locked));
    }
    /// <summary>첫 파일 뒤 취소해도 이미 삭제한 수와 남은 수를 잃지 않습니다.</summary>
    [Fact]
    public async Task CancellationPreservesPartialEvidence()
    {
        var fs = new Platform(); fs.Add("a.tmp"); fs.Add("b.tmp");
        using var cts = new CancellationTokenSource(); fs.AfterDelete = cts.Cancel;
        var gate = new OperationCoordinator();
        var service = Service(fs, gate: gate);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        await service.ExecuteAsync(plan.Id, cts.Token);
        // 공통 조율기는 취소 응답보다 늦게 도착하는 실제 결과를 별도로 보존한다.
        await OperationLifetimeTests.Idle(gate);
        var result = service.GetResult(plan.Id)!;
        Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.SkippedFiles);
        Assert.False(result.Succeeded);
    }
    /// <summary>불완전 순회/다른 볼륨은 계획을 발급하지 않습니다.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task IncompleteOrCrossVolumeInspectionCannotPrepare(bool budget)
    {
        var fs = new Platform(); var path = fs.Add("old.tmp");
        if (!budget) { fs.Nodes[path] = fs.Nodes[path] with { Identity = new(2, 9, 0) }; }
        var service = Service(fs, budget ? 1 : 20000);
        Assert.Null((await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan);
        Assert.Equal(0, fs.Deletes);
    }
    /// <summary>최근/읽기 전용/하드링크/placeholder는 알려진 제외 항목이며 삭제하지 않습니다.</summary>
    [Fact]
    public async Task UnsafeAndRecentEntriesAreExcluded()
    {
        var fs = new Platform();
        foreach (var kind in new[] { "recent", "readonly", "hardlink", "offline", "link" })
        {
            var path = fs.Add(kind + ".tmp"); var node = fs.Nodes[path];
            fs.Nodes[path] = kind switch
            {
                "recent" => node with { Written = DateTime.UtcNow.ToFileTimeUtc() },
                "readonly" => node with { Attributes = FileAttributes.ReadOnly }, "hardlink" => node with { Links = 2 },
                "offline" => node with { Attributes = FileAttributes.Offline }, _ => node with { Attributes = FileAttributes.ReparsePoint },
            };
        }
        var service = Service(fs);
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        Assert.Equal(0, plan.Preview.Details!.EstimatedLogicalBytes);
        Assert.Equal("NoEligibleFiles", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.Equal(0, fs.Deletes);
    }
    /// <summary>사용자 범위가 아닌 세션은 파일 관측도 시작하지 않습니다.</summary>
    [Fact]
    public async Task SystemOnlyRefusesBeforeFileObservation()
    {
        var fs = new Platform(); var session = Session with { Scope = ActionUserScope.SystemOnly };
        var adapter = new FileCleanupAdapter((_, _) => throw new Exception("must not resolve"), () => session, fs);
        var service = new ActionCoordinator(new OperationCoordinator(), () => session, [adapter]);
        Assert.Null((await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan);
    }
    private static ActionCoordinator Service(Platform fs, int max = 20000, OperationCoordinator? gate = null) => new(gate ?? new OperationCoordinator(), () => Session,
        [new FileCleanupAdapter((_, _) => Target(), () => Session, fs, maxEntries: max)]);
    internal sealed class Platform : IFileCleanupPlatform
    {
        internal readonly Dictionary<string, FileStamp> Nodes = new(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> Locked = [];
        internal Action? AfterDelete;
        internal int Deletes;
        internal Platform() { Nodes[Root] = Node(Root, 1) with { Attributes = FileAttributes.Directory }; }
        internal string Add(string name) { var path = Root + "\\" + name; Nodes[path] = Node(path, (ulong)Nodes.Count + 1); return path; }
        private static FileStamp Node(string path, ulong id) => new(path, new(1, id, 0), 100, 1, 1, 1, FileAttributes.Normal, 1, [new(@"C:\", new(1, 100, 0))]);
        public FileAttributes Attributes(string path) => Nodes[path].Attributes;
        public IEnumerable<string> Enumerate(string directory) => Nodes.Keys.Where(p => Path.GetDirectoryName(p) == directory).ToArray();
        public long? FreeBytes(string root) => Deletes * 100;
        public ICleanupFile Open(string path, bool directory, bool delete)
        {
            if (!Nodes.ContainsKey(path) || (delete && Locked.Contains(path))) { throw new IOException("unavailable"); }
            return new File(this, path, delete);
        }
        private sealed class File(Platform owner, string path, bool delete) : ICleanupFile
        {
            private bool _marked, _disposed;
            public FileStamp Read() => owner.Nodes[path];
            public void MarkForDeletion() { Assert.True(delete); _marked = true; }
            public void Dispose()
            {
                if (_disposed) { return; } _disposed = true;
                if (_marked) { owner.Nodes.Remove(path); owner.Deletes++; owner.AfterDelete?.Invoke(); }
            }
        }
    }
}
