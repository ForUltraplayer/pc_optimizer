/**
 * @file    : GpuProbeContract.cs
 * @author  : rudals252
 * @brief   : 설치 GPU 드라이버 프로브와 설치 드라이버 규칙이 공유하는 프로브 ID·어댑터별 측정 이름 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 설치 GPU 프로브(Probes)와 <see cref="InstalledDriverRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 어댑터별 측정 이름은 <c>gpu.adapter[인덱스].필드</c> 형식이며, 영속 식별자는 PnP 장치 ID입니다.
/// </summary>
public static class GpuProbeContract
{
    /// <summary>설치 GPU 프로브 ID.</summary>
    public const string PROBE_ID = "drivers.installedGpu";

    /// <summary>보고된 어댑터 수(정수).</summary>
    public const string ADAPTER_COUNT = "gpu.adapterCount";

    /// <summary>어댑터 측정 이름 접두사.</summary>
    public const string ADAPTER_PREFIX = "gpu.adapter";

    /// <summary>어댑터 필드: 이름(Win32_VideoController.Name).</summary>
    public const string FIELD_NAME = "name";

    /// <summary>어댑터 필드: Windows 드라이버 버전(DriverVersion, 예: "32.0.16.1656").</summary>
    public const string FIELD_DRIVER_VERSION = "driverVersion";

    /// <summary>어댑터 필드: 드라이버 날짜(DriverDate, "yyyy-MM-dd").</summary>
    public const string FIELD_DRIVER_DATE = "driverDate";

    /// <summary>어댑터 필드: PnP 장치 ID(PNPDeviceID).</summary>
    public const string FIELD_PNP_DEVICE_ID = "pnpDeviceId";

    /// <summary>어댑터 필드: 제공자 호환 이름(AdapterCompatibility).</summary>
    public const string FIELD_ADAPTER_COMPATIBILITY = "adapterCompatibility";

    /// <summary>어댑터 필드: 비디오 프로세서(VideoProcessor).</summary>
    public const string FIELD_VIDEO_PROCESSOR = "videoProcessor";

    /// <summary>어댑터 필드: 하드웨어 ID 목록(Win32_PnPEntity.HardwareID).</summary>
    public const string FIELD_HARDWARE_IDS = "hardwareIds";

    /// <summary>
    /// 어댑터 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string AdapterMeasurementName(int index, string field)
    {
        return IndexedMeasurementName.Create(ADAPTER_PREFIX, index, field);
    }

    /// <summary>
    /// 어댑터 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <returns>접두사.</returns>
    public static string AdapterMeasurementPrefix(int index)
    {
        return IndexedMeasurementName.Prefix(ADAPTER_PREFIX, index);
    }
}
