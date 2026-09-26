/**
 * @file    : ProtectionPolicyResolver.cs
 * @author  : rudals252
 * @brief   : 보호 정책 항목을 실제 정규화 경로로 해석(리디렉션 반영 Known Folder, 환경 변수 시스템 경로, OneDrive 환경 변수·HKCU 계정 UserFolder, Dropbox info.json path)하는 조회 전용 해석기
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
/// UNC·상대 경로·드라이브 루트 전체는 보호 루트로 쓰지 않습니다(비교 대상이 아니며 스캔 루트도 될 수 없음).
/// Google Drive 동기화 루트 감지는 1차에서 지원하지 않습니다.
/// </summary>
public sealed class ProtectionPolicyResolver
{
    /// <summary>동기화 앱 설정 JSON 파일의 최대 읽기 크기(바이트).</summary>
    public const int MAX_SYNC_SETTINGS_BYTES = 64 * 1024;

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
            var resolved = spec switch
            {
                KnownFolderRootSpec known => ResolveKnownFolder(known),
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
    /// Known Folder의 현재 위치(리디렉션 반영).
    /// </summary>
    private List<ProtectedRoot> ResolveKnownFolder(KnownFolderRootSpec spec)
    {
        var path = _environment.GetKnownFolderPath(spec.Folder);
        return Single(path, ProtectedRootOrigin.KnownFolder, spec.Folder.ToString());
    }

    /// <summary>
    /// 환경 변수 시스템 경로.
    /// </summary>
    private List<ProtectedRoot> ResolveEnvironmentPath(EnvironmentPathRootSpec spec)
    {
        var path = PathTemplate.Resolve(spec.PathTemplate, _environment);
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
        var normalized = string.IsNullOrWhiteSpace(path) ? null : PathTemplate.NormalizeAbsolute(path, _environment);
        return normalized is null ? [] : [new ProtectedRoot(normalized, origin, label)];
    }
}
