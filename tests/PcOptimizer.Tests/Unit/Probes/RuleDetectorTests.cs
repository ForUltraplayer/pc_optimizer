/**
 * @file    : RuleDetectorTests.cs
 * @author  : rudals252
 * @brief   : 규칙 탐지기의 OR 판정(레지스트리·파일), HKLM 64/32비트·HKCR 병합 보기, 와일드카드 DetectFile, 구성 요소당 일치 한도 초과·보호 루트 안 와일드카드·접근 거부의 확인 불가, 탐지 근거 없는 규칙 제외, 검사 ID별 캐시를 가짜 레지스트리·파일 시스템으로 검증
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="RuleDetector"/>를 검증합니다. 실제 레지스트리·파일을 읽지 않고 앱을 실행하지 않습니다.
/// </summary>
public sealed class RuleDetectorTests
{
    private const string PROFILE = @"C:\Users\tester";
    private const string LOCAL = PROFILE + @"\AppData\Local";
    private const string DOCUMENTS = PROFILE + @"\Documents";

    /// <summary>
    /// 하위 키 이름 조회를 세는 레지스트리.
    /// </summary>
    private sealed class CountingRegistry(FakeRegistryReader inner) : IRegistryReader
    {
        /// <summary>하위 키 조회 수.</summary>
        public int SubKeyReads { get; private set; }

        /// <inheritdoc />
        public RegistryValueReading ReadValue(RegistryRoot root, string subKey, string valueName) => inner.ReadValue(root, subKey, valueName);

        /// <inheritdoc />
        public RegistryKeyReading ReadKeyValues(RegistryRoot root, RegistryView view, string subKey) => inner.ReadKeyValues(root, view, subKey);

        /// <inheritdoc />
        public RegistryStringReading ReadStringValue(RegistryRoot root, RegistryView view, string subKey, string valueName) => inner.ReadStringValue(root, view, subKey, valueName);

        /// <inheritdoc />
        public RegistrySubKeyReading ReadSubKeyNames(RegistryRoot root, RegistryView view, string subKey)
        {
            SubKeyReads++;
            return inner.ReadSubKeyNames(root, view, subKey);
        }
    }

    /// <summary>
    /// 규칙 하나를 해석한다.
    /// </summary>
    private static CleaningRule Rule(string body, string name = "Fake")
    {
        return Assert.Single(Winapp2Parser.Parse("[" + name + " *]\n" + body + "\nFileKey1=%LocalAppData%\\Fake|*\n", RuleOrigin.Community).Rules);
    }

    /// <summary>
    /// 규칙들을 탐지한다(문서 폴더 보호).
    /// </summary>
    private static IReadOnlyDictionary<string, DetectionState> Detect(IRegistryReader registry, FakeDirectoryEntrySource source, params CleaningRule[] rules)
    {
        var environment = new FakePathEnvironment { Profile = PROFILE }.WithVariable("LOCALAPPDATA", LOCAL).WithKnownFolder(ProtectedKnownFolder.Documents, DOCUMENTS);
        var detector = new RuleDetector(registry, source, new Winapp2PathResolver(environment));
        return detector.Detect(Guid.NewGuid(), rules, path => PathScope.IsSameOrUnder(path, DOCUMENTS), CancellationToken.None);
    }

    /// <summary>
    /// 존재하는 키를 등록한 레지스트리.
    /// </summary>
    private static FakeRegistryReader Registry(RegistryRoot root, RegistryView view, string subKey)
    {
        return new FakeRegistryReader().WithSubKeys(root, view, subKey, new RegistrySubKeyReading(RegistryReadStatus.Found, [], null));
    }

    /// <summary>Detect 여러 개는 OR: 첫 키가 없어도 둘째 키가 있으면 탐지된다. 모두 없으면 탐지 안 됨.</summary>
    [Fact]
    public void Detect는_OR로_판정한다()
    {
        var rule = Rule("Detect1=HKCU\\Software\\Missing\nDetect2=HKCU\\Software\\Vendor");
        var registry = Registry(RegistryRoot.CurrentUser, RegistryView.Default, @"Software\Vendor");

        Assert.Equal(DetectionState.Detected, Detect(registry, new FakeDirectoryEntrySource(), rule)[rule.Id]);
        Assert.Equal(DetectionState.NotDetected, Detect(new FakeRegistryReader(), new FakeDirectoryEntrySource(), rule)[rule.Id]);
    }

    /// <summary>HKLM은 32비트 보기(WOW6432Node)만 있어도 탐지하고, HKCR은 사용자 Software\Classes에서 찾는다.</summary>
    [Fact]
    public void HKLM_32비트_보기와_HKCR을_확인한다()
    {
        var machine = Rule("Detect=HKLM\\Software\\Vendor32");
        var classes = Rule("Detect=HKCR\\Vendor.Document");

        Assert.Equal(DetectionState.Detected, Detect(Registry(RegistryRoot.LocalMachine, RegistryView.Registry32, @"Software\Vendor32"), new FakeDirectoryEntrySource(), machine)[machine.Id]);
        Assert.Equal(DetectionState.Detected, Detect(Registry(RegistryRoot.CurrentUser, RegistryView.Default, @"Software\Classes\Vendor.Document"), new FakeDirectoryEntrySource(), classes)[classes.Id]);
    }

    /// <summary>레지스트리 접근 거부만 있고 맞는 조건이 없으면 확인 불가다(탐지 안 됨으로 바꾸지 않음).</summary>
    [Fact]
    public void 레지스트리_접근_거부는_확인_불가다()
    {
        var rule = Rule("Detect=HKCU\\Software\\Denied");
        var registry = new FakeRegistryReader().WithSubKeys(RegistryRoot.CurrentUser, RegistryView.Default, @"Software\Denied", new RegistrySubKeyReading(RegistryReadStatus.AccessDenied, [], "UnauthorizedAccessException"));

        Assert.Equal(DetectionState.Unknown, Detect(registry, new FakeDirectoryEntrySource(), rule)[rule.Id]);
    }

    /// <summary>DetectFile은 파일·폴더 존재를 보고, 와일드카드 구성 요소는 부모를 열거해 찾는다(OR).</summary>
    [Fact]
    public void DetectFile은_와일드카드_경로와_파일을_찾는다()
    {
        var rule = Rule("DetectFile1=%LocalAppData%\\Nope\nDetectFile2=%LocalAppData%\\Vendor*\\app.exe");
        var source = new FakeDirectoryEntrySource()
            .Dir(LOCAL, FakeDirectoryEntrySource.Folder("Other"), FakeDirectoryEntrySource.Folder("VendorApp"))
            .Dir(LOCAL + @"\VendorApp", FakeDirectoryEntrySource.File("app.exe", 10));

        Assert.Equal(DetectionState.Detected, Detect(new FakeRegistryReader(), source, rule)[rule.Id]);
    }

    /// <summary>와일드카드 구성 요소가 64개보다 많이 맞으면 일부만 보지 않고 확인 불가로 둔다.</summary>
    [Fact]
    public void 와일드카드_일치가_한도를_넘으면_확인_불가다()
    {
        var rule = Rule("DetectFile=%LocalAppData%\\Pkg*\\marker.txt");
        var folders = Enumerable.Range(0, PathPatternExpander.MAX_MATCHES_PER_SEGMENT + 1).Select(index => FakeDirectoryEntrySource.Folder("Pkg" + index)).ToArray();
        var source = new FakeDirectoryEntrySource().Dir(LOCAL, folders);

        Assert.Equal(DetectionState.Unknown, Detect(new FakeRegistryReader(), source, rule)[rule.Id]);
    }

    /// <summary>보호 루트 안의 와일드카드는 열거하지 않아 확인 불가, 와일드카드 없는 경로는 속성 조회만으로 확인한다.</summary>
    [Fact]
    public void 보호_루트_안은_와일드카드를_열거하지_않는다()
    {
        var wildcard = Rule("DetectFile=%Documents%\\Game*\\save.dat");
        var exact = Rule("DetectFile=%Documents%\\GameA", "Exact");
        var source = new FakeDirectoryEntrySource().Dir(DOCUMENTS, FakeDirectoryEntrySource.Folder("GameA")).Dir(DOCUMENTS + @"\GameA");

        var result = Detect(new FakeRegistryReader(), source, wildcard, exact);

        Assert.Equal(DetectionState.Unknown, result[wildcard.Id]);
        Assert.Equal(DetectionState.Detected, result[exact.Id]);
        Assert.DoesNotContain(DOCUMENTS, source.Enumerated);
    }

    /// <summary>DetectFile 경로의 중간 폴더가 정션(Documents and Settings)이면 따라가지 않고 확인 불가로 둔다(링크 너머의 파일로 탐지하지 않음).</summary>
    [Fact]
    public void 중간_정션을_거치는_DetectFile은_따라가지_않는다()
    {
        var literal = Rule("DetectFile=%SystemDrive%\\Documents and Settings\\Default\\App", "Literal");
        var wildcard = Rule("DetectFile=%SystemDrive%\\Documents and Settings\\Def*\\App", "Wildcard");
        var source = new FakeDirectoryEntrySource()
            .Dir(@"C:\", FakeDirectoryEntrySource.Folder("Documents and Settings", System.IO.FileAttributes.ReparsePoint))
            .Dir(@"C:\Documents and Settings", FakeDirectoryEntrySource.Folder("Default"))
            .Dir(@"C:\Documents and Settings\Default\App");
        var detector = new RuleDetector(new FakeRegistryReader(), source, new Winapp2PathResolver(new FakePathEnvironment { Profile = PROFILE }.WithVariable("SystemDrive", "C:")));

        var result = detector.Detect(Guid.NewGuid(), [literal, wildcard], _ => false, CancellationToken.None);

        Assert.Equal(DetectionState.Unknown, result[literal.Id]);
        Assert.Equal(DetectionState.Unknown, result[wildcard.Id]);
        Assert.DoesNotContain(@"C:\Documents and Settings", source.Enumerated);
    }

    /// <summary>탐지 근거가 없는 규칙은 파서가 미지원으로 두며 탐지 결과에 나오지 않는다(모든 사용자에게 적용하지 않음).</summary>
    [Fact]
    public void 탐지_근거가_없는_규칙은_탐지하지_않는다()
    {
        var rule = Assert.Single(Winapp2Parser.Parse("[NoDetect *]\nFileKey1=%LocalAppData%\\X|*\n", RuleOrigin.Community).Rules);

        var result = Detect(new FakeRegistryReader(), new FakeDirectoryEntrySource(), rule);

        Assert.Equal(UnsupportedRuleReason.NoSafeDetection, rule.UnsupportedReason);
        Assert.Empty(result);
    }

    /// <summary>같은 검사 ID로 다시 부르면 레지스트리를 다시 읽지 않고, 새 검사 ID면 다시 읽는다.</summary>
    [Fact]
    public void 검사_ID별로_캐시한다()
    {
        var rule = Rule("Detect=HKCU\\Software\\Vendor");
        var registry = new CountingRegistry(Registry(RegistryRoot.CurrentUser, RegistryView.Default, @"Software\Vendor"));
        var detector = new RuleDetector(registry, new FakeDirectoryEntrySource(), new Winapp2PathResolver(new FakePathEnvironment { Profile = PROFILE }));
        var scanId = Guid.NewGuid();

        detector.Detect(scanId, [rule], _ => false, CancellationToken.None);
        detector.Detect(scanId, [rule], _ => false, CancellationToken.None);
        var afterSame = registry.SubKeyReads;
        detector.Detect(Guid.NewGuid(), [rule], _ => false, CancellationToken.None);

        Assert.Equal(1, afterSame);
        Assert.Equal(2, registry.SubKeyReads);
    }
}
