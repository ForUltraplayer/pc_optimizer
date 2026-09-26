/**
 * @file    : PhysicalDiskProbeContract.cs
 * @author  : rudals252
 * @brief   : 물리 디스크 프로브와 디스크 상태 규칙이 공유하는 프로브 ID·디스크별 측정 이름·상태/매체 원시 값 의미 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 물리 디스크 프로브(Probes)와 <see cref="DiskHealthRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 디스크별 측정 이름은 <c>storage.disk[인덱스].필드</c> 형식입니다(MSFT_PhysicalDisk 원시 값).
/// 영속 식별자는 저장소 제공자가 붙인 물리 디스크 GUID이며, 장치 일련번호(UniqueId·SerialNumber)는 수집하지 않습니다.
/// </summary>
public static class PhysicalDiskProbeContract
{
    /// <summary>물리 디스크 프로브 ID.</summary>
    public const string PROBE_ID = "storage.physicalDisks";

    /// <summary>보고된 디스크 수(정수).</summary>
    public const string DISK_COUNT = "storage.diskCount";

    /// <summary>디스크 측정 이름 접두사.</summary>
    public const string DISK_PREFIX = "storage.disk";

    /// <summary>디스크 필드: 디스크 번호(DeviceId, 문자열, 표시용).</summary>
    public const string FIELD_DEVICE_ID = "deviceId";

    /// <summary>디스크 필드: 저장소 제공자 물리 디스크 GUID(ObjectId의 PD 부분, 문자열).</summary>
    public const string FIELD_PROVIDER_DISK_ID = "providerDiskId";

    /// <summary>디스크 필드: 이름(FriendlyName).</summary>
    public const string FIELD_FRIENDLY_NAME = "friendlyName";

    /// <summary>디스크 필드: 매체 종류 원시 값(MediaType, 정수).</summary>
    public const string FIELD_MEDIA_TYPE = "mediaType";

    /// <summary>디스크 필드: 버스 종류 원시 값(BusType, 정수).</summary>
    public const string FIELD_BUS_TYPE = "busType";

    /// <summary>디스크 필드: Windows가 보고한 상태 원시 값(HealthStatus, 정수).</summary>
    public const string FIELD_HEALTH_STATUS = "healthStatus";

    /// <summary>디스크 필드: 크기(Size, 정수 바이트).</summary>
    public const string FIELD_SIZE = "size";

    /// <summary>디스크 필드: 회전 속도(SpindleSpeed, 정수 RPM; 0은 비회전, 4294967295는 알 수 없음).</summary>
    public const string FIELD_SPINDLE_SPEED = "spindleSpeed";

    /// <summary>크기 단위.</summary>
    public const string UNIT_BYTES = "bytes";

    /// <summary>회전 속도 단위.</summary>
    public const string UNIT_RPM = "RPM";

    /// <summary>상태: Healthy.</summary>
    public const long HEALTH_HEALTHY = 0;

    /// <summary>상태: Warning.</summary>
    public const long HEALTH_WARNING = 1;

    /// <summary>상태: Unhealthy.</summary>
    public const long HEALTH_UNHEALTHY = 2;

    /// <summary>매체 종류: HDD.</summary>
    public const long MEDIA_TYPE_HDD = 3;

    /// <summary>매체 종류: SSD.</summary>
    public const long MEDIA_TYPE_SSD = 4;

    /// <summary>매체 종류: SCM.</summary>
    public const long MEDIA_TYPE_SCM = 5;

    /// <summary>
    /// 디스크 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">디스크 인덱스.</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string DiskMeasurementName(int index, string field)
    {
        return IndexedMeasurementName.Create(DISK_PREFIX, index, field);
    }

    /// <summary>
    /// 디스크 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="index">디스크 인덱스.</param>
    /// <returns>접두사.</returns>
    public static string DiskMeasurementPrefix(int index)
    {
        return IndexedMeasurementName.Prefix(DISK_PREFIX, index);
    }
}
