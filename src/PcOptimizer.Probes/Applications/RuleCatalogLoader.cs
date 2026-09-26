/**
 * @file    : RuleCatalogLoader.cs
 * @author  : rudals252
 * @brief   : 앱 폴더의 rules\ 아래 포함 규칙 파일(sources.json이 가리키는 winapp2.ini·supplement.ini·rule-metadata.json)만 읽어 SHA-256 무결성을 확인한 뒤 해석하는 로더(내려받기·사용자 경로 없음, 파일이 없거나 해시가 다르면 규칙을 쓰지 않음, 같은 해시면 해석 결과 재사용)
 */

// 기본 패키지
using System.Security;
using System.Security.Cryptography;
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 포함 규칙 목록입니다. 무결성을 확인하지 못하면 규칙이 비어 있습니다.
/// </summary>
/// <param name="State">상태(<c>AppCacheProbeContract.CATALOG_*</c>).</param>
/// <param name="FailedFile">문제 파일 이름(실패일 때만).</param>
/// <param name="Winapp2Source">커뮤니티 스냅샷 출처 정보.</param>
/// <param name="Rules">규칙(커뮤니티 뒤에 보충, 원본 순서).</param>
/// <param name="CommunityReport">커뮤니티 해석 요약.</param>
/// <param name="SupplementReport">보충 규칙 해석 요약.</param>
/// <param name="Metadata">규칙 ID별 검토 메타데이터.</param>
/// <param name="ParseElapsed">해석 소요 시간.</param>
public sealed record RuleCatalog(
    string State,
    string? FailedFile,
    RuleSourceFile? Winapp2Source,
    IReadOnlyList<CleaningRule> Rules,
    ParseReport? CommunityReport,
    ParseReport? SupplementReport,
    IReadOnlyDictionary<string, RuleMetadata> Metadata,
    TimeSpan ParseElapsed)
{
    /// <summary>무결성을 확인하고 해석했는지 여부.</summary>
    public bool IsVerified => State == AppCacheProbeContract.CATALOG_VERIFIED;

    /// <summary>
    /// 실패 목록을 만듭니다.
    /// </summary>
    /// <param name="state">상태.</param>
    /// <param name="file">문제 파일.</param>
    /// <returns>규칙이 없는 목록.</returns>
    public static RuleCatalog Failed(string state, string file)
    {
        return new RuleCatalog(state, file, null, [], null, null, new Dictionary<string, RuleMetadata>(), TimeSpan.Zero);
    }
}

/// <summary>
/// 포함 규칙 로더입니다(스펙 §5.1 "앱은 포함된 고정 규칙 스냅샷을 사용하며 자동 규칙 다운로드는 없다", §6 "관리자 검사에서는 무결성이 확인된 포함 규칙만 사용").
/// </summary>
public sealed class RuleCatalogLoader
{
    /// <summary>앱 폴더 기준 규칙 폴더.</summary>
    public const string RULES_FOLDER = "rules";

    /// <summary>출처 목록 파일 이름.</summary>
    public const string MANIFEST_FILE = "sources.json";

    /// <summary>규칙 파일 하나의 최대 크기(바이트).</summary>
    public const int MAX_RULE_FILE_BYTES = 16 * 1024 * 1024;

    private const char BYTE_ORDER_MARK = '\uFEFF';

    private readonly Func<string, byte[]?> _readFile;
    private readonly TimeProvider _time;
    private readonly Lock _cacheLock = new();
    private string? _cachedKey;
    private RuleCatalog? _cached;

    /// <summary>
    /// 로더를 만듭니다.
    /// </summary>
    /// <param name="readFile">규칙 폴더의 파일 이름으로 바이트를 읽는 함수(없거나 읽지 못하면 null).</param>
    /// <param name="time">시간 공급자(해석 시간 측정).</param>
    public RuleCatalogLoader(Func<string, byte[]?> readFile, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(time);
        _readFile = readFile;
        _time = time;
    }

    /// <summary>
    /// 앱 폴더(<see cref="AppContext.BaseDirectory"/>)의 rules\만 읽는 로더를 만듭니다. 승격 여부와 관계없이 다른 경로를 읽지 않습니다.
    /// </summary>
    /// <returns>로더.</returns>
    public static RuleCatalogLoader CreateBundled()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, RULES_FOLDER);
        return new RuleCatalogLoader(name => ReadBundledFile(folder, name), TimeProvider.System);
    }

    /// <summary>
    /// 규칙 폴더의 파일 하나를 읽는다(단순 파일 이름만, 없거나 너무 크거나 읽지 못하면 null).
    /// </summary>
    private static byte[]? ReadBundledFile(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MAX_RULE_FILE_BYTES ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// 규칙을 읽고 무결성을 확인한 뒤 해석합니다. 파일 해시가 지난번과 같으면 해석 결과를 재사용합니다.
    /// </summary>
    /// <returns>규칙 목록.</returns>
    public RuleCatalog Load()
    {
        var manifestBytes = _readFile(MANIFEST_FILE);
        if (manifestBytes is null)
        {
            return RuleCatalog.Failed(AppCacheProbeContract.CATALOG_MISSING, MANIFEST_FILE);
        }

        var manifest = RuleSourceManifest.Parse(Decode(manifestBytes));
        if (manifest is null)
        {
            return RuleCatalog.Failed(AppCacheProbeContract.CATALOG_INVALID_MANIFEST, MANIFEST_FILE);
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kind in new[] { RuleSourceManifest.KIND_WINAPP2, RuleSourceManifest.KIND_SUPPLEMENT, RuleSourceManifest.KIND_METADATA })
        {
            var file = manifest.Find(kind)!;
            var bytes = _readFile(file.Path);
            if (bytes is null)
            {
                return RuleCatalog.Failed(AppCacheProbeContract.CATALOG_MISSING, file.Path);
            }

            if (!string.Equals(Sha256(bytes), file.Sha256, StringComparison.Ordinal))
            {
                return RuleCatalog.Failed(AppCacheProbeContract.CATALOG_MISMATCH, file.Path);
            }

            texts[kind] = Decode(bytes);
        }

        var key = string.Join('|', manifest.Files.Select(file => file.Sha256));
        lock (_cacheLock)
        {
            if (_cached is not null && _cachedKey == key)
            {
                return _cached;
            }
        }

        var catalog = Parse(manifest, texts);
        lock (_cacheLock)
        {
            _cachedKey = catalog.IsVerified ? key : null;
            _cached = catalog.IsVerified ? catalog : null;
        }

        return catalog;
    }

    /// <summary>
    /// 확인을 마친 텍스트를 해석한다.
    /// </summary>
    private RuleCatalog Parse(RuleSourceManifest manifest, Dictionary<string, string> texts)
    {
        var start = _time.GetTimestamp();
        var metadata = RuleMetadataParser.Parse(texts[RuleSourceManifest.KIND_METADATA]);
        if (metadata is null)
        {
            return RuleCatalog.Failed(AppCacheProbeContract.CATALOG_INVALID_METADATA, manifest.Find(RuleSourceManifest.KIND_METADATA)!.Path);
        }

        var community = Winapp2Parser.Parse(texts[RuleSourceManifest.KIND_WINAPP2], RuleOrigin.Community);
        var supplement = Winapp2Parser.Parse(texts[RuleSourceManifest.KIND_SUPPLEMENT], RuleOrigin.Supplement);
        return new RuleCatalog(
            AppCacheProbeContract.CATALOG_VERIFIED,
            null,
            manifest.Find(RuleSourceManifest.KIND_WINAPP2),
            [.. community.Rules, .. supplement.Rules],
            community.Report,
            supplement.Report,
            metadata,
            _time.GetElapsedTime(start));
    }

    /// <summary>
    /// UTF-8로 읽는다(BOM 제거).
    /// </summary>
    private static string Decode(byte[] bytes)
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes).TrimStart(BYTE_ORDER_MARK);
    }

    /// <summary>
    /// SHA-256 소문자 16진수.
    /// </summary>
    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
