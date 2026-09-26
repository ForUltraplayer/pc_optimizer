/**
 * @file    : MemoryProbeContract.cs
 * @author  : rudals252
 * @brief   : 메모리 프로브와 메모리 속도 규칙이 공유하는 프로브 ID·측정 이름·단위 계약 상수
 */

// 기본 패키지
using System.Globalization;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 메모리 프로브(Probes)와 <see cref="MemorySpeedRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 모듈별 측정 이름은 <c>memory.module[인덱스].필드</c> 형식이며, 인덱스는 같은 검사 안에서 모듈을 묶는 용도로만 씁니다.
/// </summary>
public static class MemoryProbeContract
{
    /// <summary>메모리 프로브 ID.</summary>
    public const string PROBE_ID = "hardware.memory";

    /// <summary>보고된 모듈 수(정수).</summary>
    public const string MODULE_COUNT = "memory.moduleCount";

    /// <summary>SMBIOS 버전 문자열(예: "3.5"). 속도 단위 판단 근거로 함께 기록한다.</summary>
    public const string SMBIOS_VERSION = "memory.smbiosVersion";

    /// <summary>모듈 필드: 장치 위치(DeviceLocator).</summary>
    public const string FIELD_DEVICE_LOCATOR = "deviceLocator";

    /// <summary>모듈 필드: 뱅크 레이블(BankLabel).</summary>
    public const string FIELD_BANK_LABEL = "bankLabel";

    /// <summary>모듈 필드: 제조사(Manufacturer).</summary>
    public const string FIELD_MANUFACTURER = "manufacturer";

    /// <summary>모듈 필드: 부품 번호(PartNumber).</summary>
    public const string FIELD_PART_NUMBER = "partNumber";

    /// <summary>모듈 필드: 용량(Capacity, 바이트).</summary>
    public const string FIELD_CAPACITY = "capacity";

    /// <summary>모듈 필드: SMBIOS 보고 속도(Speed).</summary>
    public const string FIELD_SPEED = "speed";

    /// <summary>모듈 필드: 설정 속도(ConfiguredClockSpeed).</summary>
    public const string FIELD_CONFIGURED_CLOCK_SPEED = "configuredClockSpeed";

    /// <summary>속도 단위: 초당 메가전송(SMBIOS 3.x의 Type 17 속도 단위).</summary>
    public const string UNIT_MEGATRANSFERS = "MT/s";

    /// <summary>속도 단위: 메가헤르츠(SMBIOS 3.0 이전 Type 17 속도 단위).</summary>
    public const string UNIT_MEGAHERTZ = "MHz";

    /// <summary>용량 단위: 바이트.</summary>
    public const string UNIT_BYTES = "bytes";

    /// <summary>
    /// 모듈 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="moduleIndex">같은 검사 안의 모듈 인덱스(0부터).</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string ModuleMeasurementName(int moduleIndex, string field)
    {
        return string.Create(CultureInfo.InvariantCulture, $"memory.module[{moduleIndex}].{field}");
    }

    /// <summary>
    /// 모듈 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="moduleIndex">모듈 인덱스.</param>
    /// <returns>접두사(예: "memory.module[0].").</returns>
    public static string ModuleMeasurementPrefix(int moduleIndex)
    {
        return string.Create(CultureInfo.InvariantCulture, $"memory.module[{moduleIndex}].");
    }
}
