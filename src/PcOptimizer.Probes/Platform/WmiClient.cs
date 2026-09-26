/**
 * @file    : WmiClient.cs
 * @author  : rudals252
 * @brief   : System.Management 기반 WMI 조회 구현(연결·열거 제공자 타임아웃, 실패를 상태 코드로 변환, 예외 메시지 원문 미보관)
 */

// 기본 패키지
using System.Management;
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// root\cimv2 네임스페이스를 조회하는 WMI 클라이언트입니다.
/// 동기 WMI 호출을 Task.WhenAny로 숨기지 않고, 연결·열거 옵션의 제공자 타임아웃으로 호출이 스스로 끝나게 합니다.
/// </summary>
public sealed class WmiClient : IWmiClient
{
    private const string CIMV2_NAMESPACE = @"root\cimv2";

    /// <summary>공유 인스턴스.</summary>
    public static WmiClient Instance { get; } = new();

    /// <inheritdoc />
    public WmiQueryResult Query(string className, IReadOnlyList<string> properties, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentNullException.ThrowIfNull(properties);

        try
        {
            var scope = new ManagementScope(CIMV2_NAMESPACE, new ConnectionOptions { Timeout = timeout });
            var query = new SelectQuery(className, condition: null, selectedProperties: [.. properties]);
            var options = new System.Management.EnumerationOptions
            {
                Timeout = timeout,
                ReturnImmediately = true,
                Rewindable = false,
            };

            using var searcher = new ManagementObjectSearcher(scope, query, options);
            using var collection = searcher.Get();
            var rows = new List<IReadOnlyDictionary<string, object?>>();
            foreach (var item in collection)
            {
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in properties)
                    {
                        row[property] = item[property];
                    }

                    rows.Add(row);
                }
            }

            return WmiQueryResult.Succeeded(rows);
        }
        catch (ManagementException ex)
        {
            return WmiQueryResult.Failed(MapStatus(ex.ErrorCode), ex.ErrorCode.ToString());
        }
        catch (UnauthorizedAccessException ex)
        {
            return WmiQueryResult.Failed(WmiQueryStatus.AccessDenied, ex.GetType().Name);
        }
        catch (COMException ex)
        {
            return WmiQueryResult.Failed(WmiQueryStatus.Error, ex.GetType().Name);
        }
    }

    /// <summary>
    /// WMI 상태 코드를 조회 상태로 바꾼다.
    /// </summary>
    private static WmiQueryStatus MapStatus(ManagementStatus status)
    {
        return status switch
        {
            ManagementStatus.InvalidClass or ManagementStatus.NotFound or ManagementStatus.InvalidNamespace => WmiQueryStatus.ClassUnavailable,
            ManagementStatus.AccessDenied => WmiQueryStatus.AccessDenied,
            ManagementStatus.Timedout => WmiQueryStatus.Timeout,
            _ => WmiQueryStatus.Error,
        };
    }
}
