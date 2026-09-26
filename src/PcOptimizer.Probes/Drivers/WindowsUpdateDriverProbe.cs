/**
 * @file    : WindowsUpdateDriverProbe.cs
 * @author  : rudals252
 * @brief   : 사용자가 온라인 확인을 켰을 때만 실행되는 Windows Update 드라이버 검색 프로브. 검색 결과 코드·드라이버 업데이트별 제목/모델/제조사/날짜/클래스/KB/내려받음 여부·재부팅 필요를 측정값으로, 실패는 HRESULT 요약 코드로 기록
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// Windows Update 드라이버 검색 프로브입니다(스펙 §5 Windows 업데이트 드라이버 행). 온라인 WUA 검색은 구성된 업데이트 서비스에 연결하므로
/// <see cref="RequiresNetwork"/>가 true이며, 실행 조율기는 사용자가 온라인 확인을 요청한 검사에서만 호출합니다.
/// </summary>
/// <remarks>
/// 결과 코드 2 → Success, 3 → Partial(PartialData), 4 → Failed(ProbeError), 5 → Cancelled. COM 실패는 HRESULT로 사유를 나눕니다
/// (접근 거부·정책으로 꺼짐 → AccessDenied, WU_E_PT_*·WinHTTP → NetworkFailed, 그 밖 → ProbeError). 요약에는 16진수 코드만 남깁니다.
/// 드라이버 세부 속성을 못 읽은 업데이트가 있으면 Partial입니다. 업데이트 서비스·정책 설정은 바꾸지 않고 내려받거나 설치하지 않습니다.
/// </remarks>
public sealed class WindowsUpdateDriverProbe : IProbe
{
    /// <summary>E_ACCESSDENIED.</summary>
    public const int HRESULT_ACCESS_DENIED = unchecked((int)0x80070005);

    /// <summary>WU_E_WU_DISABLED(정책으로 Windows Update 사용 안 함).</summary>
    public const int HRESULT_WU_DISABLED = unchecked((int)0x8024002E);

    private const int HRESULT_FACILITY_MASK = unchecked((int)0xFFFFF000);
    private const int HRESULT_WU_PROTOCOL_TALKER = unchecked((int)0x80244000);
    private const int HRESULT_WINHTTP_MASK = unchecked((int)0xFFFFFF00);
    private const int HRESULT_WINHTTP = unchecked((int)0x80072E00);
    private const string HRESULT_FORMAT = "0x{0:X8}";
    private const string DATE_FORMAT = "yyyy-MM-dd";
    private const string SOURCE_SEARCH = "WUA IUpdateSearcher.BeginSearch/EndSearch";
    private const string SOURCE_UPDATE = "WUA IUpdate/IWindowsDriverUpdate.";
    private const string SOURCE_SYSTEM_INFO = "WUA Microsoft.Update.SystemInfo.RebootRequired";

    private readonly IUpdateSearchGateway _gateway;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WUA 게이트웨이와 시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public WindowsUpdateDriverProbe()
        : this(new WuaSearchGateway(), SystemClock.Instance)
    {
    }

    /// <summary>
    /// 게이트웨이와 시계를 지정해 프로브를 만듭니다(테스트용).
    /// </summary>
    /// <param name="gateway">WUA 검색 게이트웨이.</param>
    /// <param name="clock">UTC 시계.</param>
    public WindowsUpdateDriverProbe(IUpdateSearchGateway gateway, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(clock);
        _gateway = gateway;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => WindowsUpdateProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => true;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_WUA_SEARCH_TIMEOUT;

    /// <summary>
    /// HRESULT를 확인 불가 사유로 나눕니다.
    /// </summary>
    /// <param name="hresult">HRESULT.</param>
    /// <returns>사유.</returns>
    public static CannotVerifyReason ReasonFor(int hresult)
    {
        if (hresult is HRESULT_ACCESS_DENIED or HRESULT_WU_DISABLED)
        {
            return CannotVerifyReason.AccessDenied;
        }

        return (hresult & HRESULT_FACILITY_MASK) == HRESULT_WU_PROTOCOL_TALKER || (hresult & HRESULT_WINHTTP_MASK) == HRESULT_WINHTTP
            ? CannotVerifyReason.NetworkFailed
            : CannotVerifyReason.ProbeError;
    }

    /// <inheritdoc />
    public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var startedAt = _clock.UtcNow;
        if (!context.OnlineCheckRequested)
        {
            // 실행 조율기가 이미 막지만, 직접 호출돼도 온라인 확인 요청 없이 업데이트 서비스에 연결하지 않는다.
            return CreateResult(context, ProbeStatus.Skipped, [], [new Issue(CannotVerifyReason.NotRequested, CoreStrings.ProbeIssue_NotRequested)], startedAt);
        }

        var outcome = await _gateway.SearchAsync(WindowsUpdateProbeContract.SEARCH_CRITERIA, ct).ConfigureAwait(false);
        var observedAt = _clock.UtcNow;

        if (outcome.ErrorHResult is { } hresult)
        {
            var code = string.Format(CultureInfo.InvariantCulture, HRESULT_FORMAT, hresult);
            return CreateResult(context, ProbeStatus.Failed, [], [new Issue(ReasonFor(hresult), ProbeText.Format(ProbeStrings.Wua_SearchFailed, code))], startedAt);
        }

        if (outcome.ResultCode is not { } resultCode)
        {
            return CreateResult(
                context, ProbeStatus.Failed, [], [new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Wua_Unavailable, outcome.ErrorCode ?? string.Empty))], startedAt);
        }

        var resultMeasurement = Integer(WindowsUpdateProbeContract.RESULT_CODE, resultCode, SOURCE_SEARCH, observedAt);
        switch (resultCode)
        {
            case WindowsUpdateProbeContract.RESULT_SUCCEEDED:
            case WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS:
                return CreateSearched(context, outcome, resultCode, resultMeasurement, startedAt, observedAt);
            case WindowsUpdateProbeContract.RESULT_ABORTED:
                return CreateResult(context, ProbeStatus.Cancelled, [resultMeasurement], [new Issue(CannotVerifyReason.Cancelled, ProbeStrings.Wua_Aborted)], startedAt);
            default:
                return CreateResult(
                    context,
                    ProbeStatus.Failed,
                    [resultMeasurement],
                    [new Issue(CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Wua_SearchFailed, resultCode.ToString(CultureInfo.InvariantCulture)))],
                    startedAt);
        }
    }

    /// <summary>
    /// 검색이 (일부) 성공한 결과를 측정값으로 만든다.
    /// </summary>
    private ProbeResult CreateSearched(
        ScanContext context, WuaSearchOutcome outcome, int resultCode, Measurement resultMeasurement, DateTimeOffset startedAt, DateTimeOffset observedAt)
    {
        var measurements = new List<Measurement>
        {
            resultMeasurement,
            Integer(WindowsUpdateProbeContract.UPDATE_COUNT, outcome.Updates.Count, SOURCE_SEARCH, observedAt),
        };

        for (var index = 0; index < outcome.Updates.Count; index++)
        {
            AddUpdate(measurements, index, outcome.Updates[index], observedAt);
        }

        var issues = new List<Issue>();
        if (resultCode == WindowsUpdateProbeContract.RESULT_SUCCEEDED_WITH_ERRORS)
        {
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Wua_PartialResult, resultCode)));
        }

        var missingDetails = outcome.Updates.Count(update => !update.DetailsAvailable);
        if (missingDetails > 0)
        {
            measurements.Add(Integer(WindowsUpdateProbeContract.DETAILS_UNAVAILABLE_COUNT, missingDetails, SOURCE_SEARCH, observedAt));
            issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeText.Format(ProbeStrings.Wua_DetailsUnavailable, missingDetails)));
        }

        if (outcome.RebootRequired is { } reboot)
        {
            measurements.Add(new Measurement(WindowsUpdateProbeContract.REBOOT_REQUIRED, new BooleanValue(reboot), null, SOURCE_SYSTEM_INFO, observedAt, MeasurementQuality.Reported));
        }

        return CreateResult(context, issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial, measurements, issues, startedAt);
    }

    /// <summary>
    /// 업데이트 하나의 측정값을 추가한다(값이 없으면 그 필드는 생략).
    /// </summary>
    private static void AddUpdate(List<Measurement> measurements, int index, WuaDriverUpdate update, DateTimeOffset observedAt)
    {
        void Text(string field, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                measurements.Add(new Measurement(
                    WindowsUpdateProbeContract.UpdateMeasurementName(index, field), new TextValue(value.Trim()), null, SOURCE_UPDATE + field, observedAt, MeasurementQuality.Reported));
            }
        }

        Text(WindowsUpdateProbeContract.FIELD_TITLE, update.Title);
        Text(WindowsUpdateProbeContract.FIELD_DRIVER_MODEL, update.DriverModel);
        Text(WindowsUpdateProbeContract.FIELD_DRIVER_MANUFACTURER, update.DriverManufacturer);
        Text(WindowsUpdateProbeContract.FIELD_DRIVER_DATE, update.DriverVerDate?.ToString(DATE_FORMAT, CultureInfo.InvariantCulture));
        Text(WindowsUpdateProbeContract.FIELD_DRIVER_CLASS, update.DriverClass);
        measurements.Add(new Measurement(
            WindowsUpdateProbeContract.UpdateMeasurementName(index, WindowsUpdateProbeContract.FIELD_KB_IDS),
            new TextListValue(update.KbArticleIds),
            null,
            SOURCE_UPDATE + WindowsUpdateProbeContract.FIELD_KB_IDS,
            observedAt,
            MeasurementQuality.Reported));
        if (update.IsDownloaded is { } downloaded)
        {
            measurements.Add(new Measurement(
                WindowsUpdateProbeContract.UpdateMeasurementName(index, WindowsUpdateProbeContract.FIELD_IS_DOWNLOADED),
                new BooleanValue(downloaded),
                null,
                SOURCE_UPDATE + WindowsUpdateProbeContract.FIELD_IS_DOWNLOADED,
                observedAt,
                MeasurementQuality.Reported));
        }
    }

    /// <summary>
    /// 정수 측정값을 만든다.
    /// </summary>
    private static Measurement Integer(string name, long value, string source, DateTimeOffset observedAt)
    {
        return new Measurement(name, new IntegerValue(value), null, source, observedAt, MeasurementQuality.Reported);
    }

    /// <summary>
    /// 이 프로브의 결과를 만든다. 시작 시각·소요 시간은 실행 조율기가 덮어쓴다.
    /// </summary>
    private ProbeResult CreateResult(
        ScanContext context, ProbeStatus status, IReadOnlyList<Measurement> measurements, IReadOnlyList<Issue> issues, DateTimeOffset startedAt)
    {
        return new ProbeResult(Id, status, measurements, issues, startedAt, TimeSpan.Zero, context.UserContext);
    }
}
