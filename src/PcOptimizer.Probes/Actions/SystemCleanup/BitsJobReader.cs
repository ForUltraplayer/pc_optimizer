/**
 * @file    : BitsJobReader.cs
 * @author  : rudals252
 * @brief   : 모든 사용자 BITS 작업의 개수만 조회하며 이름·URL·파일 경로는 수집하지 않음
 */
using System.Runtime.InteropServices;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

internal static class BitsJobReader
{
    internal static uint CountAll()
    {
        object? manager = null; IEnumJobs? jobs = null;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("4991d34b-80a1-4291-83b6-3328366b9097"), true)!;
            manager = Activator.CreateInstance(type) ?? throw new ActionUnavailableException("UpdateBitsUnavailable");
            ((IBitsManager)manager).EnumJobs(1, out jobs); // BG_JOB_ENUM_ALL_USERS
            jobs.GetCount(out var count);
            return count;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException)
        { throw new ActionUnavailableException("UpdateBitsUnavailable"); }
        finally
        {
            if (jobs is not null && Marshal.IsComObject(jobs)) { Marshal.FinalReleaseComObject(jobs); }
            if (manager is not null && Marshal.IsComObject(manager)) { Marshal.FinalReleaseComObject(manager); }
        }
    }
    // Windows SDK bits.h의 메서드 순서를 보존한다. EnumJobs/GetCount 외에는 호출하지 않는다.
    [ComImport, Guid("5ce34c0d-0dc9-4c1f-897c-daa1b78cee7c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBitsManager
    {
        void CreateJob([MarshalAs(UnmanagedType.LPWStr)] string name, int type, out Guid id, out IntPtr job);
        void GetJob(ref Guid id, out IntPtr job);
        void EnumJobs(uint flags, out IEnumJobs jobs);
        void GetErrorDescription(int error, uint language, out IntPtr description);
    }
    [ComImport, Guid("1af4f612-3b71-466f-8f58-7b6f73ac57ad"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumJobs
    {
        void Next(uint count, out IntPtr job, out uint fetched);
        void Skip(uint count);
        void Reset();
        void Clone(out IEnumJobs other);
        void GetCount(out uint count);
    }
}
