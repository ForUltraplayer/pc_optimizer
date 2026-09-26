/**
 * @file    : VolumeProbeContract.cs
 * @author  : rudals252
 * @brief   : 볼륨 프로브와 저장소 여유 공간 규칙이 공유하는 프로브 ID·볼륨별 측정 이름·Windows.old 존재·원시 값 의미 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 볼륨 프로브(Probes)와 <see cref="StorageSpaceRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 볼륨별 측정 이름은 <c>storage.volume[인덱스].필드</c> 형식입니다(MSFT_Volume 원시 값).
/// </summary>
public static class VolumeProbeContract
{
    /// <summary>볼륨 프로브 ID.</summary>
    public const string PROBE_ID = "storage.volumes";

    /// <summary>보고된 볼륨 수(정수).</summary>
    public const string VOLUME_COUNT = "storage.volumeCount";

    /// <summary>시스템 드라이브(예: "C:", 문자열).</summary>
    public const string SYSTEM_DRIVE = "storage.systemDrive";

    /// <summary>시스템 드라이브의 Windows.old 폴더 존재 여부(불리언, 크기는 재지 않음).</summary>
    public const string WINDOWS_OLD_EXISTS = "storage.windowsOld.exists";

    /// <summary>볼륨 측정 이름 접두사.</summary>
    public const string VOLUME_PREFIX = "storage.volume";

    /// <summary>볼륨 필드: 드라이브 문자(문자열, 없으면 측정값 없음).</summary>
    public const string FIELD_DRIVE_LETTER = "driveLetter";

    /// <summary>볼륨 필드: 볼륨 레이블(문자열).</summary>
    public const string FIELD_LABEL = "fileSystemLabel";

    /// <summary>볼륨 필드: 파일 시스템(문자열).</summary>
    public const string FIELD_FILE_SYSTEM = "fileSystem";

    /// <summary>볼륨 필드: 드라이브 종류 원시 값(정수).</summary>
    public const string FIELD_DRIVE_TYPE = "driveType";

    /// <summary>볼륨 필드: 전체 크기(정수, 바이트).</summary>
    public const string FIELD_SIZE = "size";

    /// <summary>볼륨 필드: 남은 공간(정수, 바이트).</summary>
    public const string FIELD_SIZE_REMAINING = "sizeRemaining";

    /// <summary>볼륨 필드: Windows가 보고한 상태 원시 값(정수).</summary>
    public const string FIELD_HEALTH_STATUS = "healthStatus";

    /// <summary>크기 단위.</summary>
    public const string UNIT_BYTES = "bytes";

    /// <summary>드라이브 종류: 고정 디스크.</summary>
    public const long DRIVE_TYPE_FIXED = 3;

    /// <summary>
    /// 볼륨 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">볼륨 인덱스.</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string VolumeMeasurementName(int index, string field)
    {
        return IndexedMeasurementName.Create(VOLUME_PREFIX, index, field);
    }

    /// <summary>
    /// 볼륨 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="index">볼륨 인덱스.</param>
    /// <returns>접두사.</returns>
    public static string VolumeMeasurementPrefix(int index)
    {
        return IndexedMeasurementName.Prefix(VOLUME_PREFIX, index);
    }
}
