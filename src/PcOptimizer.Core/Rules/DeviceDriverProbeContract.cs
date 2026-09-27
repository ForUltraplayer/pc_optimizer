/**
 * @file    : DeviceDriverProbeContract.cs
 * @author  : rudals252
 * @brief   : 장치 드라이버 프로브(칩셋·유선 랜·Wi-Fi·오디오)의 측정 이름 계약. 드라이버 안내(SP3)가 설치 버전·날짜를 보여 주는 데 쓴다
 */
namespace PcOptimizer.Core.Rules;

/// <summary>Win32_PnPSignedDriver에서 네 분류의 드라이버만 골라 이름·버전·날짜·제공자를 기록합니다. 장치 ID·일련번호는 기록하지 않습니다.</summary>
public static class DeviceDriverProbeContract
{
    /// <summary>프로브 ID.</summary>
    public const string PROBE_ID = "hardware.deviceDrivers";
    /// <summary>분류: 칩셋(시스템 장치 중 Intel/AMD 플랫폼 드라이버).</summary>
    public const string CATEGORY_CHIPSET = "chipset";
    /// <summary>분류: 유선 랜.</summary>
    public const string CATEGORY_LAN = "lan";
    /// <summary>분류: 무선 랜(Wi-Fi).</summary>
    public const string CATEGORY_WIFI = "wifi";
    /// <summary>분류: 오디오.</summary>
    public const string CATEGORY_AUDIO = "audio";
    /// <summary>모든 분류(표시 순서).</summary>
    public static readonly IReadOnlyList<string> Categories = [CATEGORY_CHIPSET, CATEGORY_LAN, CATEGORY_WIFI, CATEGORY_AUDIO];
    /// <summary>분류당 기록 상한.</summary>
    public const int MAX_ITEMS = 8;
    /// <summary>항목 필드: 장치 이름.</summary>
    public const string FIELD_NAME = "name";
    /// <summary>항목 필드: 드라이버 버전.</summary>
    public const string FIELD_VERSION = "version";
    /// <summary>항목 필드: 드라이버 날짜(ISO 8601 날짜).</summary>
    public const string FIELD_DATE = "date";
    /// <summary>항목 필드: 드라이버 제공자.</summary>
    public const string FIELD_PROVIDER = "provider";

    /// <summary>분류별 개수 측정 이름입니다.</summary>
    public static string CountName(string category) => "driver." + category + ".count";
    /// <summary>분류·순번·필드의 측정 이름입니다.</summary>
    public static string ItemName(string category, int index, string field) => "driver." + category + "." + index + "." + field;
}
