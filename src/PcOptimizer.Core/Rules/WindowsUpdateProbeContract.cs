/**
 * @file    : WindowsUpdateProbeContract.cs
 * @author  : rudals252
 * @brief   : Windows Update 드라이버 검색 프로브와 규칙이 공유하는 프로브 ID·검색 조건·결과 코드·업데이트별 측정 이름·재부팅 필요 여부 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// Windows Update Agent 드라이버 검색 프로브(Probes)와 <see cref="WindowsUpdateDriverRule"/>(Core)가 공유하는 측정값 계약입니다.
/// 업데이트별 측정 이름은 <c>wua.update[인덱스].필드</c>입니다.
/// </summary>
public static class WindowsUpdateProbeContract
{
    /// <summary>Windows Update 드라이버 검색 프로브 ID.</summary>
    public const string PROBE_ID = "drivers.windowsUpdate";

    /// <summary>검색 조건: 설치되지 않았고 숨기지 않은 드라이버(스펙 §5).</summary>
    public const string SEARCH_CRITERIA = "IsInstalled=0 and IsHidden=0 and Type='Driver'";

    /// <summary>ISearchResult.ResultCode(정수).</summary>
    public const string RESULT_CODE = "wua.resultCode";

    /// <summary>찾은 드라이버 업데이트 수(정수).</summary>
    public const string UPDATE_COUNT = "wua.driverUpdateCount";

    /// <summary>드라이버 세부 정보(IWindowsDriverUpdate 속성)를 읽지 못한 업데이트 수(정수).</summary>
    public const string DETAILS_UNAVAILABLE_COUNT = "wua.detailsUnavailableCount";

    /// <summary>재부팅 필요 여부(Microsoft.Update.SystemInfo.RebootRequired, 불리언). 읽지 못하면 없음.</summary>
    public const string REBOOT_REQUIRED = "wua.rebootRequired";

    /// <summary>업데이트 측정 이름 접두사.</summary>
    public const string UPDATE_PREFIX = "wua.update";

    /// <summary>업데이트 필드: 제목.</summary>
    public const string FIELD_TITLE = "title";

    /// <summary>업데이트 필드: 드라이버 모델(DriverModel).</summary>
    public const string FIELD_DRIVER_MODEL = "driverModel";

    /// <summary>업데이트 필드: 드라이버 제조사(DriverManufacturer).</summary>
    public const string FIELD_DRIVER_MANUFACTURER = "driverManufacturer";

    /// <summary>업데이트 필드: 드라이버 날짜(DriverVerDate, "yyyy-MM-dd").</summary>
    public const string FIELD_DRIVER_DATE = "driverVerDate";

    /// <summary>업데이트 필드: 드라이버 클래스(DriverClass).</summary>
    public const string FIELD_DRIVER_CLASS = "driverClass";

    /// <summary>업데이트 필드: KB 문서 ID 목록.</summary>
    public const string FIELD_KB_IDS = "kbArticleIds";

    /// <summary>업데이트 필드: 이미 내려받았는지(IsDownloaded, 불리언).</summary>
    public const string FIELD_IS_DOWNLOADED = "isDownloaded";

    /// <summary>OperationResultCode: 성공.</summary>
    public const int RESULT_SUCCEEDED = 2;

    /// <summary>OperationResultCode: 일부 오류와 함께 성공.</summary>
    public const int RESULT_SUCCEEDED_WITH_ERRORS = 3;

    /// <summary>OperationResultCode: 실패.</summary>
    public const int RESULT_FAILED = 4;

    /// <summary>OperationResultCode: 중단됨.</summary>
    public const int RESULT_ABORTED = 5;

    /// <summary>
    /// 업데이트 필드의 측정 이름을 만듭니다.
    /// </summary>
    /// <param name="index">업데이트 인덱스.</param>
    /// <param name="field">필드 이름.</param>
    /// <returns>측정 이름.</returns>
    public static string UpdateMeasurementName(int index, string field)
    {
        return IndexedMeasurementName.Create(UPDATE_PREFIX, index, field);
    }
}
