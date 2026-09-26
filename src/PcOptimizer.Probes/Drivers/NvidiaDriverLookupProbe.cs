/**
 * @file    : NvidiaDriverLookupProbe.cs
 * @author  : rudals252
 * @brief   : 사용자가 온라인 확인을 켰을 때만 실행되는 NVIDIA 조회 프로브. NVIDIA 어댑터(VEN_10DE)의 설치 버전을 WMI로 읽고 제품 정확 일치 → Game Ready/Studio 목록을 한 번씩 조회해 측정값으로 기록(판정 없음, 다운로드 없음)
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// NVIDIA 온라인 조회 프로브입니다(스펙 §4·§5). <see cref="RequiresNetwork"/>가 true이므로 실행 조율기는 사용자가 온라인 확인을 요청한
/// 검사에서만 호출합니다. NVIDIA 어댑터가 없거나 설치 버전을 모르면 네트워크 요청을 하지 않습니다.
/// </summary>
/// <remarks>
/// 상태: 모든 NVIDIA 어댑터의 조회가 실패하면 Failed(첫 사유), 일부 실패·Studio 목록 실패·설치 버전 불명이 있으면 Partial, 그 밖에는 Success입니다.
/// 제품 이름이 정확히 하나로 일치하지 않는 것은 조회 실패가 아니라 측정값(<see cref="NvidiaLookupProbeContract.STATE_PRODUCT_AMBIGUOUS"/>)이며 규칙이 모호로 표시합니다.
/// 설치 드라이버 카드는 별도 로컬 프로브가 만들므로 이 프로브가 실패해도 남습니다.
/// </remarks>
public sealed class NvidiaDriverLookupProbe : IProbe
{
    /// <summary>비디오 컨트롤러 WMI 클래스.</summary>
    public const string VIDEO_CONTROLLER_CLASS = "Win32_VideoController";

    /// <summary>WMI 제공자 타임아웃.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(6);

    private const string PROPERTY_NAME = "Name";
    private const string PROPERTY_DRIVER_VERSION = "DriverVersion";
    private const string PROPERTY_DRIVER_DATE = "DriverDate";
    private const string PROPERTY_PNP_DEVICE_ID = "PNPDeviceID";
    private const string GAME_READY_EMPTY_CODE = "gameReady.empty";

    private static readonly string[] CONTROLLER_PROPERTIES = [PROPERTY_NAME, PROPERTY_DRIVER_VERSION, PROPERTY_DRIVER_DATE, PROPERTY_PNP_DEVICE_ID];

    private readonly IWmiClient _wmi;
    private readonly NvidiaLookupClient _client;
    private readonly IClock _clock;

    /// <summary>
    /// 실제 WMI·네트워크 전송·시스템 시계를 쓰는 프로브를 만듭니다.
    /// </summary>
    public NvidiaDriverLookupProbe()
        : this(WmiClient.Instance, new NvidiaLookupClient(HttpClientTransport.Shared), SystemClock.Instance)
    {
    }

    /// <summary>
    /// WMI·조회 어댑터·시계를 지정해 프로브를 만듭니다(테스트용 fixture 주입).
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="client">NVIDIA 조회 어댑터.</param>
    /// <param name="clock">UTC 시계.</param>
    public NvidiaDriverLookupProbe(IWmiClient wmi, NvidiaLookupClient client, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _client = client;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => NvidiaLookupProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => true;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_NETWORK_TIMEOUT;

    /// <inheritdoc />
    public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var observedAt = _clock.UtcNow;
        if (!context.OnlineCheckRequested)
        {
            // 실행 조율기가 이미 막지만, 직접 호출돼도 온라인 확인 요청 없이 네트워크에 연결하지 않는다.
            return CreateResult(context, ProbeStatus.Skipped, [], [new Issue(CannotVerifyReason.NotRequested, CoreStrings.ProbeIssue_NotRequested)], observedAt);
        }

        var controllers = _wmi.Query(VIDEO_CONTROLLER_CLASS, CONTROLLER_PROPERTIES, WMI_PROVIDER_TIMEOUT, ct);
        if (controllers.Status != WmiQueryStatus.Success)
        {
            return CreateResult(context, ProbeStatus.Failed, [], [WmiResultInterpreter.ToIssue(VIDEO_CONTROLLER_CLASS, controllers)], observedAt);
        }

        var adapters = controllers.Rows
            .Where(row => GpuVendorClassifier.Classify(WmiResultInterpreter.GetText(row, PROPERTY_PNP_DEVICE_ID)) == GpuVendor.Nvidia)
            .ToList();
        var measurements = new NvidiaLookupMeasurements(observedAt);
        measurements.AdapterCount(adapters.Count);

        var run = new LookupRun(_client, measurements);
        for (var index = 0; index < adapters.Count; index++)
        {
            await run.LookupAdapterAsync(index, adapters[index], ct).ConfigureAwait(false);
        }

        if (run.Failures.Count > 0 && run.Failures.Count == adapters.Count)
        {
            return CreateResult(context, ProbeStatus.Failed, measurements.Items, [.. run.FailureIssues, .. run.Issues], observedAt);
        }

        var status = run.Failures.Count > 0 || run.Issues.Count > 0 ? ProbeStatus.Partial : ProbeStatus.Success;
        return CreateResult(context, status, measurements.Items, [.. run.FailureIssues, .. run.Issues], observedAt);
    }

    /// <summary>
    /// 이 프로브의 결과를 만든다. 시작 시각·소요 시간은 실행 조율기가 덮어쓴다.
    /// </summary>
    private ProbeResult CreateResult(
        ScanContext context, ProbeStatus status, IReadOnlyList<Measurement> measurements, IReadOnlyList<Issue> issues, DateTimeOffset startedAt)
    {
        return new ProbeResult(Id, status, measurements, issues, startedAt, TimeSpan.Zero, context.UserContext);
    }

    /// <summary>
    /// 검사 한 번의 조회 진행 상태(제품 목록은 한 번만 받음, 실패·Issue 모음).
    /// </summary>
    private sealed class LookupRun(NvidiaLookupClient client, NvidiaLookupMeasurements measurements)
    {
        private NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>? _products;
        private bool _versionIssueAdded;

        /// <summary>어댑터별 조회 실패.</summary>
        public List<NvidiaLookupFailure> Failures { get; } = [];

        /// <summary>조회 실패 Issue(첫 사유가 Failed 결과의 사유가 됨).</summary>
        public List<Issue> FailureIssues { get; } = [];

        /// <summary>부분 결과 Issue(Studio 목록 실패·설치 버전 불명).</summary>
        public List<Issue> Issues { get; } = [];

        /// <summary>
        /// 어댑터 하나를 조회해 측정값으로 기록한다.
        /// </summary>
        public async Task LookupAdapterAsync(int index, IReadOnlyDictionary<string, object?> row, CancellationToken ct)
        {
            var name = WmiResultInterpreter.GetText(row, PROPERTY_NAME);
            var windowsVersion = WmiResultInterpreter.GetText(row, PROPERTY_DRIVER_VERSION);
            measurements.Text(index, NvidiaLookupProbeContract.FIELD_NAME, name, NvidiaLookupMeasurements.SOURCE_WMI_PREFIX + PROPERTY_NAME);
            measurements.Text(
                index, NvidiaLookupProbeContract.FIELD_PNP_DEVICE_ID, WmiResultInterpreter.GetText(row, PROPERTY_PNP_DEVICE_ID), NvidiaLookupMeasurements.SOURCE_WMI_PREFIX + PROPERTY_PNP_DEVICE_ID);
            measurements.Text(
                index, NvidiaLookupProbeContract.FIELD_INSTALLED_WINDOWS_VERSION, windowsVersion, NvidiaLookupMeasurements.SOURCE_WMI_PREFIX + PROPERTY_DRIVER_VERSION);
            measurements.Text(
                index, NvidiaLookupProbeContract.FIELD_INSTALLED_DATE, WmiResultInterpreter.GetDmtfDate(row, PROPERTY_DRIVER_DATE), NvidiaLookupMeasurements.SOURCE_WMI_PREFIX + PROPERTY_DRIVER_DATE);

            var installed = NvidiaDriverVersion.FromWindowsVersion(windowsVersion);
            if (installed is null)
            {
                measurements.State(index, NvidiaLookupProbeContract.STATE_VERSION_UNKNOWN);
                AddVersionIssueOnce();
                return;
            }

            measurements.Text(index, NvidiaLookupProbeContract.FIELD_INSTALLED_VERSION, installed, NvidiaLookupMeasurements.SOURCE_CONVERSION);

            _products ??= await client.GetProductsAsync(ct).ConfigureAwait(false);
            if (!_products.IsSuccess)
            {
                Fail(index, _products.Failure!);
                return;
            }

            var matches = NvidiaLookupClient.FindExactMatches(_products.Value!, name ?? string.Empty);
            measurements.Integer(index, NvidiaLookupProbeContract.FIELD_PRODUCT_MATCH_COUNT, matches.Count, NvidiaLookupMeasurements.SOURCE_PRODUCT_LIST);
            if (matches.Count != 1)
            {
                measurements.State(index, NvidiaLookupProbeContract.STATE_PRODUCT_AMBIGUOUS);
                return;
            }

            var product = matches[0];
            measurements.Integer(index, NvidiaLookupProbeContract.FIELD_PSID, product.Psid, NvidiaLookupMeasurements.SOURCE_PRODUCT_LIST);
            measurements.Integer(index, NvidiaLookupProbeContract.FIELD_PFID, product.Pfid, NvidiaLookupMeasurements.SOURCE_PRODUCT_LIST);
            await LookupBranchesAsync(index, product, installed, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Game Ready·Studio 목록을 한 번씩 받아 기록하고 목록 포함 여부로 계열을 기록한다.
        /// </summary>
        private async Task LookupBranchesAsync(int index, NvidiaProduct product, string installed, CancellationToken ct)
        {
            var gameReady = await client.GetDriversAsync(product, NvidiaDriverListKind.GameReady, ct).ConfigureAwait(false);
            if (!gameReady.IsSuccess || gameReady.Value!.Count == 0)
            {
                Fail(index, gameReady.Failure ?? new NvidiaLookupFailure(CannotVerifyReason.ProbeError, GAME_READY_EMPTY_CODE));
                return;
            }

            var studio = await client.GetDriversAsync(product, NvidiaDriverListKind.Studio, ct).ConfigureAwait(false);
            measurements.State(index, NvidiaLookupProbeContract.STATE_LISTED);
            measurements.BranchList(index, NvidiaDriverListKind.GameReady, gameReady.Value!);
            measurements.StudioAvailable(index, studio.IsSuccess);
            if (studio.IsSuccess)
            {
                measurements.BranchList(index, NvidiaDriverListKind.Studio, studio.Value!);
            }
            else
            {
                Issues.Add(new Issue(studio.Failure!.Reason, ProbeText.Format(ProbeStrings.Nvidia_StudioUnavailable, studio.Failure.Code)));
            }

            var resolution = NvidiaBranchResolver.Resolve(
                installed,
                [.. gameReady.Value!.Select(entry => entry.Version)],
                studio.IsSuccess ? [.. studio.Value!.Select(entry => entry.Version)] : null);
            measurements.Text(index, NvidiaLookupProbeContract.FIELD_BRANCH, resolution.Branch.ToString(), NvidiaLookupMeasurements.SOURCE_LOOKUP);
        }

        /// <summary>
        /// 어댑터 조회 실패를 기록한다(요약 코드만, 예외 원문 없음).
        /// </summary>
        private void Fail(int index, NvidiaLookupFailure failure)
        {
            measurements.Failed(index, failure);
            Failures.Add(failure);
            FailureIssues.Add(new Issue(failure.Reason, ProbeText.Format(ProbeStrings.Nvidia_LookupFailed, failure.Code)));
        }

        /// <summary>
        /// 설치 버전 불명 Issue를 한 번만 추가한다.
        /// </summary>
        private void AddVersionIssueOnce()
        {
            if (!_versionIssueAdded)
            {
                Issues.Add(new Issue(CannotVerifyReason.PartialData, ProbeStrings.Nvidia_VersionUnknown));
                _versionIssueAdded = true;
            }
        }
    }
}
