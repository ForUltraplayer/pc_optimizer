/**
 * @file    : NvidiaLookupProbeContract.cs
 * @author  : rudals252
 * @brief   : NVIDIA 온라인 조회 프로브와 드라이버 업데이트 규칙이 공유하는 프로브 ID·어댑터별 측정 이름(설치 버전·제품 매핑·계열별 목록/최신 항목·조회 상태) 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// NVIDIA 온라인 조회 프로브(Probes)와 <see cref="DriverUpdateRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 어댑터별 측정 이름은 <c>nvidia.adapter[인덱스].필드</c>이며, 계열별 필드는 <c>gameReady.*</c>/<c>studio.*</c>입니다.
/// </summary>
public static class NvidiaLookupProbeContract
{
    /// <summary>NVIDIA 온라인 조회 프로브 ID.</summary>
    public const string PROBE_ID = "drivers.nvidiaLookup";

    /// <summary>조회 대상 NVIDIA 어댑터 수(정수, PCI VEN_10DE만).</summary>
    public const string ADAPTER_COUNT = "nvidia.adapterCount";

    /// <summary>어댑터 측정 이름 접두사.</summary>
    public const string ADAPTER_PREFIX = "nvidia.adapter";

    /// <summary>어댑터 필드: 이름(Win32_VideoController.Name).</summary>
    public const string FIELD_NAME = "name";

    /// <summary>어댑터 필드: PnP 장치 ID(Finding 식별자, 내보내기에서 토큰화).</summary>
    public const string FIELD_PNP_DEVICE_ID = "pnpDeviceId";

    /// <summary>어댑터 필드: Windows 드라이버 버전(예: 32.0.16.1656).</summary>
    public const string FIELD_INSTALLED_WINDOWS_VERSION = "installedWindowsVersion";

    /// <summary>어댑터 필드: NVIDIA 표기 설치 버전(예: 616.56). 변환할 수 없으면 없음.</summary>
    public const string FIELD_INSTALLED_VERSION = "installedVersion";

    /// <summary>어댑터 필드: 설치 드라이버 날짜("yyyy-MM-dd").</summary>
    public const string FIELD_INSTALLED_DATE = "installedDate";

    /// <summary>어댑터 필드: 조회 상태(<see cref="STATE_LISTED"/> 등).</summary>
    public const string FIELD_LOOKUP_STATE = "lookupState";

    /// <summary>어댑터 필드: 제품 목록에서 이름이 정확히 같은 항목 수(정수).</summary>
    public const string FIELD_PRODUCT_MATCH_COUNT = "productMatchCount";

    /// <summary>어댑터 필드: 제품 계열 ID(psid, 정수).</summary>
    public const string FIELD_PSID = "psid";

    /// <summary>어댑터 필드: 제품 ID(pfid, 정수).</summary>
    public const string FIELD_PFID = "pfid";

    /// <summary>어댑터 필드: 조회 실패 사유(CannotVerifyReason 이름).</summary>
    public const string FIELD_FAILURE_REASON = "failureReason";

    /// <summary>어댑터 필드: 목록 포함 여부로 정한 계열(NvidiaDriverBranch 이름).</summary>
    public const string FIELD_BRANCH = "branch";

    /// <summary>계열 접두사: Game Ready.</summary>
    public const string BRANCH_GAME_READY = "gameReady";

    /// <summary>계열 접두사: Studio.</summary>
    public const string BRANCH_STUDIO = "studio";

    /// <summary>계열 필드 접미사: 목록을 받았는지(불리언, Studio만 기록).</summary>
    public const string SUFFIX_AVAILABLE = "available";

    /// <summary>계열 필드 접미사: 목록의 버전(문자열 목록, 응답 순서).</summary>
    public const string SUFFIX_VERSIONS = "versions";

    /// <summary>계열 필드 접미사: 목록의 최고 버전(베타 제외).</summary>
    public const string SUFFIX_LATEST_VERSION = "latestVersion";

    /// <summary>계열 필드 접미사: 최고 버전 배포일("yyyy-MM-dd", UTC 날짜).</summary>
    public const string SUFFIX_LATEST_RELEASE_DATE = "latestReleaseDate";

    /// <summary>계열 필드 접미사: 최고 버전 배포 이름(URL 디코딩).</summary>
    public const string SUFFIX_LATEST_NAME = "latestName";

    /// <summary>계열 필드 접미사: 최고 버전 공식 배포 설명 URL(검증된 HTTPS NVIDIA 호스트).</summary>
    public const string SUFFIX_LATEST_DETAILS_URL = "latestDetailsUrl";

    /// <summary>계열 필드 접미사: 최고 버전 공식 다운로드 URL(검증된 HTTPS NVIDIA 호스트, 앱은 내려받지 않음).</summary>
    public const string SUFFIX_LATEST_DOWNLOAD_URL = "latestDownloadUrl";

    /// <summary>조회 상태: 계열 목록을 받았음.</summary>
    public const string STATE_LISTED = "listed";

    /// <summary>조회 상태: 제품 목록에서 이름이 정확히 하나로 일치하지 않음(0개 또는 여러 개).</summary>
    public const string STATE_PRODUCT_AMBIGUOUS = "productAmbiguous";

    /// <summary>조회 상태: 설치 버전을 NVIDIA 표기로 바꾸지 못해 조회하지 않음.</summary>
    public const string STATE_VERSION_UNKNOWN = "installedVersionUnknown";

    /// <summary>조회 상태: 네트워크·응답 오류로 목록을 받지 못함(<see cref="FIELD_FAILURE_REASON"/> 참고).</summary>
    public const string STATE_FAILED = "failed";

    private const string FIELD_SEPARATOR = ".";

    /// <summary>
    /// 어댑터 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="field">필드 이름(계열 필드는 <see cref="BranchField"/> 결과).</param>
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

    /// <summary>
    /// 계열 필드 이름을 만듭니다(예: "gameReady.latestVersion").
    /// </summary>
    /// <param name="branch">계열 접두사(<see cref="BRANCH_GAME_READY"/>, <see cref="BRANCH_STUDIO"/>).</param>
    /// <param name="suffix">필드 접미사.</param>
    /// <returns>필드 이름.</returns>
    public static string BranchField(string branch, string suffix)
    {
        return branch + FIELD_SEPARATOR + suffix;
    }
}
