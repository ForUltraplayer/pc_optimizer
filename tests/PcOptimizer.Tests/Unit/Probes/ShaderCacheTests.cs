/**
 * @file    : ShaderCacheTests.cs
 * @author  : rudals252
 * @brief   : Steam/그래픽 캐시 위치별 검사와 보호·시간 예산·카드·내보내기 연결을 가짜 트리로 검증
 */
using System.IO;
using Microsoft.Win32;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>실제 설치 게임·캐시를 열거나 변경하지 않는 검사입니다.</summary>
public sealed class ShaderCacheTests
{
    private const string Profile = @"C:\Users\tester";
    private const string Local = Profile + @"\AppData\Local";
    private const string Install = @"C:\Program Files (x86)\Steam";
    private const string Cache = Install + @"\steamapps\shadercache";
    private const string Vdf = Install + @"\steamapps\libraryfolders.vdf";
    private static FakePathEnvironment Env() => new FakePathEnvironment { Profile = Profile }.WithVariable("LOCALAPPDATA", Local).WithVariable("ProgramFiles(x86)", @"C:\Program Files (x86)");
    private static FakeRegistryReader Registry(string root = Install) => new FakeRegistryReader().WithString(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY, SteamLibraryReader.USER_VALUE, root);
    private static ResolvedProtection Policy(params ProtectedRoot[] extra) => new([new(@"C:\Program Files (x86)", ProtectedRootOrigin.SystemPath, "%ProgramFiles(x86)%"), .. extra]);
    private static ScanSnapshot Inspect(FakePathEnvironment env, IDirectoryEntrySource source, IRegistryReader? registry = null, ResolvedProtection? policy = null,
        Func<string, bool>? other = null, bool verified = true, Func<bool>? expired = null, TimeProvider? time = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var measurements = new ShaderCacheInspector(env, registry ?? Registry(), source, time ?? new ManualTimeProvider()).Inspect(policy ?? Policy(), other ?? (_ => false), verified, now, expired ?? (() => false), ct);
        return new(Guid.NewGuid(), [new(AppCacheProbeContract.PROBE_ID, ProbeStatus.Success, measurements, [], now, TimeSpan.Zero, new UserContext("anon", false))]);
    }
    private static string? State(ScanSnapshot snapshot, string group = "steam", int index = 0) => (snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, ShaderCacheRule.Field(group, index, "state"))?.Value as TextValue)?.Value;
    private static long? Bytes(ScanSnapshot snapshot, string group = "steam", int index = 0) => (snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, ShaderCacheRule.Field(group, index, "bytes"))?.Value as IntegerValue)?.Value;
    private static string Library(string path) => "\"libraryfolders\" { \"0\" { \"path\" \"" + path.Replace("\\", "\\\\") + "\" } }";

    /// <summary>기존 그래픽 위치와 별개로 NV_Cache를 관측하며 정리 가능한 양으로 승격하지 않습니다.</summary>
    [Fact]
    public void NvidiaLegacyCacheHasSeparateObservation()
    {
        var source = new FakeDirectoryEntrySource().Dir(Local + @"\NVIDIA Corporation\NV_Cache", FakeDirectoryEntrySource.File("owned", 1234));
        var snapshot = Inspect(Env(), source);
        Assert.Equal(1234, Bytes(snapshot, "graphics", 3)); Assert.Equal("Observed", State(snapshot, "graphics", 3));
        var card = Assert.Single(new ShaderCacheRule().Evaluate(snapshot), f => f.Id.Contains("graphics"));
        Assert.Equal(Verdict.Info, card.Verdict);
    }

    /// <summary>기본 Program Files와 다른 드라이브 shadercache만 관측하고 게임 본체를 제외합니다.</summary>
    [Fact]
    public void 두_라이브러리의_캐시만_세고_게임_본체는_읽지_않는다()
    {
        var env = Env().WithFile(Vdf, Library(@"D:\Games"));
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("a", 100))
            .Dir(@"D:\Games\steamapps\shadercache", FakeDirectoryEntrySource.File("b", 200))
            .Dir(@"D:\Games\steamapps\common", FakeDirectoryEntrySource.File("game.exe", 999999));
        var policy = Policy(); var snapshot = Inspect(env, source, policy: policy);
        Assert.Equal(100, Bytes(snapshot)); Assert.Equal(200, Bytes(snapshot, index: 1));
        Assert.Equal(2, source.Enumerated.Count); Assert.DoesNotContain(source.Enumerated, p => p.EndsWith("common"));
        Assert.True(policy.IsProtected(Cache)); Assert.Equal([Vdf], env.FileReads);
        var card = Assert.Single(new ShaderCacheRule().Evaluate(snapshot));
        Assert.Equal(Verdict.Info, card.Verdict); Assert.Contains("300 B", card.Title); Assert.Contains("라이브러리 2", card.Detail);
    }

    /// <summary>기본 경로가 VDF에 반복되어도 한 번만 셉니다.</summary>
    [Fact]
    public void 중복_라이브러리를_한번만_센다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("a", 100));
        var snapshot = Inspect(Env().WithFile(Vdf, Library(Install.ToUpperInvariant())), source);
        Assert.Single(source.Enumerated); Assert.Contains("100 B", Assert.Single(new ShaderCacheRule().Evaluate(snapshot)).Title);
    }

    /// <summary>Program Files 예외는 겹친 문서/클라우드 보호를 무시하지 않습니다.</summary>
    [Theory]
    [InlineData(ProtectedRootOrigin.CloudSync)]
    [InlineData(ProtectedRootOrigin.KnownFolder)]
    public void 중복_보호가_설정_본문_읽기를_막는다(ProtectedRootOrigin origin)
    {
        var env = Env().WithFile(Vdf, Library(@"D:\Games")); var source = new FakeDirectoryEntrySource().Dir(Cache);
        var snapshot = Inspect(env, source, policy: Policy(new ProtectedRoot(@"C:\Program Files (x86)", origin, "private")));
        Assert.Equal("Unreadable", State(snapshot)); Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated);
    }

    /// <summary>설정 파일 형식 실패 또는 잘못된 라이브러리를 조용히 버리고 완전 관측으로 보고하지 않습니다.</summary>
    [Theory]
    [InlineData("\"libraryfolders\" {")]
    [InlineData("\"libraryfolders\" { \"0\" { \"path\" \"%PRIVATE%\" } }")]
    public void 설정_실패는_기본_폴더로_대체하지_않는다(string text)
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache);
        var snapshot = Inspect(Env().WithFile(Vdf, text), source);
        Assert.Equal("Invalid", State(snapshot)); Assert.Null(Bytes(snapshot)); Assert.Empty(source.Enumerated);
        Assert.Equal(Verdict.CannotVerify, Assert.Single(new ShaderCacheRule().Evaluate(snapshot)).Verdict);
    }

    /// <summary>다른 사용자·클라우드·UNC·미해석 짧은 이름을 순회하지 않습니다.</summary>
    [Theory]
    [InlineData(@"C:\Users\other\Steam", "Protected")]
    [InlineData(@"D:\Cloud\Steam", "Protected")]
    [InlineData(@"\\server\share\Steam", "Unsupported")]
    [InlineData(@"D:\STEAML~1", "Unsupported")]
    public void 라이브러리_경계_위반은_부분_합계와_구분한다(string root, string state)
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("ok", 100)).Dir(root + @"\steamapps\shadercache");
        var snapshot = Inspect(Env().WithFile(Vdf, Library(root)), source, policy: Policy(new ProtectedRoot(@"D:\Cloud", ProtectedRootOrigin.CloudSync, "OneDrive")),
            other: p => p.StartsWith(@"C:\Users\other", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(state, State(snapshot, index: 1)); Assert.Null(Bytes(snapshot, index: 1)); Assert.Equal([Cache], source.Enumerated);
        var card = Assert.Single(new ShaderCacheRule().Evaluate(snapshot)); Assert.Contains("일부 위치", card.Title); Assert.Contains("확인 불가", card.Detail);
    }

    /// <summary>설치 루트가 UNC이면 작은 설정 파일도 읽지 않습니다.</summary>
    [Fact]
    public void 네트워크_설정은_읽지_않는다()
    {
        var env = Env(); var source = new FakeDirectoryEntrySource();
        Assert.Equal("Unreadable", State(Inspect(env, source, Registry(@"\\server\share\Steam"))));
        Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated);
    }

    /// <summary>사용자 설치 경로 조회 실패를 HKLM 경로나 미설치로 조용히 대체하지 않습니다.</summary>
    [Theory]
    [InlineData(RegistryReadStatus.AccessDenied)]
    [InlineData(RegistryReadStatus.Error)]
    [InlineData(RegistryReadStatus.Found)]
    public void 레지스트리_실패를_보존한다(RegistryReadStatus status)
    {
        var registry = Registry().WithStringResult(RegistryRoot.CurrentUser, RegistryView.Default, SteamLibraryReader.USER_KEY, SteamLibraryReader.USER_VALUE, new(status, null, null))
            .WithString(RegistryRoot.LocalMachine, RegistryView.Registry32, SteamLibraryReader.MACHINE_KEY, SteamLibraryReader.MACHINE_VALUE, @"D:\OtherSteam");
        var env = Env(); var source = new FakeDirectoryEntrySource().Dir(Cache);
        Assert.Equal("Unreadable", State(Inspect(env, source, registry)));
        Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated); Assert.Single(registry.StringReads);
    }

    /// <summary>Program Files와 같은 경로의 다른 시스템 보호 근거도 보존합니다.</summary>
    [Fact]
    public void 다른_시스템_보호를_완화하지_않는다()
    {
        var env = Env();
        var snapshot = Inspect(env, new FakeDirectoryEntrySource(), policy: Policy(new ProtectedRoot(@"C:\Program Files (x86)", ProtectedRootOrigin.SystemPath, "%SystemRoot%\\System32")));
        Assert.Equal("Unreadable", State(snapshot)); Assert.Empty(env.FileReads);
    }

    /// <summary>GPU별 세 폴더를 분리하고 드라이버 본체나 다운로드를 열거하지 않습니다.</summary>
    [Fact]
    public void 그래픽_캐시별_크기와_부재를_구분한다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Local + @"\NVIDIA\DXCache", FakeDirectoryEntrySource.File("a", 100))
            .Dir(Local + @"\D3DSCache", FakeDirectoryEntrySource.File("b", 200)).Dir(Local + @"\NVIDIA\Downloads", FakeDirectoryEntrySource.File("installer.exe", 99999));
        var snapshot = Inspect(Env(), source, new FakeRegistryReader());
        Assert.Equal(100, Bytes(snapshot, "graphics", 0)); Assert.Equal("Absent", State(snapshot, "graphics", 1)); Assert.Null(Bytes(snapshot, "graphics", 1));
        Assert.Equal(200, Bytes(snapshot, "graphics", 2)); Assert.Equal(2, source.Enumerated.Count);
        var card = Assert.Single(new ShaderCacheRule().Evaluate(snapshot)); Assert.Contains("300 B", card.Title); Assert.Contains("NVIDIA GLCache: 캐시 폴더 없음", card.Detail);
    }

    /// <summary>루트나 상위 링크/온라인 전용 위치는 하위 속성 조회보다 먼저 거절합니다.</summary>
    [Theory]
    [InlineData(RootPresence.ReparsePoint)]
    [InlineData(RootPresence.Placeholder)]
    [InlineData(RootPresence.AccessDenied)]
    public void 그래픽_상위_특수_폴더를_추종하지_않는다(RootPresence presence)
    {
        var source = new FakeDirectoryEntrySource();
        source.OnProbeRoot = p => { if (p.StartsWith(Local + @"\NVIDIA\", StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("crossed blocked ancestor"); } };
        var snapshot = Inspect(Env(), new PresenceSource(source, Local + @"\NVIDIA", presence), new FakeRegistryReader());
        Assert.Equal(presence.ToString(), State(snapshot, "graphics", 0)); Assert.Empty(source.Enumerated);
    }

    /// <summary>파일 단위 제외가 있으면 전체 용량으로 표시하지 않습니다.</summary>
    [Fact]
    public void 제외된_파일은_부분_관측이다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("good", 100), FakeDirectoryEntrySource.File("offline", 999, FileAttributes.Offline));
        var snapshot = Inspect(Env(), source);
        Assert.Equal("Partial", State(snapshot)); Assert.Equal(100, Bytes(snapshot)); Assert.Contains("일부 위치", Assert.Single(new ShaderCacheRule().Evaluate(snapshot)).Title);
    }

    /// <summary>전체 예산 소진은 0바이트 성공이 아닙니다.</summary>
    [Fact]
    public void 예산_소진은_미관측이다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache);
        var snapshot = Inspect(Env(), source, expired: () => true);
        Assert.Equal("TimedOut", State(snapshot)); Assert.Null(Bytes(snapshot)); Assert.Empty(source.Enumerated);
    }

    /// <summary>개별 위치 3초 한도에서 읽은 파일의 크기를 잃지 않습니다.</summary>
    [Fact]
    public void 위치_예산에서_부분_결과를_보존한다()
    {
        var time = new ManualTimeProvider(); var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("a", 100), FakeDirectoryEntrySource.File("b", 200));
        source.OnEntry = (_, _) => time.Advance(TimeSpan.FromSeconds(4));
        var snapshot = Inspect(Env(), source, time: time);
        Assert.Equal("Partial", State(snapshot)); Assert.Equal(100, Bytes(snapshot));
    }

    /// <summary>프로필 미확인에는 설정 본문과 폴더를 읽지 않습니다.</summary>
    [Fact]
    public void 프로필_미확인은_읽지_않는다()
    {
        var env = Env(); var source = new FakeDirectoryEntrySource().Dir(Cache);
        Assert.Empty(new ShaderCacheRule().Evaluate(Inspect(env, source, verified: false))); Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated);
    }

    /// <summary>앱/캐시가 없는 환경에는 카드가 생기지 않습니다.</summary>
    [Fact]
    public void 미설치_미관측은_카드가_없다() => Assert.Empty(new ShaderCacheRule().Evaluate(Inspect(Env(), new FakeDirectoryEntrySource(), new FakeRegistryReader())));

    /// <summary>확인한 설치 위치에서 캐시 폴더만 없으면 부재 안내입니다.</summary>
    [Fact]
    public void 부재와_실패를_구분한다()
    {
        var card = Assert.Single(new ShaderCacheRule().Evaluate(Inspect(Env(), new FakeDirectoryEntrySource())));
        Assert.Equal(Verdict.Info, card.Verdict); Assert.Contains("캐시가 없습니다", card.Title);
    }

    /// <summary>승격/일반 검사 모두 위치별 카드를 내고 기존 보충 카드와 중복되지 않습니다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 프로브와_새_카드가_연결된다(bool elevated)
    {
        var pc = new AppCacheTestEnvironment();
        var result = await pc.Probe().RunAsync(AppCacheTestEnvironment.Context(elevated), CancellationToken.None);
        var snapshot = new ScanSnapshot(Guid.NewGuid(), [result]);
        Assert.Equal(2, new ShaderCacheRule().Evaluate(snapshot).Count);
        Assert.DoesNotContain(new AppCacheRule().Evaluate(snapshot), f => f.Id is "appCache.app:Steam 셰이더 캐시" or "appCache.app:NVIDIA·Direct3D 셰이더 캐시" or "appCache.config:steam");
    }

    /// <summary>설치 경로 값 외의 계정 값은 조회하지 않고 외부 드라이브 경로는 익명화합니다.</summary>
    [Fact]
    public void 계정_미수집과_경로_익명화를_확인한다()
    {
        var registry = Registry(); var source = new FakeDirectoryEntrySource().Dir(@"D:\Private Games\steamapps\shadercache", FakeDirectoryEntrySource.File("a", 1));
        var snapshot = Inspect(Env().WithFile(Vdf, Library(@"D:\Private Games")), source, registry);
        Assert.Empty(registry.KeyReads); Assert.Single(registry.StringReads); Assert.EndsWith("|SteamPath", registry.StringReads[0]);
        var report = new ScanReport { ScanId = Guid.NewGuid(), StartedAtUtc = DateTimeOffset.UtcNow, CompletedAtUtc = DateTimeOffset.UtcNow, Outcome = ScanOutcome.Completed,
            AppVersion = "test", RulesVersion = "test", UserContext = new UserContext("anon", false), ProbeSummaries = [], Findings = new ShaderCacheRule().Evaluate(snapshot) };
        var json = new ReportExporter(new PersonalDataScrubber(Profile, "tester", "FAKE-PC"), @"C:\Windows").SerializeAnonymized(report);
        Assert.DoesNotContain("Private Games", json); Assert.DoesNotContain("tester", json); Assert.Contains("folder-", json);
    }

    /// <summary>카드에서 상세를 펼치되 자동 정리 버튼을 만들지 않습니다.</summary>
    [Fact]
    public void 카드_상세를_펼친다()
    {
        var finding = Assert.Single(new ShaderCacheRule().Evaluate(Inspect(Env(), new FakeDirectoryEntrySource().Dir(Cache))));
        var vm = new FindingCardViewModel(finding, new SettingsUriPolicy(NullAppLogger.Instance, _ => throw new InvalidOperationException()), new LinkPolicy(null, NullAppLogger.Instance, _ => throw new InvalidOperationException()));
        Assert.Contains("캐시별 용량", vm.DetailsButtonText); vm.ToggleDetailsCommand.Execute(null); Assert.True(vm.IsDetailsVisible); Assert.False(vm.HasApplyAction);
    }
    private sealed class PresenceSource(FakeDirectoryEntrySource inner, string path, RootPresence presence) : IDirectoryEntrySource
    {
        /// <inheritdoc />
        public RootPresence ProbeRoot(string candidate) => candidate.Equals(path, StringComparison.OrdinalIgnoreCase) ? presence : inner.ProbeRoot(candidate);
        /// <inheritdoc />
        public IEnumerable<DirectoryEntry> Enumerate(string path) => inner.Enumerate(path);
    }
}
