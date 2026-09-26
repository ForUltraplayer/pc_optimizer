/**
 * @file    : SystemDetailsProbeContract.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 섹션용 시스템 상세 프로브의 ID·측정 이름 계약(OS, CPU, BIOS 날짜, 메인보드, 물리 네트워크 어댑터)
 */

namespace PcOptimizer.Core.Rules;

/// <summary>시스템 상세 프로브의 측정 이름 계약입니다.</summary>
public static class SystemDetailsProbeContract
{
    /// <summary>프로브 ID.</summary>
    public const string PROBE_ID = "hardware.systemDetails";

    /// <summary>OS 이름(에디션 포함).</summary>
    public const string OS_CAPTION = "os.caption";

    /// <summary>OS 버전 문자열.</summary>
    public const string OS_VERSION = "os.version";

    /// <summary>OS 빌드 번호.</summary>
    public const string OS_BUILD = "os.buildNumber";

    /// <summary>OS 설치일(ISO 8601, 오프셋 포함).</summary>
    public const string OS_INSTALL_DATE = "os.installDate";

    /// <summary>CPU 모델명.</summary>
    public const string CPU_NAME = "cpu.name";

    /// <summary>물리 코어 수.</summary>
    public const string CPU_CORES = "cpu.cores";

    /// <summary>논리 프로세서 수.</summary>
    public const string CPU_THREADS = "cpu.threads";

    /// <summary>최대 클럭(MHz).</summary>
    public const string CPU_MAX_CLOCK_MHZ = "cpu.maxClockMHz";

    /// <summary>BIOS 배포일(yyyy-MM-dd).</summary>
    public const string BIOS_RELEASE_DATE = "system.biosReleaseDate";

    /// <summary>메인보드 제조사.</summary>
    public const string BOARD_MANUFACTURER = "board.manufacturer";

    /// <summary>메인보드 제품명(모델).</summary>
    public const string BOARD_PRODUCT = "board.product";

    /// <summary>메인보드 버전/리비전.</summary>
    public const string BOARD_VERSION = "board.version";

    /// <summary>물리 네트워크 어댑터 수.</summary>
    public const string NIC_COUNT = "network.adapterCount";

    /// <summary>어댑터 측정 이름 접두.</summary>
    public const string NIC_PREFIX = "network.adapter";

    /// <summary>어댑터 이름 필드.</summary>
    public const string FIELD_NAME = "name";

    /// <summary>어댑터 종류 필드(WMI AdapterTypeId 원시 값).</summary>
    public const string FIELD_ADAPTER_TYPE = "adapterType";

    /// <summary>제조사 필드.</summary>
    public const string FIELD_MANUFACTURER = "manufacturer";

    /// <summary>사용 중 여부 필드.</summary>
    public const string FIELD_NET_ENABLED = "netEnabled";

    /// <summary>드라이버 버전 필드.</summary>
    public const string FIELD_DRIVER_VERSION = "driverVersion";

    /// <summary>단위: MHz.</summary>
    public const string UNIT_MEGAHERTZ = "MHz";

    /// <summary>어댑터 인덱스·필드로 측정 이름을 만듭니다(예: network.adapter[0].name).</summary>
    /// <param name="index">물리 어댑터 인덱스(0부터).</param>
    /// <param name="field">필드 이름(예: <see cref="FIELD_NAME"/>).</param>
    /// <returns>측정 이름.</returns>
    public static string AdapterMeasurementName(int index, string field) => $"{NIC_PREFIX}[{index}].{field}";
}
