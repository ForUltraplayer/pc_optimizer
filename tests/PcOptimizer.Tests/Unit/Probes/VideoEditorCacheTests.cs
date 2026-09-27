/**
 * @file    : VideoEditorCacheTests.cs
 * @author  : rudals252
 * @brief   : 영상 편집 캐시의 좁은 읽기 범위·설정 해석·부분 집계·보호·익명화·프로브 연결을 가짜 파일 트리로 검증
 */
using System.IO;
using System.Text.Json;
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Applications.ConfigReaders;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>실제 사용자 파일을 변경하지 않는 영상 편집 캐시 검사입니다.</summary>
public sealed class VideoEditorCacheTests
{
    private const string Profile = @"C:\Users\tester";
    private const string Videos = Profile + @"\Videos";
    private const string Cache = Videos + @"\CacheClip";
    private const string CapCut = Profile + @"\AppData\Local\CapCut\User Data";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static FakePathEnvironment Env() => new FakePathEnvironment { Profile = Profile }.WithKnownFolder(ProtectedKnownFolder.Videos, Videos);
    private static ResolvedProtection Policy(params ProtectedRoot[] extra) => new([new(Videos, ProtectedRootOrigin.KnownFolder, "Videos"), .. extra]);
    private static List<Measurement> Inspect(FakePathEnvironment env, IDirectoryEntrySource source, ResolvedProtection? policy = null,
        Func<string, bool>? other = null, bool verified = true, Func<bool>? expired = null, TimeProvider? time = null, CancellationToken ct = default)
        => new VideoEditorCacheInspector(env, source, time ?? new ManualTimeProvider()).Inspect(policy ?? Policy(), other ?? (_ => false), verified, Now, expired ?? (() => false), ct);
    private static string? State(IEnumerable<Measurement> measurements, string app = "davinci") => (measurements.SingleOrDefault(m => m.Name == VideoEditorCacheRule.Field(app, "state"))?.Value as TextValue)?.Value;
    private static long? Bytes(IEnumerable<Measurement> measurements, string app = "davinci") => (measurements.SingleOrDefault(m => m.Name == VideoEditorCacheRule.Field(app, "bytes"))?.Value as IntegerValue)?.Value;
    private static ScanSnapshot Snapshot(IReadOnlyList<Measurement> measurements) => new(Guid.NewGuid(), [new(AppCacheProbeContract.PROBE_ID,
        ProbeStatus.Success, measurements, [], Now, TimeSpan.Zero, new UserContext("anon", false))]);
    private static string Config(string root) => $"Site.Count = 1\nSite.1.FS.1.Type = IOFileSys\nSite.1.FS.1.Root = {root}\nRenderCaching.CacheDir = CacheClip\nPrivateToken = secret-do-not-collect";

    /// <summary>설정에서 저장소 전체가 아닌 CacheClip 하나만 반환하고 관련 없는 값은 버립니다.</summary>
    [Fact]
    public void 설정_이동_경로만_반환하고_비밀값을_버린다()
    {
        var reading = DaVinciCacheConfigReader.Parse(Config(@"D:\Video work"));
        Assert.Equal(AppConfigReadState.Configured, reading.State);
        Assert.Equal([@"D:\Video work\CacheClip"], reading.Paths);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(reading));
        var absolute = DaVinciCacheConfigReader.Parse(@"RenderCaching.CacheDir = E:\Render\CacheClip");
        Assert.Equal([@"E:\Render\CacheClip"], absolute.Paths);
    }

    /// <summary>지원하지 않는 형식을 기본 위치나 임의 미디어 폴더로 추측하지 않습니다.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("RenderCaching.CacheDir = ../CacheClip")]
    [InlineData("RenderCaching.CacheDir = C:\\Originals")]
    [InlineData("RenderCaching.CacheDir = %TEMP%\\CacheClip")]
    [InlineData("RenderCaching.CacheDir = CacheClip")]
    [InlineData("RenderCaching.CacheDir = C:\\x\\CacheClip\nRenderCaching.CacheDir = D:\\y\\CacheClip")]
    [InlineData("RenderCaching.CacheDir = C:\\x\\CacheClip\0")]
    public void 불명확한_설정은_거절한다(string text)
    {
        var reading = DaVinciCacheConfigReader.Parse(text);
        Assert.Equal(AppConfigReadState.Invalid, reading.State);
        Assert.Empty(reading.Paths);
    }

    /// <summary>64 KiB 초과 설정을 해석하지 않습니다.</summary>
    [Fact]
    public void 과대_설정은_거절한다() => Assert.Equal(AppConfigReadState.Invalid, DaVinciCacheConfigReader.Parse(new string('x', 65537)).State);

    /// <summary>원본·프로젝트 폴더를 열거하지 않고 CacheClip만 읽으며 공통 보호는 유지합니다.</summary>
    [Fact]
    public void 기본_CacheClip만_읽고_Videos_보호는_그대로다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Videos, FakeDirectoryEntrySource.Folder("CacheClip"), FakeDirectoryEntrySource.File("original.mov", 99999))
            .Dir(Cache, FakeDirectoryEntrySource.File("render.dvcc", 100));
        var policy = Policy();
        var observed = Inspect(Env(), source, policy);
        Assert.Equal("Observed", State(observed)); Assert.Equal(100, Bytes(observed));
        Assert.Equal([Cache], source.Enumerated);
        Assert.True(policy.IsProtected(Cache));
    }

    /// <summary>동일 경로의 중복 보호 근거도 남겨 읽기 예외로 제거되지 않게 합니다.</summary>
    [Theory]
    [InlineData(ProtectedRootOrigin.CloudSync, "OneDrive")]
    [InlineData(ProtectedRootOrigin.KnownFolder, "Documents")]
    [InlineData(ProtectedRootOrigin.SystemPath, "System")]
    public void 겹친_보호는_해제하지_않는다(ProtectedRootOrigin origin, string label)
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("x", 100));
        var policy = Policy(new ProtectedRoot(Videos, origin, label));
        Assert.Single(policy.Roots); Assert.Equal(2, policy.SourceRoots.Count);
        Assert.Equal("Protected", State(Inspect(Env(), source, policy)));
        Assert.Empty(source.Enumerated);
    }

    /// <summary>사용자가 옮긴 CacheClip만 관측하며 기본 폴더는 다시 세지 않습니다.</summary>
    [Fact]
    public void 설정으로_옮긴_캐시를_관측한다()
    {
        var env = Env().WithFile(Path.Combine(Profile, DaVinciCacheConfigReader.RelativeConfig), Config(@"D:\Render"));
        var source = new FakeDirectoryEntrySource().Dir(@"D:\Render\CacheClip", FakeDirectoryEntrySource.File("x", 222)).Dir(Cache, FakeDirectoryEntrySource.File("y", 444));
        var observed = Inspect(env, source);
        Assert.Equal(222, Bytes(observed)); Assert.Equal([@"D:\Render\CacheClip"], source.Enumerated);
        Assert.Single(env.FileReads);
    }

    /// <summary>설정 해석 실패 시 존재하는 기본 폴더로 조용히 대체하지 않습니다.</summary>
    [Fact]
    public void 잘못된_설정은_기본_폴더로_대체하지_않는다()
    {
        var env = Env().WithFile(Path.Combine(Profile, DaVinciCacheConfigReader.RelativeConfig), "invalid");
        var source = new FakeDirectoryEntrySource().Dir(Cache);
        var observed = Inspect(env, source);
        Assert.Equal("Invalid", State(observed)); Assert.Null(Bytes(observed)); Assert.Empty(source.Enumerated);
    }

    /// <summary>다른 사용자·보호·미해석 8.3·UNC 경로는 읽지 않습니다.</summary>
    [Theory]
    [InlineData(@"C:\Users\other\CacheClip", "Protected")]
    [InlineData(@"D:\Private\CacheClip", "Protected")]
    [InlineData(@"D:\PRIVATE~1\CacheClip", "Unreadable")]
    [InlineData(@"\\server\share\CacheClip", "Unsupported")]
    public void 설정_경로의_경계를_확인한다(string path, string state)
    {
        var env = Env().WithFile(Path.Combine(Profile, DaVinciCacheConfigReader.RelativeConfig), "RenderCaching.CacheDir = " + path);
        var source = new FakeDirectoryEntrySource().Dir(path, FakeDirectoryEntrySource.File("x", 100));
        var observed = Inspect(env, source, Policy(new ProtectedRoot(@"D:\Private", ProtectedRootOrigin.CloudSync, "OneDrive")), p => p.StartsWith(@"C:\Users\other", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(state, State(observed)); Assert.Null(Bytes(observed)); Assert.Empty(source.Enumerated);
    }

    /// <summary>프로필을 증명하지 못하면 사용자 설정을 읽지 않습니다.</summary>
    [Fact]
    public void 프로필_불명확시_설정도_읽지_않는다()
    {
        var env = Env(); var source = new FakeDirectoryEntrySource().Dir(Cache);
        Assert.Empty(Inspect(env, source, verified: false)); Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated);
    }

    /// <summary>설정 자체가 보호 루트 아래면 파일 본문을 요청하지 않습니다.</summary>
    [Fact]
    public void 보호된_설정은_열지_않는다()
    {
        var env = Env(); var source = new FakeDirectoryEntrySource().Dir(Cache);
        var observed = Inspect(env, source, Policy(new ProtectedRoot(Profile + @"\AppData", ProtectedRootOrigin.CloudSync, "OneDrive")));
        Assert.Equal("Unreadable", State(observed)); Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated);
    }

    /// <summary>설정 파일 상위 정션이면 본문을 읽기 전에 차단합니다.</summary>
    [Fact]
    public void 설정_정션은_본문을_읽지_않는다()
    {
        var env = Env().WithFile(Path.Combine(Profile, DaVinciCacheConfigReader.RelativeConfig), Config(@"D:\Render"));
        var source = new FakeDirectoryEntrySource().Dir(Cache);
        var measured = Inspect(env, new PresenceSource(source, Profile + @"\AppData", RootPresence.ReparsePoint));
        Assert.Equal("Unreadable", State(measured)); Assert.Empty(env.FileReads); Assert.Empty(source.Enumerated);
    }

    /// <summary>CapCut은 Cache만 세고 프로젝트·내보내기·프로그램 본체는 제외합니다.</summary>
    [Fact]
    public void CapCut_Cache만_읽는다()
    {
        var source = new FakeDirectoryEntrySource().Dir(CapCut, FakeDirectoryEntrySource.Folder("Cache"), FakeDirectoryEntrySource.Folder("Projects"))
            .Dir(CapCut + @"\Cache", FakeDirectoryEntrySource.File("effect.bin", 123))
            .Dir(CapCut + @"\Projects", FakeDirectoryEntrySource.File("draft.json", 999999));
        var measured = Inspect(Env(), source);
        Assert.Equal("Observed", State(measured, "capcut")); Assert.Equal(123, Bytes(measured, "capcut"));
        Assert.Equal([CapCut + @"\Cache"], source.Enumerated);
    }

    /// <summary>앱도 경로도 없으면 불필요한 카드가 생기지 않습니다.</summary>
    [Fact]
    public void 미설치_앱은_카드를_만들지_않는다() => Assert.Empty(Inspect(Env(), new FakeDirectoryEntrySource()));

    /// <summary>존재하는 CapCut 사용자 데이터에서 캐시만 없을 때는 부재로 구분합니다.</summary>
    [Fact]
    public void 캐시_없음과_읽기_실패를_구분한다()
    {
        var source = new FakeDirectoryEntrySource().Dir(CapCut);
        Assert.Equal("Absent", State(Inspect(Env(), source), "capcut"));
        source.Deny(CapCut + @"\Cache");
        var observed = Inspect(Env(), source);
        Assert.Equal("AccessDenied", State(observed, "capcut")); Assert.Null(Bytes(observed, "capcut"));
    }

    /// <summary>루트·상위 정션과 온라인 전용 폴더를 열거하지 않습니다.</summary>
    [Theory]
    [InlineData(RootPresence.ReparsePoint, true)]
    [InlineData(RootPresence.ReparsePoint, false)]
    [InlineData(RootPresence.Placeholder, true)]
    [InlineData(RootPresence.Placeholder, false)]
    [InlineData(RootPresence.AccessDenied, false)]
    public void 루트와_상위_특수_폴더를_차단한다(RootPresence presence, bool atRoot)
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache);
        var observed = Inspect(Env(), new PresenceSource(source, atRoot ? Cache : Videos, presence));
        Assert.Equal(presence.ToString(), State(observed)); Assert.Null(Bytes(observed)); Assert.Empty(source.Enumerated);
    }

    /// <summary>일부 파일만 읽었거나 제외했으면 전체 크기로 표현하지 않습니다.</summary>
    [Fact]
    public void 부분_집계와_건너뜀을_보존한다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("good", 100),
            FakeDirectoryEntrySource.Folder("linked", FileAttributes.ReparsePoint), FakeDirectoryEntrySource.File("cloud", 900, FileAttributes.Offline));
        var observed = Inspect(Env(), source);
        Assert.Equal("Partial", State(observed)); Assert.Equal(100, Bytes(observed)); Assert.Single(source.Enumerated);
        var finding = Assert.Single(new VideoEditorCacheRule().Evaluate(Snapshot(observed)));
        Assert.Equal(Verdict.Info, finding.Verdict); Assert.Contains("일부 관측", finding.Title); Assert.All(finding.Actions, a => Assert.IsType<ShowDetailsAction>(a));
    }

    /// <summary>시간이 끝나면 미관측을 0바이트로 오보고하지 않습니다.</summary>
    [Fact]
    public void 예산_소진시_미관측이다()
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache);
        var observed = Inspect(Env(), source, expired: () => true);
        Assert.Equal("TimedOut", State(observed)); Assert.Null(Bytes(observed)); Assert.Empty(source.Enumerated);
    }

    /// <summary>대상별 5초 예산을 적용하고 이미 처리한 크기는 보존합니다.</summary>
    [Fact]
    public void 대상_시간_초과시_부분_집계한다()
    {
        var time = new ManualTimeProvider();
        var source = new FakeDirectoryEntrySource().Dir(Cache, FakeDirectoryEntrySource.File("one", 100), FakeDirectoryEntrySource.File("two", 200));
        source.OnEntry = (_, _) => time.Advance(TimeSpan.FromSeconds(6));
        var observed = Inspect(Env(), source, time: time);
        Assert.Equal("Partial", State(observed)); Assert.Equal(100, Bytes(observed));
    }

    /// <summary>취소 요청을 정상 빈 결과로 감추지 않습니다.</summary>
    [Fact]
    public void 취소를_전달한다() => Assert.Throws<OperationCanceledException>(() => Inspect(Env(), new FakeDirectoryEntrySource().Dir(Cache), ct: new CancellationToken(true)));

    /// <summary>실제 프로브 연결은 승격 여부와 관계없이 이 제한된 읽기 경로만 제공합니다.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 프로브에서_영상_캐시_카드까지_연결된다(bool elevated)
    {
        var pc = new AppCacheTestEnvironment();
        pc.Source.Dir(Cache, FakeDirectoryEntrySource.File("render.dvcc", 100));
        var result = await pc.Probe().RunAsync(AppCacheTestEnvironment.Context(elevated), CancellationToken.None);
        var finding = Assert.Single(new VideoEditorCacheRule().Evaluate(new ScanSnapshot(Guid.NewGuid(), [result])));
        Assert.Equal("video-editor-cache:davinci", finding.Id); Assert.Equal(Verdict.Info, finding.Verdict);
    }

    /// <summary>사용자가 옮긴 경로와 설정의 비밀 값은 기본 내보내기에 남지 않습니다.</summary>
    [Fact]
    public void 내보내기에서_외부_드라이브_경로를_익명화한다()
    {
        var env = Env().WithFile(Path.Combine(Profile, DaVinciCacheConfigReader.RelativeConfig), Config(@"D:\Private Client Video"));
        var measured = Inspect(env, new FakeDirectoryEntrySource().Dir(@"D:\Private Client Video\CacheClip", FakeDirectoryEntrySource.File("x", 123)));
        var report = new ScanReport { ScanId = Guid.NewGuid(), StartedAtUtc = Now, CompletedAtUtc = Now, Outcome = ScanOutcome.Completed,
            AppVersion = "test", RulesVersion = "test", UserContext = new UserContext("anon", false), ProbeSummaries = [], Findings = new VideoEditorCacheRule().Evaluate(Snapshot(measured)) };
        var json = new ReportExporter(new PersonalDataScrubber(Profile, "tester", "FAKE-PC"), @"C:\Windows").SerializeAnonymized(report);
        Assert.DoesNotContain("Private Client", json); Assert.DoesNotContain("secret", json); Assert.Contains("folder-", json);
    }

    /// <summary>관측 카드에서 정리 방법을 직접 펼치며 자동 실행 동작으로 연결하지 않습니다.</summary>
    [Theory]
    [InlineData("davinci", "Delete Render Cache")]
    [InlineData("capcut", "Clear Cache")]
    public void 카드에서_정리_방법을_펼친다(string app, string guide)
    {
        var source = new FakeDirectoryEntrySource().Dir(Cache).Dir(CapCut).Dir(CapCut + @"\Cache");
        var finding = new VideoEditorCacheRule().Evaluate(Snapshot(Inspect(Env(), source))).Single(f => f.Id.EndsWith(app));
        var card = new FindingCardViewModel(finding, new SettingsUriPolicy(NullAppLogger.Instance, _ => throw new InvalidOperationException()),
            new LinkPolicy(null, NullAppLogger.Instance, _ => throw new InvalidOperationException()));
        Assert.True(card.CanShowDetails); Assert.Contains("정리 방법", card.DetailsButtonText);
        card.ToggleDetailsCommand.Execute(null);
        Assert.True(card.IsDetailsVisible); Assert.Contains(guide, finding.Detail);
        Assert.False(card.HasApplyAction); Assert.False(card.CanPrepareAdobe);
    }

    /// <summary>실패·불완전 원시 값을 성공한 0바이트 관측으로 바꾸지 않습니다.</summary>
    [Theory]
    [InlineData("TimedOut", null)]
    [InlineData("Protected", null)]
    [InlineData("Observed", null)]
    [InlineData("Observed", -1L)]
    public void 미관측_용량은_확인불가다(string state, long? bytes)
    {
        var measured = new List<Measurement> { new(VideoEditorCacheRule.Field("davinci", "state"), new TextValue(state), null, "fixture", Now, MeasurementQuality.Observed) };
        if (bytes is { } value) { measured.Add(new(VideoEditorCacheRule.Field("davinci", "bytes"), new IntegerValue(value), "bytes", "fixture", Now, MeasurementQuality.Observed)); }
        var finding = Assert.Single(new VideoEditorCacheRule().Evaluate(Snapshot(measured)));
        Assert.Equal(Verdict.CannotVerify, finding.Verdict); Assert.DoesNotContain("GiB", finding.Title);
    }

    private sealed class PresenceSource(FakeDirectoryEntrySource inner, string path, RootPresence presence) : IDirectoryEntrySource
    {
        /// <inheritdoc />
        public RootPresence ProbeRoot(string candidate) => candidate.Equals(path, StringComparison.OrdinalIgnoreCase) ? presence : inner.ProbeRoot(candidate);
        /// <inheritdoc />
        public IEnumerable<DirectoryEntry> Enumerate(string path) => inner.Enumerate(path);
    }
}
