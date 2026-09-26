/**
 * @file    : ProtectionPolicyParserTests.cs
 * @author  : rudals252
 * @brief   : 보호 정책 JSON 파싱·검증(정상, 알 수 없는 종류·버전 누락·필수 값 누락·상대 경로·잘못된 JSON은 무효) 단위 테스트와 포함 보호 정책(Probes 어셈블리 리소스)의 유효성 확인
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Tests.Unit.Cleaning;

/// <summary>
/// <see cref="ProtectionPolicyParser"/>를 검증합니다. 파일 시스템은 읽지 않습니다(포함 정책은 어셈블리 리소스).
/// </summary>
public sealed class ProtectionPolicyParserTests
{
    private const string VALID_POLICY = """
        {
          "schemaVersion": 1,
          "notes": "테스트",
          "protectedRoots": [
            { "kind": "knownFolder", "folder": "Documents" },
            { "kind": "knownFolder", "folder": "Pictures" },
            { "kind": "environmentPath", "path": "%SystemRoot%\\System32" },
            { "kind": "cloudSyncEnvironment", "provider": "OneDrive", "variable": "OneDrive" },
            { "kind": "cloudSyncRegistry", "provider": "OneDrive", "hive": "HKCU", "key": "Software\\Microsoft\\OneDrive\\Accounts", "value": "UserFolder" },
            { "kind": "cloudSyncJsonFile", "provider": "Dropbox", "file": "%LocalAppData%\\Dropbox\\info.json", "property": "path" }
          ]
        }
        """;

    /// <summary>모든 종류가 들어 있는 정상 정책은 순서대로 해석된다.</summary>
    [Fact]
    public void 정상_정책을_해석한다()
    {
        var result = ProtectionPolicyParser.Parse(VALID_POLICY);

        Assert.True(result.IsValid);
        Assert.Equal(ProtectionPolicyError.None, result.Error);
        var policy = Assert.IsType<ProtectionPolicy>(result.Policy);
        Assert.Equal(ProtectionPolicyParser.SUPPORTED_SCHEMA_VERSION, policy.SchemaVersion);
        Assert.Equal(
            [
                new KnownFolderRootSpec(ProtectedKnownFolder.Documents),
                new KnownFolderRootSpec(ProtectedKnownFolder.Pictures),
                new EnvironmentPathRootSpec(@"%SystemRoot%\System32"),
                new CloudSyncEnvironmentRootSpec("OneDrive", "OneDrive"),
                new CloudSyncRegistryRootSpec("OneDrive", @"Software\Microsoft\OneDrive\Accounts", "UserFolder"),
                new CloudSyncJsonFileRootSpec("Dropbox", @"%LocalAppData%\Dropbox\info.json", "path"),
            ],
            policy.Roots);
    }

    /// <summary>알 수 없는 종류가 하나라도 있으면 정책 전체가 무효다(건너뛰고 계속하지 않음).</summary>
    [Fact]
    public void 알_수_없는_종류는_무효다()
    {
        var json = VALID_POLICY.Replace("\"kind\": \"knownFolder\", \"folder\": \"Pictures\"", "\"kind\": \"wildcardGlob\", \"folder\": \"Pictures\"", StringComparison.Ordinal);

        var result = ProtectionPolicyParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Null(result.Policy);
        Assert.Equal(ProtectionPolicyError.UnknownKind, result.Error);
        Assert.Equal(1, result.EntryIndex);
    }

    /// <summary>스키마 버전이 없으면 무효다.</summary>
    [Fact]
    public void 스키마_버전이_없으면_무효다()
    {
        var result = ProtectionPolicyParser.Parse("""{ "protectedRoots": [ { "kind": "knownFolder", "folder": "Documents" } ] }""");

        Assert.False(result.IsValid);
        Assert.Equal(ProtectionPolicyError.MissingSchemaVersion, result.Error);
    }

    /// <summary>지원하지 않는 스키마 버전과 문자열 버전은 무효다.</summary>
    [Theory]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("\"1\"")]
    [InlineData("1.5")]
    public void 지원하지_않는_스키마_버전은_무효다(string version)
    {
        var result = ProtectionPolicyParser.Parse($$"""{ "schemaVersion": {{version}}, "protectedRoots": [ { "kind": "knownFolder", "folder": "Documents" } ] }""");

        Assert.False(result.IsValid);
        Assert.Equal(ProtectionPolicyError.UnsupportedSchemaVersion, result.Error);
    }

    /// <summary>보호 루트 목록이 없거나 비어 있으면 무효다(보호 없이 순회하지 않도록).</summary>
    [Theory]
    [InlineData("""{ "schemaVersion": 1 }""", ProtectionPolicyError.MissingRoots)]
    [InlineData("""{ "schemaVersion": 1, "protectedRoots": {} }""", ProtectionPolicyError.MissingRoots)]
    [InlineData("""{ "schemaVersion": 1, "protectedRoots": [] }""", ProtectionPolicyError.EmptyRoots)]
    public void 보호_루트가_없으면_무효다(string json, ProtectionPolicyError expected)
    {
        var result = ProtectionPolicyParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
    }

    /// <summary>잘못된 JSON·빈 문자열·null·객체가 아닌 최상위는 무효다.</summary>
    [Theory]
    [InlineData(null, ProtectionPolicyError.Missing)]
    [InlineData("", ProtectionPolicyError.Missing)]
    [InlineData("   ", ProtectionPolicyError.Missing)]
    [InlineData("{ \"schemaVersion\": 1, ", ProtectionPolicyError.MalformedJson)]
    [InlineData("[1, 2]", ProtectionPolicyError.NotAnObject)]
    public void 읽을_수_없는_정책은_무효다(string? json, ProtectionPolicyError expected)
    {
        var result = ProtectionPolicyParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
    }

    /// <summary>항목의 필수 값 누락·빈 값·알 수 없는 Known Folder·HKCU가 아닌 하이브·상대 경로·UNC는 무효다.</summary>
    [Theory]
    [InlineData("""{ "kind": "knownFolder" }""", ProtectionPolicyError.MissingField)]
    [InlineData("""{ "kind": "knownFolder", "folder": "Downloads" }""", ProtectionPolicyError.UnknownKnownFolder)]
    [InlineData("""{ "kind": "knownFolder", "folder": "0" }""", ProtectionPolicyError.UnknownKnownFolder)]
    [InlineData("""{ "kind": "environmentPath", "path": "" }""", ProtectionPolicyError.MissingField)]
    [InlineData("""{ "kind": "environmentPath", "path": "Windows\\System32" }""", ProtectionPolicyError.InvalidPath)]
    [InlineData("""{ "kind": "environmentPath", "path": "\\\\server\\share" }""", ProtectionPolicyError.InvalidPath)]
    [InlineData("""{ "kind": "environmentPath", "path": "C:\\" }""", ProtectionPolicyError.InvalidPath)]
    [InlineData("""{ "kind": "cloudSyncEnvironment", "provider": "OneDrive" }""", ProtectionPolicyError.MissingField)]
    [InlineData("""{ "kind": "cloudSyncRegistry", "provider": "OneDrive", "hive": "HKLM", "key": "Software\\X", "value": "UserFolder" }""", ProtectionPolicyError.UnsupportedHive)]
    [InlineData("""{ "kind": "cloudSyncJsonFile", "provider": "Dropbox", "file": "info.json", "property": "path" }""", ProtectionPolicyError.InvalidPath)]
    [InlineData("""{ "kind": 3 }""", ProtectionPolicyError.MissingField)]
    [InlineData("\"knownFolder\"", ProtectionPolicyError.InvalidEntry)]
    public void 잘못된_항목은_무효다(string entry, ProtectionPolicyError expected)
    {
        var result = ProtectionPolicyParser.Parse($$"""{ "schemaVersion": 1, "protectedRoots": [ { "kind": "knownFolder", "folder": "Desktop" }, {{entry}} ] }""");

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
        Assert.Equal(1, result.EntryIndex);
    }

    /// <summary>포함된 rules/protect.json(Probes 어셈블리 리소스)은 유효하며 스펙 §5.2의 기본 보호 루트를 모두 담는다.</summary>
    [Fact]
    public void 저장소_보호_정책_파일은_유효하다()
    {
        var policy = FileScanService.ReadBundledPolicy();

        var result = ProtectionPolicyParser.Parse(policy ?? string.Empty);

        Assert.True(result.IsValid, result.Error.ToString());
        var roots = result.Policy!.Roots;
        foreach (var folder in Enum.GetValues<ProtectedKnownFolder>())
        {
            Assert.Contains(new KnownFolderRootSpec(folder), roots);
        }

        Assert.Contains(new EnvironmentPathRootSpec("%ProgramFiles%"), roots);
        Assert.Contains(new EnvironmentPathRootSpec("%ProgramFiles(x86)%"), roots);
        Assert.Contains(new EnvironmentPathRootSpec(@"%SystemRoot%\System32"), roots);
        Assert.Contains(new EnvironmentPathRootSpec(@"%SystemRoot%\Installer"), roots);
        Assert.Contains(new EnvironmentPathRootSpec(@"%SystemRoot%\WinSxS"), roots);
        Assert.Contains(roots, root => root is CloudSyncEnvironmentRootSpec { Provider: "OneDrive" });
        Assert.Contains(roots, root => root is CloudSyncRegistryRootSpec { Provider: "OneDrive" });
        Assert.Contains(roots, root => root is CloudSyncJsonFileRootSpec { Provider: "Dropbox" });
    }
}
