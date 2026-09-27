/**
 * @file    : StartupRunSmokeTests.cs
 * @author  : rudals252
 * @brief   : 실제 자동 실행 위치와 분리된 GUID 키에서 레지스트리 비교·원문 복원을 검증
 */
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
using PcOptimizer.Core.Actions;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Startup;

namespace PcOptimizer.Tests.Smoke;

/// <summary>Software\PcOptimizer.Tests 아래 자기 소유 키만 만들고 제거합니다.</summary>
[Trait("Category", "Smoke")]
public sealed class StartupRunSmokeTests
{
    /// <summary>실제 레지스트리 링크가 전용 대상 키를 가리켜도 수집·변경 전에 거절합니다.</summary>
    [Fact]
    public void RegistrySymbolicLinkIsNotFollowed()
    {
        var id = Guid.NewGuid(); var path = @"Software\PcOptimizer.Tests\" + id.ToString("N");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using var parent = root.CreateSubKey(path, true);
            using var target = parent.CreateSubKey("Target", true);
            target.SetValue("fixture", "untouched", RegistryValueKind.String);
            var session = SystemActionSession.Read();
            Assert.Equal(0, RegCreateKeyExW(parent.Handle, "Run", 0, null, 2, 0x20006 | 0x100, IntPtr.Zero, out var link, out _));
            using (link)
            {
                var destination = Encoding.Unicode.GetBytes(@"\Registry\User\" + session.Sid + "\\" + path + @"\Target");
                Assert.Equal(0, RegSetValueExW(link, "SymbolicLinkValue", 0, 6, destination, destination.Length));
            }
            Assert.Throws< PcOptimizer.Probes.Actions.ActionUnavailableException>(() => new StartupRunPlatform(id).Read("fixture", session));
            Assert.Equal("untouched", target.GetValue("fixture"));
        }
        finally
        {
            // RegDeleteKeyEx(path)는 링크 대상을 지울 수 있다. OPEN_LINK 핸들 자체만 삭제한다.
            var code = RegOpenKeyExW(root.Handle, path + @"\Run", 8, 0x10000 | 1 | 0x100, out var ownedLink);
            using (ownedLink)
            {
                if (code == 0) { Assert.Equal(0, NtDeleteKey(ownedLink)); }
                else { Assert.Equal(2, code); }
            }
            root.DeleteSubKeyTree(path, false);
        }
    }
    /// <summary>실제 Windows API로 문자열 형식 보존·해제·원복·취소·외부 변경 보호를 검증합니다.</summary>
    [Theory]
    [InlineData(RegistryValueKind.String)]
    [InlineData(RegistryValueKind.ExpandString)]
    public void PinnedValueDeletesRestoresAndRejectsCancelledWrites(RegistryValueKind kind)
    {
        var id = Guid.NewGuid(); var path = @"Software\PcOptimizer.Tests\" + id.ToString("N");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using var run = root.CreateSubKey(path + @"\Run", true);
            run.SetValue("fixture", "%TEMP%\\never-execute.exe --fixture", kind);
            run.SetValue("untouched", "other", RegistryValueKind.String);
            var platform = new StartupRunPlatform(id); var session = SystemActionSession.Read();
            var before = platform.Read("fixture", session);
            Assert.Equal((int)kind, before.NativeType);
            Assert.Throws<OperationCanceledException>(() => platform.CompareExchange("fixture", before, StartupRegistration.Absent, session, () =>
            { throw new OperationCanceledException(); }));
            Assert.True(before.SameAs(platform.Read("fixture", session)));
            Assert.True(platform.CompareExchange("fixture", before, StartupRegistration.Absent, session, () => { }));
            Assert.False(platform.Read("fixture", session).Exists);
            Assert.True(platform.CompareExchange("fixture", StartupRegistration.Absent, before, session, () => { }));
            Assert.True(before.SameAs(platform.Read("fixture", session)));
            run.SetValue("fixture", "external", RegistryValueKind.String);
            Assert.False(platform.CompareExchange("fixture", before, StartupRegistration.Absent, session, () => { }));
            Assert.Equal("external", run.GetValue("fixture"));
            Assert.Equal("other", run.GetValue("untouched"));
            var changed = platform.Read("fixture", session);
            Assert.False(platform.CompareExchange("fixture", changed, StartupRegistration.Absent, session, () => run.SetValue("fixture", "late change")));
            Assert.Equal("late change", run.GetValue("fixture"));
        }
        finally { root.DeleteSubKeyTree(path, false); }
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegCreateKeyExW(SafeRegistryHandle key, string subkey, int reserved, string? @class, int options, int access, IntPtr security, out SafeRegistryHandle result, out int disposition);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegSetValueExW(SafeRegistryHandle key, string name, int reserved, uint type, byte[] data, int size);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyExW(SafeRegistryHandle key, string subkey, int options, int access, out SafeRegistryHandle result);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtDeleteKey(SafeRegistryHandle key);
}
