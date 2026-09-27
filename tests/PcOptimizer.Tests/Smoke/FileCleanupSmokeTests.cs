/**
 * @file    : FileCleanupSmokeTests.cs
 * @author  : rudals252
 * @brief   : 생성한 임시 fixture에서만 실제 핸들 삭제·잠금·링크·파일 교체 검증
 */
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Files;

namespace PcOptimizer.Tests.Smoke;

/// <summary>사용자 Temp 전체 대신 GUID로 만든 소유 테스트 폴더만 대상으로 사용합니다.</summary>
[Trait("Category", "Smoke")]
public sealed class FileCleanupSmokeTests
{
    /// <summary>실제 삭제도 테스트 소유 폴더의 오래된 Adobe 캐시 파일만 처리합니다.</summary>
    [Fact]
    public async Task AdobeNativeDeletionPreservesNewLockedAndNonCacheFiles()
    {
        using var fixture = new Fixture();
        var old = fixture.Add("audio.cfa"); var locked = fixture.Add("wave.pek");
        foreach (var path in new[] { old, locked })
        { File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-100)); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-100)); }
        var recent = fixture.Add("recent.cfa"); var project = fixture.Add("edit.prproj");
        var session = SystemActionSession.Read();
        var service = new ActionCoordinator(new OperationCoordinator(), () => session, [new FileCleanupAdapter(
            (_, _) => AdobeCacheTargets.Target(AdobeCacheTargets.Media, fixture.Root, _ => false, () => null),
            () => session, new NativeFileCleanupPlatform(), definition: new(ActionId.AppFiles, ActionScope.CurrentUser))]);
        var prepared = await service.PrepareAsync(ActionId.AppFiles, new ActionTarget.Files(AdobeCacheTargets.Media), false, default);
        Assert.NotNull(prepared.Plan);
        var added = fixture.Add("after-preview.cfa");
        using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await service.ExecuteAsync(prepared.Plan.Id, default);
            Assert.Equal("Partial", result.Code); Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.FailedFiles);
        }
        Assert.False(File.Exists(old)); Assert.True(File.Exists(locked)); Assert.True(File.Exists(recent));
        Assert.True(File.Exists(project)); Assert.True(File.Exists(added)); Assert.True(Directory.Exists(fixture.Root));
    }

    /// <summary>실제 호스트에서는 이름만 조회하고 프로그램 실행·종료·캐시 삭제는 하지 않습니다.</summary>
    [Fact]
    public void AdobeProcessStateCanBeObservedWithoutReadingCommandLines()
    {
        var state = AdobeProcessGuard.Check();
        Assert.True(state is null or "AdobeAppRunning", "Process name enumeration could not be completed.");
    }

    /// <summary>별칭은 긴 경로와 같은 파일 ID로 정규화하고 하드링크 파일은 제외합니다.</summary>
    [Fact]
    public async Task ShortAliasAndHardLinkCannotBypassIdentity()
    {
        using var fixture = new Fixture(); var path = fixture.Add("long-file-name-for-alias.tmp");
        var buffer = new StringBuilder(32768);
        Assert.NotEqual(0u, GetShortPathNameW(path, buffer, buffer.Capacity));
        var platform = new NativeFileCleanupPlatform();
        using (var normal = platform.Open(path, false, false))
        using (var alias = platform.Open(buffer.ToString(), false, false))
        { Assert.Equal(normal.Read().Path, alias.Read().Path); Assert.Equal(normal.Read().Identity, alias.Read().Identity); }
        var link = Path.Combine(fixture.Root, "hardlink.tmp");
        Assert.True(CreateHardLinkW(link, path, 0));
        var service = fixture.Service();
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        Assert.Equal("NoEligibleFiles", (await service.ExecuteAsync(plan.Id, default)).Code);
        Assert.True(File.Exists(path)); Assert.True(File.Exists(link));
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string existing, nint security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string path, StringBuilder buffer, int size);

    /// <summary>실제 핸들 삭제는 승인된 오래된 파일만 지우고 새 파일/루트는 남깁니다.</summary>
    [Fact]
    public async Task DeletesOnlyOwnedApprovedFilesAndLeavesLockedFile()
    {
        using var fixture = new Fixture();
        var old = fixture.Add("old.tmp"); var locked = fixture.Add("locked.tmp");
        var service = fixture.Service();
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        var newFile = fixture.Add("new-after-preview.tmp");
        using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await service.ExecuteAsync(plan.Id, default);
            Assert.Equal(1, result.Effect!.ChangedFiles); Assert.Equal(1, result.Effect.FailedFiles);
            Assert.False(result.Succeeded);
        }
        Assert.False(File.Exists(old)); Assert.True(File.Exists(locked)); Assert.True(File.Exists(newFile)); Assert.True(Directory.Exists(fixture.Root));
    }
    /// <summary>같은 경로 파일 교체는 메타데이터/ID 재검증으로 거절합니다.</summary>
    [Fact]
    public async Task ReplacedFileIsNotDeleted()
    {
        using var fixture = new Fixture(); var path = fixture.Add("old.tmp");
        var service = fixture.Service();
        var plan = (await service.PrepareAsync(ActionId.UserFiles, new ActionTarget.Files("fixture"), false, default)).Plan!;
        File.Move(path, path + ".original"); File.WriteAllText(path, "new content must survive");
        var result = await service.ExecuteAsync(plan.Id, default);
        Assert.Equal("TargetChanged", result.Code); Assert.False(result.Started); Assert.True(File.Exists(path));
    }
    /// <summary>조상 링크는 원본 위치의 파일 삭제 허가로 해석하지 않습니다.</summary>
    [Fact]
    public void LinkedParentCannotBeOpenedForDeletion()
    {
        using var fixture = new Fixture(); var file = fixture.Add("old.tmp");
        var target = Path.Combine(fixture.Root, "target"); Directory.CreateDirectory(target);
        var alias = Path.Combine(fixture.Root, "alias"); Directory.CreateSymbolicLink(alias, target);
        try
        {
            File.WriteAllText(Path.Combine(target, "keep.tmp"), "keep");
            Assert.Throws<UnauthorizedAccessException>(() => new NativeFileCleanupPlatform().Open(Path.Combine(alias, "keep.tmp"), false, true));
            Assert.True(File.Exists(file)); Assert.True(File.Exists(Path.Combine(target, "keep.tmp")));
        }
        finally { Directory.Delete(alias); }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "pcoptimizer-file-cleanup-" + Guid.NewGuid().ToString("N"));
        internal Fixture() { Directory.CreateDirectory(Root); }
        internal string Add(string name)
        {
            var path = Path.Combine(Root, name); File.WriteAllText(path, "owned fixture");
            File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-10)); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10)); return path;
        }
        internal ActionCoordinator Service()
        {
            var session = SystemActionSession.Read();
            return new(new OperationCoordinator(), () => session, [new FileCleanupAdapter((_, _) => new("fixture", Root, "owned", true, ["*"], TimeSpan.FromDays(7), _ => false),
                () => session, new NativeFileCleanupPlatform())]);
        }
        public void Dispose()
        {
            var absolute = Path.GetFullPath(Root);
            if (absolute != Root || !absolute.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "pcoptimizer-file-cleanup-"), StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("FixtureBoundary"); }
            Directory.Delete(absolute, true);
        }
    }
}
