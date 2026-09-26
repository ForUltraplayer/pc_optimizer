/**
 * @file    : RuleTestData.cs
 * @author  : rudals252
 * @brief   : 메모리·전원 규칙 단위 테스트용 가짜 측정값·스냅샷 생성 도우미와 금지 문구 목록
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 메모리 모듈 한 개의 원시 값입니다. null은 "값 없음"입니다.
/// </summary>
public sealed record FakeModule(
    string? DeviceLocator,
    string? BankLabel,
    long? Speed,
    long? ConfiguredClockSpeed,
    string? SpeedUnit = RuleTestData.UNIT,
    string? ConfiguredUnit = RuleTestData.UNIT);

/// <summary>
/// 규칙 단위 테스트용 데이터 생성 도우미입니다. 숫자는 테스트용 예시이며 실제 PC 값이 아닙니다.
/// </summary>
internal static class RuleTestData
{
    /// <summary>테스트용 속도 단위.</summary>
    public const string UNIT = MemoryProbeContract.UNIT_MEGATRANSFERS;

    /// <summary>테스트용 관측 시각.</summary>
    public static readonly DateTimeOffset OBSERVED_AT = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>
    /// 어떤 경우에도 규칙 문구에 나오면 안 되는 표현(스펙 §5 메모리·전원 행, §9).
    /// </summary>
    public static readonly string[] FORBIDDEN_MEMORY_PHRASES =
    [
        "정격 미달",
        "XMP 꺼짐",
        "XMP를 켜",
        "XMP 활성화",
        "EXPO를 켜",
        "EXPO 활성화",
        "광고",
        "보장",
    ];

    /// <summary>전원 규칙 문구에 나오면 안 되는 표현(고성능을 정상/최적으로 단정 금지).</summary>
    public static readonly string[] FORBIDDEN_POWER_PHRASES = ["정상", "최적"];

    /// <summary>
    /// 정수 측정값을 만든다.
    /// </summary>
    public static Measurement Integer(string name, long value, string? unit = null)
    {
        return new Measurement(name, new IntegerValue(value), unit, "fake", OBSERVED_AT, MeasurementQuality.Reported);
    }

    /// <summary>
    /// 문자열 측정값을 만든다.
    /// </summary>
    public static Measurement Text(string name, string value)
    {
        return new Measurement(name, new TextValue(value), null, "fake", OBSERVED_AT, MeasurementQuality.Reported);
    }

    /// <summary>
    /// 문자열 목록 측정값을 만든다.
    /// </summary>
    public static Measurement TextList(string name, params string[] values)
    {
        return new Measurement(name, new TextListValue(values), null, "fake", OBSERVED_AT, MeasurementQuality.Reported);
    }

    /// <summary>
    /// 가짜 모듈 목록으로 메모리 프로브 결과 스냅샷을 만든다.
    /// </summary>
    public static ScanSnapshot MemorySnapshot(ProbeStatus status, params FakeModule[] modules)
    {
        var measurements = new List<Measurement>
        {
            Integer(MemoryProbeContract.MODULE_COUNT, modules.Length),
        };

        for (var index = 0; index < modules.Length; index++)
        {
            var module = modules[index];
            if (module.DeviceLocator is not null)
            {
                measurements.Add(Text(MemoryProbeContract.ModuleMeasurementName(index, MemoryProbeContract.FIELD_DEVICE_LOCATOR), module.DeviceLocator));
            }

            if (module.BankLabel is not null)
            {
                measurements.Add(Text(MemoryProbeContract.ModuleMeasurementName(index, MemoryProbeContract.FIELD_BANK_LABEL), module.BankLabel));
            }

            if (module.Speed is { } speed)
            {
                measurements.Add(Integer(MemoryProbeContract.ModuleMeasurementName(index, MemoryProbeContract.FIELD_SPEED), speed, module.SpeedUnit));
            }

            if (module.ConfiguredClockSpeed is { } configured)
            {
                measurements.Add(Integer(
                    MemoryProbeContract.ModuleMeasurementName(index, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), configured, module.ConfiguredUnit));
            }
        }

        return EngineTestData.CreateSnapshot(EngineTestData.CreateResult(MemoryProbeContract.PROBE_ID, status, measurements));
    }

    /// <summary>
    /// 전원 프로브 결과 스냅샷을 만든다. null 인자는 해당 측정값이 없음을 뜻한다.
    /// </summary>
    public static ScanSnapshot PowerSnapshot(
        string? schemeGuid,
        string? schemeName,
        string[]? chassisTypes,
        long? acLineStatus,
        long? batteryFlag,
        ProbeStatus status = ProbeStatus.Success)
    {
        var measurements = new List<Measurement>();
        if (schemeGuid is not null)
        {
            measurements.Add(Text(PowerProbeContract.ACTIVE_SCHEME_GUID, schemeGuid));
        }

        if (schemeName is not null)
        {
            measurements.Add(Text(PowerProbeContract.ACTIVE_SCHEME_NAME, schemeName));
        }

        if (chassisTypes is not null)
        {
            measurements.Add(TextList(PowerProbeContract.CHASSIS_TYPES, chassisTypes));
        }

        if (acLineStatus is { } ac)
        {
            measurements.Add(Integer(PowerProbeContract.AC_LINE_STATUS, ac));
        }

        if (batteryFlag is { } flag)
        {
            measurements.Add(Integer(PowerProbeContract.BATTERY_FLAG, flag));
        }

        return EngineTestData.CreateSnapshot(EngineTestData.CreateResult(PowerProbeContract.PROBE_ID, status, measurements));
    }

    /// <summary>
    /// Finding의 사용자 노출 문장(제목·근거·상세·권고·영향)을 모두 이어 붙인다.
    /// </summary>
    public static string AllText(Finding finding)
    {
        return string.Join(
            "\n",
            finding.Title,
            finding.Evidence,
            finding.Detail ?? string.Empty,
            finding.Recommendation?.Text ?? string.Empty,
            finding.Recommendation?.Condition ?? string.Empty,
            finding.Impact?.Benefit ?? string.Empty,
            finding.Impact?.SideEffect ?? string.Empty);
    }
}
