/**
 * @file    : ProtectionPolicyResolver.cs
 * @author  : rudals252
 * @brief   : 보호 정책 항목을 실제 정규화 경로로 해석(리디렉션 반영 Known Folder와 프로필 기본 위치 항상 보호, 환경 변수 시스템 경로, OneDrive 환경 변수·HKCU 계정 UserFolder, Dropbox info.json path)하는 조회 전용 해석기
 */

// 기본 패키지
using System.Text.Json;
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 보호 정책 해석기입니다. 경로를 찾지 못한 항목(설치되지 않은 동기화 앱, 없는 환경 변수 등)은 건너뛰고 개수만 셉니다.
/// Known Folder는 현재 위치와 프로필 안 기본 위치를 모두 보호하며, 현재 위치를 못 읽어도 기본 위치 보호는 유지합니다(실패 시 닫힘).
/// UNC·상대 경로는 보호 루트로 쓰지 않습니다(스캔 루트가 될 수 없어 비교 대상이 아님). 드라이브 루트(예: 문서 폴더를 "D:\"로 지정)는
/// 드라이브 전체를 보호하는 루트로 인정합니다(스캔 루트에서는 금지).
/// Google Drive 동기화 루트 감지는 1차에서 지원하지 않습니다.
/// </summary>
public sealed class ProtectionPolicyResolver
{
    /// <summary>동기화 앱 설정 JSON 파일의 최대 읽기 크기(바이트).</summary>
    public const int MAX_SYNC_SETTINGS_BYTES = 64 * 1024;

    /// <summary>Known Folder의 프로필 안 기본 폴더 이름(리디렉션 여부와 관계없이 항상 보호).</summary>
    public static readonly IReadOnlyDictionary<ProtectedKnownFolder, string> KNOWN_FOLDER_DEFAULT_NAMES = new Dictionary<ProtectedKnownFolder, string>
    {
        [ProtectedKnownFolder.Documents] = "Documents",
        [ProtectedKnownFolder.Pictures] = "Pictures",
        [ProtectedKnownFolder.Desktop] = "Desktop",
        [ProtectedKnownFolder.Videos] = "Videos",
        [ProtectedKnownFolder.Music] = "Music",
    };

    private readonly IPathEnvironment _environment;
    private readonly IRegistryReader _registry;

    /// <summary>
    /// 해석기를 만듭니다.
    /// </summary>
    /// <param name="environment">경로 환경.</param>
    /// <param name="registry">레지스트리 읽기(HKCU 동기화 계정 조회).</param>
    public ProtectionPolicyResolver(IPathEnvironment environment, IRegistryReader registry)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(registry);
        _environment = environment;
        _registry = registry;
    }

    /// <summary>
    /// 정책을 해석합니다.
    /// </summary>
    /// <param name="policy">검증된 정책.</param>
    /// <returns>해석된 보호 루트.</returns>
    public ResolvedProtection Resolve(ProtectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var roots = new List<ProtectedRoot>();
        var unresolved = 0;
        foreach (var spec in policy.Roots)
        {
            if (spec is KnownFolderRootSpec known)
            {
                // Known Folder는 실패해도 닫힌 쪽으로: 기본 위치는 항상 보호하고, 현재 위치를 못 읽은 것만 해석 실패로 센다.
                var (knownRoots, located) = ResolveKnownFolder(known);
                if (!located)
                {
                    unresolved++;
                }

                roots.AddRange(knownRoots);
                continue;
            }

            var resolved = spec switch
            {
                EnvironmentPathRootSpec environmentPath => ResolveEnvironmentPath(environmentPath),
                CloudSyncEnvironmentRootSpec variable => ResolveCloudSyncEnvironment(variable),
                CloudSyncRegistryRootSpec registry => ResolveCloudSyncRegistry(registry),
                CloudSyncJsonFileRootSpec jsonFile => ResolveCloudSyncJsonFile(jsonFile),
                _ => [],
            };

            if (resolved.Count == 0)
            {
                unresolved++;
            }

            roots.AddRange(resolved);
        }

        return new ResolvedProtection(roots, unresolved);
    }

    /// <summary>
    /// Known Folder의 현재 위치(리디렉션 반영)와 프로필 안 기본 위치(예: %USERPROFILE%\Documents)를 함께 보호한다.
    /// 현재 위치를 읽지 못해도 기본 위치는 보호하며(실패 시 닫힘), 리디렉션 뒤 남은 기본 폴더도 순회하지 않는다.
    /// </summary>
    /// <returns>보호 루트와 현재 위치를 읽었는지 여부.</returns>
    private (List<ProtectedRoot> Roots, bool Located) ResolveKnownFolder(KnownFolderRootSpec spec)
    {
        var label = spec.Folder.ToString();
        var current = Single(_environment.GetKnownFolderPath(spec.Folder), ProtectedRootOrigin.KnownFolder, label);
        var roots = new List<ProtectedRoot>(current);
        if (_environment.GetUserProfilePath() is { } profile && KNOWN_FOLDER_DEFAULT_NAMES.TryGetValue(spec.Folder, out var name))
        {
            roots.AddRange(Single(Path.Join(profile, name), ProtectedRootOrigin.KnownFolder, label));
        }

        return (roots, current.Count > 0);
    }

    /// <summary>
    /// 환경 변수 시스템 경로.
    /// </summary>
    private List<ProtectedRoot> ResolveEnvironmentPath(EnvironmentPathRootSpec spec)
    {
        var path = PathTemplate.ResolveProtected(spec.PathTemplate, _environment);
        return path is null ? [] : [new ProtectedRoot(path, ProtectedRootOrigin.SystemPath, spec.PathTemplate)];
    }

    /// <summary>
    /// 환경 변수 값이 동기화 루트인 경우.
    /// </summary>
    private List<ProtectedRoot> ResolveCloudSyncEnvironment(CloudSyncEnvironmentRootSpec spec)
    {
        return Single(_environment.GetEnvironmentVariable(spec.VariableName), ProtectedRootOrigin.CloudSync, spec.Provider);
    }

    /// <summary>
    /// HKCU 계정 하위 키마다의 문자열 값(예: OneDrive Accounts\*\UserFolder).
    /// </summary>
    private List<ProtectedRoot> ResolveCloudSyncRegistry(CloudSyncRegistryRootSpec spec)
    {
        var accounts = _registry.ReadSubKeyNames(RegistryRoot.CurrentUser, RegistryView.Registry64, spec.AccountsSubKey);
        if (accounts.Status != RegistryReadStatus.Found)
        {
            return [];
        }

        var roots = new List<ProtectedRoot>();
        foreach (var account in accounts.Names)
        {
            var values = _registry.ReadKeyValues(RegistryRoot.CurrentUser, RegistryView.Registry64, spec.AccountsSubKey + @"\" + account);
            if (values.Status != RegistryReadStatus.Found)
            {
                continue;
            }

            var folder = values.Values.FirstOrDefault(v => string.Equals(v.Name, spec.ValueName, StringComparison.OrdinalIgnoreCase))?.Text;
            roots.AddRange(Single(folder, ProtectedRootOrigin.CloudSync, spec.Provider));
        }

        return roots;
    }

    /// <summary>
    /// 작은 JSON 설정 파일의 최상위 객체마다 있는 경로 속성(예: Dropbox info.json의 personal/business.path).
    /// </summary>
    private List<ProtectedRoot> ResolveCloudSyncJsonFile(CloudSyncJsonFileRootSpec spec)
    {
        var file = PathTemplate.Resolve(spec.FilePathTemplate, _environment);
        var text = file is null ? null : _environment.ReadSmallTextFile(file, MAX_SYNC_SETTINGS_BYTES);
        if (text is null)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var roots = new List<ProtectedRoot>();
            foreach (var account in document.RootElement.EnumerateObject())
            {
                if (account.Value.ValueKind == JsonValueKind.Object
                    && account.Value.TryGetProperty(spec.PropertyName, out var path)
                    && path.ValueKind == JsonValueKind.String)
                {
                    roots.AddRange(Single(path.GetString(), ProtectedRootOrigin.CloudSync, spec.Provider));
                }
            }

            return roots;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 경로 하나를 정규화해 보호 루트로 만든다(쓸 수 없는 경로면 빈 목록).
    /// </summary>
    private List<ProtectedRoot> Single(string? path, ProtectedRootOrigin origin, string label)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? null : PathTemplate.NormalizeProtected(path, _environment);
        return normalized is null ? [] : [new ProtectedRoot(normalized, origin, label)];
    }
}
