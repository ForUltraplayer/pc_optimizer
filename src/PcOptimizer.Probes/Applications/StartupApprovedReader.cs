/**
 * @file    : StartupApprovedReader.cs
 * @author  : rudals252
 * @brief   : Explorer\StartupApproved 키를 키마다 한 번만 64비트 보기로 읽어, 시작 항목 이름과 대소문자 무시로 정확히 같은 값만 찾아 주는 조회 도우미(읽기 실패는 Issue)
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// StartupApproved 조회 도우미입니다. 부분 일치·유사 이름 연결은 하지 않습니다.
/// </summary>
internal sealed class StartupApprovedReader
{
    private const string HIVE_CURRENT_USER = "HKCU";
    private const string HIVE_LOCAL_MACHINE = "HKLM";

    private readonly IRegistryReader _registry;
    private readonly List<Issue> _issues;
    private readonly Dictionary<StartupApprovedKey, ApprovedTable> _tables = [];

    /// <summary>
    /// 조회 도우미를 만듭니다.
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="issues">읽기 실패 Issue를 추가할 목록(키마다 한 번).</param>
    public StartupApprovedReader(IRegistryReader registry, List<Issue> issues)
    {
        _registry = registry;
        _issues = issues;
    }

    /// <summary>
    /// 루트의 짧은 이름("HKCU"/"HKLM").
    /// </summary>
    /// <param name="root">루트.</param>
    /// <returns>짧은 이름.</returns>
    public static string HiveName(RegistryRoot root)
    {
        return root == RegistryRoot.LocalMachine ? HIVE_LOCAL_MACHINE : HIVE_CURRENT_USER;
    }

    /// <summary>
    /// 항목 이름과 정확히 같은(대소문자 무시) StartupApproved 값을 찾습니다.
    /// </summary>
    /// <param name="key">항목의 출처에 대응하는 StartupApproved 키.</param>
    /// <param name="name">항목 이름(값 이름 또는 파일 이름).</param>
    /// <returns>조회 결과 코드(<see cref="StartupItemsProbeContract"/>의 LOOKUP_*)와 찾은 값(없으면 null).</returns>
    public (string Lookup, RegistryValueEntry? Entry) Find(StartupApprovedKey key, string name)
    {
        if (!_tables.TryGetValue(key, out var table))
        {
            table = Read(key);
            _tables.Add(key, table);
        }

        if (table.Values is null)
        {
            return (table.LookupWhenAbsent, null);
        }

        return table.Values.TryGetValue(name, out var entry)
            ? (StartupItemsProbeContract.LOOKUP_FOUND, entry)
            : (StartupItemsProbeContract.LOOKUP_MISSING, null);
    }

    /// <summary>
    /// 키 하나를 64비트 보기로 읽는다. 키가 없으면 모든 항목이 missing, 읽기 실패는 Issue와 함께 unreadable.
    /// </summary>
    private ApprovedTable Read(StartupApprovedKey key)
    {
        var location = HiveName(key.Root) + @"\" + key.SubKey;
        var reading = _registry.ReadKeyValues(key.Root, RegistryView.Registry64, key.SubKey);
        switch (reading.Status)
        {
            case RegistryReadStatus.Found:
                var values = new Dictionary<string, RegistryValueEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var value in reading.Values)
                {
                    values.TryAdd(value.Name, value);
                }

                return new ApprovedTable(values, StartupItemsProbeContract.LOOKUP_MISSING);
            case RegistryReadStatus.KeyMissing:
            case RegistryReadStatus.ValueMissing:
                return new ApprovedTable(null, StartupItemsProbeContract.LOOKUP_MISSING);
            case RegistryReadStatus.AccessDenied:
                _issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.Startup_ApprovedAccessDenied, location)));
                return new ApprovedTable(null, StartupItemsProbeContract.LOOKUP_UNREADABLE);
            case RegistryReadStatus.Error:
            default:
                _issues.Add(new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Startup_ApprovedError, location, reading.ErrorCode ?? string.Empty)));
                return new ApprovedTable(null, StartupItemsProbeContract.LOOKUP_UNREADABLE);
        }
    }

    /// <summary>
    /// 읽은 StartupApproved 키입니다. Values가 null이면 키가 없거나 읽지 못한 것이며 모든 항목의 조회 결과는 LookupWhenAbsent입니다.
    /// </summary>
    private sealed record ApprovedTable(Dictionary<string, RegistryValueEntry>? Values, string LookupWhenAbsent);
}

/// <summary>
/// 항목과 연결할 StartupApproved 키입니다(항상 64비트 보기로 읽음).
/// </summary>
/// <param name="Root">루트.</param>
/// <param name="SubKey">하위 키 경로.</param>
internal sealed record StartupApprovedKey(RegistryRoot Root, string SubKey);
