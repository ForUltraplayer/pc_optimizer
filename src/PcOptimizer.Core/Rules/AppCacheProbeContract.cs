/**
 * @file    : AppCacheProbeContract.cs
 * @author  : rudals252
 * @brief   : 앱 캐시 프로브(Probes)와 앱 캐시·Squirrel 버전 폴더·규칙 목록 요약 규칙(Core)이 공유하는 프로브 ID·측정 이름·상태 코드 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 앱 캐시 측정값 계약입니다. 목록 측정 이름은 <c>appCache.rule[i].필드</c>, <c>appCache.squirrel[i].필드</c>, <c>appCache.config[i].필드</c> 형식입니다.
/// </summary>
/// <remarks>
/// 경로(<c>paths</c>·<c>path</c> 필드)는 측정값에만 두고 화면 문장에는 쓰지 않습니다. 기본 내보내기는 드라이브와 관계없이 이 경로들을 토큰으로 바꿉니다.
/// 크기는 관측한 파일의 논리 크기이며 비울 수 있는 용량을 뜻하지 않습니다.
/// </remarks>
public static class AppCacheProbeContract
{
    /// <summary>앱 캐시 프로브 ID.</summary>
    public const string PROBE_ID = "appCache.scan";

    /// <summary>모든 앱 캐시 측정 이름의 접두사(내보내기 경로 토큰화 기준).</summary>
    public const string MEASUREMENT_PREFIX = "appCache.";

    /// <summary>규칙 파일 무결성 상태(<c>CATALOG_*</c>, 문자열).</summary>
    public const string CATALOG_STATE = "appCache.catalog.state";

    /// <summary>무결성 확인에 실패한 파일 이름(문자열, 실패일 때만).</summary>
    public const string CATALOG_FAILED_FILE = "appCache.catalog.failedFile";

    /// <summary>winapp2 스냅샷 버전(문자열).</summary>
    public const string WINAPP2_VERSION = "appCache.catalog.winapp2.version";

    /// <summary>winapp2 스냅샷 원본 커밋(문자열).</summary>
    public const string WINAPP2_COMMIT = "appCache.catalog.winapp2.commit";

    /// <summary>winapp2 스냅샷 SHA-256(문자열).</summary>
    public const string WINAPP2_SHA256 = "appCache.catalog.winapp2.sha256";

    /// <summary>winapp2 스냅샷 라이선스(문자열).</summary>
    public const string WINAPP2_LICENSE = "appCache.catalog.winapp2.license";

    /// <summary>커뮤니티 규칙 수(정수).</summary>
    public const string COMMUNITY_TOTAL = "appCache.catalog.community.total";

    /// <summary>지원 커뮤니티 규칙 수(정수).</summary>
    public const string COMMUNITY_SUPPORTED = "appCache.catalog.community.supported";

    /// <summary>보충 규칙 수(정수).</summary>
    public const string SUPPLEMENT_TOTAL = "appCache.catalog.supplement.total";

    /// <summary>지원 보충 규칙 수(정수).</summary>
    public const string SUPPLEMENT_SUPPORTED = "appCache.catalog.supplement.supported";

    /// <summary>해석 단계 사유별 미지원 규칙 수("사유=개수" 문자열 목록).</summary>
    public const string UNSUPPORTED_BY_REASON = "appCache.catalog.unsupportedByReason";

    /// <summary>규칙 해석 소요 시간(밀리초, 정수).</summary>
    public const string PARSE_MS = "appCache.catalog.parseMs";

    /// <summary>탐지된 지원 규칙 수(정수).</summary>
    public const string DETECTED_COUNT = "appCache.detection.detected";

    /// <summary>탐지되지 않은 지원 규칙 수(정수).</summary>
    public const string NOT_DETECTED_COUNT = "appCache.detection.notDetected";

    /// <summary>탐지 여부를 확인하지 못한 지원 규칙 수(정수, 접근 거부·보호 위치 와일드카드 등).</summary>
    public const string DETECTION_UNKNOWN_COUNT = "appCache.detection.unknown";

    /// <summary>탐지됐지만 이 PC에서 경로를 펼치다 미지원이 된 규칙의 사유별 수("사유=개수" 문자열 목록).</summary>
    public const string RUNTIME_UNSUPPORTED_BY_REASON = "appCache.detection.runtimeUnsupportedByReason";

    /// <summary>관리자 권한 검사라 사용자 설정 경로를 적용하지 않았는지 여부(불리언).</summary>
    public const string ELEVATED_DEFAULTS_ONLY = "appCache.elevatedDefaultsOnly";

    /// <summary>앱 캐시 관측 소요 시간(밀리초, 정수, 공유 스캔 대기 제외).</summary>
    public const string ELAPSED_MS = "appCache.elapsedMs";

    /// <summary>규칙 결과 수(정수).</summary>
    public const string RULE_COUNT = "appCache.ruleCount";

    /// <summary>규칙 결과 접두사.</summary>
    public const string RULE_PREFIX = "appCache.rule";

    /// <summary>Squirrel 앱 수(정수).</summary>
    public const string SQUIRREL_COUNT = "appCache.squirrelCount";

    /// <summary>Squirrel 앱 접두사.</summary>
    public const string SQUIRREL_PREFIX = "appCache.squirrel";

    /// <summary>앱 설정 리더 결과 수(정수).</summary>
    public const string CONFIG_COUNT = "appCache.configCount";

    /// <summary>앱 설정 리더 결과 접두사.</summary>
    public const string CONFIG_PREFIX = "appCache.config";

    /// <summary>필드: 규칙 ID(문자열).</summary>
    public const string FIELD_ID = "id";

    /// <summary>필드: 표시 이름(문자열).</summary>
    public const string FIELD_NAME = "name";

    /// <summary>필드: 출처(<c>ORIGIN_*</c>, 문자열).</summary>
    public const string FIELD_ORIGIN = "origin";

    /// <summary>필드: 상태(<c>RULE_STATE_*</c> 또는 <c>CONFIG_STATE_*</c>, 문자열).</summary>
    public const string FIELD_STATE = "state";

    /// <summary>필드: 관측 논리 크기(정수, 바이트).</summary>
    public const string FIELD_BYTES = "bytes";

    /// <summary>필드: 관측 파일 수(정수).</summary>
    public const string FIELD_FILE_COUNT = "fileCount";

    /// <summary>필드: 부분 관측 여부(불리언).</summary>
    public const string FIELD_PARTIAL = "partial";

    /// <summary>필드: 하드링크 등 중복 가능 여부(불리언, 경로 단위로만 중복을 걸렀음).</summary>
    public const string FIELD_DUPLICATES_POSSIBLE = "duplicatesPossible";

    /// <summary>필드: 관측 위치 수(정수).</summary>
    public const string FIELD_TARGET_COUNT = "targetCount";

    /// <summary>필드: 보호 위치라 관측하지 않은 위치 수(정수).</summary>
    public const string FIELD_PROTECTED_TARGETS = "protectedTargets";

    /// <summary>필드: 규칙 제외로 뺀 위치 수(정수).</summary>
    public const string FIELD_EXCLUDED_TARGETS = "excludedTargets";

    /// <summary>필드: 다른 규칙 위치와 겹쳐 그쪽에서 센 위치 수(정수).</summary>
    public const string FIELD_MERGED_TARGETS = "mergedTargets";

    /// <summary>필드: 같은 위치를 가리킨 다른 규칙 ID(문자열 목록).</summary>
    public const string FIELD_SHARED_WITH = "sharedWith";

    /// <summary>필드: 관측한 폴더 경로(문자열 목록, 측정값 전용).</summary>
    public const string FIELD_PATHS = "paths";

    /// <summary>필드: 경로 하나(문자열, 측정값 전용).</summary>
    public const string FIELD_PATH = "path";

    /// <summary>필드: 건너뜀 - 접근 거부(정수).</summary>
    public const string FIELD_SKIP_ACCESS_DENIED = "skip.accessDenied";

    /// <summary>필드: 건너뜀 - 사용 중(정수).</summary>
    public const string FIELD_SKIP_IN_USE = "skip.inUse";

    /// <summary>필드: 건너뜀 - 시간 초과(정수).</summary>
    public const string FIELD_SKIP_TIMEOUT = "skip.timeout";

    /// <summary>필드: 건너뜀 - 보호 제외(정수).</summary>
    public const string FIELD_SKIP_PROTECTED = "skip.protectedExcluded";

    /// <summary>필드: 건너뜀 - reparse point(정수).</summary>
    public const string FIELD_SKIP_REPARSE = "skip.reparse";

    /// <summary>필드: 건너뜀 - 클라우드 자리 표시자(정수).</summary>
    public const string FIELD_SKIP_PLACEHOLDER = "skip.placeholder";

    /// <summary>필드: 앱 설정 경로 출처(<c>CONFIG_SOURCE_*</c>, 문자열).</summary>
    public const string FIELD_CONFIG_SOURCE = "configSource";

    /// <summary>필드: 앱 설정 리더 이름(문자열).</summary>
    public const string FIELD_CONFIG_APP = "configApp";

    /// <summary>필드: 원본 Warning 문구 존재 여부(불리언).</summary>
    public const string FIELD_HAS_WARNING = "hasWarning";

    /// <summary>필드: 검토한 영향 설명이 있는지 여부(불리언).</summary>
    public const string FIELD_REVIEWED = "reviewed";

    /// <summary>필드: 영향 - 기대 효과(문자열, 검토 규칙만).</summary>
    public const string FIELD_IMPACT_BENEFIT = "impact.benefit";

    /// <summary>필드: 영향 - 부작용(문자열, 검토 규칙만).</summary>
    public const string FIELD_IMPACT_SIDE_EFFECT = "impact.sideEffect";

    /// <summary>필드: 영향 - 재생성(문자열, 검토 규칙만).</summary>
    public const string FIELD_IMPACT_REGENERATION = "impact.regeneration";

    /// <summary>필드: 지원 앱 버전 메모(문자열, 검토 규칙만).</summary>
    public const string FIELD_APP_VERSION_NOTES = "appVersionNotes";

    /// <summary>필드: 참조 출처(문자열 목록, 검토 규칙만).</summary>
    public const string FIELD_SOURCES = "sources";

    /// <summary>필드: RegKey 개수(정수, 지원하지 않는 효과).</summary>
    public const string FIELD_REG_KEY_COUNT = "regKeyCount";

    /// <summary>필드: 앱 이름(문자열). 규칙은 카드 묶음 기준 앱 이름(검토 규칙의 appLabel 또는 섹션 이름), Squirrel은 앱 폴더 이름, 앱 설정은 리더 이름.</summary>
    public const string FIELD_APP = "app";

    /// <summary>필드: Squirrel 버전 폴더 이름(문자열 목록).</summary>
    public const string FIELD_VERSION_FOLDERS = "versionFolders";

    /// <summary>필드: 앱 설정 값 출처(<c>CONFIG_ORIGIN_*</c>, 문자열).</summary>
    public const string FIELD_CONFIG_ORIGIN = "valueOrigin";

    /// <summary>필드: 앱 설정 리더가 연결된 규칙 ID(문자열).</summary>
    public const string FIELD_RULE_ID = "ruleId";

    /// <summary>필드: Steam 라이브러리 수(정수).</summary>
    public const string FIELD_LIBRARY_COUNT = "libraryCount";

    /// <summary>규칙 파일 상태: 무결성 확인.</summary>
    public const string CATALOG_VERIFIED = "verified";

    /// <summary>규칙 파일 상태: 파일 없음.</summary>
    public const string CATALOG_MISSING = "missingFile";

    /// <summary>규칙 파일 상태: SHA-256 불일치.</summary>
    public const string CATALOG_MISMATCH = "integrityMismatch";

    /// <summary>규칙 파일 상태: 출처 목록(sources.json) 무효.</summary>
    public const string CATALOG_INVALID_MANIFEST = "invalidManifest";

    /// <summary>규칙 파일 상태: 메타데이터 무효.</summary>
    public const string CATALOG_INVALID_METADATA = "invalidMetadata";

    /// <summary>규칙 파일 상태: 보호 정책 무효로 관측하지 않음.</summary>
    public const string CATALOG_POLICY_INVALID = "protectionPolicyInvalid";

    /// <summary>출처: 커뮤니티.</summary>
    public const string ORIGIN_COMMUNITY = "community";

    /// <summary>출처: 보충.</summary>
    public const string ORIGIN_SUPPLEMENT = "supplement";

    /// <summary>규칙 상태: 모두 관측.</summary>
    public const string RULE_STATE_OBSERVED = "observed";

    /// <summary>규칙 상태: 일부 항목을 읽지 못한 부분 관측.</summary>
    public const string RULE_STATE_PARTIAL = "partial";

    /// <summary>규칙 상태: 위치가 없음(관측 파일 0).</summary>
    public const string RULE_STATE_ABSENT = "absent";

    /// <summary>규칙 상태: 모든 위치가 보호 위치라 관측하지 않음.</summary>
    public const string RULE_STATE_PROTECTED = "protected";

    /// <summary>규칙 상태: 모든 위치가 다른 규칙 위치와 겹쳐 그쪽에서 셈.</summary>
    public const string RULE_STATE_MERGED = "merged";

    /// <summary>규칙 상태: 모든 위치가 규칙 제외 안.</summary>
    public const string RULE_STATE_EXCLUDED = "excluded";

    /// <summary>규칙 상태: 접근 거부로 관측하지 못함.</summary>
    public const string RULE_STATE_ACCESS_DENIED = "accessDenied";

    /// <summary>규칙 상태: 시간 초과로 관측하지 못함.</summary>
    public const string RULE_STATE_TIMED_OUT = "timedOut";

    /// <summary>규칙 상태: 그 밖의 이유로 관측하지 못함.</summary>
    public const string RULE_STATE_NOT_OBSERVED = "notObserved";

    /// <summary>앱 설정 경로 출처: 사용자 설정 경로를 적용함.</summary>
    public const string CONFIG_SOURCE_USER = "userConfig";

    /// <summary>앱 설정 경로 출처: 기본 위치만 확인함.</summary>
    public const string CONFIG_SOURCE_DEFAULT_ONLY = "defaultOnly";

    /// <summary>앱 설정 경로 출처: 해당 없음(앱 설정 리더 없음).</summary>
    public const string CONFIG_SOURCE_NOT_APPLICABLE = "notApplicable";

    /// <summary>앱 설정 상태: 설정 없음(기본 위치만).</summary>
    public const string CONFIG_STATE_NOT_CONFIGURED = "notConfigured";

    /// <summary>앱 설정 상태: 설정 경로 적용.</summary>
    public const string CONFIG_STATE_APPLIED = "applied";

    /// <summary>앱 설정 상태: 네트워크(UNC) 경로라 순회하지 않음.</summary>
    public const string CONFIG_STATE_UNC = "unc";

    /// <summary>앱 설정 상태: 보호 위치라 순회하지 않음.</summary>
    public const string CONFIG_STATE_PROTECTED = "protected";

    /// <summary>앱 설정 상태: 드라이브가 연결되어 있지 않아 순회하지 않음.</summary>
    public const string CONFIG_STATE_OFFLINE = "offline";

    /// <summary>앱 설정 상태: 값을 해석하지 못함(상대 경로·변수 등).</summary>
    public const string CONFIG_STATE_INVALID = "invalid";

    /// <summary>앱 설정 상태: 설정 파일이 있으나 읽지 못함.</summary>
    public const string CONFIG_STATE_UNREADABLE = "unreadable";

    /// <summary>앱 설정 상태: 형식을 검증할 수 없어 읽지 않음(Adobe).</summary>
    public const string CONFIG_STATE_CANNOT_VERIFY = "cannotVerify";

    /// <summary>앱 설정 상태: 관리자 권한 검사라 적용하지 않음.</summary>
    public const string CONFIG_STATE_NOT_APPLIED_ELEVATED = "notAppliedElevated";

    /// <summary>앱 설정 값 출처: 환경 변수.</summary>
    public const string CONFIG_ORIGIN_ENVIRONMENT = "environment";

    /// <summary>앱 설정 값 출처: 사용자 설정 파일.</summary>
    public const string CONFIG_ORIGIN_USER_FILE = "userFile";

    /// <summary>앱 설정 값 출처: 레지스트리.</summary>
    public const string CONFIG_ORIGIN_REGISTRY = "registry";

    /// <summary>앱 설정 값 출처: 없음.</summary>
    public const string CONFIG_ORIGIN_NONE = "none";

    /// <summary>앱 설정 리더 이름: Adobe.</summary>
    public const string CONFIG_APP_ADOBE = "adobe";

    /// <summary>크기 단위.</summary>
    public const string UNIT_BYTES = "bytes";

    /// <summary>시간 단위.</summary>
    public const string UNIT_MILLISECONDS = "ms";

    /// <summary>"사유=개수" 목록 항목 구분자.</summary>
    public const char COUNT_SEPARATOR = '=';

    /// <summary>
    /// 목록 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="prefix">접두사 상수.</param>
    /// <param name="index">인덱스.</param>
    /// <param name="field">필드 상수.</param>
    /// <returns>측정 이름.</returns>
    public static string Name(string prefix, int index, string field)
    {
        return IndexedMeasurementName.Create(prefix, index, field);
    }

    /// <summary>
    /// 목록 항목 하나의 측정 이름 접두사를 만듭니다.
    /// </summary>
    /// <param name="prefix">접두사 상수.</param>
    /// <param name="index">인덱스.</param>
    /// <returns>접두사.</returns>
    public static string ItemPrefix(string prefix, int index)
    {
        return IndexedMeasurementName.Prefix(prefix, index);
    }
}
