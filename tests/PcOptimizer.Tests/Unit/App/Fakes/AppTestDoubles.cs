/**
 * @file    : AppTestDoubles.cs
 * @author  : rudals252
 * @brief   : 뷰모델 테스트용 대역(즉시 실행 UI 마샬러, 고정 경로 선택기, 메모리·시스템 상세 fixture 프로브, 풀어 줄 때까지 기다리는 시스템 상세 프로브, 첫 호출만 취소를 기다리는 프로브, 고정 권한 상태, 기록·예외 프로세스 시작기, 고정 앱 내 실행 판정, 기록 클립보드, 일반 검사 컨텍스트, 사양 뷰모델 생성기)
 */

// 기본 패키지
using System.Diagnostics;
using System.Windows;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App.Fakes;

/// <summary>
/// 작업을 호출한 스레드에서 바로 실행하는 UI 마샬러입니다(WPF Dispatcher 없이 뷰모델을 검증).
/// </summary>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    /// <summary>호출 횟수.</summary>
    public int InvocationCount { get; private set; }

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        InvocationCount++;
        action();
        return Task.CompletedTask;
    }
}

/// <summary>
/// 미리 정한 경로(또는 취소)를 돌려주는 저장 경로 선택기입니다.
/// </summary>
internal sealed class FixedExportPathPicker(string? path) : IExportPathPicker
{
    /// <summary>제안받은 파일 이름.</summary>
    public string? SuggestedFileName { get; private set; }

    /// <inheritdoc />
    public string? PickSavePath(string suggestedFileName)
    {
        SuggestedFileName = suggestedFileName;
        return path;
    }
}

/// <summary>
/// 메모리 계약 측정값(모듈 2개: 일치 1, 설정 속도 낮음 1)을 돌려주는 가짜 메모리 프로브입니다. 숫자는 테스트 예시입니다.
/// </summary>
internal sealed class FixtureMemoryProbe : IProbe
{
    private const long SPEED = 4800;
    private const long LOWER_SPEED = 3600;
    private const long MODULE_COUNT = 2;

    /// <inheritdoc />
    public string Id => MemoryProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Memory;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => FakeProbe.GENEROUS_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        Measurement[] measurements =
        [
            Integer(MemoryProbeContract.MODULE_COUNT, MODULE_COUNT, null),
            Text(MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_DEVICE_LOCATOR), "DIMM A"),
            Integer(MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_SPEED), SPEED, MemoryProbeContract.UNIT_MEGATRANSFERS),
            Integer(MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), SPEED, MemoryProbeContract.UNIT_MEGATRANSFERS),
            Text(MemoryProbeContract.ModuleMeasurementName(1, MemoryProbeContract.FIELD_DEVICE_LOCATOR), "DIMM B"),
            Integer(MemoryProbeContract.ModuleMeasurementName(1, MemoryProbeContract.FIELD_SPEED), SPEED, MemoryProbeContract.UNIT_MEGATRANSFERS),
            Integer(MemoryProbeContract.ModuleMeasurementName(1, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), LOWER_SPEED, MemoryProbeContract.UNIT_MEGATRANSFERS),
        ];

        return Task.FromResult(new ProbeResult(Id, ProbeStatus.Success, measurements, [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext));
    }

    private static Measurement Integer(string name, long value, string? unit)
    {
        return new Measurement(name, new IntegerValue(value), unit, "fixture", FakeProbe.OBSERVED_AT, MeasurementQuality.Reported);
    }

    private static Measurement Text(string name, string value)
    {
        return new Measurement(name, new TextValue(value), null, "fixture", FakeProbe.OBSERVED_AT, MeasurementQuality.Reported);
    }
}

/// <summary>
/// 첫 호출은 취소될 때까지 기다리고(취소에 협조), 이후 호출은 바로 성공하는 전원 분류 프로브입니다.
/// </summary>
internal sealed class FirstCallWaitsForCancelProbe : IProbe
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    /// <summary>첫 호출이 시작되면 완료되는 작업.</summary>
    public Task Started => _started.Task;

    /// <inheritdoc />
    public string Id => "fixture.power";

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Power;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => FakeProbe.GENEROUS_TIMEOUT;

    /// <inheritdoc />
    public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        if (Interlocked.Increment(ref _calls) == 1)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }

        return new ProbeResult(Id, ProbeStatus.Success, [], [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext);
    }
}

/// <summary>
/// 고정 권한 상태입니다.
/// </summary>
internal sealed class FakeElevationState(bool isElevated, string? currentUserSid = FakeElevationState.USER_SID) : IElevationState
{
    /// <summary>테스트 사용자 SID(가짜 값).</summary>
    public const string USER_SID = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    /// <inheritdoc />
    public bool IsElevated { get; } = isElevated;

    /// <inheritdoc />
    public string? CurrentUserSid { get; } = currentUserSid;
}

/// <summary>
/// 시작 요청을 기록하고, 지정하면 예외를 던지는 프로세스 시작기입니다(실제 프로세스·UAC 없음).
/// </summary>
internal sealed class RecordingProcessStarter : IProcessStarter
{
    /// <summary>받은 시작 정보.</summary>
    public List<ProcessStartInfo> Started { get; } = [];

    /// <summary>Start에서 던질 예외(없으면 null).</summary>
    public Exception? ThrowOnStart { get; init; }

    /// <inheritdoc />
    public void Start(ProcessStartInfo startInfo)
    {
        Started.Add(startInfo);
        if (ThrowOnStart is not null)
        {
            throw ThrowOnStart;
        }
    }
}

/// <summary>
/// 모든 후보에 같은 앱 내 실행 판정을 돌려주는 대역입니다(실제 도구 위치를 확인하지 않음).
/// </summary>
/// <param name="value">모든 후보에 돌려줄 앱 내 실행 가능 여부.</param>
/// <param name="cacheToolsAvailable">정리 창을 열 수 있는지(보호 위치 도구 존재) 여부.</param>
internal sealed class FixedActionAvailability(bool value, bool cacheToolsAvailable = true) : IActionAvailability
{
    /// <inheritdoc />
    public bool CacheToolsAvailable => cacheToolsAvailable;

    /// <inheritdoc />
    public bool CanExecuteInApp(Finding finding) => value;
}

/// <summary>
/// 시스템 상세 계약(운영체제·CPU) 측정값을 돌려주는 가짜 프로브입니다. 값은 테스트 예시이며 실제 PC 값이 아닙니다.
/// </summary>
internal sealed class FixtureSystemDetailsProbe : IProbe
{
    /// <summary>예시 운영체제 이름.</summary>
    public const string OS_CAPTION = "Windows 11 Pro";

    /// <summary>예시 CPU 이름.</summary>
    public const string CPU_NAME = "Ryzen 7 fixture";

    private const long CORES = 8;
    private const long THREADS = 16;

    /// <inheritdoc />
    public string Id => SystemDetailsProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => FakeProbe.GENEROUS_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        Measurement[] measurements =
        [
            Text(SystemDetailsProbeContract.OS_CAPTION, OS_CAPTION),
            Text(SystemDetailsProbeContract.CPU_NAME, CPU_NAME),
            new(SystemDetailsProbeContract.CPU_CORES, new IntegerValue(CORES), null, "fixture", FakeProbe.OBSERVED_AT, MeasurementQuality.Reported),
            new(SystemDetailsProbeContract.CPU_THREADS, new IntegerValue(THREADS), null, "fixture", FakeProbe.OBSERVED_AT, MeasurementQuality.Reported),
        ];

        return Task.FromResult(new ProbeResult(Id, ProbeStatus.Success, measurements, [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext));
    }

    private static Measurement Text(string name, string value)
    {
        return new Measurement(name, new TextValue(value), null, "fixture", FakeProbe.OBSERVED_AT, MeasurementQuality.Reported);
    }
}

/// <summary>
/// 테스트가 <see cref="Release"/>를 부를 때까지 기다렸다가 성공하는 시스템 상세 프로브입니다(오래 걸리는 첫 사양 읽기 모사).
/// </summary>
internal sealed class GatedSystemDetailsProbe : IProbe
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>RunAsync가 시작되면 완료되는 작업.</summary>
    public Task Started => _started.Task;

    /// <inheritdoc />
    public string Id => SystemDetailsProbeContract.PROBE_ID;

    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;

    /// <inheritdoc />
    public bool RequiresElevation => false;

    /// <inheritdoc />
    public bool RequiresNetwork => false;

    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => FakeProbe.GENEROUS_TIMEOUT;

    /// <summary>기다리는 프로브를 풀어 줍니다.</summary>
    public void Release() => _gate.TrySetResult();

    /// <inheritdoc />
    public async Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        _started.TrySetResult();
        await _gate.Task.WaitAsync(ct);
        return new ProbeResult(Id, ProbeStatus.Success, [], [], context.StartedAtUtc, TimeSpan.Zero, context.UserContext);
    }
}

/// <summary>
/// 마지막으로 받은 텍스트를 보관하는 클립보드 대역입니다(실제 클립보드를 건드리지 않음).
/// </summary>
internal sealed class RecordingClipboard : IClipboard
{
    /// <summary>마지막으로 받은 텍스트(없으면 빈 문자열).</summary>
    public string LastText { get; private set; } = string.Empty;

    /// <summary>받은 횟수.</summary>
    public int SetCount { get; private set; }

    /// <inheritdoc />
    public void SetText(string text)
    {
        LastText = text;
        SetCount++;
    }
}

/// <summary>
/// 테스트용 검사 컨텍스트 생성기입니다.
/// </summary>
internal static class TestContexts
{
    /// <summary>일반 권한·온라인 확인 없음·고정 시각의 컨텍스트를 만듭니다.</summary>
    public static ScanContext Normal() => new(Guid.NewGuid(), EngineTestData.USER, false, FakeClock.DEFAULT_NOW);
}

/// <summary>
/// 가짜 시스템 상세 프로브로 사양 뷰모델을 만드는 생성기입니다.
/// </summary>
internal static class SpecTestFactory
{
    /// <summary>테스트용 PC 이름(가짜 값).</summary>
    public const string MACHINE_NAME = "MY-PC";

    /// <summary>테스트용 사용자명(가짜 값).</summary>
    public const string USER_NAME = "kim";

    /// <summary>사양 뷰모델을 만듭니다.</summary>
    /// <param name="clipboard">클립보드(없으면 기록 클립보드).</param>
    /// <param name="picker">저장 경로 선택기(없으면 취소).</param>
    /// <param name="captureTarget">이미지 렌더 대상 공급자(없으면 대상 없음).</param>
    /// <param name="probes">사양 프로브(없으면 시스템 상세 fixture 프로브 하나).</param>
    public static PcSpecViewModel Create(IClipboard? clipboard = null, IExportPathPicker? picker = null, Func<FrameworkElement?>? captureTarget = null,
        IReadOnlyList<IProbe>? probes = null)
    {
        var service = new PcSpecService(probes ?? [new FixtureSystemDetailsProbe()], new FakeClock(), NullAppLogger.Instance, TestContexts.Normal);
        return new PcSpecViewModel(service, new PcSpecTextFormatter(), clipboard ?? new RecordingClipboard(), picker ?? new FixedExportPathPicker(null),
            captureTarget ?? (() => null), new ImmediateUiDispatcher(), NullAppLogger.Instance, MACHINE_NAME, USER_NAME);
    }
}
