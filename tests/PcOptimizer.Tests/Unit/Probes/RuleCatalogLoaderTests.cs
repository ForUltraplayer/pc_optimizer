/**
 * @file    : RuleCatalogLoaderTests.cs
 * @author  : rudals252
 * @brief   : 포함 규칙 로더의 무결성 확인(파일 없음·SHA-256 불일치·출처 목록·메타데이터 무효 시 규칙 미사용), 같은 해시 재사용, 어셈블리 포함 리소스만 읽기(출력 폴더에 규칙 없음·폴더의 일관된 변조 무시), 실제 포함 규칙(스냅샷 해시·항목 수·보충 규칙 6종 전부 지원·Squirrel 목록·메타데이터 짝·금지 문구·app-* FileKey 없음)을 검증
 */

// 기본 패키지
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Tests.Unit.Probes.Fakes;
using PcOptimizer.Tests.Unit.Rules;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="RuleCatalogLoader"/>와 포함 규칙 데이터를 검증합니다. 네트워크에 접근하지 않습니다.
/// </summary>
public sealed class RuleCatalogLoaderTests(ITestOutputHelper output)
{
    /// <summary>테스트 출력 폴더의 rules 폴더(보호 정책·라이선스 고지만 복사되고 앱 캐시 규칙 파일은 없어야 함).</summary>
    public static readonly string OUTPUT_RULES = Path.Combine(AppContext.BaseDirectory, "rules");

    /// <summary>
    /// 포함 리소스 하나를 읽는다(없으면 테스트 실패).
    /// </summary>
    internal static byte[] Embedded(string name)
    {
        return RuleCatalogLoader.ReadEmbeddedFile(name) ?? throw new InvalidOperationException(name + " 포함 리소스 없음");
    }

    private const string SMALL_WINAPP2 = "[Tiny App *]\nDetectFile=%LocalAppData%\\Tiny\nFileKey1=%LocalAppData%\\Tiny\\Cache|*|RECURSE\n";

    /// <summary>
    /// 파일 이름 → 바이트 사전과 그에 맞는 sources.json을 만든다.
    /// </summary>
    internal static Dictionary<string, byte[]> Files(string winapp2, string? supplement = null, string? metadata = null)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["winapp2.ini"] = Encoding.UTF8.GetBytes(winapp2),
            ["supplement.ini"] = supplement is null ? Embedded("supplement.ini") : Encoding.UTF8.GetBytes(supplement),
            ["rule-metadata.json"] = metadata is null ? Embedded("rule-metadata.json") : Encoding.UTF8.GetBytes(metadata),
        };
        files["sources.json"] = Encoding.UTF8.GetBytes(Manifest(files));
        return files;
    }

    /// <summary>
    /// 파일 해시로 출처 목록 JSON을 만든다.
    /// </summary>
    internal static string Manifest(Dictionary<string, byte[]> files)
    {
        object Entry(string path, string kind) => new
        {
            path,
            kind,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(files[path])),
            license = "CC-BY-SA-4.0",
            upstreamCommit = "0123456789abcdef0123456789abcdef01234567",
            upstreamVersion = "999999",
        };
        return JsonSerializer.Serialize(new { schemaVersion = 1, files = new[] { Entry("winapp2.ini", "winapp2"), Entry("supplement.ini", "supplement"), Entry("rule-metadata.json", "metadata") } });
    }

    /// <summary>
    /// 사전으로 로더를 만든다.
    /// </summary>
    internal static RuleCatalogLoader Loader(Dictionary<string, byte[]> files)
    {
        return new RuleCatalogLoader(name => files.TryGetValue(name, out var bytes) ? bytes : null, new ManualTimeProvider());
    }

    /// <summary>해시가 맞으면 두 규칙 파일을 해석하고 메타데이터를 붙인다. 같은 해시로 다시 부르면 해석 결과를 재사용한다.</summary>
    [Fact]
    public void 해시가_맞으면_해석하고_재사용한다()
    {
        var loader = Loader(Files(SMALL_WINAPP2));

        var first = loader.Load();
        var second = loader.Load();

        Assert.True(first.IsVerified);
        Assert.Same(first, second);
        Assert.Equal(1, first.CommunityReport!.TotalRules);
        Assert.Equal(6, first.SupplementReport!.TotalRules);
        Assert.Equal("999999", first.Winapp2Source!.UpstreamVersion);
    }

    /// <summary>무결성 실패 경로: 스냅샷이 한 바이트라도 다르면 불일치, 파일이 없으면 없음, 출처 목록·메타데이터가 무효이면 각각의 상태로 규칙을 쓰지 않는다.</summary>
    [Fact]
    public void 무결성_확인에_실패하면_규칙을_쓰지_않는다()
    {
        var tampered = Files(SMALL_WINAPP2);
        tampered["winapp2.ini"] = Encoding.UTF8.GetBytes(SMALL_WINAPP2 + "FileKey2=%LocalAppData%|*.*|RECURSE\n");
        var missing = Files(SMALL_WINAPP2);
        missing.Remove("supplement.ini");
        var noManifest = Files(SMALL_WINAPP2);
        noManifest.Remove("sources.json");
        var badManifest = Files(SMALL_WINAPP2);
        badManifest["sources.json"] = Encoding.UTF8.GetBytes("""{ "schemaVersion": 1, "files": [] }""");
        var badMetadata = Files(SMALL_WINAPP2, metadata: """{ "schemaVersion": 1, "rules": [ { "ruleId": "x" } ] }""");

        AssertFailed(Loader(tampered).Load(), AppCacheProbeContract.CATALOG_MISMATCH, "winapp2.ini");
        AssertFailed(Loader(missing).Load(), AppCacheProbeContract.CATALOG_MISSING, "supplement.ini");
        AssertFailed(Loader(noManifest).Load(), AppCacheProbeContract.CATALOG_MISSING, RuleCatalogLoader.MANIFEST_FILE);
        AssertFailed(Loader(badManifest).Load(), AppCacheProbeContract.CATALOG_INVALID_MANIFEST, RuleCatalogLoader.MANIFEST_FILE);
        AssertFailed(Loader(badMetadata).Load(), AppCacheProbeContract.CATALOG_INVALID_METADATA, "rule-metadata.json");
    }

    /// <summary>
    /// 실패 목록인지 확인한다.
    /// </summary>
    private static void AssertFailed(RuleCatalog catalog, string state, string file)
    {
        Assert.False(catalog.IsVerified);
        Assert.Equal(state, catalog.State);
        Assert.Equal(file, catalog.FailedFile);
        Assert.Empty(catalog.Rules);
    }

    /// <summary>
    /// 규칙은 어셈블리 포함 리소스에서만 읽는다: 출력 폴더(AppContext.BaseDirectory\rules)에는 앱 캐시 규칙 파일이 없고(복사하지 않음),
    /// 그래도 포함 규칙이 무결성 확인을 통과한다. 따라서 사용자 쓰기 가능한 폴더에 규칙·sources.json을 함께 바꿔 놓아도 적용되지 않는다.
    /// </summary>
    [Fact]
    public void 포함_리소스만_읽고_출력_폴더의_규칙_파일은_쓰지_않는다()
    {
        foreach (var name in new[] { "winapp2.ini", "supplement.ini", "rule-metadata.json", RuleCatalogLoader.MANIFEST_FILE })
        {
            Assert.False(File.Exists(Path.Combine(OUTPUT_RULES, name)), name + " 파일이 출력 폴더에 있다");
        }

        var catalog = RuleCatalogLoader.CreateEmbedded().Load();

        Assert.True(catalog.IsVerified);
        Assert.Contains(RuleCatalogLoader.RESOURCE_PREFIX + "winapp2.ini", typeof(RuleCatalogLoader).Assembly.GetManifestResourceNames());
    }

    /// <summary>
    /// 사용자 쓰기 가능한 폴더에 규칙 파일과 그 해시를 담은 sources.json을 함께 바꿔 놓아도(일관된 변조) 로더는 포함 리소스만 써서
    /// 원래 스냅샷(4,068개)을 해석한다. 같은 클래스 안이라 출력 폴더 검사와 동시에 실행되지 않으며, 끝나면 만든 파일을 지운다.
    /// </summary>
    [Fact]
    public void 출력_폴더의_규칙과_출처_목록을_함께_바꿔도_포함_규칙을_쓴다()
    {
        var written = new List<string>();
        Directory.CreateDirectory(OUTPUT_RULES);
        try
        {
            foreach (var (name, bytes) in Files(SMALL_WINAPP2))
            {
                var path = Path.Combine(OUTPUT_RULES, name);
                File.WriteAllBytes(path, bytes);
                written.Add(path);
            }

            var catalog = RuleCatalogLoader.CreateEmbedded().Load();

            Assert.True(catalog.IsVerified);
            Assert.Equal(4068, catalog.CommunityReport!.TotalRules);
        }
        finally
        {
            foreach (var path in written)
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>실제 포함 규칙: sources.json의 해시와 파일이 맞고, 스냅샷은 머리글의 항목 수(4,068)와 같으며, 커밋·원본 해시·라이선스가 기록되어 있다.</summary>
    [Fact]
    public void 포함_규칙_스냅샷의_출처와_무결성이_맞다()
    {
        var catalog = RuleCatalogLoader.CreateEmbedded().Load();
        using var manifest = JsonDocument.Parse(Embedded(RuleCatalogLoader.MANIFEST_FILE));
        var winapp2 = manifest.RootElement.GetProperty("files").EnumerateArray().Single(file => file.GetProperty("kind").GetString() == "winapp2");

        Assert.True(catalog.IsVerified, catalog.State + " " + catalog.FailedFile);
        Assert.Equal(4068, catalog.CommunityReport!.TotalRules);
        Assert.Equal("260915", catalog.Winapp2Source!.UpstreamVersion);
        Assert.Equal("53ae41946c3d3c8bb348d81081c34b6050f0b561", catalog.Winapp2Source.UpstreamCommit);
        Assert.Equal("CC-BY-SA-4.0", catalog.Winapp2Source.License);
        Assert.Matches("^[0-9a-f]{64}$", winapp2.GetProperty("upstreamSha256").GetString());
        Assert.Contains("the winapp2 project", winapp2.GetProperty("attribution").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("; Version: 260915", Encoding.UTF8.GetString(Embedded("winapp2.ini")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(OUTPUT_RULES, "LICENSE-winapp2.md")));

        var report = catalog.CommunityReport;
        output.WriteLine($"winapp2 total={report.TotalRules} supported={report.SupportedRules} unsupported={report.UnsupportedRules} parseMs={catalog.ParseElapsed.TotalMilliseconds:0}");
        foreach (var (reason, count) in report.UnsupportedByReason.OrderByDescending(pair => pair.Value))
        {
            output.WriteLine($"  {reason}={count}");
        }

        foreach (var rule in catalog.Rules.Where(rule => rule.UnsupportedReason is { } reason
            && reason is not (UnsupportedRuleReason.RegistryOnly or UnsupportedRuleReason.VolumeRootPath)))
        {
            output.WriteLine($"  - {rule.Name}: {rule.UnsupportedReason} ({rule.UnsupportedDetail})");
        }

        Assert.True(report.SupportedRules > report.TotalRules / 2, "지원 규칙이 절반 이상이어야 한다(모두 미지원으로만 처리한 구현 방지)");
    }

    /// <summary>
    /// 보충 규칙 6종은 모두 지원 문법으로 해석되고 각각 검토 메타데이터(앱 이름 포함)가 있으며, Squirrel 버전 폴더는 정리 규칙이 아니라
    /// 메타데이터 목록(확인한 앱 폴더·app-* 패턴·Update.exe)으로만 정의된다. 영향 문장에 금지 문구가 없다.
    /// </summary>
    [Fact]
    public void 보충_규칙은_모두_지원되고_메타데이터와_짝이_맞다()
    {
        var catalog = RuleCatalogLoader.CreateEmbedded().Load();
        var supplement = catalog.Rules.Where(rule => rule.Origin == RuleOrigin.Supplement).ToList();

        Assert.Equal(6, supplement.Count);
        Assert.All(supplement, rule => Assert.True(rule.IsSupported, rule.Name + " " + rule.UnsupportedReason));
        Assert.All(supplement, rule => Assert.False(string.IsNullOrWhiteSpace(catalog.Metadata[rule.Id].AppLabel)));
        var listing = Assert.Single(catalog.Metadata.Values, meta => meta.Observation == ObservationKind.FolderNamesOnly);
        Assert.Equal("listing:Squirrel app version folders", listing.RuleId);
        Assert.Equal([@"%LocalAppData%\Discord"], listing.Listing!.ParentTemplates);
        Assert.Equal("app-*", listing.Listing.Pattern);
        Assert.Equal("Update.exe", listing.Listing.Marker);
        Assert.Equal(
            supplement.Select(rule => rule.Id).Append(listing.RuleId).Order(StringComparer.Ordinal),
            catalog.Metadata.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["adobe", "npm", "nuget", "pip", "steam"], catalog.Metadata.Values.Select(meta => meta.ConfigReader).OfType<string>().Order(StringComparer.Ordinal));
        foreach (var meta in catalog.Metadata.Values)
        {
            var text = string.Join('\n', meta.Impact.Benefit, meta.Impact.SideEffect, meta.Impact.Regeneration, meta.AppVersionNotes);
            Assert.NotEmpty(meta.Sources);
            foreach (var phrase in AppCacheTestData.FORBIDDEN_PHRASES)
            {
                Assert.DoesNotContain(phrase, text, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// supplement.ini(winapp2 형식, 다른 정리 도구가 읽을 수 있음)에는 앱 버전 폴더(app-*)를 가리키는 FileKey가 없다.
    /// 그런 FileKey는 실제 정리 도구에서 설치된 앱을 지우는 삭제 규칙이 되므로 이름 나열은 코드와 메타데이터로만 한다.
    /// </summary>
    [Fact]
    public void 보충_규칙에는_앱_버전_폴더_FileKey가_없다()
    {
        var text = Encoding.UTF8.GetString(Embedded("supplement.ini"));
        var rules = Winapp2Parser.Parse(text, RuleOrigin.Supplement).Rules;

        Assert.DoesNotContain(rules.SelectMany(rule => rule.FileKeys), key => key.PathTemplate.Contains("app-", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Squirrel", string.Join('|', rules.Select(rule => rule.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("app-*", text, StringComparison.OrdinalIgnoreCase);
    }
}
