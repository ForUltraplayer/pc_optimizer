/**
 * @file    : StartupFolderSmokeTests.cs
 * @author  : rudals252
 * @brief   : 실제 시작 위치 밖의 소유 파일로 핸들 이동·복원·변조·충돌 경계 검증
 */
using System.IO;
using System.Runtime.InteropServices;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Startup;

namespace PcOptimizer.Tests.Smoke;

/// <summary>GUID 임시 디렉터리만 사용하며 바로가기를 실행하지 않습니다.</summary>
[Trait("Category", "Smoke")]
public sealed class StartupFolderSmokeTests
{
    /// <summary>이동 후 원본 ID·내용·ACL 지문을 유지하고 취소는 이동 전에 멈춥니다.</summary>
    [Fact]
    public void ParkRestoreAndCancelPreserveOriginal()
    {
        using var f = new Fixture(); var before = f.Platform.Read("owned.lnk", f.Session);
        Assert.True(StartupFolderPlatform.ValidValue(before));
        Assert.Throws<OperationCanceledException>(() => f.Platform.CompareExchange("owned.lnk", before, StartupRegistration.Absent, f.Session, () => throw new OperationCanceledException()));
        Assert.True(before.SameAs(f.Platform.Read("owned.lnk", f.Session)));
        Assert.True(f.Park(before)); Assert.False(File.Exists(f.FilePath));
        Assert.False(f.Platform.Read("owned.lnk", f.Session).Exists);
        Assert.True(f.Restore(before)); Assert.Equal(f.Bytes, File.ReadAllBytes(f.FilePath));
        Assert.True(before.SameAs(f.Platform.Read("owned.lnk", f.Session)));
        Assert.Empty(Directory.GetFiles(f.Root, "*.disabled"));
    }
    /// <summary>외부에서 재등록한 이름이나 변경된 보관 파일을 덮어쓰지 않습니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void RestoreRefusesConflictOrTampering(bool tamper)
    {
        using var f = new Fixture(); var before = f.Platform.Read("owned.lnk", f.Session); Assert.True(f.Park(before));
        var parked = Assert.Single(Directory.GetFiles(f.Root, "*.disabled"));
        if (tamper) { var bytes = f.Bytes.ToArray(); bytes[72] = 1; File.WriteAllBytes(parked, bytes); }
        else { File.WriteAllText(f.FilePath, "external registration"); }
        Assert.ThrowsAny<Exception>(() => f.Restore(before));
        Assert.True(File.Exists(parked));
        if (!tamper) { Assert.Equal("external registration", File.ReadAllText(f.FilePath)); }
    }
    /// <summary>대체 스트림·형식 불명·읽기 전용은 조치 대상으로 삼지 않습니다.</summary>
    [Theory]
    [InlineData("ads")][InlineData("header")][InlineData("readonly")]
    public void UnsupportedFileMetadataIsRejected(string kind)
    {
        using var f = new Fixture();
        if (kind == "ads") { File.WriteAllText(f.FilePath + ":owned", "stream"); }
        if (kind == "header") { File.WriteAllBytes(f.FilePath, new byte[76]); }
        if (kind == "readonly") { File.SetAttributes(f.FilePath, FileAttributes.ReadOnly); }
        Assert.Throws<ActionUnavailableException>(() => f.Platform.Read("owned.lnk", f.Session));
        Assert.True(File.Exists(f.FilePath));
    }
    /// <summary>다른 세션과 다른 경로 형태를 네이티브 접근 전에 거절합니다.</summary>
    [Fact]
    public void SessionAndNamesAreBounded()
    {
        using var f = new Fixture();
        Assert.Throws<ActionUnavailableException>(() => f.Platform.Read("owned.lnk", f.Session with { SessionId = f.Session.SessionId + 1 }));
        foreach (var name in new[] { "../owned.lnk", "x:owned.lnk", "x.cmd", "x~1.lnk", "x.lnk:ads" })
        { Assert.Throws<ActionUnavailableException>(() => f.Platform.Read(name, f.Session)); }
    }
    /// <summary>내용이 같아도 심볼릭 링크나 하드링크는 이동하지 않습니다.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void LinksNeverBecomeMovableShortcuts(bool hardlink)
    {
        using var f = new Fixture(); var other = Path.Combine(f.Root, "other.lnk");
        File.Move(f.FilePath, other);
        if (hardlink) { Assert.True(CreateHardLinkW(f.FilePath, other, IntPtr.Zero)); }
        else { File.CreateSymbolicLink(f.FilePath, other); }
        Assert.Throws<ActionUnavailableException>(() => f.Platform.Read("owned.lnk", f.Session));
        Assert.Equal(f.Bytes, File.ReadAllBytes(other));
        File.Delete(f.FilePath); // 링크 자체만 제거한 뒤 소유 트리를 정리한다.
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string existing, IntPtr reserved);
    private sealed class Fixture : IDisposable
    {
        internal readonly Guid Id = Guid.NewGuid();
        internal readonly ActionSession Session = SystemActionSession.Read();
        internal string Root => Path.Combine(Path.GetTempPath(), "PcOptimizer.StartupFixture." + Id.ToString("N"));
        internal string FilePath => Path.Combine(Root, "Startup", "owned.lnk");
        internal readonly byte[] Bytes = new byte[76];
        internal StartupFolderPlatform Platform { get; }
        internal Fixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            BitConverter.GetBytes(76).CopyTo(Bytes, 0);
            new Guid("00021401-0000-0000-C000-000000000046").ToByteArray().CopyTo(Bytes, 4);
            File.WriteAllBytes(FilePath, Bytes); Platform = new(Id);
        }
        internal bool Park(RollbackValue before) => Platform.CompareExchange("owned.lnk", before, StartupRegistration.Absent, Session, () => { });
        internal bool Restore(RollbackValue before) => Platform.CompareExchange("owned.lnk", StartupRegistration.Absent, before, Session, () => { });
        public void Dispose()
        {
            foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories)) { File.SetAttributes(file, FileAttributes.Normal); }
            Directory.Delete(Root, true);
        }
    }
}
