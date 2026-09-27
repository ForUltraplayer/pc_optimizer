/**
 * @file    : RollbackStoreSmokeTests.cs
 * @author  : rudals252
 * @brief   : 소유 임시 폴더의 실제 ACL·원자 교체·잠금·링크·부분 파일 거절 검증
 */
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.App;

namespace PcOptimizer.Tests.Smoke;

/// <summary>관리자 토큰에서 소유 fixture만 만들고 지웁니다. 실제 사용자 복구 폴더/설정은 변경하지 않습니다.</summary>
[Trait("Category", "Smoke")]
public sealed class RollbackStoreSmokeTests
{
    /// <summary>실제 등록 프로필을 읽기만 하고 저장 위치가 현재 LocalAppData 아래인지 확인합니다.</summary>
    [Fact]
    public void CurrentProfileIsBoundWithoutCreatingProductionStore()
    {
        var session = SystemActionSession.Read();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcOptimizer", "rollback"), RollbackStore.CurrentRoot(session.Sid));
        Assert.Throws<UnauthorizedAccessException>(() => RollbackStore.CurrentRoot("S-1-5-21-other"));
    }

    /// <summary>루트 자체가 아닌 중간 폴더가 링크여도 대상에 저장소를 생성하지 않습니다.</summary>
    [Fact]
    public void AncestorLinkCannotRedirectStoreCreation()
    {
        using var fixture = new Fixture();
        var destination = Path.Combine(fixture.Parent, "target");
        var link = Path.Combine(fixture.Parent, "alias");
        Directory.CreateDirectory(destination);
        Directory.CreateSymbolicLink(link, destination);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => new RollbackStore(Path.Combine(link, "rollback")).Open(fixture.Session));
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        }
        finally { Directory.Delete(link); }
    }

    /// <summary>전용 ACL로 생성하고 재시작 후 원문을 읽으며 같은 저장소를 이중으로 열지 못합니다.</summary>
    [Fact]
    public void AtomicRoundTripAndCrossInstanceExclusion()
    {
        using var fixture = new Fixture();
        var record = RollbackTests.Record(fixture.Session.Sid);
        using (var transaction = fixture.Store.Open(fixture.Session))
        {
            transaction.Save(record);
            Assert.Throws<IOException>(() => fixture.Store.Open(fixture.Session));
            Assert.True(transaction.Read(record.Id)!.Before.SameAs(record.Before));
            transaction.Save(record with { State = RollbackState.Applied, Revision = 1 });
            Assert.Empty(transaction.ReadAll().Issues);
            var acl = new DirectoryInfo(fixture.Root).GetAccessControl();
            Assert.True(acl.AreAccessRulesProtected);
            Assert.Equal("S-1-5-32-544", acl.GetOwner(typeof(SecurityIdentifier))!.Value);
        }
        using var restarted = new RollbackStore(fixture.Root).Open(fixture.Session);
        Assert.Equal(RollbackState.Applied, restarted.Read(record.Id)!.State);
    }

    /// <summary>교체 실패는 기존 파일을 보존하며 완료로 승격하지 않고 부분 파일을 이슈로 남깁니다.</summary>
    [Fact]
    public void FailedRenamePreservesPendingAndReportsPartialFile()
    {
        using var fixture = new Fixture();
        using var transaction = fixture.Store.Open(fixture.Session);
        var record = RollbackTests.Record(fixture.Session.Sid);
        transaction.Save(record);
        using (var pinned = new FileStream(fixture.PathFor(record.Id), FileMode.Open, FileAccess.Read, FileShare.Read))
        { Assert.Throws<IOException>(() => transaction.Save(record with { State = RollbackState.Applied, Revision = 1 })); }
        Assert.Equal(RollbackState.Pending, transaction.Read(record.Id)!.State);
        Assert.Single(transaction.ReadAll().Issues);
    }

    /// <summary>관리자가 시험용으로 손상시킨 JSON도 상태를 재해석해 실행하지 않고 이슈로 남깁니다.</summary>
    [Theory]
    [InlineData("partial")]
    [InlineData("otherSid")]
    [InlineData("version")]
    [InlineData("oversize")]
    [InlineData("acl")]
    public void UntrustedFilesNeverEnterRecoveryCatalog(string kind)
    {
        using var fixture = new Fixture();
        using var transaction = fixture.Store.Open(fixture.Session);
        var record = RollbackTests.Record(fixture.Session.Sid);
        transaction.Save(record);
        var file = fixture.PathFor(record.Id);
        if (kind == "acl")
        {
            var security = new FileInfo(file).GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(fixture.Session.Sid), FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(file).SetAccessControl(security);
        }
        else
        {
            var text = File.ReadAllText(file);
            text = kind switch
            {
                "partial" => "{",
                "otherSid" => text.Replace(fixture.Session.Sid, "S-1-5-21-other", StringComparison.Ordinal),
                "version" => text.Replace("\"Version\":1", "\"Version\":0", StringComparison.Ordinal),
                _ => new string(' ', RollbackCodec.MaxBytes + 1),
            };
            File.WriteAllText(file, text);
        }
        Assert.Empty(transaction.ReadAll().Records);
        Assert.Single(transaction.ReadAll().Issues);
    }

    /// <summary>공유/상속 ACL로 미리 만든 루트를 안전한 저장소로 간주하거나 자동 수선하지 않습니다.</summary>
    [Fact]
    public void PrecreatedWritableDirectoryIsRejected()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Root);
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Store.Open(fixture.Session));
    }

    /// <summary>다른 계정이나 로그온 세션으로 저장소를 열 수 없습니다.</summary>
    [Fact]
    public void NativeTokenMustMatchSession()
    {
        using var fixture = new Fixture();
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Store.Open(fixture.Session with { Sid = "S-1-5-21-other" }));
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Store.Open(fixture.Session with { SessionId = fixture.Session.SessionId + 1 }));
        Assert.False(Directory.Exists(fixture.Root));
    }

    /// <summary>디렉터리 링크를 따라가거나 하드 링크 파일을 신뢰하지 않습니다.</summary>
    [Fact]
    public void DirectoryAndRecordLinksAreRejected()
    {
        using var fixture = new Fixture();
        var destination = Path.Combine(fixture.Parent, "target");
        Directory.CreateDirectory(destination);
        Directory.CreateSymbolicLink(fixture.Root, destination);
        try { Assert.Throws<UnauthorizedAccessException>(() => fixture.Store.Open(fixture.Session)); }
        finally { Directory.Delete(fixture.Root); }
        using var transaction = fixture.Store.Open(fixture.Session);
        var record = RollbackTests.Record(fixture.Session.Sid);
        transaction.Save(record);
        var hardLink = Path.Combine(fixture.Parent, "owned-hardlink");
        Assert.True(CreateHardLinkW(hardLink, fixture.PathFor(record.Id), 0));
        try { Assert.Throws<UnauthorizedAccessException>(() => transaction.Read(record.Id)); }
        finally { File.Delete(hardLink); }
        Assert.NotNull(transaction.Read(record.Id));
    }

    /// <summary>30일 보존은 완료 기록에만 적용합니다.</summary>
    [Fact]
    public void RetentionNeverDeletesUnfinishedRecords()
    {
        using var fixture = new Fixture();
        using var transaction = fixture.Store.Open(fixture.Session);
        var pending = RollbackTests.Record(fixture.Session.Sid);
        var applied = RollbackTests.Record(fixture.Session.Sid);
        transaction.Save(pending); transaction.Save(applied);
        transaction.Save(applied with { State = RollbackState.Applied, Revision = 1 });
        Assert.Equal(1, transaction.PruneCompleted(DateTimeOffset.Parse("2026-08-01T00:00:00Z")));
        Assert.NotNull(transaction.Read(pending.Id));
        Assert.Null(transaction.Read(applied.Id));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string existing, nint security);
    private sealed class Fixture : IDisposable
    {
        internal readonly string Parent = Path.Combine(Path.GetTempPath(), "pcoptimizer-rollback-test-" + Guid.NewGuid().ToString("N"));
        internal readonly ActionSession Session = SystemActionSession.Read();
        internal string Root => Path.Combine(Parent, "rollback");
        internal RollbackStore Store => new(Root);
        internal Fixture() { Directory.CreateDirectory(Parent); }
        internal string PathFor(Guid id) => Path.Combine(Root, id.ToString("N") + ".json");
        public void Dispose()
        {
            var absolute = Path.GetFullPath(Parent);
            var prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "pcoptimizer-rollback-test-");
            if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || absolute != Parent) { throw new InvalidOperationException("FixtureBoundary"); }
            Directory.Delete(absolute, recursive: true);
        }
    }
}
