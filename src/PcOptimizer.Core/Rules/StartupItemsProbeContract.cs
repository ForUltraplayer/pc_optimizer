/**
 * @file    : StartupItemsProbeContract.cs
 * @author  : rudals252
 * @brief   : 시작 프로그램 프로브와 규칙이 공유하는 프로브 ID·항목별 측정 이름·출처 코드·StartupApproved 조회 결과 코드와 알려진 원시 값 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 시작 프로그램 프로브(Probes)와 <see cref="StartupItemsRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 항목별 측정 이름은 <c>startup.item[인덱스].필드</c> 형식입니다.
/// </summary>
/// <remarks>
/// 대상: HKCU·HKLM(64/32비트 보기)의 Run·RunOnce 키와 사용자·모든 사용자 시작프로그램 폴더.
/// 예약 작업·서비스·패키지 앱 StartupTask는 수집하지 않습니다.
/// 활성 상태는 Explorer\StartupApproved의 같은 이름(대소문자 무시 정확히 일치) 값만 연결하며, 원시 값(형식·첫 바이트)을 그대로 기록합니다.
/// </remarks>
public static class StartupItemsProbeContract
{
    /// <summary>시작 프로그램 프로브 ID.</summary>
    public const string PROBE_ID = "applications.startupItems";

    /// <summary>수집한 항목 수(정수).</summary>
    public const string ITEM_COUNT = "startup.itemCount";

    /// <summary>읽지 못한 위치의 출처 코드 목록(문자열 목록, 모두 읽었으면 빈 목록).</summary>
    public const string UNREADABLE_SOURCES = "startup.unreadableSources";

    /// <summary>항목 측정 이름 접두사.</summary>
    public const string ITEM_PREFIX = "startup.item";

    /// <summary>항목 필드: 이름(레지스트리 값 이름 또는 파일 이름, 문자열).</summary>
    public const string FIELD_NAME = "name";

    /// <summary>항목 필드: 출처 코드(<c>SOURCE_*</c>, 문자열).</summary>
    public const string FIELD_SOURCE = "source";

    /// <summary>항목 필드: 위치(레지스트리 키 경로 또는 특수 폴더 이름, 문자열). 폴더의 실제 경로는 담지 않습니다.</summary>
    public const string FIELD_LOCATION = "location";

    /// <summary>항목 필드: 레지스트리 보기("Registry64"/"Registry32", 레지스트리 항목만).</summary>
    public const string FIELD_REGISTRY_VIEW = "registryView";

    /// <summary>항목 필드: 레지스트리 값 형식(RegistryValueKind 이름, 레지스트리 항목만).</summary>
    public const string FIELD_VALUE_KIND = "valueKind";

    /// <summary>항목 필드: 명령/대상 문자열(레지스트리 문자열 값 또는 폴더 항목의 파일 경로). 개인 경로가 들어갈 수 있어 측정값에만 둡니다.</summary>
    public const string FIELD_COMMAND = "command";

    /// <summary>항목 필드: StartupApproved 조회 결과 코드(<c>LOOKUP_*</c>, 문자열).</summary>
    public const string FIELD_APPROVED_LOOKUP = "approvedLookup";

    /// <summary>항목 필드: StartupApproved 값 형식(RegistryValueKind 이름, 값을 찾았을 때만).</summary>
    public const string FIELD_APPROVED_KIND = "approvedKind";

    /// <summary>항목 필드: StartupApproved 이진 값의 첫 바이트(정수, 비어 있지 않은 이진 값일 때만).</summary>
    public const string FIELD_APPROVED_FIRST_BYTE = "approvedFirstByte";

    /// <summary>출처: HKCU Run.</summary>
    public const string SOURCE_HKCU_RUN = "hkcu.run";

    /// <summary>출처: HKCU RunOnce.</summary>
    public const string SOURCE_HKCU_RUN_ONCE = "hkcu.runOnce";

    /// <summary>출처: HKLM Run(64비트 보기).</summary>
    public const string SOURCE_HKLM64_RUN = "hklm64.run";

    /// <summary>출처: HKLM RunOnce(64비트 보기).</summary>
    public const string SOURCE_HKLM64_RUN_ONCE = "hklm64.runOnce";

    /// <summary>출처: HKLM Run(32비트 보기, WOW6432Node).</summary>
    public const string SOURCE_HKLM32_RUN = "hklm32.run";

    /// <summary>출처: HKLM RunOnce(32비트 보기, WOW6432Node).</summary>
    public const string SOURCE_HKLM32_RUN_ONCE = "hklm32.runOnce";

    /// <summary>출처: 사용자 시작프로그램 폴더.</summary>
    public const string SOURCE_USER_FOLDER = "folder.user";

    /// <summary>출처: 모든 사용자 시작프로그램 폴더.</summary>
    public const string SOURCE_COMMON_FOLDER = "folder.common";

    /// <summary>StartupApproved: 같은 이름의 값을 찾음.</summary>
    public const string LOOKUP_FOUND = "found";

    /// <summary>StartupApproved: 키나 같은 이름의 값이 없음.</summary>
    public const string LOOKUP_MISSING = "missing";

    /// <summary>StartupApproved: 이 출처(RunOnce)는 StartupApproved로 관리되지 않음.</summary>
    public const string LOOKUP_NOT_TRACKED = "notTracked";

    /// <summary>StartupApproved: 키를 읽지 못함(접근 거부·오류).</summary>
    public const string LOOKUP_UNREADABLE = "unreadable";

    /// <summary>이진 값 형식 이름.</summary>
    public const string KIND_BINARY = "Binary";

    /// <summary>StartupApproved 첫 바이트: 활성.</summary>
    public const long APPROVED_ENABLED = 0x02;

    /// <summary>StartupApproved 첫 바이트: 비활성.</summary>
    public const long APPROVED_DISABLED = 0x03;

    /// <summary>
    /// 항목 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">항목 인덱스.</param>
    /// <param name="field">필드 이름 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string ItemMeasurementName(int index, string field)
    {
        return IndexedMeasurementName.Create(ITEM_PREFIX, index, field);
    }

    /// <summary>
    /// 항목 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="index">항목 인덱스.</param>
    /// <returns>접두사.</returns>
    public static string ItemMeasurementPrefix(int index)
    {
        return IndexedMeasurementName.Prefix(ITEM_PREFIX, index);
    }
}
