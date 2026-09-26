/**
 * @file    : NvidiaLookupMeasurements.cs
 * @author  : rudals252
 * @brief   : NVIDIA 온라인 조회 프로브의 측정값을 계약 이름(어댑터별·계열별)과 출처·UTC 관측 시각으로 기록하는 도우미(계열 최신은 베타 제외 최고 버전)
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// NVIDIA 조회 측정값 기록기입니다. 판정은 하지 않으며 이름은 <see cref="NvidiaLookupProbeContract"/>를 따릅니다.
/// </summary>
internal sealed class NvidiaLookupMeasurements
{
    /// <summary>WMI 출처 접두사.</summary>
    public const string SOURCE_WMI_PREFIX = "WMI Win32_VideoController.";

    /// <summary>NVIDIA 표기 변환 출처.</summary>
    public const string SOURCE_CONVERSION = "NVIDIA 표기 변환(Win32_VideoController.DriverVersion)";

    /// <summary>제품 목록 출처.</summary>
    public const string SOURCE_PRODUCT_LIST = "NVIDIA lookupValueSearch TypeID=3";

    /// <summary>조회 어댑터 판단(상태·일치 수·계열) 출처.</summary>
    public const string SOURCE_LOOKUP = "PcOptimizer NVIDIA 조회 어댑터";

    private const string DATE_FORMAT = "yyyy-MM-dd";

    private readonly List<Measurement> _items = [];
    private readonly DateTimeOffset _observedAt;

    /// <summary>
    /// 기록기를 만듭니다.
    /// </summary>
    /// <param name="observedAt">관측 시각(UTC).</param>
    public NvidiaLookupMeasurements(DateTimeOffset observedAt)
    {
        _observedAt = observedAt;
    }

    /// <summary>기록한 측정값.</summary>
    public IReadOnlyList<Measurement> Items => _items;

    /// <summary>
    /// 조회 대상 어댑터 수를 기록합니다.
    /// </summary>
    /// <param name="count">어댑터 수.</param>
    public void AdapterCount(int count)
    {
        _items.Add(new Measurement(NvidiaLookupProbeContract.ADAPTER_COUNT, new IntegerValue(count), null, SOURCE_WMI_PREFIX + "Count", _observedAt, MeasurementQuality.Observed));
    }

    /// <summary>
    /// 값이 있으면 어댑터 문자열 측정값을 기록합니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="field">필드.</param>
    /// <param name="value">값.</param>
    /// <param name="source">출처.</param>
    public void Text(int index, string field, string? value, string source)
    {
        if (value is not null)
        {
            _items.Add(new Measurement(NvidiaLookupProbeContract.AdapterMeasurementName(index, field), new TextValue(value), null, source, _observedAt, MeasurementQuality.Reported));
        }
    }

    /// <summary>
    /// 어댑터 정수 측정값을 기록합니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="field">필드.</param>
    /// <param name="value">값.</param>
    /// <param name="source">출처.</param>
    public void Integer(int index, string field, long value, string source)
    {
        _items.Add(new Measurement(NvidiaLookupProbeContract.AdapterMeasurementName(index, field), new IntegerValue(value), null, source, _observedAt, MeasurementQuality.Reported));
    }

    /// <summary>
    /// 조회 상태를 기록합니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="state">상태 상수.</param>
    public void State(int index, string state)
    {
        Text(index, NvidiaLookupProbeContract.FIELD_LOOKUP_STATE, state, SOURCE_LOOKUP);
    }

    /// <summary>
    /// 조회 실패 상태와 사유를 기록합니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="failure">실패.</param>
    public void Failed(int index, NvidiaLookupFailure failure)
    {
        State(index, NvidiaLookupProbeContract.STATE_FAILED);
        Text(index, NvidiaLookupProbeContract.FIELD_FAILURE_REASON, failure.Reason.ToString(), SOURCE_LOOKUP);
    }

    /// <summary>
    /// Studio 목록을 받았는지 기록합니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="available">받았으면 true.</param>
    public void StudioAvailable(int index, bool available)
    {
        _items.Add(new Measurement(
            NvidiaLookupProbeContract.AdapterMeasurementName(
                index, NvidiaLookupProbeContract.BranchField(NvidiaLookupProbeContract.BRANCH_STUDIO, NvidiaLookupProbeContract.SUFFIX_AVAILABLE)),
            new BooleanValue(available),
            null,
            SourceFor(NvidiaDriverListKind.Studio),
            _observedAt,
            MeasurementQuality.Reported));
    }

    /// <summary>
    /// 계열 목록(버전 목록, 베타 제외 최고 버전의 배포일·이름·URL)을 기록합니다.
    /// </summary>
    /// <param name="index">어댑터 인덱스.</param>
    /// <param name="kind">목록 종류.</param>
    /// <param name="entries">검증한 항목(응답 순서).</param>
    public void BranchList(int index, NvidiaDriverListKind kind, IReadOnlyList<NvidiaDriverEntry> entries)
    {
        var branch = kind == NvidiaDriverListKind.Studio ? NvidiaLookupProbeContract.BRANCH_STUDIO : NvidiaLookupProbeContract.BRANCH_GAME_READY;
        var source = SourceFor(kind);
        string Name(string suffix) => NvidiaLookupProbeContract.BranchField(branch, suffix);

        _items.Add(new Measurement(
            NvidiaLookupProbeContract.AdapterMeasurementName(index, Name(NvidiaLookupProbeContract.SUFFIX_VERSIONS)),
            new TextListValue([.. entries.Select(entry => entry.Version)]),
            null,
            source,
            _observedAt,
            MeasurementQuality.Reported));

        var latestVersion = DriverVersionComparer.Highest(entries.Where(entry => !entry.IsBeta).Select(entry => entry.Version));
        if (latestVersion is null)
        {
            return;
        }

        var latest = entries.First(entry => !entry.IsBeta && entry.Version == latestVersion);
        Text(index, Name(NvidiaLookupProbeContract.SUFFIX_LATEST_VERSION), latest.Version, source);
        Text(index, Name(NvidiaLookupProbeContract.SUFFIX_LATEST_RELEASE_DATE), latest.ReleaseDate.ToString(DATE_FORMAT, CultureInfo.InvariantCulture), source);
        Text(index, Name(NvidiaLookupProbeContract.SUFFIX_LATEST_NAME), latest.Name, source);
        Text(index, Name(NvidiaLookupProbeContract.SUFFIX_LATEST_DETAILS_URL), latest.DetailsUrl, source);
        Text(index, Name(NvidiaLookupProbeContract.SUFFIX_LATEST_DOWNLOAD_URL), latest.DownloadUrl, source);
    }

    /// <summary>
    /// 목록 종류별 출처 문자열(고정 요청 매개변수 포함).
    /// </summary>
    private static string SourceFor(NvidiaDriverListKind kind)
    {
        var parameters = kind == NvidiaDriverListKind.Studio ? NvidiaLookupClient.STUDIO_PARAMETERS : NvidiaLookupClient.GAME_READY_PARAMETERS;
        return string.Create(CultureInfo.InvariantCulture, $"NVIDIA DriverManualLookup {kind} (osID={NvidiaLookupClient.OS_ID}, {parameters})");
    }
}
