/**
 * @file    : FileScanProbeContract.cs
 * @author  : rudals252
 * @brief   : 공유 파일 스캔 프로브와 임시 위치·스캔 요약·미분류 폴더 규칙이 공유하는 프로브 ID·루트/위치 ID·상태 코드·측정 이름 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 파일 스캔 프로브(Probes)와 <see cref="TempLocationsRule"/>·<see cref="FileScanSummaryRule"/>·<see cref="UnclassifiedFolderRule"/>(Core)가 공유하는 측정값 계약입니다.
/// </summary>
/// <remarks>
/// 목록 측정 이름은 <c>fileScan.root[i].필드</c>, <c>fileScan.volume[i].필드</c>, <c>fileScan.temp[i].필드</c>,
/// <c>fileScan.unclassified[i].필드</c>, <c>fileScan.partialFolder[i].필드</c> 형식입니다.
/// 경로(<c>path</c> 필드)는 측정값에만 두고 화면 문장에는 쓰지 않습니다(기본 내보내기에서 토큰화).
/// 크기는 모두 관측한 파일의 논리 크기이며 '확보 가능량'이 아닙니다.
/// </remarks>
public static class FileScanProbeContract
{
    /// <summary>파일 스캔 프로브 ID.</summary>
    public const string PROBE_ID = "storage.fileScan";

    /// <summary>보호 정책 상태(<c>POLICY_*</c>, 문자열).</summary>
    public const string POLICY_STATE = "fileScan.policy.state";

    /// <summary>보호 정책 오류 코드(무효일 때만, 문자열).</summary>
    public const string POLICY_ERROR = "fileScan.policy.error";

    /// <summary>해석된 보호 루트 수(정수).</summary>
    public const string PROTECTED_ROOT_COUNT = "fileScan.protected.count";

    /// <summary>
    /// 보호 루트의 출처 이름(문자열 목록, 중복 제거, 예: "KnownFolder:Documents", "Cloud:OneDrive", "System:WinSxS").
    /// 리디렉션된 폴더·다른 드라이브의 동기화 루트가 개인 경로일 수 있으므로 보호 루트의 경로 자체는 측정값에 넣지 않습니다.
    /// </summary>
    public const string PROTECTED_ROOT_LABELS = "fileScan.protected.labels";

    /// <summary>위치를 찾지 못한 보호 정책 항목 수(정수). Known Folder는 이때도 프로필 기본 위치를 보호합니다.</summary>
    public const string PROTECTED_UNRESOLVED_COUNT = "fileScan.protected.unresolvedCount";

    /// <summary>보호 루트 출처 이름 접두사: Known Folder.</summary>
    public const string PROTECTED_LABEL_KNOWN_FOLDER = "KnownFolder:";

    /// <summary>보호 루트 출처 이름 접두사: 시스템 경로.</summary>
    public const string PROTECTED_LABEL_SYSTEM = "System:";

    /// <summary>보호 루트 출처 이름 접두사: 클라우드 동기화.</summary>
    public const string PROTECTED_LABEL_CLOUD = "Cloud:";

    /// <summary>감지된 동기화 루트 수(정수).</summary>
    public const string SYNC_ROOT_COUNT = "fileScan.protected.syncRootCount";

    /// <summary>스캔 전체 소요 시간(밀리초, 정수).</summary>
    public const string ELAPSED_MS = "fileScan.elapsedMs";

    /// <summary>파일 ID로 확인해 한 번만 센 하드링크 중복 파일 수(정수).</summary>
    public const string HARD_LINK_DUPLICATE_COUNT = "fileScan.hardLinkDuplicates.count";

    /// <summary>하드링크 중복으로 빼 준 논리 크기(정수, 바이트).</summary>
    public const string HARD_LINK_DUPLICATE_BYTES = "fileScan.hardLinkDuplicates.bytes";

    /// <summary>스캔 루트 수(정수).</summary>
    public const string ROOT_COUNT = "fileScan.rootCount";

    /// <summary>루트 측정 이름 접두사.</summary>
    public const string ROOT_PREFIX = "fileScan.root";

    /// <summary>볼륨 수(정수).</summary>
    public const string VOLUME_COUNT = "fileScan.volumeCount";

    /// <summary>볼륨 측정 이름 접두사.</summary>
    public const string VOLUME_PREFIX = "fileScan.volume";

    /// <summary>표준 임시 위치 수(정수).</summary>
    public const string TEMP_COUNT = "fileScan.tempCount";

    /// <summary>임시 위치 측정 이름 접두사.</summary>
    public const string TEMP_PREFIX = "fileScan.temp";

    /// <summary>미분류 후보 수(상위 개수로 자른 뒤, 정수).</summary>
    public const string UNCLASSIFIED_COUNT = "fileScan.unclassifiedCount";

    /// <summary>자르기 전 미분류 후보 수(정수).</summary>
    public const string UNCLASSIFIED_QUALIFYING_COUNT = "fileScan.unclassifiedQualifyingCount";

    /// <summary>미분류 후보 최소 크기(정수, 바이트).</summary>
    public const string UNCLASSIFIED_MIN_BYTES = "fileScan.unclassifiedMinBytes";

    /// <summary>미분류 후보 측정 이름 접두사.</summary>
    public const string UNCLASSIFIED_PREFIX = "fileScan.unclassified";

    /// <summary>부분 관측 대용량 폴더 수(상위 개수로 자른 뒤, 정수).</summary>
    public const string PARTIAL_FOLDER_COUNT = "fileScan.partialFolderCount";

    /// <summary>자르기 전 부분 관측 대용량 폴더 수(정수).</summary>
    public const string PARTIAL_FOLDER_TOTAL_COUNT = "fileScan.partialFolderTotalCount";

    /// <summary>부분 관측 대용량 폴더 측정 이름 접두사.</summary>
    public const string PARTIAL_FOLDER_PREFIX = "fileScan.partialFolder";

    /// <summary>필드: ID(문자열, 루트·임시 위치).</summary>
    public const string FIELD_ID = "id";

    /// <summary>필드: 전체 경로(문자열, 측정값 전용).</summary>
    public const string FIELD_PATH = "path";

    /// <summary>필드: 상태 코드(문자열).</summary>
    public const string FIELD_STATE = "state";

    /// <summary>필드: 관측 논리 크기(정수, 바이트).</summary>
    public const string FIELD_BYTES = "bytes";

    /// <summary>필드: 관측 파일 수(정수).</summary>
    public const string FIELD_FILE_COUNT = "fileCount";

    /// <summary>필드: 하위 폴더 수(정수).</summary>
    public const string FIELD_DIRECTORY_COUNT = "directoryCount";

    /// <summary>필드: 부분 집계 여부(불리언).</summary>
    public const string FIELD_PARTIAL = "partial";

    /// <summary>필드: 하드링크 중복 가능 여부(불리언, 논리 크기 추정).</summary>
    public const string FIELD_DUPLICATES_POSSIBLE = "duplicatesPossible";

    /// <summary>필드: 시스템 위치 여부(불리언, 임시 위치).</summary>
    public const string FIELD_SYSTEM = "system";

    /// <summary>필드: 볼륨 루트(문자열, 예: "C:\").</summary>
    public const string FIELD_VOLUME = "volume";

    /// <summary>필드: 소요 시간(밀리초, 정수, 볼륨).</summary>
    public const string FIELD_ELAPSED_MS = "elapsedMs";

    /// <summary>필드: 시간 예산 초과 여부(불리언, 볼륨).</summary>
    public const string FIELD_TIMED_OUT = "timedOut";

    /// <summary>필드: 이 루트가 들어 있는 다른 루트 ID(중첩 루트만, 문자열).</summary>
    public const string FIELD_NESTED_IN = "nestedIn";

    /// <summary>필드: 할당 크기를 읽은 압축·희소 파일 수(정수).</summary>
    public const string FIELD_COMPRESSED_SPARSE_COUNT = "compressedSparse.count";

    /// <summary>필드: 압축·희소 파일의 논리 크기(정수, 바이트).</summary>
    public const string FIELD_COMPRESSED_SPARSE_LOGICAL = "compressedSparse.logicalBytes";

    /// <summary>필드: 압축·희소 파일의 할당 크기(정수, 바이트).</summary>
    public const string FIELD_COMPRESSED_SPARSE_ALLOCATED = "compressedSparse.allocatedBytes";

    /// <summary>필드: 건너뜀 - 보호 제외(정수).</summary>
    public const string FIELD_SKIP_PROTECTED = "skip.protectedExcluded";

    /// <summary>필드: 건너뜀 - 접근 거부(정수).</summary>
    public const string FIELD_SKIP_ACCESS_DENIED = "skip.accessDenied";

    /// <summary>필드: 건너뜀 - 사용 중·변경 중(정수).</summary>
    public const string FIELD_SKIP_IN_USE = "skip.inUse";

    /// <summary>필드: 건너뜀 - reparse point(정수).</summary>
    public const string FIELD_SKIP_REPARSE = "skip.reparse";

    /// <summary>필드: 건너뜀 - 클라우드 placeholder(정수).</summary>
    public const string FIELD_SKIP_PLACEHOLDER = "skip.placeholder";

    /// <summary>필드: 건너뜀 - 시간 초과(정수).</summary>
    public const string FIELD_SKIP_TIMEOUT = "skip.timeout";

    /// <summary>필드: 크기순 상위 확장자("확장자=바이트" 문자열 목록, 확장자 없음은 빈 확장자).</summary>
    public const string FIELD_TOP_EXTENSIONS = "topExtensions";

    /// <summary>필드: 관측 파일 중 가장 최근 수정 시각(ISO 8601 UTC 문자열). 마지막 사용 시각이 아님.</summary>
    public const string FIELD_NEWEST_WRITE_UTC = "newestWriteUtc";

    /// <summary>필드: 하위 후보 크기를 빼고 계산했는지 여부(불리언).</summary>
    public const string FIELD_EXCLUDES_REPORTED_CHILDREN = "excludesReportedChildren";

    /// <summary>정책 상태: 유효.</summary>
    public const string POLICY_VALID = "valid";

    /// <summary>정책 상태: 무효(파일 순회 안 함).</summary>
    public const string POLICY_INVALID = "invalid";

    /// <summary>루트 ID: 사용자 프로필.</summary>
    public const string ROOT_PROFILE = "profile";

    /// <summary>루트 ID: ProgramData.</summary>
    public const string ROOT_PROGRAM_DATA = "programData";

    /// <summary>임시 위치 ID: 사용자 임시 폴더(%TEMP%).</summary>
    public const string LOCATION_USER_TEMP = "userTemp";

    /// <summary>임시 위치 ID: Windows 임시 폴더(%SystemRoot%\Temp).</summary>
    public const string LOCATION_WINDOWS_TEMP = "windowsTemp";

    /// <summary>임시 위치 ID: Windows 업데이트 다운로드(%SystemRoot%\SoftwareDistribution\Download).</summary>
    public const string LOCATION_UPDATE_DOWNLOAD = "updateDownload";

    /// <summary>임시 위치 ID: 탐색기 썸네일·아이콘 캐시(thumbcache_*.db, iconcache_*.db).</summary>
    public const string LOCATION_THUMBNAIL_CACHE = "thumbnailCache";

    /// <summary>임시 위치 ID: 배달 최적화 캐시.</summary>
    public const string LOCATION_DELIVERY_OPTIMIZATION = "deliveryOptimization";

    /// <summary>루트 상태: 순회함.</summary>
    public const string ROOT_STATE_SCANNED = "scanned";

    /// <summary>루트 상태: 없음(오류 아님).</summary>
    public const string ROOT_STATE_ABSENT = "absent";

    /// <summary>루트 상태: 루트 자체 접근 거부.</summary>
    public const string ROOT_STATE_ACCESS_DENIED = "accessDenied";

    /// <summary>루트 상태: 다른 루트 안에 있어 따로 순회하지 않음(중복 합산 방지).</summary>
    public const string ROOT_STATE_NESTED = "nested";

    /// <summary>루트 상태: 보호 루트 안이라 순회하지 않음.</summary>
    public const string ROOT_STATE_PROTECTED = "protectedExcluded";

    /// <summary>루트 상태: 환경 변수 등을 해석하지 못함.</summary>
    public const string ROOT_STATE_UNRESOLVED = "unresolved";

    /// <summary>루트 상태: 허용하지 않는 위치(다른 사용자 프로필·볼륨 루트·UNC).</summary>
    public const string ROOT_STATE_REJECTED = "rejected";

    /// <summary>루트 상태: reparse point라 따라가지 않음.</summary>
    public const string ROOT_STATE_REPARSE = "reparsePoint";

    /// <summary>루트 상태: 시간 예산 초과로 시작하지 못함.</summary>
    public const string ROOT_STATE_TIMED_OUT = "timedOut";

    /// <summary>루트 상태: 그 밖의 오류.</summary>
    public const string ROOT_STATE_ERROR = "error";

    /// <summary>임시 위치 상태: 완전 관측.</summary>
    public const string LOCATION_STATE_OBSERVED = "observed";

    /// <summary>임시 위치 상태: 부분 관측(일부 항목을 읽지 못함).</summary>
    public const string LOCATION_STATE_PARTIAL = "partial";

    /// <summary>임시 위치 상태: 없음.</summary>
    public const string LOCATION_STATE_ABSENT = "absent";

    /// <summary>임시 위치 상태: 위치 자체 접근 거부.</summary>
    public const string LOCATION_STATE_ACCESS_DENIED = "accessDenied";

    /// <summary>임시 위치 상태: 상위 폴더를 읽지 못했거나 시간 초과로 관측하지 못함.</summary>
    public const string LOCATION_STATE_NOT_OBSERVED = "notObserved";

    /// <summary>임시 위치 상태: 경로를 해석하지 못함.</summary>
    public const string LOCATION_STATE_UNRESOLVED = "unresolved";

    /// <summary>임시 위치 상태: 보호 루트 안이라 순회하지 않음.</summary>
    public const string LOCATION_STATE_PROTECTED = "protectedExcluded";

    /// <summary>크기 단위.</summary>
    public const string UNIT_BYTES = "bytes";

    /// <summary>시간 단위.</summary>
    public const string UNIT_MILLISECONDS = "ms";

    /// <summary>확장자 목록 항목의 확장자·크기 구분자.</summary>
    public const char EXTENSION_SEPARATOR = '=';

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
