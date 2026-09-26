/**
 * @file    : DisplayProbeContract.cs
 * @author  : rudals252
 * @brief   : 디스플레이 프로브와 주사율 규칙이 공유하는 프로브 ID·대상별 측정 이름·단위·원시 값 의미 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 디스플레이 프로브(Probes)와 <see cref="DisplayRefreshRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 활성 경로(대상)별 측정 이름은 <c>display.target[인덱스].필드</c> 형식입니다.
/// 대상의 영속 식별자는 모니터 장치 경로(<see cref="FIELD_MONITOR_DEVICE_PATH"/>)이며, GDI 이름("DISPLAY3")은 표시용 레이블로만 씁니다.
/// </summary>
public static class DisplayProbeContract
{
    /// <summary>디스플레이 프로브 ID.</summary>
    public const string PROBE_ID = "hardware.display";

    /// <summary>활성 대상 수(정수).</summary>
    public const string TARGET_COUNT = "display.targetCount";

    /// <summary>원격 세션 여부(불리언, GetSystemMetrics(SM_REMOTESESSION)).</summary>
    public const string REMOTE_SESSION = "display.remoteSession";

    /// <summary>대상 측정 이름 접두사.</summary>
    public const string TARGET_PREFIX = "display.target";

    /// <summary>대상 필드: 모니터 장치 경로(EDID 기반 인스턴스 경로, 문자열).</summary>
    public const string FIELD_MONITOR_DEVICE_PATH = "monitorDevicePath";

    /// <summary>대상 필드: 모니터 표시 이름(문자열).</summary>
    public const string FIELD_MONITOR_NAME = "monitorName";

    /// <summary>대상 필드: GDI 원본 이름(예: "\\.\DISPLAY3", 표시용).</summary>
    public const string FIELD_GDI_DEVICE_NAME = "gdiDeviceName";

    /// <summary>대상 필드: 어댑터 이름(EnumDisplayDevices DeviceString, 문자열).</summary>
    public const string FIELD_ADAPTER_NAME = "adapterName";

    /// <summary>대상 필드: 어댑터 장치 경로(문자열).</summary>
    public const string FIELD_ADAPTER_DEVICE_PATH = "adapterDevicePath";

    /// <summary>대상 필드: 경로 대상 ID(정수, 어댑터 안에서 고유).</summary>
    public const string FIELD_TARGET_ID = "targetId";

    /// <summary>대상 필드: 출력 기술 원시 값(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY, 정수).</summary>
    public const string FIELD_OUTPUT_TECHNOLOGY = "outputTechnology";

    /// <summary>대상 필드: 현재 가로 해상도(정수, px).</summary>
    public const string FIELD_WIDTH = "width";

    /// <summary>대상 필드: 현재 세로 해상도(정수, px).</summary>
    public const string FIELD_HEIGHT = "height";

    /// <summary>대상 필드: 현재 모드 주사율(EnumDisplaySettings 정수 Hz).</summary>
    public const string FIELD_REFRESH_HZ = "refreshHz";

    /// <summary>대상 필드: 현재 방향(DMDO_* 원시 값 0~3, 정수).</summary>
    public const string FIELD_ORIENTATION = "orientation";

    /// <summary>대상 필드: 픽셀당 비트 수(정수).</summary>
    public const string FIELD_BITS_PER_PIXEL = "bitsPerPixel";

    /// <summary>대상 필드: 현재 모드 인터레이스 여부(불리언).</summary>
    public const string FIELD_INTERLACED = "interlaced";

    /// <summary>대상 필드: 대상 신호 주사율(QueryDisplayConfig 대상 모드 vSyncFreq 분자/분모, 실수 Hz).</summary>
    public const string FIELD_SIGNAL_REFRESH_HZ = "signalRefreshHz";

    /// <summary>대상 필드: 경로 주사율(QueryDisplayConfig 경로 targetInfo.refreshRate, 실수 Hz).</summary>
    public const string FIELD_PATH_REFRESH_HZ = "pathRefreshHz";

    /// <summary>대상 필드: 드라이버가 보고한 전체 모드 수(정수).</summary>
    public const string FIELD_ENUMERATED_MODE_COUNT = "enumeratedModeCount";

    /// <summary>대상 필드: 현재와 같은 해상도·방향·색 깊이·순차 주사 모드의 주사율 목록(정수 Hz 문자열, 오름차순).</summary>
    public const string FIELD_SAME_MODE_REFRESH_RATES = "sameModeRefreshRates";

    /// <summary>대상 필드: 위 목록의 최댓값(정수 Hz).</summary>
    public const string FIELD_MAX_SAME_MODE_REFRESH_HZ = "maxSameModeRefreshHz";

    /// <summary>대상 필드: 다른 활성 경로와 같은 원본을 공유하는지(복제 표시) 여부(불리언).</summary>
    public const string FIELD_CLONED = "cloned";

    /// <summary>주사율 단위.</summary>
    public const string UNIT_HZ = "Hz";

    /// <summary>해상도 단위.</summary>
    public const string UNIT_PIXELS = "px";

    /// <summary>색 깊이 단위.</summary>
    public const string UNIT_BITS_PER_PIXEL = "bpp";

    /// <summary>출력 기술: 간접 가상 표시(DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INDIRECT_VIRTUAL).</summary>
    public const long OUTPUT_TECHNOLOGY_INDIRECT_VIRTUAL = 17;

    /// <summary>
    /// 대상 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">대상 인덱스.</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string TargetMeasurementName(int index, string field)
    {
        return IndexedMeasurementName.Create(TARGET_PREFIX, index, field);
    }

    /// <summary>
    /// 대상 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="index">대상 인덱스.</param>
    /// <returns>접두사.</returns>
    public static string TargetMeasurementPrefix(int index)
    {
        return IndexedMeasurementName.Prefix(TARGET_PREFIX, index);
    }
}
