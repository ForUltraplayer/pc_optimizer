/**
 * @file    : DeliveryCachePlatform.cs
 * @author  : rudals252
 * @brief   : Windows 공식 배달 최적화 도구와 같은 로컬 제공자의 캐시 조회·개별 삭제
 */
using System.Management;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

internal sealed record DeliveryCacheEntry(string Id, long Bytes, byte Status, bool Pinned);
internal interface IDeliveryCachePlatform
{
    IReadOnlyList<DeliveryCacheEntry> Read(CancellationToken ct);
    void Delete(string id, Action beforeDelete);
}

internal sealed class DeliveryCachePlatform : IDeliveryCachePlatform
{
    private const string Namespace = @"\\.\root\Microsoft\Windows\DeliveryOptimization";
    private const string Class = "MSFT_DeliveryOptimizationFile";
    public IReadOnlyList<DeliveryCacheEntry> Read(CancellationToken ct)
    {
        try
        {
            using var search = new ManagementObjectSearcher(new ManagementScope(Namespace),
                new ObjectQuery("SELECT FileId, FileSizeInCache, Status, IsPinned FROM " + Class),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = true, Rewindable = false });
            using var objects = search.Get();
            var result = new List<DeliveryCacheEntry>();
            foreach (ManagementObject row in objects)
            {
                using (row)
                {
                    ct.ThrowIfCancellationRequested();
                    if (result.Count >= 1024 || row["FileId"] is not string id || row["FileSizeInCache"] is not ulong bytes || bytes > long.MaxValue
                        || row["Status"] is not byte state || row["IsPinned"] is not bool pinned) { throw new ActionUnavailableException("DeliveryStateUnavailable"); }
                    result.Add(new(id, (long)bytes, state, pinned));
                }
            }
            ct.ThrowIfCancellationRequested(); return result;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { throw new ActionUnavailableException("DeliveryStateUnavailable"); }
    }
    public void Delete(string id, Action beforeDelete)
    {
        try
        {
            using var type = new ManagementClass(new ManagementScope(Namespace), new ManagementPath(Class), null);
            using var input = Parameters(type, id);
            beforeDelete();
            // 동기 호출이 반환할 때까지 작업 관문을 소유한다. UI 취소로 제공자 작업 완료를 가장하지 않는다.
            using var output = type.InvokeMethod("Delete", input, null);
            if (output?["ReturnValue"] is not uint result || result != 0) { throw new ActionUnavailableException("DeliveryDeleteFailed"); }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { throw new ActionUnavailableException("DeliveryDeleteFailed"); }
    }
    internal static ManagementClass OpenClass() => new(new ManagementScope(Namespace), new ManagementPath(Class), null);
    internal static ManagementBaseObject Parameters(ManagementClass type, string id)
    {
        var input = type.GetMethodParameters("Delete");
        if (input.Properties["fileId"].Type != CimType.String || input.Properties["deletePinned"].Type != CimType.Boolean
            || type.Methods["Delete"].OutParameters.Properties["ReturnValue"].Type != CimType.UInt32)
        { input.Dispose(); throw new ActionUnavailableException("DeliveryContractUnsupported"); }
        input["fileId"] = id;
        input["deletePinned"] = false;
        return input;
    }
}
