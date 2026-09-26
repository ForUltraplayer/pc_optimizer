/**
 * @file    : AppTestDoubles.cs
 * @author  : rudals252
 * @brief   : 뷰모델 테스트용 대역(즉시 실행 UI 마샬러, 고정 경로 선택기, 메모리 fixture 프로브, 첫 호출만 취소를 기다리는 프로브)
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
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
