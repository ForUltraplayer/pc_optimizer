/**
 * @file    : NvidiaAdapterView.cs
 * @author  : rudals252
 * @brief   : NVIDIA 조회 프로브 결과에서 어댑터 하나의 측정값(설치 버전·조회 상태·계열별 목록/최신 항목)을 값 없음(null)과 구분해 읽는 규칙 내부 보기
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 계열(Game Ready/Studio) 하나의 측정값 보기입니다.
/// </summary>
/// <param name="Versions">목록 버전(목록을 받지 못했으면 null).</param>
/// <param name="LatestVersion">최신(베타 제외) 버전.</param>
/// <param name="LatestReleaseDate">최신 배포일.</param>
/// <param name="LatestDetailsUrl">공식 배포 설명 URL.</param>
/// <param name="LatestDownloadUrl">공식 다운로드 URL.</param>
internal sealed record NvidiaBranchView(
    IReadOnlyList<string>? Versions,
    string? LatestVersion,
    string? LatestReleaseDate,
    string? LatestDetailsUrl,
    string? LatestDownloadUrl);

/// <summary>
/// NVIDIA 조회 어댑터 하나의 측정값 보기입니다(<see cref="NvidiaLookupProbeContract"/>).
/// </summary>
/// <param name="Index">어댑터 인덱스.</param>
/// <param name="Name">어댑터 이름.</param>
/// <param name="PnpDeviceId">PnP 장치 ID.</param>
/// <param name="InstalledVersion">NVIDIA 표기 설치 버전.</param>
/// <param name="InstalledDate">설치 드라이버 날짜.</param>
/// <param name="State">조회 상태.</param>
/// <param name="ProductMatchCount">제품 목록 정확 일치 수.</param>
/// <param name="FailureReason">조회 실패 사유 이름.</param>
/// <param name="GameReady">Game Ready 계열 보기.</param>
/// <param name="Studio">Studio 계열 보기.</param>
/// <param name="StudioAvailable">Studio 목록을 받았는지.</param>
/// <param name="Measured">이 어댑터의 측정값 묶음.</param>
internal sealed record NvidiaAdapterView(
    int Index,
    string? Name,
    string? PnpDeviceId,
    string? InstalledVersion,
    string? InstalledDate,
    string? State,
    long? ProductMatchCount,
    string? FailureReason,
    NvidiaBranchView GameReady,
    NvidiaBranchView Studio,
    bool StudioAvailable,
    Measurement[] Measured)
{
    /// <summary>
    /// Studio 목록을 받았으면 그 버전 목록(빈 목록 포함), 받지 못했으면 null.
    /// </summary>
    public IReadOnlyList<string>? StudioVersionsIfAvailable => StudioAvailable ? Studio.Versions ?? [] : null;

    /// <summary>
    /// 스냅샷에서 어댑터 하나를 읽습니다.
    /// </summary>
    /// <param name="snapshot">검사 스냅샷.</param>
    /// <param name="result">NVIDIA 조회 프로브 결과.</param>
    /// <param name="index">어댑터 인덱스.</param>
    /// <returns>어댑터 보기.</returns>
    public static NvidiaAdapterView Read(ScanSnapshot snapshot, ProbeResult result, int index)
    {
        string Name(string field) => NvidiaLookupProbeContract.AdapterMeasurementName(index, field);
        string? Text(string field) => SnapshotValues.Text(snapshot, NvidiaLookupProbeContract.PROBE_ID, Name(field));

        return new NvidiaAdapterView(
            index,
            Text(NvidiaLookupProbeContract.FIELD_NAME),
            Text(NvidiaLookupProbeContract.FIELD_PNP_DEVICE_ID),
            Text(NvidiaLookupProbeContract.FIELD_INSTALLED_VERSION),
            Text(NvidiaLookupProbeContract.FIELD_INSTALLED_DATE),
            Text(NvidiaLookupProbeContract.FIELD_LOOKUP_STATE),
            SnapshotValues.Integer(snapshot, NvidiaLookupProbeContract.PROBE_ID, Name(NvidiaLookupProbeContract.FIELD_PRODUCT_MATCH_COUNT)),
            Text(NvidiaLookupProbeContract.FIELD_FAILURE_REASON),
            ReadBranch(snapshot, index, NvidiaLookupProbeContract.BRANCH_GAME_READY),
            ReadBranch(snapshot, index, NvidiaLookupProbeContract.BRANCH_STUDIO),
            SnapshotValues.Boolean(
                snapshot,
                NvidiaLookupProbeContract.PROBE_ID,
                Name(NvidiaLookupProbeContract.BranchField(NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_AVAILABLE))) == true,
            SnapshotValues.WithPrefix(result, NvidiaLookupProbeContract.AdapterMeasurementPrefix(index)));
    }

    /// <summary>
    /// 계열 하나의 측정값을 읽는다.
    /// </summary>
    private static NvidiaBranchView ReadBranch(ScanSnapshot snapshot, int index, string branch)
    {
        string Name(string suffix) => NvidiaLookupProbeContract.AdapterMeasurementName(index, NvidiaLookupProbeContract.BranchField(branch, suffix));
        string? Text(string suffix) => SnapshotValues.Text(snapshot, NvidiaLookupProbeContract.PROBE_ID, Name(suffix));

        return new NvidiaBranchView(
            SnapshotValues.TextList(snapshot, NvidiaLookupProbeContract.PROBE_ID, Name(NvidiaLookupProbeContract.SUFFIX_VERSIONS)),
            Text(NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION),
            Text(NvidiaLookupProbeContract.SUFFIX_LATEST_RELEASE_DATE),
            Text(NvidiaLookupProbeContract.SUFFIX_LATEST_DETAILS_URL),
            Text(NvidiaLookupProbeContract.SUFFIX_LATEST_DOWNLOAD_URL));
    }
}
